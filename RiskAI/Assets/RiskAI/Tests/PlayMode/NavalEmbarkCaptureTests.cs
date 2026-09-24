using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class NavalEmbarkCaptureTests
    {
        Scene previous, scene;
        BattleSession battle;
        NavalWorld naval;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene(); scene = SceneManager.CreateScene("Naval embark and capture"); SceneManager.SetActiveScene(scene);
            new GameObject("Naval embark bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current; battle.AiEnabled = false; naval = NavalWorld.Current;
            var controller = Object.FindFirstObjectByType<RtsController>(); if (controller) controller.enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransportBoardsByRadiusAndFrigateTakesAnUnguardedHarbor()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var transport = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True);
            Assert.That(transport.CargoCount, Is.EqualTo(1));
            Assert.That(transport.SailToShore(home.Berth), Is.Not.Null, "Open water is not a valid unload cursor target.");
            Assert.That(transport.SailToShore(home.Landing), Is.Null);
            const BindingFlags privateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
            var shoreBerth=(Vector3)typeof(Ship).GetField("routeGoal",privateInstance).GetValue(transport);
            Assert.That(Vector3.Distance(new Vector3(shoreBerth.x,0,shoreBerth.z),new Vector3(home.Landing.x,0,home.Landing.z)),
                Is.LessThanOrEqualTo(UnitCatalog.TransportLoadRadius-.45f), "The completed .4 m arrival margin must remain inside unload range.");
            transport.Select(false); // Pending shore work belongs to the ship, not UI selection.
            for(int tick=0;tick<240&&transport.CargoCount>0;tick++)transport.SimTick(.1f);
            Assert.That(transport.CargoCount, Is.Zero, "A valid queued beach unload completes after selection changes.");
            Assert.That(soldier.gameObject.activeInHierarchy, Is.True);

            int marineCost=UnitCatalog.Get(UnitKind.MarinePrivate).Cost;battle.Economy.Gold[0]=marineCost;
            Assert.That(home.RecruitLand(UnitKind.MarinePrivate,0),Is.Null);
            Assert.That(home.LandQueueCount,Is.EqualTo(1));
            home.State.Owner=1;home.SimTick(.1f);
            Assert.That(home.LandQueueCount,Is.Zero);
            Assert.That(battle.Economy.Gold[0],Is.EqualTo(marineCost),"Capturing a port refunds its pending land recruit.");

            var target = naval.Harbors.First(harbor => harbor!=home && harbor.Owner != 0 && !harbor.IsImportedPort);
            var guard=target.Defender;
            if(guard)guard.TakeDamage(guard.MaxHealth+1,guard.Team==0?1:0);
            var frigate = BattleTestScenario.Ship(naval, 0, UnitKind.Frigate, target.Berth);
            target.SimTick(.1f);
            Assert.That(target.Owner, Is.EqualTo(0), "Source-eligible h00W warships capture an empty harbor.");
            Assert.That(target.NavalDefender,Is.SameAs(frigate));
            Assert.That(frigate.IsGarrison,Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoardingSurvivesDeselectionAndFocusLossButRespectsPause()
        {
            var home=naval.Harbors.First(h=>h.Owner==0);
            var transport=BattleTestScenario.Ship(naval,0,UnitKind.Transport,home.Berth);
            var inland=(home.Landing-home.Berth).normalized;
            var soldier=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,home.Landing+inland*16);
            var controller=Object.FindFirstObjectByType<RtsController>();
            controller.SelectOnly(soldier);
            const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(RtsController).GetMethod("BeginBoarding",hidden,null,new[]{typeof(Ship)},null).Invoke(controller,new object[]{transport});
            Assert.That(transport.CargoCount,Is.Zero,"The distant soldier must first approach the shore.");
            controller.Clear();
            typeof(RtsController).GetMethod("OnApplicationFocus",hidden).Invoke(controller,new object[]{false});
            Assert.That(soldier.Agent.Warp(home.Landing),Is.True);
            transport.transform.position=home.Berth;
            battle.TogglePause();
            typeof(RtsController).GetMethod("Update",hidden).Invoke(controller,null);
            Assert.That(transport.CargoCount,Is.Zero,"Pause suspends the pending boarding mission.");
            battle.TogglePause();
            typeof(RtsController).GetMethod("Update",hidden).Invoke(controller,null);
            Assert.That(transport.CargoCount,Is.EqualTo(1),"The issued mission progresses independently of selection and application focus.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DockedWarshipBlocksALandedEnemyUntilTheGuardianDies()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var landGuard=port.Defender;port.ClaimZone.SetDefender(null);landGuard.gameObject.SetActive(false);
            var guard=BattleTestScenario.Ship(naval,0,UnitKind.Frigate,port.Berth);
            port.SimTick(.1f);
            Assert.That(port.NavalDefender,Is.SameAs(guard));
            Assert.That(port.Owner,Is.EqualTo(0));

            var enemy=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,port.ClaimZone.Center);
            port.SimTick(.1f);

            Assert.That(enemy.IsAlive,Is.True);
            Assert.That(port.Owner,Is.EqualTo(0),"A living naval guardian retains the same post as a land guardian.");
            Assert.That(port.ClaimZone.Guardian,Is.SameAs(guard));
            Assert.That(port.ClaimZone.Contested,Is.True);

            guard.TakeDamage(guard.MaxHealth+1,1);
            port.SimTick(.1f);
            Assert.That(port.Owner,Is.EqualTo(1));
            Assert.That(port.Defender,Is.SameAs(enemy));
            Assert.That(port.NavalDefender,Is.Null);
            Assert.That(guard.IsGarrison,Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LivingLandGuardianIsNotDisplacedByEnemyFrigate()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var defender=port.Defender;
            var enemy=BattleTestScenario.Ship(naval,1,UnitKind.Frigate,port.Berth);
            port.SimTick(.1f);
            Assert.That(port.Owner,Is.EqualTo(0));
            Assert.That(port.ClaimZone.Guardian,Is.SameAs(defender));
            Assert.That(port.NavalDefender,Is.Null);
            Assert.That(enemy.IsGarrison,Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoardingReportsFailureWhenTheOrderedActorsCannotProgress()
        {
            var port=naval.Harbors.First(h=>h.Owner==0);
            Vector3 start=default;bool found=false;
            for(int direction=0;direction<8&&!found;direction++)
            {
                float angle=direction*Mathf.PI*.25f;
                var point=port.Landing+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*18f;
                if(!UnityEngine.AI.NavMesh.SamplePosition(point,out var hit,2f,UnityEngine.AI.NavMesh.AllAreas))continue;
                var distance=hit.position-port.Berth;distance.y=0;
                if(distance.magnitude<=UnitCatalog.TransportLoadRadius+2f)continue;
                start=hit.position;found=true;
            }
            Assert.That(found,Is.True,"The boarder must begin outside instant embark range.");
            var ship=BattleTestScenario.Ship(naval,0,UnitKind.Transport,port.Berth);
            var soldier=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,start);
            var controller=Object.FindFirstObjectByType<RtsController>();controller.SelectOnly(soldier);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(RtsController).GetMethod("BeginBoarding",flags,null,new[]{typeof(Ship)},null).Invoke(controller,new object[]{ship});
            var pending=typeof(RtsController).GetField("pendingBoardingTransport",flags);
            Assert.That(pending.GetValue(controller),Is.SameAs(ship),"Begin with a real planned boarding order.");
            battle.Commands.Tick();yield return null;
            // Simulate a movement interruption after the order was accepted.
            // No successful embark or shore permission is fabricated.
            soldier.Stop();ship.Stop();
            var process=typeof(RtsController).GetMethod("ProcessPendingBoarding",flags);
            process.Invoke(controller,null);
            float deadline=battle.BattleTime+21f;
            while(battle.BattleTime<deadline)battle.Clock.Advance(.4f,false,_=>{});
            process.Invoke(controller,null);
            Assert.That(pending.GetValue(controller),Is.Null,"A stalled boarding intent must stop retrying forever.");
            Assert.That(ship.CargoCount,Is.Zero);
            Assert.That(battle.Messages[0],Does.StartWith("Embarque detenido:"));
        }

        [UnityTest]
        public IEnumerator NearestWarshipOccupiesAnEmptyPortAndTheSecondCanRelieveIt()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var landGuard=port.Defender;port.ClaimZone.SetDefender(null);landGuard.gameObject.SetActive(false);
            var first=BattleTestScenario.Ship(naval,0,UnitKind.Frigate,port.Berth);
            var second=BattleTestScenario.Ship(naval,0,UnitKind.Frigate,port.Berth+Vector3.right);
            port.SimTick(.1f);
            Assert.That(port.Owner,Is.EqualTo(0));
            Assert.That(port.NavalDefender,Is.SameAs(first));
            Assert.That(first.IsGarrison,Is.True);
            Assert.That(second.IsGarrison,Is.False);

            first.SailToHarbor(naval.Harbors.First(h=>h!=port&&h.CanLaunch));
            Assert.That(first.LastActionError,Is.Null);
            Assert.That(port.NavalDefender,Is.SameAs(second),"The old guardian may leave only after an in-circle allied handoff.");
            Assert.That(first.IsGarrison,Is.False);
            Assert.That(second.IsGarrison,Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WarshipAtBerthRemainsAControllableCombatUnit()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var ship=BattleTestScenario.Ship(naval,0,UnitKind.Frigate,port.Berth);
            port.SimTick(.1f);
            Assert.That(ship.IsGarrison,Is.False);
            var destination=naval.Harbors.First(h=>h!=port&&h.CanLaunch).Berth;
            ship.MoveTo(destination);
            Assert.That(ship.LastActionError,Is.Null);
            Assert.That(port.NavalDefender,Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PortKeepsOneLandCircleWhenAWarshipIsSelected()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var ship=BattleTestScenario.Ship(naval,0,UnitKind.Frigate,port.Berth);
            ship.Select(true);yield return null;
            Assert.That(port.NavalDefender,Is.Null);
            Assert.That(port.NavalClaimRing.enabled,Is.False);
            Assert.That(Vector3.Distance(port.ClaimZone.Center,port.Landing),Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator FailedShipRouteDoesNotIssueTheSoldiersEmbarkOrder()
        {
            var home=naval.Harbors.First(h=>h.Owner==0);
            var transport=BattleTestScenario.Ship(naval,0,UnitKind.Transport,home.Berth);
            var soldier=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,home.Landing);
            transport.transform.position=home.Landing; // Valid transport, deliberately not in navigable water.
            Vector3 before=soldier.Agent.destination;

            string embark=naval.OrderEmbark(transport,soldier);
            Assert.That(embark,Is.EqualTo("No hay una ruta marítima hasta ese destino."));
            Assert.That(soldier.Agent.destination,Is.EqualTo(before),"A rejected ship route must leave the soldier without a new land order.");

            string disembark=naval.OrderDisembark(transport,home);
            Assert.That(disembark,Is.EqualTo("No hay una ruta marítima segura hasta esa playa."));
            Assert.That(disembark,Is.EqualTo(transport.LastActionError));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WarshipCaptureAdvancesWhileAnEnemyStaysInRange()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var target = naval.Harbors.First(harbor => harbor != home && harbor.Owner != 0 && !harbor.IsImportedPort);
            var guard = target.Defender;
            if (guard) guard.TakeDamage(guard.MaxHealth + 1, guard.Team == 0 ? 1 : 0);
            var frigate = BattleTestScenario.Ship(naval, 0, UnitKind.Frigate, target.Berth);
            var enemy = BattleTestScenario.Ship(naval, target.Owner < 0 ? 1 : target.Owner, UnitKind.Frigate, target.Berth + new Vector3(6f, 0f, 0f));
            var away = target.Berth + new Vector3(0f, 0f, 50f);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, frigate.EntityId, UnitCommandKind.Capture, target.Landing.x, target.Landing.y, target.Landing.z, structureId: target.BuildingId.LocalId, structureKind: BuildingKind.Harbor)), Is.True);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, frigate.EntityId, UnitCommandKind.Move, away.x, away.y, away.z, append: true)), Is.True);
            for (int tick = 0; tick < 40 && enemy.IsAlive; tick++)
            {
                battle.Commands.Tick();
                frigate.SimTick(.2f);
                enemy.SimTick(.2f);
                target.SimTick(.2f);
            }
            Assert.That(enemy.IsAlive, Is.True, "The enemy has to still be in the fight, or a dead target would hide the stall.");
            Assert.That(target.Owner, Is.EqualTo(0));
            Assert.That(frigate.Orders.Count, Is.Zero, "The queued move is no longer stuck behind the capture while a target is in range.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyAndLoadedTransportsBothAdvanceToTheQueuedMove()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var loaded = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth);
            var empty = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth + new Vector3(5f, 0f, 0f));
            var soldier = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Landing);
            Assert.That(loaded.TryEmbark(soldier), Is.True, loaded.LastActionError);
            var away = SeaAway(home.Berth, 36f);
            CaptureThenMove(loaded, home, away);
            CaptureThenMove(empty, home, away);
            for (int tick = 0; tick < 360 && (loaded.Orders.Count > 0 || empty.Orders.Count > 0 || loaded.CargoCount > 0); tick++)
            {
                battle.Commands.Tick();
                loaded.SimTick(.2f);
                empty.SimTick(.2f);
            }
            Assert.That(empty.Orders.Count, Is.EqualTo(0), "an empty transport must finish Capture and run the queued move");
            Assert.That(loaded.CargoCount, Is.EqualTo(0), loaded.LastActionError);
            Assert.That(loaded.Orders.Count, Is.EqualTo(0), "the loaded transport runs the queued move after unloading");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransportCaptureKeepsUnloadingWhenAnotherPlayerTakesThePort()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var target = naval.Harbors.First(harbor => harbor != home && harbor.Owner != 0 && !harbor.IsImportedPort);
            var transport = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            var away = SeaAway(home.Berth, 30f);
            CaptureThenMove(transport, target, away);
            for (int tick = 0; tick < 6; tick++) { battle.Commands.Tick(); transport.SimTick(.2f); }
            int other = target.Owner == 1 ? 2 : 1;
            target.State.Owner = other;
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            bool unloading = (bool)typeof(Ship).GetField("pendingShoreUnload", hidden).GetValue(transport);
            int routeCount = ((System.Collections.Generic.List<Vector3>)typeof(Ship).GetField("route", hidden).GetValue(transport)).Count;
            Assert.That(unloading || routeCount > 0, Is.True, "taking the port must not halt a transport that still has to land");
            Assert.That(transport.CargoCount, Is.EqualTo(1));
            for (int tick = 0; tick < 400 && transport.CargoCount > 0; tick++)
            {
                Assert.That(transport.Orders.Count, Is.GreaterThan(0), "the queued move waits until the troops are ashore");
                battle.Commands.Tick();
                transport.SimTick(.2f);
            }
            Assert.That(transport.CargoCount, Is.EqualTo(0), transport.LastActionError);
            for (int tick = 0; tick < 40 && transport.Orders.Count > 0; tick++) { battle.Commands.Tick(); transport.SimTick(.2f); }
            Assert.That(transport.Orders.Count, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BlockedBeachHoldsTheQueueWhileTroopsRemainAboard()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var transport = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True, transport.LastActionError);
            var away = SeaAway(home.Berth, 24f);
            CaptureThenMove(transport, home, away);
            battle.Commands.Tick();
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Ship).GetField("pendingShore", hidden).SetValue(transport, transport.transform.position);
            typeof(Ship).GetField("pendingShoreUnload", hidden).SetValue(transport, true);
            transport.SimTick(.2f);
            Assert.That(transport.CargoCount, Is.EqualTo(1));
            Assert.That((bool)typeof(Ship).GetField("pendingShoreUnload", hidden).GetValue(transport), Is.True,
                "a beach that admits nobody keeps the unload; the next move must not sail the army away");
            Assert.That(transport.Orders.Count, Is.GreaterThan(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AttackMoveStaysBusyWhileTheTargetLives()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var frigate = BattleTestScenario.Ship(naval, 0, UnitKind.Frigate, home.Berth);
            int enemyTeam = home.Owner == 1 ? 2 : 1;
            var enemy = BattleTestScenario.Ship(naval, enemyTeam, UnitKind.Frigate, home.Berth + new Vector3(6f, 0f, 0f));
            var away = SeaAway(home.Berth, 40f);
            var here = frigate.transform.position;
            Assert.That(battle.Commands.Submit(new UnitCommand(0, frigate.EntityId, UnitCommandKind.AttackMove, here.x, here.y, here.z)), Is.True, battle.Commands.LastRejection);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, frigate.EntityId, UnitCommandKind.Move, away.x, away.y, away.z, append: true)), Is.True, battle.Commands.LastRejection);
            for (int tick = 0; tick < 12 && enemy.IsAlive; tick++)
            {
                battle.Commands.Tick();
                frigate.SimTick(.2f);
                enemy.SimTick(.2f);
            }
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(frigate.Orders.Count, Is.GreaterThan(0), "a live target keeps the attack-move from releasing the queued move");
            enemy.TakeDamage(enemy.MaxHealth + 1, 0);
            for (int tick = 0; tick < 20 && frigate.Orders.Count > 0; tick++) { battle.Commands.Tick(); frigate.SimTick(.2f); }
            Assert.That(frigate.Orders.Count, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AChaseRepathDoesNotReplaceTheValidatedMove()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var transport = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth);
            var away = SeaAway(home.Berth, 28f);
            var submitted = battle.Commands.SubmitResult(new UnitCommand(0, transport.EntityId, UnitCommandKind.Move, away.x, away.y, away.z));
            Assert.That(submitted.Accepted, Is.True, battle.Commands.LastRejection);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var scratch = (System.Collections.Generic.List<Vector3>)typeof(Ship).GetField("pathScratch", hidden).GetValue(transport);
            var bogus = transport.transform.position + new Vector3(70f, 0f, 70f);
            scratch.Clear();
            scratch.Add(bogus);
            battle.Commands.Tick();
            transport.SimTick(.2f);
            var goal = (Vector3)typeof(Ship).GetField("routeGoal", hidden).GetValue(transport);
            Assert.That(Vector3.Distance(goal, bogus), Is.GreaterThan(20f), "the chase scratch must not become the admitted route");
            Assert.That(Vector3.Distance(new Vector3(goal.x, 0f, goal.z), new Vector3(away.x, 0f, away.z)), Is.LessThan(12f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ARetriedUnloadDoesNotReuseTheFirstSlot()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var transport = BattleTestScenario.Ship(naval, 0, UnitKind.Transport, home.Berth + (home.Landing - home.Berth).normalized * 2f);
            var first = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Landing);
            var second = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, home.Landing + Vector3.right);
            Assert.That(transport.TryEmbark(first), Is.True, transport.LastActionError);
            Assert.That(transport.TryEmbark(second), Is.True, transport.LastActionError);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Ship).GetField("unloadSlot", hidden).SetValue(transport, 4);
            Assert.That(transport.UnloadAt(home.Landing), Is.True, transport.LastActionError);
            float spacing = UnitCatalog.Get(UnitKind.Footman).SpawnRadius * 2f + .14f;
            Assert.That(Vector3.Distance(first.transform.position, home.Landing), Is.GreaterThan(spacing * .5f),
                "a retry must not drop the next soldier on the slot already used");
            yield return null;
        }

        void CaptureThenMove(Ship ship, Harbor harbor, Vector3 away)
        {
            Assert.That(battle.Commands.Submit(new UnitCommand(0, ship.EntityId, UnitCommandKind.Capture, harbor.Landing.x, harbor.Landing.y, harbor.Landing.z, structureId: harbor.BuildingId.LocalId, structureKind: BuildingKind.Harbor)), Is.True, battle.Commands.LastRejection);
            Assert.That(battle.Commands.Submit(new UnitCommand(0, ship.EntityId, UnitCommandKind.Move, away.x, away.y, away.z, append: true)), Is.True, battle.Commands.LastRejection);
        }

        Vector3 SeaAway(Vector3 origin, float distance)
        {
            var directions = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left, new Vector3(1f, 0f, 1f) };
            for (int i = 0; i < directions.Length; i++)
            {
                var point = origin + directions[i].normalized * distance;
                point.y = -.24f;
                if (SeaNavigation.HasClearance(point)) return point;
            }
            Assert.Fail("Need open water near " + origin);
            return origin;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
