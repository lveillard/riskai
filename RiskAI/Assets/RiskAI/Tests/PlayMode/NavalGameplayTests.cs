using System.Collections;
using System.Linq;
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
   var home=naval.Harbors.First(h=>h.Owner==0);var island=naval.Harbors.First(h=>h.IsIsland);
   island.Defense.TakeDamage(10000,0);Assert.That(island.Defense.IsAlive,Is.False,"The transport fixture destroys the island tower so capture remains a landing test.");
   var ship=naval.Ships.First(s=>s.Team==0&&s.Kind==ShipKind.Transport);
   var soldiers=battle.Units.Where(u=>u.Team==0).OrderBy(u=>Vector3.Distance(u.transform.position,home.Landing)).Take(3).ToArray();
   int population=battle.Population(0);float health=soldiers[0].Health;
   foreach(var u in soldiers)Assert.That(ship.TryEmbark(u),Is.True,"Starter dock troops must embark without teleporting.");
   Assert.That(ship.CargoCount,Is.EqualTo(3));Assert.That(battle.Population(0),Is.EqualTo(population));Assert.That(soldiers.All(u=>!u.IsAlive),Is.True);
   var input=Object.FindFirstObjectByType<RtsController>();input.SelectAll();Assert.That(input.Selection.All(u=>u.Team==0&&u.IsAlive),Is.True);Assert.That(input.Selection.Intersect(soldiers).Count(),Is.Zero);
   Assert.That(SeaNavigation.TryBuildPath(ship.transform.position,island.Berth,out var route),Is.True);
   var anchor=ship.transform.position;foreach(var p in route){Assert.That(SeaNavigation.ClearSegment(anchor,p),Is.True);anchor=p;}
   ship.MoveTo(island.Berth);Time.timeScale=4;float deadline=Time.realtimeSinceStartup+18;
   while(Vector3.Distance(ship.transform.position,island.Berth)>1&&Time.realtimeSinceStartup<deadline){Assert.That(SeaNavigation.HasClearance(ship.transform.position),Is.True);yield return null;}
   Assert.That(Vector3.Distance(ship.transform.position,island.Berth),Is.LessThan(1),"Ship must reach the island through the ocean.");
   yield return new WaitForSeconds(1);Assert.That(island.Owner,Is.EqualTo(-1));Assert.That(island.CaptureProgress,Is.Zero,"Embarked units cannot occupy an island.");
   Assert.That(ship.Unload(island),Is.True);Assert.That(soldiers.All(u=>u.IsAlive&&u.Agent.isOnNavMesh),Is.True);Assert.That(soldiers[0].Health,Is.EqualTo(health));
   yield return new WaitForSeconds(8);Assert.That(island.Owner,Is.Zero);Assert.That(ship.CargoCount,Is.Zero);Assert.That(battle.Population(0),Is.EqualTo(population));
   Assert.That(battle.Economy.Towns.Contains(island.State),Is.True);
  }
  [UnityTest] public IEnumerator NavalPurchasesCancelRefundAndCompleteExactlyOnce()
  {
   var port=naval.Harbors.First(h=>h.Owner==0);battle.Economy.Gold[0]=300;
   Assert.That(port.Buy(ShipKind.Galley),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(225));
   Assert.That(port.CancelTraining(0),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(300));
   Assert.That(port.Buy(ShipKind.Transport),Is.Null);Assert.That(battle.Economy.Gold[0],Is.EqualTo(255));
   port.LinkedTown.State.Owner=1;yield return null;Assert.That(port.QueueCount,Is.Zero);Assert.That(battle.Economy.Gold[0],Is.EqualTo(300));
   port.LinkedTown.State.Owner=0;yield return null;int count=naval.Ships.Count;
   Assert.That(port.Buy(ShipKind.Galley),Is.Null);Time.timeScale=4;yield return new WaitForSeconds(4.3f);
   Assert.That(naval.Ships.Count,Is.EqualTo(count+1));Assert.That(port.QueueCount,Is.Zero);Assert.That(battle.Economy.Gold[0],Is.EqualTo(225));
  }
  [UnityTest] public IEnumerator GalleyFiresAndTransportDestructionRemovesCargo()
  {
   var port=naval.Harbors.First(h=>h.Owner==0);var transport=naval.Ships.First(s=>s.Team==0&&s.Kind==ShipKind.Transport);
   var soldier=battle.Units.Where(u=>u.Team==0).OrderBy(u=>Vector3.Distance(u.transform.position,port.Landing)).First();Assert.That(transport.TryEmbark(soldier),Is.True);
   int before=battle.Population(0);Assert.That(SeaNavigation.TryNearestOcean(transport.transform.position+Vector3.forward*9,8,out var spot),Is.True);
   var enemy=naval.Spawn(1,ShipKind.Galley,spot);Assert.That(enemy,Is.Not.Null);float health=transport.Health;enemy.Attack(transport);
   yield return new WaitForSeconds(1.3f);Assert.That(transport.Health,Is.LessThan(health),"A galley must fire a projectile which resolves damage.");
   transport.TakeDamage(10000,1,enemy);yield return null;Assert.That(battle.Population(0),Is.EqualTo(before-1));Assert.That(battle.Units.Contains(soldier),Is.False);Assert.That(naval.Ships.Contains(transport),Is.False);
  }
  [UnityTest] public IEnumerator RiverHasDownhillBedAndIslandsAreSeparateLandmasses()
  {
   for(int i=0;i<TerrainHydrology.Samples.Length-1;i++)
   {
    var a=TerrainHydrology.Samples[i];var b=TerrainHydrology.Samples[i+1];Assert.That(a.y,Is.GreaterThanOrEqualTo(b.y));
    for(int s=0;s<10;s++){var p=Vector3.Lerp(a,b,s/10f);if(p.z<MapLayout.Coast(p.x))Assert.That(MapLayout.Height(p.x,p.z),Is.LessThan(p.y-.25f));}
   }
   foreach(var port in naval.Harbors){Assert.That(MapLayout.IsLand(port.Landing.x,port.Landing.z),Is.True,port.DisplayName);Assert.That(SeaNavigation.HasClearance(port.Berth),Is.True,port.DisplayName);}
   Assert.That(SeaNavigation.TryBuildPath(naval.Harbors[0].Berth,battle.Towns[0].transform.position,out _),Is.False);
   yield return null;
  }
  [UnityTearDown] public IEnumerator TearDown(){Time.timeScale=1;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.RandomCities;SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);}
 }
}
