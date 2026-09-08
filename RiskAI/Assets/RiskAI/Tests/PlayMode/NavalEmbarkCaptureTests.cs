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
        public IEnumerator TransportBoardsByRadiusAndFrigateHoldsAnUnguardedHarbor()
        {
            var home = naval.Harbors.First(harbor => harbor.Owner == 0);
            var transport = BattleTestScenario.Ship(naval, 0, ShipKind.Transport, home.Berth);
            var soldier = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, home.Landing);
            Assert.That(transport.TryEmbark(soldier), Is.True);
            Assert.That(transport.CargoCount, Is.EqualTo(1));
            Assert.That(transport.SailToShore(home.Berth), Is.Not.Null, "Open water is not a valid unload cursor target.");
            Assert.That(transport.SailToShore(home.Landing), Is.Null);
            const BindingFlags privateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
            var shoreBerth=(Vector3)typeof(Ship).GetField("routeGoal",privateInstance).GetValue(transport);
            Assert.That(Vector3.Distance(new Vector3(shoreBerth.x,0,shoreBerth.z),new Vector3(home.Landing.x,0,home.Landing.z)),
                Is.LessThanOrEqualTo(Ship.LoadRadius-.45f), "The completed .4 m arrival margin must remain inside unload range.");
            transport.Select(false); // Pending shore work belongs to the ship, not UI selection.
            for(int tick=0;tick<240&&transport.CargoCount>0;tick++)transport.SimTick(.1f);
            Assert.That(transport.CargoCount, Is.Zero, "A valid queued beach unload completes after selection changes.");
            Assert.That(soldier.gameObject.activeInHierarchy, Is.True);

            int marineCost=BattleRules.Cost(UnitKind.MarinePrivate);battle.Economy.Gold[0]=marineCost;
            Assert.That(home.RecruitLand(UnitKind.MarinePrivate,0),Is.Null);
            Assert.That(home.LandQueueCount,Is.EqualTo(1));
            home.State.Owner=1;home.SimTick(.1f);
            Assert.That(home.LandQueueCount,Is.Zero);
            Assert.That(battle.Economy.Gold[0],Is.EqualTo(marineCost),"Capturing a port refunds its pending land recruit.");

            var target = naval.Harbors.First(harbor => harbor!=home && harbor.Owner != 0 && !harbor.IsImportedPort);
            var guard=target.Defender;
            if(guard)guard.TakeDamage(guard.MaxHealth+1,guard.Team==0?1:0);
            var frigate = BattleTestScenario.Ship(naval, 0, ShipKind.Galley, target.Berth);
            target.SimTick(.1f);
            Assert.That(target.Owner, Is.EqualTo(0));
            target.SimTick(.1f); target.SimTick(.1f);
            Assert.That(target.Owner, Is.EqualTo(0), "The same docked frigate holds its captured harbor across ticks.");
            frigate.transform.position += Vector3.forward * (Harbor.BerthRadius + 2);
            target.SimTick(.1f);
            Assert.That(target.Owner, Is.EqualTo(-1), "An undefended harbor becomes neutral after its naval defender leaves.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoardingSurvivesDeselectionAndFocusLossButRespectsPause()
        {
            var home=naval.Harbors.First(h=>h.Owner==0);
            var transport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,home.Berth);
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
        public IEnumerator NavalGuardNeedsReliefAndAnchorsAtTheWaterBerth()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var landGuard=port.Defender;port.ClaimZone.SetDefender(null);landGuard.gameObject.SetActive(false);
            var guard=BattleTestScenario.Ship(naval,0,ShipKind.Galley,port.Berth);
            port.SimTick(.1f);
            Assert.That(port.NavalDefender,Is.SameAs(guard));
            Assert.That(Vector3.Distance(guard.transform.position,port.Berth),Is.LessThan(.001f),"A naval defender uses the water berth, never the land claim center.");
            yield return null;
            Assert.That(port.NavalClaimRing,Is.Not.Null);
            Assert.That(port.NavalClaimRing.enabled,Is.True);
            Assert.That(Vector3.Distance(port.NavalClaimRing.transform.position,port.Berth),Is.LessThan(.001f),"The visible naval claim circle must share the berth anchor.");
            var berthBefore=port.NavalClaimRing.transform.position;
            guard.Select(true);yield return null;
            var unitRing=guard.GetComponentInChildren<LineRenderer>();
            Vector3 selectedCenter=Vector3.zero;
            for(int i=0;i<unitRing.positionCount;i++)selectedCenter+=unitRing.transform.TransformPoint(unitRing.GetPosition(i));
            selectedCenter/=unitRing.positionCount;
            Assert.That(Vector2.Distance(new Vector2(selectedCenter.x,selectedCenter.z),new Vector2(berthBefore.x,berthBefore.z)),Is.LessThan(.001f));
            Assert.That(port.NavalClaimRing.transform.position,Is.EqualTo(berthBefore),"Selecting a ship cannot move its harbor claim circle.");
            Assert.That(unitRing.transform.lossyScale.x,Is.LessThan(.8f),"The selected guard ring must remain visually separate from the almost equal berth ring.");

            var destination=naval.Harbors.First(h=>h!=port&&h.CanLaunch).Berth;
            guard.MoveTo(destination);
            Assert.That(guard.LastActionError,Is.Not.Null);
            Assert.That(port.NavalDefender,Is.SameAs(guard),"A lone guard ship cannot abandon its harbor.");

            var shipRelief=BattleTestScenario.Ship(naval,0,ShipKind.Galley,port.Berth);
            guard.MoveTo(destination);
            Assert.That(guard.LastActionError,Is.Null);
            Assert.That(port.NavalDefender,Is.SameAs(shipRelief));
            Assert.That(port.Owner,Is.EqualTo(0),"A same-team berth relief preserves port ownership.");

            // The previous guard has a valid move order but is still at the berth
            // in the submission frame. Let it leave before testing a land relief;
            // land/sea candidates intentionally share distance and stable-ID ties.
            float departDeadline=Time.realtimeSinceStartup+4;
            while(Vector2.Distance(new Vector2(guard.transform.position.x,guard.transform.position.z),
                new Vector2(port.Berth.x,port.Berth.z))<=ClaimRules.ReliefRadius&&Time.realtimeSinceStartup<departDeadline)yield return null;
            Assert.That(Vector2.Distance(new Vector2(guard.transform.position.x,guard.transform.position.z),
                new Vector2(port.Berth.x,port.Berth.z)),Is.GreaterThan(ClaimRules.ReliefRadius));
            var landRelief=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,port.ClaimZone.Center);
            shipRelief.MoveTo(destination);
            Assert.That(port.Defender,Is.SameAs(landRelief),"An available land relief can replace the naval guard through the shared slot.");
            Assert.That(port.NavalDefender,Is.Null);
            yield return null;
            Assert.That(port.NavalClaimRing.enabled,Is.False,"The water circle hides as soon as land defense resumes.");
        }

        [UnityTest]
        public IEnumerator FailedShipRouteDoesNotIssueTheSoldiersEmbarkOrder()
        {
            var home=naval.Harbors.First(h=>h.Owner==0);
            var transport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,home.Berth);
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
