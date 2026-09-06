using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class BattleIntegrationTests
    {
        Scene scene,previous;BattleSession battle;
        [UnitySetUp] public IEnumerator SetUp()
        {
            BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Battle integration");SceneManager.SetActiveScene(scene);
            new GameObject("Test bootstrap").AddComponent<RiskBootstrap>();battle=BattleSession.Current;battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;
            yield return null;
        }
        [UnityTest] public IEnumerator HighlandsRequireRampRoutesAndTownsStandOnLevelGround()
        {
            foreach(var town in battle.Towns)
            {
                var p=town.transform.position;
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                    Assert.That(Mathf.Abs(MapLayout.Height(p.x+x*1.4f,p.z+z*1.4f)-p.y),Is.LessThan(.45f),town.DisplayName+" must not straddle a cliff.");
            }
            Assert.That(NavMesh.SamplePosition(MapLayout.Point(-51*MapLayout.Spacing,12*MapLayout.Spacing),out var upper,3,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(MapLayout.Point(-61*MapLayout.Spacing,12*MapLayout.Spacing),out var lower,3,NavMesh.AllAreas),Is.True);
            Assert.That(upper.position.y,Is.GreaterThan(3));Assert.That(upper.position.y-lower.position.y,Is.GreaterThan(2.5f),"Continuous hills must retain the discrete cliff separation.");
            Assert.That(NavMesh.Raycast(upper.position,lower.position,out _,NavMesh.AllAreas),Is.True,"A cliff must block direct walking.");
            var path=new NavMeshPath();NavMesh.CalculatePath(upper.position,lower.position,NavMesh.AllAreas,path);
            Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"Both elevations must connect through a ramp.");
            Assert.That(path.corners.Length,Is.GreaterThan(2));
            Assert.That(NavMesh.SamplePosition(MapLayout.Point(-11*MapLayout.Spacing,-30*MapLayout.Spacing),out _,1,NavMesh.AllAreas),Is.False,"The lake must not be walkable.");
            yield return null;
        }
        [UnityTest] public IEnumerator ZoomAndGroundPickingStayAnchoredOnHighGround()
        {
            var rig=Object.FindFirstObjectByType<RtsCameraRig>();rig.Focus(battle.Towns[1].transform.position);
            yield return new WaitForSecondsRealtime(.5f);
            var cursor=new Vector2(Screen.width*.52f,Screen.height*.52f);var anchor=rig.Ground(cursor);
            Assert.That(anchor.y,Is.GreaterThan(3),"Picking must hit the raised terrain, not a plane at zero.");
            rig.ZoomAt(1,cursor);yield return new WaitForSecondsRealtime(.7f);
            Assert.That(Vector2.Distance(Camera.main.WorldToScreenPoint(anchor),cursor),Is.LessThan(1.5f));
            Assert.That(Vector3.Distance(anchor,rig.Ground(cursor)),Is.LessThan(.08f));
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale=1;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.RandomCities;SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator MapHasAllTownsAndReachableRoutes()
        {
            Assert.That(battle.Towns.Count,Is.EqualTo(MapLayout.Towns.Length));Assert.That(battle.Units.Count,Is.GreaterThan(0));
            Assert.That(battle.Units.All(u=>u.Agent.isOnNavMesh),Is.True);
            var path=new NavMeshPath();
            for(int i=0;i<battle.Towns.Count;i++) for(int j=i+1;j<battle.Towns.Count;j++)
            {
                var from=battle.Towns[i].Rally;var to=battle.Towns[j].Rally;
                Assert.That(NavMesh.CalculatePath(from,to,NavMesh.AllAreas,path),Is.True,$"Path calculation failed from town {i}.");
                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),$"Town route {i} must be complete.");
            }
            var coastPoint=new Vector3(0,0,MapLayout.Coast(0)+4);
            Assert.That(NavMesh.SamplePosition(coastPoint,out _,.5f,NavMesh.AllAreas),Is.False,"The coast test point must remain off the walkable NavMesh.");
            var blueHome=battle.Towns.First(t=>t.State.Owner==0&&t.IsCapital);var army=BattleTestScenario.MobileArmy(battle,0,UnitKind.Footman,8,blueHome.Rally).ToList();
            var field=blueHome.transform.position+Vector3.back*10;BattleSession.GiveFormation(army,field,false,false);
            yield return new WaitForSeconds(4);
            Assert.That(army.Count(u=>Vector3.Distance(u.transform.position,field)<6),Is.GreaterThanOrEqualTo(army.Count-1),"The explicit test formation must reach the destination; only one crowd-avoidance straggler is allowed.");
        }
        [UnityTest] public IEnumerator RecruitmentPaysOnceAndPauseStopsSimulation()
        {
            var town=battle.Towns[0];int before=battle.Population(0);int goldBefore=battle.Economy.Gold[0];
            Assert.That(town.Recruit(UnitKind.Footman),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(goldBefore-BattleRules.Cost(UnitKind.Footman)));
            battle.TogglePause();float time=battle.BattleTime;yield return new WaitForSecondsRealtime(.3f);
            Assert.That(battle.BattleTime,Is.EqualTo(time));Assert.That(town.QueueCount,Is.EqualTo(1));
            battle.TogglePause();yield return new WaitForSeconds(3.4f);
            Assert.That(battle.Population(0),Is.EqualTo(before+1));Assert.That(town.QueueCount,Is.Zero);
        }
        [UnityTest] public IEnumerator ArmyDefeatsGuardsAndCapturesNeutralTown()
        {
            var town=battle.Towns.First(t=>t.State.Owner<0);var army=BattleTestScenario.MobileArmy(battle,0,UnitKind.Footman,10,battle.Towns.First(t=>t.State.Owner==0).Rally).ToList();
            for(int i=0;i<army.Count;i++)army[i].Agent.Warp(town.transform.position+new Vector3((i%5-2)*1.2f,0,7+i/5));
            BattleSession.GiveFormation(army,town.ClaimPoint,true,false);
            float deadline=Time.time+35;while(town.State.Owner!=0&&Time.time<deadline)yield return null;
            Assert.That(town.State.Owner,Is.Zero,"Soldiers should fight the defender then occupy the capture ring.");
            Assert.That(town.Defense.IsAlive,Is.True,"The permanent tower must remain alive while its defender is captured.");
            Assert.That(battle.Kills[0],Is.GreaterThanOrEqualTo(1));Assert.That(battle.Economy.Income(0),Is.GreaterThanOrEqualTo(BattleRules.BaseIncome));
        }
        [UnityTest] public IEnumerator StopEngagesWhileHoldAndMoveRespectTheirOrders()
        {
            // City zero's southern deployment clearing is deliberately reserved by
            // terrain generation. Sample it after the enlarged classic NavMesh bake
            // instead of warping actors to a historic y=0 probe.
            foreach(var tower in battle.Towers)if(tower)tower.enabled=false;
            foreach(var town in battle.Towns)if(town)town.enabled=false;
            foreach(var unit in battle.Units.ToArray())if(unit)unit.gameObject.SetActive(false);
            var clearing=battle.Towns[0];
            Assert.That(NavMesh.SamplePosition(clearing.Rally,out var footPoint,.8f,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(footPoint.position+Vector3.right*5,out var enemyPoint,.8f,NavMesh.AllAreas),Is.True);
            Assert.That(Vector2.Distance(new Vector2(footPoint.position.x,footPoint.position.z),new Vector2(enemyPoint.position.x,enemyPoint.position.z)),Is.EqualTo(5).Within(.1f));
            Assert.That(NavMesh.SamplePosition(footPoint.position+Vector3.back*6,out var retreat,.8f,NavMesh.AllAreas),Is.True);
            var foot=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,footPoint.position);
            var enemy=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,enemyPoint.position);
            Assert.That(foot.Agent.Warp(footPoint.position),Is.True);Assert.That(enemy.Agent.Warp(enemyPoint.position),Is.True);enemy.HoldPosition();
            Assert.That(NavMesh.Raycast(foot.transform.position,enemy.transform.position,out _,NavMesh.AllAreas),Is.False,"Test opponents must have a clear line across the town clearing.");
            foot.HoldPosition();yield return new WaitForSeconds(.5f);
            Assert.That(foot.CurrentTarget,Is.Null);Assert.That(Vector2.Distance(new Vector2(foot.transform.position.x,foot.transform.position.z),new Vector2(footPoint.position.x,footPoint.position.z)),Is.LessThan(.2f),"Hold keeps its XZ position while the agent settles onto sculpted ground.");
            foot.Stop();yield return new WaitForSeconds(2);
            Assert.That(enemy.Health,Is.LessThan(enemy.MaxHealth),$"Stop must acquire and approach nearby enemies. Target: {foot.CurrentTarget}, positions: {foot.transform.position} / {enemy.transform.position}; path: {foot.Agent.pathStatus}, remaining: {foot.Agent.remainingDistance}, visible: {!NavMesh.Raycast(foot.transform.position,enemy.transform.position,out _,NavMesh.AllAreas)}");
            foot.MoveTo(retreat.position,false,false);yield return new WaitForSeconds(.25f);
            Assert.That(foot.CurrentTarget,Is.Null,"Explicit movement must remain usable for retreating.");
        }
        [UnityTest] public IEnumerator AttackMoveResumesAfterKillingItsTarget()
        {
            var archer=BattleTestScenario.Mobile(battle,0,UnitKind.Archer,new Vector3(-30,0,-16));
            var enemy=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,new Vector3(-29,0,-21));
            archer.Agent.Warp(new Vector3(-30,0,-16));enemy.Agent.Warp(new Vector3(-29,0,-21));enemy.HoldPosition();enemy.TakeDamage(enemy.MaxHealth-5,0);
            archer.MoveTo(new Vector3(-30,0,-30),true,false);
            yield return new WaitForSeconds(5.5f);
            Assert.That(enemy.IsAlive,Is.False);
            Assert.That(battle.Units.Contains(enemy),Is.False,"A pooled casualty must leave the active army registry.");
            Assert.That(Vector3.Distance(archer.transform.position,new Vector3(-30,0,-30)),Is.LessThan(2));
        }
        [UnityTest] public IEnumerator CameraZoomIsSmoothAndMiddleDragTracksGround()
        {
            var rig=Object.FindFirstObjectByType<RtsCameraRig>();var camera=Camera.main;
            Assert.That(camera.orthographic,Is.False);Assert.That(rig.TargetZoom,Is.EqualTo(34).Within(.01f));
            Vector2 cursor=new Vector2(Screen.width*.55f,Screen.height*.5f);Vector3 anchor=rig.Ground(cursor);
            var sizeBeforeZoom=camera.orthographicSize;rig.ZoomAt(2,cursor);Assert.That(rig.TargetZoom,Is.EqualTo(34*Mathf.Exp(-.48f)).Within(.05f),"Wheel input must update the virtual zoom by two steps.");
            Assert.That(camera.orthographicSize,Is.EqualTo(sizeBeforeZoom).Within(.001f),"Zoom should be smoothed by the camera rig.");
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(rig.TargetZoom,Is.EqualTo(21).Within(.15f));Assert.That(Vector3.Distance(anchor,rig.Ground(cursor)),Is.LessThan(.15f));
            Vector2 moved=cursor+new Vector2(30,0);Vector3 before=rig.Ground(cursor);rig.Drag(cursor,moved);
            Assert.That(Vector3.Distance(before,rig.Ground(moved)),Is.LessThan(.05f),"Dragging must keep the grabbed ground under the cursor.");
            var panAnchor=rig.Ground(new Vector2(Screen.width*.5f,Screen.height*.5f));var beforePan=camera.WorldToScreenPoint(panAnchor).x;
            rig.Pan(Vector3.left,1);yield return new WaitForSecondsRealtime(.2f);
            Assert.That(camera.WorldToScreenPoint(panAnchor).x,Is.GreaterThan(beforePan+1),"Panning left must move the world to the right at the oblique camera yaw.");
        }
        [UnityTest] public IEnumerator NormalizedWheelEventChangesVirtualZoomAndKeepsCursorAnchored()
        {
            var rig=Object.FindFirstObjectByType<RtsCameraRig>();var camera=Camera.main;
            var previousMouse=Mouse.current;var previousKeyboard=Keyboard.current;
            Mouse testMouse=null;Keyboard testKeyboard=null;
            try
            {
                testMouse=InputSystem.AddDevice<Mouse>("RiskAI test mouse");
                testKeyboard=InputSystem.AddDevice<Keyboard>("RiskAI test keyboard");
                var cursor=new Vector2(Screen.width*.5f,Screen.height*.5f);var anchor=rig.Ground(cursor);
                var state=new MouseState { position=cursor, scroll=Vector2.up };
                InputSystem.QueueStateEvent(testMouse,state);
                InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
                // Batch mode has no focused Game view. Process the real queued device state,
                // then run the same controller handler used by the player.
                InputSystem.Update();testMouse.MakeCurrent();testKeyboard.MakeCurrent();
                var controller=Object.FindFirstObjectByType<RtsController>();controller.SendMessage("OnApplicationFocus",true);controller.SendMessage("Update");
                Assert.That(rig.TargetZoom,Is.LessThan(34*.85f),"One normalized wheel unit must produce a meaningful zoom step.");
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(Vector3.Distance(anchor,rig.Ground(cursor)),Is.LessThan(.15f),"A wheel zoom must stay anchored under the cursor.");
            }
            finally
            {
                if(testMouse!=null)InputSystem.RemoveDevice(testMouse);
                if(testKeyboard!=null)InputSystem.RemoveDevice(testKeyboard);
                if(previousMouse!=null)previousMouse.MakeCurrent();
                if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
            }
        }
        [UnityTest] public IEnumerator RecruitmentPurchasesEveryProfileAtItsDeclaredCost()
        {
            var town=battle.Towns.First(t=>t.State.Owner==0);
            var profiles=new[]{UnitKind.Footman,UnitKind.Archer,UnitKind.Guard,UnitKind.Mage,UnitKind.Mortar,UnitKind.Medic};
            foreach(var kind in profiles)
            {
                int cost=BattleRules.Cost(kind);battle.Economy.Gold[0]=cost;
                Assert.That(town.Recruit(kind),Is.Null,$"{kind} should be available at its declared profile level.");
                Assert.That(battle.Economy.Gold[0],Is.EqualTo(0));
                Assert.That(town.CancelTraining(0),Is.Null);
                Assert.That(battle.Economy.Gold[0],Is.EqualTo(cost));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator EnemyPickingAcceptsClicksOutsideTheNarrowCollider()
        {
            var target=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,new Vector3(-20,0,-5));target.Agent.Warp(new Vector3(-20,0,-5));target.HoldPosition();
            yield return null;var bounds=RtsPicking.Bounds(Camera.main,target);
            var pointer=new Vector2(bounds.xMin-7,bounds.center.y);
            Assert.That(RtsPicking.Target(battle,Camera.main,pointer,-1),Is.EqualTo(target),"Clicks near the visible silhouette should acquire the enemy.");
        }
        [UnityTest] public IEnumerator ReinforcementsRespectQueuesAcrossAllFriendlyTowns()
        {
            var region0=battle.Towns.Where(t=>t.State.Region==0).ToArray();
            var region1=battle.Towns.Where(t=>t.State.Region==1).ToArray();
            foreach(var town in region0.Concat(region1))town.State.Owner=0;
            battle.Economy.Gold[0]=1000;
            foreach(var unused in Enumerable.Range(0,5))Assert.That(region1[0].Recruit(UnitKind.Footman),Is.Null);
            while(battle.Population(0)<95)
            {
                var unit=battle.Spawn(0,UnitKind.Footman,new Vector3(-20,0,-12));
                Assert.That(unit,Is.Not.Null);unit.HoldPosition();
            }
            battle.Economy.Advance(59.9f);
            yield return new WaitForSeconds(.2f);
            Assert.That(battle.Population(0),Is.EqualTo(95));
            Assert.That(region1[0].QueueCount,Is.EqualTo(5));
            Assert.That(battle.Population(0)+battle.Towns.Where(t=>t.State.Owner==0).Sum(t=>t.QueueCount),Is.EqualTo(100),"Reinforcements must count queued units across every friendly town.");
        }
        [UnityTest] public IEnumerator FormationPlacesMeleeBeforeRanged()
        {
            var spawnedArcher=BattleTestScenario.Mobile(battle,0,UnitKind.Archer,new Vector3(-20,0,-12));
            var spawnedGuard=BattleTestScenario.Mobile(battle,0,UnitKind.Guard,new Vector3(-18.8f,0,-12));
            var units=new[]{spawnedArcher,spawnedGuard};
            Assert.That(units.Select(u=>u.Kind),Does.Contain(UnitKind.Archer));
            Assert.That(units.Select(u=>u.Kind),Does.Contain(UnitKind.Guard));
            for(int i=0;i<units.Length;i++){Assert.That(units[i].Agent.Warp(new Vector3(-20+i*1.2f,0,-12)),Is.True);units[i].Stop();}
            var forward = new Vector3(-20,0,0) - (units[0].transform.position+units[1].transform.position)*.5f;
            forward.y=0;forward.Normalize();
            BattleSession.GiveFormation(units,new Vector3(-20,0,0),false,false);
            float deadline=Time.realtimeSinceStartup+2;
            while(battle.Commands.PendingCount>0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(battle.Commands.PendingCount,Is.Zero,"Formation commands must be applied by the next simulation tick.");
            var archer=units.First(u=>u.Kind==UnitKind.Archer);var guard=units.First(u=>u.Kind==UnitKind.Guard);
            Assert.That(Vector3.Dot(guard.Agent.destination-archer.Agent.destination,forward),Is.GreaterThan(.75f),"Even a two-unit squad needs melee ahead of ranged along its travel direction.");
            yield return null;
        }
        [UnityTest] public IEnumerator SettingRallyLeavesSettlementPositionUnchanged()
        {
            var town=battle.Towns[0];var before=town.transform.position;var requested=before+new Vector3(0,0,8);
            Assert.That(NavMesh.SamplePosition(requested,out var hit,8,NavMesh.AllAreas),Is.True);
            town.SetRally(requested);
            Assert.That(town.transform.position,Is.EqualTo(before));
            Assert.That(town.Rally,Is.EqualTo(hit.position));
            var rallyObject=town.GetComponentsInChildren<Transform>().First(t=>t.name=="Punto de reunión");
            Assert.That(rallyObject.position,Is.EqualTo(hit.position));
            yield return null;
        }
        [UnityTest] public IEnumerator CountryReinforcementsUseArcherWavesAndReplenishToTheTenPointCapAfterRoundSix()
        {
            var countryTowns = battle.Towns.Where(t => t.State.Country == 0).ToArray();
            Assert.That(countryTowns.Length, Is.EqualTo(2));
            foreach (var town in countryTowns) town.State.Owner = 0;
            battle.SendMessage("CountryReinforcements"); yield return null;
            var firstWave = battle.Units.Where(u => u && u.Team == 0 && u.OriginCountry == 0).ToArray();
            Assert.That(firstWave.Length, Is.EqualTo(1));
            Assert.That(firstWave.All(unit => unit.Kind == UnitKind.Archer), Is.True);
            for (int wave = 0; wave < 9; wave++) battle.SendMessage("CountryReinforcements");
            Assert.That(battle.Units.Count(u => u && u.Team == 0 && u.OriginCountry == 0), Is.EqualTo(10));
            battle.Economy.Advance(BattleRules.RoundSeconds * 7);
            Assert.That(battle.Economy.Round, Is.GreaterThan(6));
            var casualty = battle.Units.First(u => u && u.Team == 0 && u.OriginCountry == 0);
            casualty.TakeDamage(casualty.MaxHealth + 1, 1); yield return new WaitForSeconds(1.5f);
            battle.SendMessage("CountryReinforcements");
            Assert.That(battle.Units.Count(u => u && u.Team == 0 && u.OriginCountry == 0), Is.EqualTo(10));
        }
        [UnityTest] public IEnumerator CountryIncomeStopsOnLossAndReturnsAfterRecapture()
        {
            var countryTowns = battle.Towns.Where(t => t.State.Country == 0).ToArray(); Assert.That(countryTowns.Length, Is.EqualTo(2));
            foreach (var town in countryTowns) town.State.Owner = 0;
            Assert.That(battle.Economy.CountryOwner(0), Is.EqualTo(0)); Assert.That(battle.Economy.Income(0), Is.GreaterThan(BattleRules.BaseIncome));
            countryTowns[1].State.Owner = 1; Assert.That(battle.Economy.CountryOwner(0), Is.EqualTo(-1)); Assert.That(battle.Economy.Income(0), Is.EqualTo(BattleRules.BaseIncome));
            countryTowns[1].State.Owner = 0; Assert.That(battle.Economy.CountryOwner(0), Is.EqualTo(0)); Assert.That(battle.Economy.Income(0), Is.GreaterThan(BattleRules.BaseIncome)); yield return null;
        }
    }
}
