using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RiskAI.Tests
{
 public sealed class NavalGameplayTests
 {
  Scene scene,previous;BattleSession battle;NavalWorld naval;
  [UnitySetUp] public IEnumerator SetUp()
  {
   BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
   previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Naval gameplay");SceneManager.SetActiveScene(scene);
   new GameObject("Naval test bootstrap").AddComponent<RiskBootstrap>();battle=BattleSession.Current;battle.AiEnabled=false;
   Object.FindFirstObjectByType<RtsController>().enabled=false;naval=NavalWorld.Current;yield return null;
  }
  [UnityTest] public IEnumerator TransportActuallySailsAndDisembarkedTroopsCaptureIsland()
  {
   var home=naval.Harbors.First(h=>h.State.Owner==0);var island=naval.Harbors.First(h=>h.IsIsland);
   var islandGuard=island.Defender;island.ClaimZone.SetDefender(null);islandGuard.gameObject.SetActive(false);island.State.Owner=-1;island.Defense.enabled=false;
   Assert.That(island.Defender,Is.Null,"This transport fixture deliberately uses an unguarded neutral island.");
   var ship=BattleTestScenario.Ship(naval,0,ShipKind.Transport,home.Berth);
   var soldiers=BattleTestScenario.MobileArmy(battle,0,UnitKind.Footman,3,home.Landing);
   int population=battle.RecruitmentPopulation(0);float health=soldiers[0].Health;
   foreach(var u in soldiers)Assert.That(ship.TryEmbark(u),Is.True,"The explicit transport fixture must embark without teleporting.");
   Assert.That(ship.CargoCount,Is.EqualTo(3));Assert.That(battle.RecruitmentPopulation(0),Is.EqualTo(population));Assert.That(soldiers.All(u=>!u.IsAlive),Is.True);
   var input=Object.FindFirstObjectByType<RtsController>();input.SelectAll();Assert.That(input.Selection.All(u=>u.Team==0&&u.IsAlive),Is.True);Assert.That(input.Selection.Intersect(soldiers).Count(),Is.Zero);
   Assert.That(SeaNavigation.TryBuildPath(ship.transform.position,island.Berth,out var route),Is.True);
   var anchor=ship.transform.position;foreach(var p in route){Assert.That(SeaNavigation.ClearSegment(anchor,p),Is.True);anchor=p;}
   ship.MoveTo(island.Berth);Time.timeScale=4;float deadline=Time.realtimeSinceStartup+18;
   while(Vector3.Distance(ship.transform.position,island.Berth)>1&&Time.realtimeSinceStartup<deadline){Assert.That(SeaNavigation.HasClearance(ship.transform.position),Is.True);yield return null;}
   Assert.That(Vector3.Distance(ship.transform.position,island.Berth),Is.LessThan(1),"Ship must reach the island through the ocean.");
   yield return new WaitForSeconds(1);Assert.That(island.Owner,Is.EqualTo(-1));Assert.That(island.CaptureProgress,Is.Zero,"Embarked units cannot occupy an island.");
   Assert.That(ship.Unload(island),Is.True);Assert.That(soldiers.All(u=>u.IsAlive&&u.Agent.isOnNavMesh),Is.True);Assert.That(soldiers[0].Health,Is.EqualTo(health));
   yield return new WaitForSeconds(8);Assert.That(island.Owner,Is.Zero);Assert.That(ship.CargoCount,Is.Zero);
   Assert.That(island.Defender,Is.Not.Null);Assert.That(soldiers,Does.Contain(island.Defender),"The captured harbor anchors one landed soldier as its garrison.");
   Assert.That(battle.RecruitmentPopulation(0),Is.EqualTo(population-1),"A harbor garrison is excluded from the mobile recruitment budget.");
   Assert.That(battle.Economy.Towns.Contains(island.State),Is.False,"Island staging harbors do not create an independent economy payout source.");
  }
  [UnityTest] public IEnumerator NavalPurchasesCancelRefundAndCompleteExactlyOnce()
  {
   var port=naval.Harbors.First(h=>h.State.Owner==0);const int budget=300;int galleyCost=Harbor.Cost(ShipKind.Galley),transportCost=Harbor.Cost(ShipKind.Transport);battle.Economy.Gold[0]=budget;
   Assert.That(port.Buy(ShipKind.Galley),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(budget-galleyCost));
   Assert.That(port.CancelTraining(0),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(budget));
   Assert.That(port.Buy(ShipKind.Transport),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(budget-transportCost));
   Assert.That(port.LinkedTown,Is.Not.Null);int linkedOwner=port.LinkedTown.State.Owner;
   long tick=battle.Clock.TickCount;float deadline=Time.realtimeSinceStartup+2;
   port.State.Owner=1;while(battle.Clock.TickCount==tick&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.That(port.QueueCount,Is.Zero);Assert.That(battle.Economy.Gold[0],Is.EqualTo(budget));Assert.That(port.LinkedTown.State.Owner,Is.EqualTo(linkedOwner));
   tick=battle.Clock.TickCount;deadline=Time.realtimeSinceStartup+2;
   port.State.Owner=0;while(battle.Clock.TickCount==tick&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.That(battle.Clock.TickCount,Is.GreaterThan(tick));int count=naval.Ships.Count;
   Assert.That(port.Buy(ShipKind.Galley),Is.Null);
   float finishAt=battle.BattleTime+Harbor.TrainTime(ShipKind.Galley)+.2f;
   deadline=Time.realtimeSinceStartup+10;
   while(naval.Ships.Count==count&&battle.BattleTime<finishAt&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.That(naval.Ships.Count,Is.EqualTo(count+1));Assert.That(port.QueueCount,Is.Zero);Assert.That(battle.Economy.Gold[0],Is.EqualTo(budget-galleyCost));
  }
  [UnityTest] public IEnumerator AiSavesForAndBuysItsFirstGalleyThroughThePortQueue()
  {
   var city=battle.Towns.First(t=>t.State.Owner==1);
   BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,2,city.Rally);
   battle.Economy.Gold[1]=Harbor.Cost(ShipKind.Galley);
   battle.AiEnabled=true;
   naval.SimTick(.05f);
   Assert.That(naval.PendingShips(1),Is.Zero,"The naval grace period must still apply.");
   while(battle.BattleTime<battle.AiFirstNavalOffensiveTime+.1f)battle.Clock.Advance(.4,false,_=>{});
   battle.Commander.Tick(.05f);
   Assert.That(battle.Economy.Gold[1],Is.EqualTo(Harbor.Cost(ShipKind.Galley)),"The army must leave savings for the first purchased ship.");
   naval.SimTick(.05f);
   Assert.That(naval.PendingShips(1),Is.EqualTo(1));
   Assert.That(naval.Ships,Is.Empty,"Buying a ship must not bypass its training queue.");
   Assert.That(battle.Economy.Gold[1],Is.Zero);
   battle.AiEnabled=false;
   float deadline=Time.realtimeSinceStartup+Harbor.TrainTime(ShipKind.Galley)+3;
   while(naval.Ships.Count==0&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.That(naval.Ships.Count,Is.EqualTo(1));
   Assert.That(naval.Ships[0].Team,Is.EqualTo(1));
   Assert.That(naval.Ships[0].Kind,Is.EqualTo(ShipKind.Galley));
   Assert.That(naval.PendingShips(1),Is.Zero);
  }
  [UnityTest] public IEnumerator AiFleetPassKeepsAnExistingRouteInsteadOfRebuildingIt()
  {
   var home=naval.Harbors.First(h=>h.Owner==1);var ship=BattleTestScenario.Ship(naval,1,ShipKind.Galley,home.Berth);
   var target=naval.Harbors.Where(h=>PlayerRules.IsPlayer(h.Owner)&&h.Owner!=1&&h.CanLaunch).OrderBy(h=>(h.Berth-ship.transform.position).sqrMagnitude).First();
   while(battle.BattleTime<battle.AiFirstNavalOffensiveTime+.1f)battle.Clock.Advance(.4,false,_=>{});
   ship.MoveTo(target.Berth,true);Assert.That(ship.IsAtOrRoutingTo(target.Berth),Is.True);
   long revision=ship.RouteRevision;battle.AiEnabled=true;
   naval.SimTick(.05f);
   Assert.That(ship.RouteRevision,Is.EqualTo(revision),"The staggered fleet pass must retain an active route to its chosen rally.");
   Assert.That(ship.IsAtOrRoutingTo(target.Berth),Is.True);yield return null;
  }
  [UnityTest] public IEnumerator StalledActiveRouteBecomesEligibleForAiReissue()
  {
   var home=naval.Harbors.First(h=>h.Owner==0&&!h.IsIsland);var ship=BattleTestScenario.Ship(naval,0,ShipKind.Galley,home.Berth);
   var target=naval.Harbors.First(h=>h!=home&&h.CanLaunch);
   const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(Ship);
   var route=(List<Vector3>)type.GetField("route",hidden).GetValue(ship);
   // This represents a route whose ocean clearance disappeared after it was accepted.
   route.Clear();route.Add(home.Landing);type.GetField("routeIndex",hidden).SetValue(ship,0);
   type.GetField("routeGoal",hidden).SetValue(ship,target.Berth);type.GetField("hasRouteGoal",hidden).SetValue(ship,true);
   type.GetField("lastRouteProgressAt",hidden).SetValue(ship,battle.BattleTime);
   Assert.That(ship.IsAtOrRoutingTo(target.Berth),Is.True,"An active route initially suppresses duplicate AI orders.");
   float stalledUntil=battle.BattleTime+6.2f;while(battle.BattleTime<stalledUntil)battle.Clock.Advance(.1,false,battle.World.Tick);
   Assert.That(ship.IsAtOrRoutingTo(target.Berth),Is.False,"A blocked route must become eligible for a fresh AI order after the bounded progress grace.");
   yield return null;
  }
  [UnityTest] public IEnumerator GalleyFiresAndTransportDestructionRemovesCargo()
  {
   var port=naval.Harbors.First(h=>h.State.Owner==0);var transport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,port.Berth);
   var soldier=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,port.Landing);Assert.That(transport.TryEmbark(soldier),Is.True);
   int before=battle.Population(0);Assert.That(SeaNavigation.TryNearestOcean(transport.transform.position+Vector3.forward*9,8,out var spot),Is.True);
   var enemy=naval.Spawn(1,ShipKind.Galley,spot);Assert.That(enemy,Is.Not.Null);float health=transport.Health;enemy.Attack(transport);
   yield return new WaitForSeconds(1.3f);Assert.That(transport.Health,Is.LessThan(health),"A galley must fire a projectile which resolves damage.");
   transport.TakeDamage(10000,1,enemy);yield return null;Assert.That(battle.Population(0),Is.EqualTo(before-1));Assert.That(battle.Units.Contains(soldier),Is.False);Assert.That(naval.Ships.Contains(transport),Is.False);
  }
  [UnityTest] public IEnumerator GalleyAutoTargetsCoastalSoldierAtMainlandHarbor()
  {
   var port=naval.Harbors.First(h=>h.State.Owner==0&&!h.IsIsland);
   var galley=BattleTestScenario.Ship(naval,0,ShipKind.Galley,port.Berth);
   galley.Stop();galley.transform.position=port.Berth;
   foreach(var tower in battle.Towers.ToArray())if(tower)tower.gameObject.SetActive(false);
   foreach(var ship in naval.Ships.ToArray())if(ship&&ship!=galley)ship.gameObject.SetActive(false);
   var coastal=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,port.Landing);
   coastal.HoldPosition();
   foreach(var unit in battle.Units.ToArray())if(unit&&unit!=coastal)unit.gameObject.SetActive(false);
   float before=coastal.Health;
   float deadline=Time.realtimeSinceStartup+2.5f;
   while(galley.CurrentTarget!=coastal&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.That(galley.CurrentTarget,Is.SameAs(coastal),"A galley should acquire a coastal soldier through the spatial target index.");
   yield return new WaitForSecondsRealtime(1.6f);
   Assert.That(coastal.Health,Is.LessThan(before),"The galley should resolve a projectile against its coastal target.");
  }
  [UnityTest] public IEnumerator GalleyAttackMoveResumesItsOriginalDestinationAfterCombat()
  {
   var home=naval.Harbors.First(h=>h.State.Owner==0&&!h.IsIsland);
   var destination=naval.Harbors.Where(h=>h!=home&&h.CanLaunch).OrderBy(h=>(h.Berth-home.Berth).sqrMagnitude).First();
   var galley=BattleTestScenario.Ship(naval,0,ShipKind.Galley,home.Berth);
   var enemy=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,home.Landing);
   foreach(var tower in battle.Towers.ToArray())if(tower)tower.gameObject.SetActive(false);
   foreach(var ship in naval.Ships.ToArray())if(ship&&ship!=galley)ship.gameObject.SetActive(false);
   foreach(var unit in battle.Units.ToArray())if(unit&&unit!=enemy)unit.gameObject.SetActive(false);
   enemy.HoldPosition();galley.MoveTo(destination.Berth,true);
   Assert.That(galley.LastActionError,Is.Null);
   Assert.That(galley.IsAtOrRoutingTo(destination.Berth),Is.True);
   battle.Spatial.Rebuild(battle.Targets,battle.Units);galley.SimTick(.05f);
   Assert.That(galley.CurrentTarget,Is.SameAs(enemy),"Attack-move must interrupt for a nearby enemy.");

   enemy.TakeDamage(10000,galley.Team,galley);galley.SimTick(.05f);
   Assert.That(galley.CurrentTarget,Is.Null);
   Assert.That(galley.IsAtOrRoutingTo(destination.Berth),Is.True,"After combat the ship must resume the destination saved by attack-move.");
   yield return null;
  }
  [UnityTest] public IEnumerator GalleyDamageReactionKeepsAnExplicitAttackTarget()
  {
   var home=naval.Harbors.First(h=>h.State.Owner==0&&!h.IsIsland);
   var galley=BattleTestScenario.Ship(naval,0,ShipKind.Galley,home.Berth);
   var orderedTarget=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,home.Landing);
   var attacker=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,home.Landing+Vector3.right*1.5f);
   orderedTarget.HoldPosition();attacker.HoldPosition();galley.Attack(orderedTarget);
   Assert.That(galley.CurrentTarget,Is.SameAs(orderedTarget));

   galley.TakeDamage(1,attacker.Team,attacker);
   Assert.That(galley.CurrentTarget,Is.SameAs(orderedTarget),"Retaliation must not replace a living explicit attack target.");
   yield return null;
  }
  [UnityTest] public IEnumerator RiverHasDownhillBedAndIslandsAreSeparateLandmasses()
  {
   for(int i=0;i<TerrainHydrology.Samples.Length-1;i++)
   {
    var a=TerrainHydrology.Samples[i];var b=TerrainHydrology.Samples[i+1];Assert.That(a.y,Is.GreaterThanOrEqualTo(b.y));
    for(int s=0;s<10;s++)
    {
     var p=Vector3.Lerp(a,b,s/10f);if(p.z>=MapLayout.Coast(p.x))continue;
     float bed=MapLayout.Height(p.x,p.z);Assert.That(bed,Is.LessThan(p.y-.25f));
     if(p.y>-.2f)Assert.That(bed,Is.GreaterThanOrEqualTo(p.y-.8f),"An inland river must have a shallow bed, not float above a low valley.");
    }
   }
   foreach(var port in naval.Harbors){Assert.That(MapLayout.IsLand(port.Landing.x,port.Landing.z),Is.True,port.DisplayName);Assert.That(SeaNavigation.HasClearance(port.Berth),Is.True,port.DisplayName);}
   Assert.That(SeaNavigation.TryBuildPath(naval.Harbors[0].Berth,battle.Towns[0].transform.position,out _),Is.False);
   yield return null;
  }
  [UnityTearDown] public IEnumerator TearDown(){Time.timeScale=1;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.RandomCities;SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);}
 }
}
