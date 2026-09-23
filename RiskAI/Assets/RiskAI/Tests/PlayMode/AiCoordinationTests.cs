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
    /// <summary>Army grouping, proportional defense, naval threats and the Hard difficulty.</summary>
    public sealed class AiCoordinationTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        Scene previous, scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.AiDifficulty previousDifficulty;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;
            previousLayout=BattleSession.LayoutForNewMatch;previousDifficulty=BattleSession.DifficultyForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.DifficultyForNewMatch=BattleSession.AiDifficulty.Relaxed;
            scene=SceneManager.CreateScene("AI coordination");SceneManager.SetActiveScene(scene);
            new GameObject("AI coordination bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;
            yield return null;
        }

        SkirmishCommander Commander => (SkirmishCommander)battle.Commander;
        void SetDifficulty(BattleSession.AiDifficulty difficulty) => typeof(BattleSession).GetProperty("Difficulty").SetValue(battle,difficulty);
        void Invoke(string method,params object[] arguments) => typeof(SkirmishCommander).GetMethod(method,Hidden).Invoke(Commander,arguments);

        [UnityTest]
        public IEnumerator OneRaiderAtAPostNoLongerFreezesTheWholeOffense()
        {
            var harassed=battle.Towns.First(t=>t.State.Owner==1&&!OnIsland(t.ClaimPoint));
            // Four swordsmen outweigh the post's own guardian and tower.
            var raiders=BattleTestScenario.MobileArmy(battle,0,UnitKind.Footman,4,Sample(harassed.ClaimPoint+Vector3.right*1.5f,4));
            foreach(var raider in raiders)raider.HoldPosition();
            var path=new NavMeshPath();
            var far=battle.Towns.First(t=>t.State.Owner<0&&!OnIsland(t.ClaimPoint)&&Vector3.Distance(t.ClaimPoint,harassed.ClaimPoint)>70&&
                NavMesh.CalculatePath(t.Rally,harassed.ClaimPoint,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete);
            var army=BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,4,Sample(far.Rally,4));

            Invoke("DecideDefense");
            battle.Commands.Tick();
            var threats=(System.Collections.IList)typeof(SkirmishCommander).GetField("threats",Hidden).GetValue(Commander);
            Assert.That(threats.Count,Is.GreaterThan(0),"The raiders register as a threat to the post.");
            Invoke("IssueOffensiveOrders",true);
            battle.Commands.Tick();
            Assert.That(army.Any(unit=>!unit.IsIdle),Is.True,"A distant army keeps attacking while a single raider sits at one post.");
            Assert.That(Commander.ArmyCount,Is.GreaterThan(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WaveStagesOutsideTheTowerThenAssaultsTogether()
        {
            SetDifficulty(BattleSession.AiDifficulty.Standard);
            var home=battle.Towns.First(t=>t.State.Owner==1&&!OnIsland(t.ClaimPoint));
            var path=new NavMeshPath();
            var target=battle.Towns.First(t=>t.State.Owner<0&&!OnIsland(t.ClaimPoint)&&t.Defender&&
                Vector3.Distance(home.Rally,t.ClaimPoint)>45&&
                NavMesh.CalculatePath(home.Rally,t.ClaimPoint,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete);
            foreach(var town in battle.Towns)if(town!=target)town.State.Owner=1;
            var army=BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,6,home.Rally);

            Invoke("IssueOffensiveOrders",false);
            battle.Commands.Tick();
            yield return WaitForPaths(army);
            Assert.That(Commander.ArmyCount,Is.EqualTo(1));
            foreach(var unit in army)
            {
                float distance=FlatDistance(unit.Agent.destination,target.ClaimPoint);
                Assert.That(distance,Is.GreaterThan(13f),"The wave assembles outside the post tower's reach.");
                Assert.That(distance,Is.LessThan(30f),"The staging point is next to the objective.");
            }

            // Arrive together: the next strategic pass launches the whole wave.
            foreach(var unit in army)Assert.That(unit.Agent.Warp(unit.Agent.destination),Is.True);
            foreach(var unit in army)unit.Stop();
            Invoke("IssueOffensiveOrders",false);
            battle.Commands.Tick();
            yield return WaitForPaths(army);
            foreach(var unit in army)
                Assert.That(FlatDistance(unit.Agent.destination,target.ClaimPoint),Is.LessThan(6f),"The assembled wave attacks the post together.");
        }

        [UnityTest]
        public IEnumerator GalleyBombardingAHarborIsAnsweredByRangedDefendersOnly()
        {
            var naval=NavalWorld.Current;
            var harbor=naval.Harbors.FirstOrDefault(h=>h.Owner==1&&h.CanLaunch&&!h.IsIsland)??naval.Harbors.First(h=>h.CanLaunch&&!h.IsIsland);
            harbor.State.Owner=1;
            foreach(var ship in naval.Ships.ToArray())if(ship)ship.gameObject.SetActive(false);
            var galley=BattleTestScenario.Ship(naval,0,ShipKind.Galley,harbor.Berth);
            galley.Stop();
            var archer=BattleTestScenario.Mobile(battle,1,UnitKind.Archer,Sample(harbor.Landing+Vector3.right*6,6));
            var footman=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,Sample(harbor.Landing+Vector3.left*6,6));
            Invoke("DecideDefense");
            battle.Commands.Tick();
            Assert.That(archer.IsIdle,Is.False,"A galley at the berth is a threat, answered by ranged troops.");
            Assert.That(footman.IsIdle,Is.True,"Swordsmen cannot reach a ship and stay free for other orders.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HardCommanderRecruitsSupportForALargeArmyWithoutExtraGold()
        {
            SetDifficulty(BattleSession.AiDifficulty.Hard);
            Assert.That(battle.AiInterval,Is.EqualTo(AiDifficultyProfile.Hard.DecisionInterval));
            Assert.That(battle.DifficultyName,Is.EqualTo("Difícil"));
            var home=battle.Towns.First(t=>t.State.Owner==1&&!OnIsland(t.ClaimPoint));
            BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,6,home.Rally);
            BattleTestScenario.MobileArmy(battle,1,UnitKind.Archer,6,home.Rally+Vector3.right*5);
            battle.Economy.Gold[1]=40;
            int income=battle.Economy.Income(1);
            var commander=new SkirmishCommander(battle,1);
            battle.AiEnabled=true;
            while(battle.BattleTime<1f)battle.Clock.Advance(SimClock.StepSeconds,false,delta=>{commander.Tick(delta);battle.Commands.Tick();});
            battle.AiEnabled=false;
            var queued=battle.Towns.Where(t=>t.State.Owner==1).SelectMany(t=>Enumerable.Range(0,t.QueueCount).Select(t.QueuedKind))
                .Concat(battle.Units.Where(u=>u&&u.Team==1&&!u.IsGarrison).Select(u=>u.Kind)).ToArray();
            Assert.That(queued.Any(kind=>AiUnitAnalysis.For(kind).Role==AiUnitRole.Splash||AiUnitAnalysis.For(kind).Role==AiUnitRole.Healer),Is.True,
                "A twelve-unit army adds area damage or healers instead of another crossbow.");
            Assert.That(battle.Economy.Gold[1],Is.LessThan(40));
            Assert.That(battle.Economy.Gold[1],Is.GreaterThanOrEqualTo(0));
            Assert.That(battle.Economy.Income(1),Is.EqualTo(income),"Difficulty never changes income.");
            yield return null;
        }

        IEnumerator WaitForPaths(Soldier[] units)
        {
            float deadline=Time.realtimeSinceStartup+2;
            while(units.Any(unit=>unit.Agent.pathPending)&&Time.realtimeSinceStartup<deadline)yield return null;
        }

        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}

        static bool OnIsland(Vector3 point)
        {
            for(int island=0;island<MapLayout.Islands.Length;island++)if(MapLayout.IslandDistance(point.x,point.z,island)>=0)return true;
            return false;
        }

        static Vector3 Sample(Vector3 point,float radius)
        {
            Assert.That(NavMesh.SamplePosition(point,out var hit,radius,NavMesh.AllAreas),Is.True,"Fixture point must be on the NavMesh.");
            return hit.position;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;BattleSession.DifficultyForNewMatch=previousDifficulty;
            SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
