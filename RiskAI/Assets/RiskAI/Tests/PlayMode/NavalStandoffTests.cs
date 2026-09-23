using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    /// <summary>Warships fire from their weapon range instead of sailing into the target.</summary>
    public sealed class NavalStandoffTests
    {
        Scene scene, previous;
        BattleSession battle;
        NavalWorld naval;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Naval standoff");SceneManager.SetActiveScene(scene);
            new GameObject("Naval standoff bootstrap").AddComponent<RiskBootstrap>();battle=BattleSession.Current;battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;naval=NavalWorld.Current;
            foreach(var ship in naval.Ships.ToArray())if(ship)ship.gameObject.SetActive(false);
            foreach(var tower in battle.Towers.ToArray())if(tower)tower.gameObject.SetActive(false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrderedWarshipFiresFromRangeWithoutClosingIn()
        {
            yield return Standoff(NavalUnitKind.Warship,true);
        }

        [UnityTest]
        public IEnumerator IdleWarshipAcquiresAndHoldsItsPosition()
        {
            yield return Standoff(NavalUnitKind.Warship,false);
        }

        [UnityTest]
        public IEnumerator RangeIsMeasuredToTheTargetHullNotItsPivot()
        {
            var probe=BattleTestScenario.Ship(naval,0,NavalUnitKind.Warship,FirstClearBerth());
            float range=probe.Type.Weapon.Range;probe.TakeDamage(1e6f,1);
            Assert.That(FindOpenWater(range+1.5f,out var a,out var b),Is.True);
            var attacker=BattleTestScenario.Ship(naval,0,NavalUnitKind.Warship,a);
            var victim=BattleTestScenario.Ship(naval,1,NavalUnitKind.Battleship,b);
            attacker.Stop();victim.Stop();
            // Present the long hull end-on, so its stern is inside range and its pivot is not.
            var along=b-a;along.y=0;victim.transform.rotation=Quaternion.LookRotation(along.normalized);
            yield return null;
            float hull=FlatDistance(a,victim.ApproachPoint(a));
            Assume.That(hull,Is.LessThan(range),"The battleship hull must reach inside the warship range for this fixture.");
            Assert.That(FlatDistance(a,victim.transform.position),Is.GreaterThan(range));
            var start=attacker.transform.position;float health=victim.Health;
            attacker.Attack(victim);
            float deadline=Time.realtimeSinceStartup+6f;
            while(victim&&victim.Health>=health&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(!victim||victim.Health<health,Is.True,"A hull inside weapon range is a legal target.");
            Vector3 moved=attacker.transform.position-start;moved.y=0;
            Assert.That(moved.magnitude,Is.LessThan(1f),"The warship must not close in on a hull already in range.");
        }

        [UnityTest]
        public IEnumerator ClickingTheBowOfALongHullPicksTheShip()
        {
            Assert.That(FindOpenWater(10f,out var a,out _),Is.True);
            var victim=BattleTestScenario.Ship(naval,1,NavalUnitKind.Battleship,a);victim.Stop();
            yield return null;
            Assert.That(victim.TryGetHullBounds(out var hull),Is.True);
            var cam=Camera.main;Assert.That(cam,Is.Not.Null);
            cam.transform.position=victim.transform.position+new Vector3(28,30,0);cam.transform.LookAt(victim.transform.position);
            float half=Mathf.Max(hull.extents.x,hull.extents.z);
            var bow=victim.transform.position+victim.transform.forward*half*.85f+Vector3.up*.5f;
            Vector2 screen=cam.WorldToScreenPoint(bow);
            Assert.That(RtsPicking.Target(battle,cam,screen,-1),Is.SameAs(victim),"A click on the hull, not only its pivot, targets the ship.");
        }

        Vector3 FirstClearBerth()
        {
            foreach(var harbor in naval.Harbors)if(SeaNavigation.HasClearance(harbor.Berth))return harbor.Berth;
            Assert.Fail("No clear berth.");return default;
        }

        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}

        IEnumerator Standoff(NavalUnitKind kind,bool ordered)
        {
            Assert.That(FindOpenWater(25f,out var a,out var b),Is.True,"The fixture needs two clear sea points 25 m apart.");
            var attacker=BattleTestScenario.Ship(naval,0,kind,a);
            var victim=BattleTestScenario.Ship(naval,1,kind,b);
            Assert.That(attacker.Type.Weapon.Range,Is.GreaterThan(25f));
            attacker.Stop();victim.Stop();
            var start=attacker.transform.position;float health=victim.Health;
            if(ordered)attacker.Attack(victim);
            float deadline=Time.realtimeSinceStartup+6f;
            while(victim&&victim.Health>=health&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(victim&&victim.Health<health||!victim,Is.True,"The warship must hit a ship inside its weapon range.");
            // Keep firing a little longer; the attacker must stay put the whole time.
            float settle=Time.realtimeSinceStartup+1.5f;
            while(Time.realtimeSinceStartup<settle)yield return null;
            Vector3 moved=attacker.transform.position-start;moved.y=0;
            Assert.That(moved.magnitude,Is.LessThan(1f),"An in-range warship fires from where it stands instead of sailing at its target.");
        }

        static bool FindOpenWater(float spacing,out Vector3 a,out Vector3 b)
        {
            a=b=default;
            var naval=NavalWorld.Current;
            foreach(var harbor in naval.Harbors)
            {
                var seaward=harbor.Berth-harbor.Landing;seaward.y=0;if(seaward.sqrMagnitude<.01f)continue;seaward.Normalize();
                var side=new Vector3(seaward.z,0,-seaward.x);
                for(float distance=12;distance<=60;distance+=4)
                {
                    var first=harbor.Berth+seaward*distance;first.y=-.24f;
                    foreach(var direction in new[]{side,-side,seaward})
                    {
                        var second=first+direction*spacing;
                        if(SeaNavigation.HasClearance(first)&&SeaNavigation.HasClearance(second)&&SeaNavigation.ClearSegment(first,second)&&
                           !Physics.Raycast(first+Vector3.up*.55f,(second-first).normalized,spacing,1<<MapLayout.TerrainLayer,QueryTriggerInteraction.Ignore))
                        {a=first;b=second;return true;}
                    }
                }
            }
            return false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.RandomCities;
            SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
