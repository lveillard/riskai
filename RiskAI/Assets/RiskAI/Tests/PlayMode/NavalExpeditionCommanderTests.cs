using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class NavalExpeditionCommanderTests
    {
        Scene previous, scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers, previousSeed;
        float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=51829;
            previousTimeScale=Time.timeScale;Time.timeScale=10f;
            scene=SceneManager.CreateScene("Naval expedition commander");SceneManager.SetActiveScene(scene);
            new GameObject("Naval expedition commander bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;Object.FindFirstObjectByType<RtsController>().enabled=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AiBuysBoardsSailsUnloadsAndOrdersOneRealExpedition()
        {
            var naval=NavalWorld.Current;
            var home=naval.Harbors.First(harbor=>harbor.Owner==1&&harbor.CanLaunch);
            foreach(var unit in battle.Units.Where(unit=>unit&&unit.Team==1&&!unit.IsGarrison))unit.gameObject.SetActive(false);
            var troops=BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,2,Sample(home.Landing+Vector3.right*3f));
            var sourceFrigate=BattleTestScenario.Ship(naval,1,UnitKind.Frigate,home.Berth);
            Assert.That(sourceFrigate,Is.Not.Null,"The occupied source berth is part of the transport integration fixture.");
            // Advance only the clock to the first naval decision; no rules tick
            // means no income or recruitment enters this economic fixture.
            while(battle.BattleTime<battle.AiFirstNavalOffensiveTime+.1f)battle.Clock.Advance(.4f,false,_=>{});
            int cost=UnitCatalog.Get(UnitKind.Transport).Cost;battle.Economy.Gold[1]=cost;
            battle.AiEnabled=true;
            // Reserve and queue before the land commander receives its first turn.
            naval.ExpeditionFor(1).Tick(0);
            Assert.That(naval.PendingShips(1),Is.GreaterThan(0),"The fixture must enter paid transport training.");
            foreach(var unit in troops)Assert.That(naval.ExpeditionFor(1).Reserves(unit),Is.False,"Training must leave the land army available to defend; embark troops are chosen when the transport is ready.");
            battle.AiEnabled=false;

            bool embarked=false,unloaded=false,attackOrders=false;int goldWhenQueued=naval.PendingShips(1)>0?battle.Economy.Gold[1]:-1;Ship transport=null;Soldier[] expedition=null;
            float deadline=Time.realtimeSinceStartup+30f;
            while(Time.realtimeSinceStartup<deadline&&!attackOrders)
            {
                yield return null; // NavMeshAgent path solving and movement need live engine frames.
                // Exercise only the naval commander in this identity fixture.
                // The land commander can now legitimately recruit or deploy a
                // different squad while its transport is training.
                battle.AiEnabled=true;
                naval.ExpeditionFor(1).Tick(0);
                battle.AiEnabled=false;
                transport=naval.Ships.FirstOrDefault(ship=>ship&&ship.Team==1&&ship.Kind==UnitKind.Transport);
                if(naval.PendingShips(1)>0&&goldWhenQueued<0)goldWhenQueued=battle.Economy.Gold[1];
                if(transport&&transport.CargoCount>=2){embarked=true;if(expedition==null)expedition=transport.Cargo.Take(2).ToArray();}
                if(embarked&&expedition!=null&&expedition.All(unit=>unit&&unit.IsAlive&&unit.gameObject.activeInHierarchy))
                {
                    unloaded=true;
                    attackOrders=expedition.All(unit=>
                        unit.Agent.enabled&&unit.Agent.isOnNavMesh&&!unit.Agent.pathPending&&unit.Agent.hasPath&&
                        unit.Agent.pathStatus==NavMeshPathStatus.PathComplete&&DistanceXZ(unit.Agent.destination,home.Landing)>10f);
                }
            }

            Assert.That(goldWhenQueued,Is.EqualTo(0),"The expedition must pay the catalog transport cost through the harbor queue.");
            Assert.That(transport,Is.Not.Null,"The harbor must finish a paid transport instead of spawning one.");
            Assert.That(embarked,Is.True,"The expedition must board the selected mobile troops through the transport load radius.");
            Assert.That(unloaded,Is.True,"The same transport must reach a marked destination harbor and unload its cargo.");
            Assert.That(attackOrders,Is.True,"Landed troops must receive the normal attack-move capture order.");
            Assert.That(expedition,Is.Not.Null,"The paid transport must board an existing AI squad.");
            Assert.That(expedition,Is.EquivalentTo(troops),"Only the two mobile fixture troops may form the opening expedition.");
            foreach(var unit in expedition)
            {
                Assert.That(unit.Agent.enabled,Is.True);
                Assert.That(unit.Agent.isOnNavMesh,Is.True);
                Assert.That(unit.Agent.pathStatus,Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(unit.Agent.hasPath,Is.True,"Landed troops must receive the normal attack-move capture order.");
            }
        }

        [UnityTest]
        public IEnumerator AFailedExpeditionSubmitsOneReturnAndARejectedDrainGoesIdle()
        {
            var naval = NavalWorld.Current;
            var home = naval.Harbors.First(harbor => harbor.Owner == 1 && harbor.CanLaunch);
            var transport = BattleTestScenario.Ship(naval, 1, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            var commander = naval.ExpeditionFor(1);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var phaseType = typeof(NavalExpeditionCommander).GetNestedType("Phase", BindingFlags.NonPublic);
            typeof(NavalExpeditionCommander).GetField("transport", hidden).SetValue(commander, transport);
            typeof(NavalExpeditionCommander).GetField("phase", hidden).SetValue(commander, System.Enum.Parse(phaseType, "Sailing"));
            typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).SetValue(commander, battle.BattleTime - 1f);
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            battle.AiEnabled = true;
            long before = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount;
            commander.Tick(0);
            long submitted = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(submitted, Is.EqualTo(1), "Fail submits the return once");
            float deadline = (float)typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).GetValue(commander);
            Assert.That(deadline, Is.GreaterThan(battle.BattleTime), "ReturningCargo arms a deadline on the pending confirmation");
            Assert.That(typeof(NavalExpeditionCommander).GetField("phase", hidden).GetValue(commander).ToString(), Is.EqualTo("ReturningCargo"));
            typeof(CombatTarget).GetProperty("Team").SetValue(transport, 0);
            battle.Commands.Tick();
            battle.Clock.Advance(1.5f, false, _ => { });
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            commander.Tick(0);
            long after = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(after, Is.EqualTo(1), "a drain rejection cools down instead of submitting another return");
            Assert.That(typeof(NavalExpeditionCommander).GetField("phase", hidden).GetValue(commander).ToString(), Is.EqualTo("Cooldown"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AStalledReturnDoesNotResubmitAndThePhaseTimesOut()
        {
            var naval = NavalWorld.Current;
            var home = naval.Harbors.First(harbor => harbor.Owner == 1 && harbor.CanLaunch);
            var transport = BattleTestScenario.Ship(naval, 1, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            Assert.That(home.TryTransportLanding(out _, out var berth), Is.True);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var route = (System.Collections.Generic.List<Vector3>)typeof(Ship).GetField("route", hidden).GetValue(transport);
            route.Clear();
            route.Add(berth);
            typeof(Ship).GetField("routeIndex", hidden).SetValue(transport, 0);
            typeof(Ship).GetField("routeGoal", hidden).SetValue(transport, berth);
            typeof(Ship).GetField("lastRouteProgressAt", hidden).SetValue(transport, -100f);
            typeof(Ship).GetField("hasActiveCommand", hidden).SetValue(transport, true);
            typeof(Ship).GetField("activeCommand", hidden).SetValue(transport,
                new UnitCommand(1, transport.EntityId, UnitCommandKind.Capture, home.Landing.x, home.Landing.y, home.Landing.z).WithCommandId(77));
            var commander = naval.ExpeditionFor(1);
            var phaseType = typeof(NavalExpeditionCommander).GetNestedType("Phase", BindingFlags.NonPublic);
            var slotType = typeof(DisembarkConfirmation).GetNestedType("Slot");
            object slot = System.Activator.CreateInstance(slotType);
            slotType.GetField("CommandId").SetValue(slot, 77);
            slotType.GetField("Waiting").SetValue(slot, true);
            slotType.GetField("HarborId").SetValue(slot, home.GetInstanceID());
            slotType.GetField("Berth").SetValue(slot, berth);
            typeof(NavalExpeditionCommander).GetField("transport", hidden).SetValue(commander, transport);
            typeof(NavalExpeditionCommander).GetField("returnHarbor", hidden).SetValue(commander, home);
            typeof(NavalExpeditionCommander).GetField("returnDisembark", hidden).SetValue(commander, slot);
            typeof(NavalExpeditionCommander).GetField("phase", hidden).SetValue(commander, System.Enum.Parse(phaseType, "ReturningCargo"));
            float deadline = battle.BattleTime + 30f;
            typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).SetValue(commander, deadline);
            typeof(NavalExpeditionCommander).GetField("returnRetryAt", hidden).SetValue(commander, 0f);
            battle.AiEnabled = true;
            long before = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount;
            for (int i = 0; i < 4; i++)
            {
                typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
                commander.Tick(0);
                battle.Clock.Advance(1f, false, _ => { });
                typeof(Ship).GetField("lastRouteProgressAt", hidden).SetValue(transport, -100f);
            }
            long during = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(during, Is.EqualTo(0), "a stalled route that still aims at the return berth is not ordered again");
            Assert.That((float)typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).GetValue(commander), Is.EqualTo(deadline), "a resubmit must not extend the phase");
            typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).SetValue(commander, battle.BattleTime - 1f);
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            commander.Tick(0);
            long after = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(after, Is.EqualTo(0), "timing out a stalled return does not order another");
            Assert.That(typeof(NavalExpeditionCommander).GetField("phase", hidden).GetValue(commander).ToString(), Is.EqualTo("Cooldown"), "the stalled phase times out");
            Assert.That((float)typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).GetValue(commander), Is.LessThanOrEqualTo(battle.BattleTime), "timing out does not arm a new deadline");
            yield return null;
        }

        [UnityTest]
        public IEnumerator APendingReturnResultSurvivesTheCooldown()
        {
            var naval = NavalWorld.Current;
            var home = naval.Harbors.First(harbor => harbor.Owner == 1 && harbor.CanLaunch);
            var transport = BattleTestScenario.Ship(naval, 1, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            var submitted = naval.SubmitDisembark(transport, home);
            Assert.That(submitted.Accepted, Is.True, submitted.Error);
            battle.Commands.Tick();
            Assert.That(transport.RunsCommand(submitted.CommandId), Is.True);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var commander = naval.ExpeditionFor(1);
            var phaseType = typeof(NavalExpeditionCommander).GetNestedType("Phase", BindingFlags.NonPublic);
            var slotType = typeof(DisembarkConfirmation).GetNestedType("Slot");
            object slot = System.Activator.CreateInstance(slotType);
            slotType.GetField("CommandId").SetValue(slot, submitted.CommandId);
            slotType.GetField("Waiting").SetValue(slot, true);
            slotType.GetField("HarborId").SetValue(slot, home.GetInstanceID());
            slotType.GetField("Berth").SetValue(slot, transport.RouteGoalMatches(home.Berth) ? home.Berth : SubmittedBerth(transport));
            typeof(NavalExpeditionCommander).GetField("transport", hidden).SetValue(commander, transport);
            typeof(NavalExpeditionCommander).GetField("returnHarbor", hidden).SetValue(commander, home);
            typeof(NavalExpeditionCommander).GetField("returnDisembark", hidden).SetValue(commander, slot);
            typeof(NavalExpeditionCommander).GetField("phase", hidden).SetValue(commander, System.Enum.Parse(phaseType, "Cooldown"));
            typeof(NavalExpeditionCommander).GetField("retryAt", hidden).SetValue(commander, battle.BattleTime);
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            battle.AiEnabled = true;
            long before = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount;
            commander.Tick(0);
            long after = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(after, Is.EqualTo(0), "an accepted return already in the ring is not sent again");
            Assert.That(typeof(NavalExpeditionCommander).GetField("phase", hidden).GetValue(commander).ToString(), Is.EqualTo("ReturningCargo"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AMovingReturnWhoseDeadlineExpiresIsNotOrderedAgain()
        {
            var naval = NavalWorld.Current;
            var home = naval.Harbors.First(harbor => harbor.Owner == 1 && harbor.CanLaunch);
            var transport = BattleTestScenario.Ship(naval, 1, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            Assert.That(home.TryTransportLanding(out _, out var berth), Is.True);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var route = (System.Collections.Generic.List<Vector3>)typeof(Ship).GetField("route", hidden).GetValue(transport);
            route.Clear();
            route.Add(berth);
            typeof(Ship).GetField("routeIndex", hidden).SetValue(transport, 0);
            typeof(Ship).GetField("routeGoal", hidden).SetValue(transport, berth);
            typeof(Ship).GetField("lastRouteProgressAt", hidden).SetValue(transport, battle.BattleTime);
            typeof(Ship).GetField("hasActiveCommand", hidden).SetValue(transport, true);
            typeof(Ship).GetField("activeCommand", hidden).SetValue(transport,
                new UnitCommand(1, transport.EntityId, UnitCommandKind.Capture, home.Landing.x, home.Landing.y, home.Landing.z).WithCommandId(77));
            var commander = naval.ExpeditionFor(1);
            var phaseType = typeof(NavalExpeditionCommander).GetNestedType("Phase", BindingFlags.NonPublic);
            var slotType = typeof(DisembarkConfirmation).GetNestedType("Slot");
            object slot = System.Activator.CreateInstance(slotType);
            slotType.GetField("CommandId").SetValue(slot, 77);
            slotType.GetField("Waiting").SetValue(slot, true);
            slotType.GetField("HarborId").SetValue(slot, home.GetInstanceID());
            slotType.GetField("Berth").SetValue(slot, berth);
            typeof(NavalExpeditionCommander).GetField("transport", hidden).SetValue(commander, transport);
            typeof(NavalExpeditionCommander).GetField("returnHarbor", hidden).SetValue(commander, home);
            typeof(NavalExpeditionCommander).GetField("returnDisembark", hidden).SetValue(commander, slot);
            typeof(NavalExpeditionCommander).GetField("phase", hidden).SetValue(commander, System.Enum.Parse(phaseType, "ReturningCargo"));
            typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).SetValue(commander, battle.BattleTime - 1f);
            typeof(NavalExpeditionCommander).GetField("returnRetryAt", hidden).SetValue(commander, 0f);
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            battle.AiEnabled = true;
            long before = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount;
            commander.Tick(0);
            long once = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(once, Is.EqualTo(0), "a moving return is extended, not ordered again");
            Assert.That((float)typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).GetValue(commander), Is.GreaterThan(battle.BattleTime));
            Assert.That(((DisembarkConfirmation.Slot)typeof(NavalExpeditionCommander).GetField("returnDisembark", hidden).GetValue(commander)).Waiting, Is.True);
            battle.Clock.Advance(1.5f, false, _ => { });
            typeof(Ship).GetField("lastRouteProgressAt", hidden).SetValue(transport, battle.BattleTime);
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            commander.Tick(0);
            long twice = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(twice, Is.EqualTo(0), "the next second still sees the same order");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ADockedBeachThatLandsNobodyIsOrderedAgain()
        {
            var naval = NavalWorld.Current;
            var home = naval.Harbors.First(harbor => harbor.Owner == 1 && harbor.CanLaunch);
            var transport = BattleTestScenario.Ship(naval, 1, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            Assert.That(transport.UnloadAt(transport.transform.position), Is.False);
            Assert.That(transport.CargoCount, Is.EqualTo(1));
            Assert.That(home.TryTransportLanding(out _, out var berth), Is.True);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var capture = default(CaptureOrderState);
            capture.Begin(true, home.Owner);
            typeof(Ship).GetField("capture", hidden).SetValue(transport, capture);
            var route = (System.Collections.Generic.List<Vector3>)typeof(Ship).GetField("route", hidden).GetValue(transport);
            route.Clear();
            typeof(Ship).GetField("routeIndex", hidden).SetValue(transport, 0);
            typeof(Ship).GetField("routeGoal", hidden).SetValue(transport, berth);
            typeof(Ship).GetField("pendingShoreUnload", hidden).SetValue(transport, true);
            typeof(Ship).GetField("pendingShore", hidden).SetValue(transport, transport.transform.position);
            typeof(Ship).GetField("hasActiveCommand", hidden).SetValue(transport, true);
            typeof(Ship).GetField("activeCommand", hidden).SetValue(transport,
                new UnitCommand(1, transport.EntityId, UnitCommandKind.Capture, home.Landing.x, home.Landing.y, home.Landing.z,
                    structureId: home.BuildingId.LocalId, structureKind: BuildingKind.Harbor).WithCommandId(41));
            float elapsed = 0f;
            while (elapsed < Ship.ShoreUnloadWindow + Ship.ShoreUnloadRetryInterval)
            {
                transport.SimTick(.2f);
                elapsed += .2f;
            }
            Assert.That(transport.CargoCount, Is.EqualTo(1));
            Assert.That((bool)typeof(Ship).GetField("pendingShoreUnload", hidden).GetValue(transport), Is.False);
            Assert.That(transport.RouteGoalMatches(berth), Is.False, "ending the order clears the berth");
            var commander = naval.ExpeditionFor(1);
            var phaseType = typeof(NavalExpeditionCommander).GetNestedType("Phase", BindingFlags.NonPublic);
            var slotType = typeof(DisembarkConfirmation).GetNestedType("Slot");
            object slot = System.Activator.CreateInstance(slotType);
            slotType.GetField("CommandId").SetValue(slot, 41);
            slotType.GetField("Waiting").SetValue(slot, true);
            slotType.GetField("HarborId").SetValue(slot, home.GetInstanceID());
            slotType.GetField("Berth").SetValue(slot, berth);
            typeof(NavalExpeditionCommander).GetField("transport", hidden).SetValue(commander, transport);
            typeof(NavalExpeditionCommander).GetField("returnHarbor", hidden).SetValue(commander, home);
            typeof(NavalExpeditionCommander).GetField("returnDisembark", hidden).SetValue(commander, slot);
            typeof(NavalExpeditionCommander).GetField("phase", hidden).SetValue(commander, System.Enum.Parse(phaseType, "ReturningCargo"));
            typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).SetValue(commander, battle.BattleTime + 100f);
            typeof(NavalExpeditionCommander).GetField("returnRetryAt", hidden).SetValue(commander, battle.BattleTime);
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            battle.AiEnabled = true;
            long before = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount;
            commander.Tick(0);
            long submitted = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(submitted, Is.EqualTo(1), "a docked return is ordered again inside the retry interval");
            Assert.That(typeof(NavalExpeditionCommander).GetField("phase", hidden).GetValue(commander).ToString(), Is.EqualTo("ReturningCargo"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AFinishedReturnResubmitKeepsTheSlot()
        {
            var naval = NavalWorld.Current;
            var home = naval.Harbors.First(harbor => harbor.Owner == 1 && harbor.CanLaunch);
            var transport = BattleTestScenario.Ship(naval, 1, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            var submitted = naval.SubmitDisembark(transport, home);
            Assert.That(submitted.Accepted, Is.True, submitted.Error);
            battle.Commands.Tick();
            transport.Stop();
            Assert.That(transport.CargoCount, Is.EqualTo(1));
            Assert.That(transport.RunsCommand(submitted.CommandId), Is.False);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var commander = naval.ExpeditionFor(1);
            var phaseType = typeof(NavalExpeditionCommander).GetNestedType("Phase", BindingFlags.NonPublic);
            var slotType = typeof(DisembarkConfirmation).GetNestedType("Slot");
            object slot = System.Activator.CreateInstance(slotType);
            slotType.GetField("CommandId").SetValue(slot, submitted.CommandId);
            slotType.GetField("Waiting").SetValue(slot, true);
            slotType.GetField("HarborId").SetValue(slot, home.GetInstanceID());
            slotType.GetField("Berth").SetValue(slot, SubmittedBerth(transport));
            typeof(NavalExpeditionCommander).GetField("transport", hidden).SetValue(commander, transport);
            typeof(NavalExpeditionCommander).GetField("returnHarbor", hidden).SetValue(commander, home);
            typeof(NavalExpeditionCommander).GetField("returnDisembark", hidden).SetValue(commander, slot);
            typeof(NavalExpeditionCommander).GetField("phase", hidden).SetValue(commander, System.Enum.Parse(phaseType, "ReturningCargo"));
            float deadline = battle.BattleTime - 1f;
            typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).SetValue(commander, deadline);
            typeof(NavalExpeditionCommander).GetField("returnRetryAt", hidden).SetValue(commander, 0f);
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            typeof(Ship).GetField("lastRouteProgressAt", hidden).SetValue(transport, -100f);
            battle.AiEnabled = true;
            long before = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount;
            commander.Tick(0);
            long once = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(once, Is.EqualTo(1), "the finished return is sent once");
            Assert.That(typeof(NavalExpeditionCommander).GetField("phase", hidden).GetValue(commander).ToString(), Is.EqualTo("ReturningCargo"));
            var kept = (DisembarkConfirmation.Slot)typeof(NavalExpeditionCommander).GetField("returnDisembark", hidden).GetValue(commander);
            Assert.That(kept.Waiting, Is.True, "the new slot is kept");
            Assert.That(kept.CommandId, Is.Not.EqualTo(submitted.CommandId));
            Assert.That((float)typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).GetValue(commander), Is.EqualTo(deadline), "the resubmit does not grant a new deadline");
            battle.Clock.Advance(1.5f, false, _ => { });
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            commander.Tick(0);
            long twice = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(twice, Is.EqualTo(1), "the kept slot is not sent again on the next second");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnEvictedSailResultStillConfirmsWhileTheHullRunsIt()
        {
            var naval = NavalWorld.Current;
            var home = naval.Harbors.First(harbor => harbor.Owner == 1 && harbor.CanLaunch);
            var destination = naval.Harbors.First(harbor => harbor != home && harbor.TryTransportLanding(out _, out _));
            var transport = BattleTestScenario.Ship(naval, 1, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            var submitted = naval.SubmitDisembark(transport, destination);
            Assert.That(submitted.Accepted, Is.True, submitted.Error);
            battle.Commands.Tick();
            Assert.That(transport.RunsCommand(submitted.CommandId), Is.True);
            Assert.That(battle.Commands.TryGetResult(submitted.CommandId, out _), Is.True);
            for (int i = 0; i < BattleCommands.InboxLimit + 1; i++)
                battle.Commands.Submit(new UnitCommand(1, 0, UnitCommandKind.Stop));
            Assert.That(battle.Commands.TryGetResult(submitted.CommandId, out _), Is.False, "the ring dropped the sail result");
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var commander = naval.ExpeditionFor(1);
            var phaseType = typeof(NavalExpeditionCommander).GetNestedType("Phase", BindingFlags.NonPublic);
            var slotType = typeof(DisembarkConfirmation).GetNestedType("Slot");
            object sail = System.Activator.CreateInstance(slotType);
            slotType.GetField("CommandId").SetValue(sail, submitted.CommandId);
            slotType.GetField("Waiting").SetValue(sail, true);
            slotType.GetField("HarborId").SetValue(sail, destination.GetInstanceID());
            object returning = System.Activator.CreateInstance(slotType);
            slotType.GetField("CommandId").SetValue(returning, 7);
            slotType.GetField("Waiting").SetValue(returning, true);
            slotType.GetField("HarborId").SetValue(returning, home.GetInstanceID());
            typeof(NavalExpeditionCommander).GetField("transport", hidden).SetValue(commander, transport);
            typeof(NavalExpeditionCommander).GetField("destination", hidden).SetValue(commander, destination);
            typeof(NavalExpeditionCommander).GetField("sailDisembark", hidden).SetValue(commander, sail);
            typeof(NavalExpeditionCommander).GetField("returnDisembark", hidden).SetValue(commander, returning);
            typeof(NavalExpeditionCommander).GetField("sailConfirmed", hidden).SetValue(commander, false);
            typeof(NavalExpeditionCommander).GetField("phase", hidden).SetValue(commander, System.Enum.Parse(phaseType, "Sailing"));
            typeof(NavalExpeditionCommander).GetField("phaseDeadline", hidden).SetValue(commander, battle.BattleTime + 100f);
            typeof(NavalExpeditionCommander).GetField("nextDecision", hidden).SetValue(commander, battle.BattleTime);
            ((System.Collections.Generic.List<Soldier>)typeof(Ship).GetField("cargo", hidden).GetValue(transport)).Clear();
            battle.AiEnabled = true;
            long before = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount;
            commander.Tick(0);
            long after = battle.Commands.PendingCount + battle.Commands.AppliedCount + battle.Commands.RejectedCount - before;
            Assert.That(after, Is.EqualTo(0), "an evicted sail is not submitted again");
            Assert.That((bool)typeof(NavalExpeditionCommander).GetField("sailConfirmed", hidden).GetValue(commander), Is.True);
            var sailSlot = (DisembarkConfirmation.Slot)typeof(NavalExpeditionCommander).GetField("sailDisembark", hidden).GetValue(commander);
            var returnSlot = (DisembarkConfirmation.Slot)typeof(NavalExpeditionCommander).GetField("returnDisembark", hidden).GetValue(commander);
            Assert.That(sailSlot.Waiting, Is.True, "confirming the sail does not clear Waiting");
            Assert.That(returnSlot.Waiting, Is.True, "the return slot stays waiting");
            Assert.That(returnSlot.CommandId, Is.EqualTo(7));
            Assert.That(typeof(NavalExpeditionCommander).GetField("phase", hidden).GetValue(commander).ToString(), Is.EqualTo("Landing"));
            yield return null;
        }

        static Vector3 SubmittedBerth(Ship transport)
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            return (Vector3)typeof(Ship).GetField("routeGoal", hidden).GetValue(transport);
        }

        static float DistanceXZ(Vector3 a,Vector3 b)
        {
            a.y=b.y=0;return Vector3.Distance(a,b);
        }

        static Vector3 Sample(Vector3 point)
        {
            Assert.That(NavMesh.SamplePosition(point,out var hit,2f,NavMesh.AllAreas),Is.True,"The fixture must stand on the source port NavMesh.");
            return hit.position;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;Time.timeScale=previousTimeScale;
            SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
