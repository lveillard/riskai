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
        public IEnumerator TransportBoardsByRadiusAndFrigateCannotTakeAnUnguardedHarbor()
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
            Assert.That(target.Owner, Is.EqualTo(-1), "An undefended harbor becomes neutral instead of being captured by a warship.");
            Assert.That(target.NavalDefender,Is.Null);
            Assert.That(frigate.IsGarrison,Is.False);
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
        public IEnumerator DockedWarshipCannotBlockALandedEnemyCapture()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var landGuard=port.Defender;port.ClaimZone.SetDefender(null);landGuard.gameObject.SetActive(false);
            var guard=BattleTestScenario.Ship(naval,0,ShipKind.Galley,port.Berth);
            port.SimTick(.1f);
            Assert.That(port.NavalDefender,Is.Null);
            Assert.That(port.Owner,Is.EqualTo(PlayerRules.NeutralOwner));

            var enemy=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,port.ClaimZone.Center);
            port.SimTick(.1f);

            Assert.That(enemy.IsAlive,Is.True);
            Assert.That(port.Owner,Is.EqualTo(1));
            Assert.That(port.Defender,Is.SameAs(enemy));
            Assert.That(port.NavalDefender,Is.Null);
            Assert.That(guard.IsGarrison,Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LivingLandGuardianIsNotDisplacedByEnemyGalley()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var defender=port.Defender;
            var enemy=BattleTestScenario.Ship(naval,1,ShipKind.Galley,port.Berth);
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
                if(distance.magnitude<=Ship.LoadRadius+2f)continue;
                start=hit.position;found=true;
            }
            Assert.That(found,Is.True,"The boarder must begin outside instant embark range.");
            var ship=BattleTestScenario.Ship(naval,0,ShipKind.Transport,port.Berth);
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
        public IEnumerator MultipleWarshipsAtABerthNeverBecomeReliefGuards()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var landGuard=port.Defender;port.ClaimZone.SetDefender(null);landGuard.gameObject.SetActive(false);
            var first=BattleTestScenario.Ship(naval,0,ShipKind.Galley,port.Berth);
            var second=BattleTestScenario.Ship(naval,0,ShipKind.Galley,port.Berth+Vector3.right);
            port.SimTick(.1f);
            Assert.That(port.Owner,Is.EqualTo(PlayerRules.NeutralOwner));
            Assert.That(port.NavalDefender,Is.Null);
            Assert.That(first.IsGarrison||second.IsGarrison,Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WarshipAtBerthRemainsAControllableCombatUnit()
        {
            var port=naval.Harbors.First(h=>h.Owner==0&&!h.IsImportedPort&&h.Defender);
            var ship=BattleTestScenario.Ship(naval,0,ShipKind.Galley,port.Berth);
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
            var ship=BattleTestScenario.Ship(naval,0,ShipKind.Galley,port.Berth);
            ship.Select(true);yield return null;
            Assert.That(port.NavalDefender,Is.Null);
            Assert.That(port.NavalClaimRing.enabled,Is.False);
            Assert.That(Vector3.Distance(port.ClaimZone.Center,port.Landing),Is.LessThan(.001f));
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
