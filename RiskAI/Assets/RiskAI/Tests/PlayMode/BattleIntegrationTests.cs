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
            Assert.That(battle.Towns.Count,Is.EqualTo(12));Assert.That(battle.Units.Count,Is.EqualTo(48));
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
            var blueHome=battle.Towns.First(t=>t.State.Owner==0&&t.IsCapital);var army=battle.Units.Where(u=>u.Team==0&&Vector3.Distance(u.transform.position,blueHome.transform.position)<8).Take(14).ToList();
            var field=blueHome.transform.position+Vector3.back*10;BattleSession.GiveFormation(army,field,false,false);
            yield return new WaitForSeconds(4);
            Assert.That(army.Count,Is.GreaterThanOrEqualTo(8),"The local starting force remains usable after allocating garrisons and three dock troops.");
            Assert.That(army.Count(u=>Vector3.Distance(u.transform.position,field)<6),Is.GreaterThanOrEqualTo(army.Count-1),"The selected local formation must reach the destination; only one crowd-avoidance straggler is allowed.");
        }
        [UnityTest] public IEnumerator RecruitmentPaysOnceAndPauseStopsSimulation()
        {
            var town=battle.Towns[0];int before=battle.Population(0);
            Assert.That(town.Recruit(UnitKind.Footman),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(100));
            battle.TogglePause();float time=battle.BattleTime;yield return new WaitForSecondsRealtime(.3f);
            Assert.That(battle.BattleTime,Is.EqualTo(time));Assert.That(town.QueueCount,Is.EqualTo(1));
            battle.TogglePause();yield return new WaitForSeconds(3.4f);
            Assert.That(battle.Population(0),Is.EqualTo(before+1));Assert.That(town.QueueCount,Is.Zero);
        }
        [UnityTest] public IEnumerator ArmyDefeatsGuardsAndCapturesNeutralTown()
        {
            var town=battle.Towns.First(t=>t.State.Owner<0);var army=battle.Units.Where(u=>u.Team==0).ToList();
            for(int i=0;i<army.Count;i++)army[i].Agent.Warp(town.transform.position+new Vector3((i%5-2)*1.2f,0,7+i/5));
            BattleSession.GiveFormation(army,town.ClaimPoint,true,false);
            float deadline=Time.time+35;while(town.State.Owner!=0&&Time.time<deadline)yield return null;
            Assert.That(town.State.Owner,Is.Zero,"Soldiers should fight the guardians then occupy the capture ring.");
            Assert.That(battle.Kills[0],Is.GreaterThanOrEqualTo(1));Assert.That(battle.Economy.Income(0),Is.EqualTo(28));
        }
        [UnityTest] public IEnumerator StopEngagesWhileHoldAndMoveRespectTheirOrders()
        {
            var foot=battle.Units.First(u=>u.Team==0&&u.Kind==UnitKind.Footman);
            var enemy=battle.Units.First(u=>u.Team==1&&u.Kind==UnitKind.Footman);
            Assert.That(foot.Agent.Warp(new Vector3(-30,0,-16)),Is.True);Assert.That(enemy.Agent.Warp(new Vector3(-25,0,-16)),Is.True);enemy.HoldPosition();
            Assert.That(NavMesh.Raycast(foot.transform.position,enemy.transform.position,out _,NavMesh.AllAreas),Is.False,"Test opponents must have a clear line across the town clearing.");
            foot.HoldPosition();yield return new WaitForSeconds(.5f);
            Assert.That(foot.CurrentTarget,Is.Null);Assert.That(Vector2.Distance(new Vector2(foot.transform.position.x,foot.transform.position.z),new Vector2(-30,-16)),Is.LessThan(.2f),"Hold keeps its XZ position while the agent settles onto sculpted ground.");
            foot.Stop();yield return new WaitForSeconds(2);
            Assert.That(enemy.Health,Is.LessThan(enemy.MaxHealth),$"Stop must acquire and approach nearby enemies. Target: {foot.CurrentTarget}, positions: {foot.transform.position} / {enemy.transform.position}; path: {foot.Agent.pathStatus}, remaining: {foot.Agent.remainingDistance}, visible: {!NavMesh.Raycast(foot.transform.position,enemy.transform.position,out _,NavMesh.AllAreas)}");
            foot.MoveTo(new Vector3(-25,0,-10),false,false);yield return new WaitForSeconds(.25f);
            Assert.That(foot.CurrentTarget,Is.Null,"Explicit movement must remain usable for retreating.");
        }
        [UnityTest] public IEnumerator AttackMoveResumesAfterKillingItsTarget()
        {
            var archer=battle.Units.First(u=>u.Team==0&&u.Kind==UnitKind.Archer);
            var enemy=battle.Units.First(u=>u.Team==1&&u.Kind==UnitKind.Footman);
            archer.Agent.Warp(new Vector3(-30,0,-16));enemy.Agent.Warp(new Vector3(-29,0,-21));enemy.HoldPosition();enemy.TakeDamage(enemy.MaxHealth-5,0);
            archer.MoveTo(new Vector3(-30,0,-30),true,false);
            yield return new WaitForSeconds(5.5f);
            Assert.That(enemy==null,Is.True);Assert.That(Vector3.Distance(archer.transform.position,new Vector3(-30,0,-30)),Is.LessThan(2));
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
                Object.FindFirstObjectByType<RtsController>().SendMessage("Update");
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
        [UnityTest] public IEnumerator RecruitmentCancellationAndCityUpgradeRespectGold()
        {
            var town=battle.Towns[0];battle.Economy.Gold[0]=200;
            Assert.That(town.Recruit(UnitKind.Mage),Is.Not.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(200));
            Assert.That(town.Recruit(UnitKind.Archer),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(180));
            Assert.That(town.CancelTraining(0),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(200));
            Assert.That(town.Upgrade(),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(110));
            Assert.That(town.Upgrade(),Is.Not.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(110));
            yield return new WaitForSeconds(7.2f);
            Assert.That(town.State.Level,Is.EqualTo(2));Assert.That(battle.Economy.Income(0),Is.EqualTo(34));
            Assert.That(town.Recruit(UnitKind.Guard),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(10));
        }
        [UnityTest] public IEnumerator CapturedTownKeepsEnemyTowerUntilDestroyedAndRebuilt()
        {
            var town=battle.Towns[0];foreach(var blue in battle.Units.Where(u=>u.Team==0).ToArray()){blue.Agent.Warp(new Vector3(-42,0,-28));blue.HoldPosition();}
            var attacker=battle.Spawn(1,UnitKind.Guard,town.ClaimPoint);attacker.HoldPosition();
            yield return new WaitForSeconds(1.8f);
            Assert.That(town.State.Owner,Is.EqualTo(1));Assert.That(town.Defense.Team,Is.EqualTo(0));
            Assert.That(attacker.Health,Is.LessThan(attacker.MaxHealth),"The independently owned tower must keep shooting invaders after city capture.");
            Assert.That(town.BuildTower(1),Is.Not.Null,"An enemy tower must be destroyed before the slot can be rebuilt.");
            town.Defense.TakeDamage(1000,1,attacker);
            battle.Economy.Gold[1]=100;Assert.That(town.BuildTower(1),Is.Null);Assert.That(battle.Economy.Gold[1],Is.EqualTo(40));
            yield return new WaitForSeconds(7.3f);
            Assert.That(town.Defense.IsAlive,Is.True);Assert.That(town.Defense.Team,Is.EqualTo(1));Assert.That(battle.Targets.Contains(town.Defense),Is.True);
        }
        [UnityTest] public IEnumerator SoldiersCanSiegeAndDestroyATower()
        {
            var tower=battle.Towns.First(t=>t.State.Owner==1&&t.IsCapital).Defense;
            for(int side=0;side<8;side++)
            {
                float angle=side*Mathf.PI/4;
                var approach=tower.ApproachPoint(tower.transform.position+new Vector3(Mathf.Cos(angle)*8,0,Mathf.Sin(angle)*8));
                NavMesh.SamplePosition(approach,out var nearest,3,NavMesh.AllAreas);
                Assert.That(NavMesh.SamplePosition(approach,out var hit,.25f,NavMesh.AllAreas),Is.True,$"Tower approach must stay on walkable ground from side {side}: approach {approach}, nearest {nearest.position}, tower {tower.transform.position}.");
                Assert.That(Vector3.Distance(hit.position,approach),Is.LessThan(.2f));
            }
            Assert.That(NavMesh.SamplePosition(MapLayout.Point(43,30),out var redStaging,5,NavMesh.AllAreas),Is.True);
            foreach(var red in battle.Units.Where(u=>u.Team==1).ToArray()){Assert.That(red.Agent.Warp(redStaging.position),Is.True);red.Stop();}
            for(int i=0;i<8;i++)
            {
                var guard=battle.Spawn(0,UnitKind.Guard,tower.transform.position+new Vector3((i%4-1.5f)*1.5f,0,-6-i/4));guard.Attack(tower);
            }
            yield return new WaitForSeconds(9);
            Assert.That(tower.IsAlive,Is.False,"Melee soldiers must approach a building's perimeter and damage it.");
        }
        [UnityTest] public IEnumerator EnemyPickingAcceptsClicksOutsideTheNarrowCollider()
        {
            var target=battle.Units.First(u=>u.Team==1);target.Agent.Warp(new Vector3(-20,0,-5));target.HoldPosition();
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
            var spawnedGuard=battle.Spawn(0,UnitKind.Guard,new Vector3(-20,0,-12));
            Assert.That(spawnedGuard,Is.Not.Null);
            var units=battle.Units.Where(u=>u.Team==0).GroupBy(u=>u.Kind).Select(g=>g.First()).ToArray();
            Assert.That(units.Select(u=>u.Kind),Does.Contain(UnitKind.Archer));
            Assert.That(units.Select(u=>u.Kind),Does.Contain(UnitKind.Guard));
            for(int i=0;i<units.Length;i++){Assert.That(units[i].Agent.Warp(new Vector3(-20+i*1.2f,0,-12)),Is.True);units[i].Stop();}
            BattleSession.GiveFormation(units,new Vector3(-20,0,0),false,false);
            var archer=units.First(u=>u.Kind==UnitKind.Archer);var guard=units.First(u=>u.Kind==UnitKind.Guard);
            Assert.That(guard.Agent.destination.z,Is.GreaterThan(archer.Agent.destination.z),"Melee must occupy the leading formation row.");
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
        [UnityTest] public IEnumerator CountryReinforcementsUseLivingFiveWaveCapAndReplenishAfterRoundSix()
        {
            var countryTowns = battle.Towns.Where(t => t.State.Country == 0).ToArray();
            Assert.That(countryTowns.Length, Is.EqualTo(2));
            foreach (var town in countryTowns) town.State.Owner = 0;
            battle.SendMessage("CountryReinforcements"); yield return null;
            Assert.That(battle.Units.Count(u => u && u.Team == 0 && u.OriginCountry == 0), Is.EqualTo(2));
            for (int wave = 0; wave < 4; wave++) battle.SendMessage("CountryReinforcements");
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
        [UnityTest] public IEnumerator CapitalsWinOnCapturedFoundingCapitalWithOtherEnemyForcesRemaining()
        {
            battle.Mode = BattleSession.VictoryMode.Capitals; var redCapital = battle.Towns.First(t => t.IsCapital && t.FoundingTeam == 1); redCapital.State.Owner = 0;
            Assert.That(battle.Towns.Count(t => t.State.Owner == 1), Is.GreaterThan(0)); Assert.That(battle.Units.Any(u => u && u.Team == 1), Is.True);
            yield return null; Assert.That(battle.Winner, Is.EqualTo(0));
        }
    }
}
