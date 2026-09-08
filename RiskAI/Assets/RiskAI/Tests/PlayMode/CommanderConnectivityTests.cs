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
    public sealed class CommanderConnectivityTests
    {
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
            scene=SceneManager.CreateScene("Commander connectivity");SceneManager.SetActiveScene(scene);
            new GameObject("Commander connectivity bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CountryPrioritySkipsIslandAndOnlyCommandsReachableOrigins()
        {
            var home=battle.Towns.First(town=>town.State.Owner==1);
            var island=battle.Towns.First(town=>OnIsland(town.ClaimPoint));
            var otherIsland=battle.Towns.First(town=>town!=island&&OnIsland(town.ClaimPoint)&&IslandIndex(town.ClaimPoint)!=IslandIndex(island.ClaimPoint));
            var mainland=battle.Towns.First(town=>!OnIsland(town.ClaimPoint)&&town.State.Country!=home.State.Country);
            // Leave one owned country so the island can be a higher-priority
            // completion target. The mainland candidate remains the next legal
            // neutral objective.
            foreach(var town in battle.Towns)town.State.Owner=town==home?1:0;
            island.State.Owner=-1;island.State.Country=home.State.Country;
            otherIsland.State.Owner=1;
            mainland.State.Owner=-1;
            if(otherIsland.Defender)otherIsland.Defender.gameObject.SetActive(false);

            var partial=new NavMeshPath();
            var mainlandForce=BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,2,Sample(mainland.ClaimPoint));
            var islandForce=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,Sample(otherIsland.ClaimPoint));
            Assert.That(NavMesh.CalculatePath(mainlandForce[0].transform.position,island.ClaimPoint,NavMesh.AllAreas,partial),Is.True);
            Assert.That(partial.status,Is.EqualTo(NavMeshPathStatus.PathPartial),"The priority objective must require transport from the mainland fixture.");

            typeof(SkirmishCommander).GetMethod("IssueOffensiveOrders",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(battle.Commander,new object[]{true});
            battle.Commands.Tick();
            yield return null;
            float deadline=Time.realtimeSinceStartup+2;
            while(mainlandForce.Any(unit=>unit.Agent.pathPending)&&Time.realtimeSinceStartup<deadline)yield return null;

            foreach(var unit in mainlandForce)
            {
                Assert.That(unit.Agent.pathStatus,Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(Vector3.Distance(unit.Agent.destination,mainland.ClaimPoint),Is.LessThan(4f),"The next reachable objective must receive the formation.");
            }
            Assert.That(islandForce.IsIdle||islandForce.IsGarrison,Is.True,"The isolated troop may guard its own vacant post, but must not receive an unreachable land order.");
            Assert.That(islandForce.Agent.hasPath,Is.False);
        }

        [UnityTest]
        public IEnumerator ReachabilityCursorContinuesPastBudgetedIslandCandidates()
        {
            var home=battle.Towns.First(town=>town.State.Owner==1);
            var firstIsland=battle.Towns.First(town=>OnIsland(town.ClaimPoint));
            var secondIsland=battle.Towns.First(town=>town!=firstIsland&&OnIsland(town.ClaimPoint)&&IslandIndex(town.ClaimPoint)!=IslandIndex(firstIsland.ClaimPoint));
            var mainland=battle.Towns.First(town=>!OnIsland(town.ClaimPoint)&&town.State.Country!=home.State.Country);
            foreach(var town in battle.Towns)town.State.Owner=town==home?1:0;
            firstIsland.State.Owner=-1;firstIsland.State.Country=home.State.Country;
            secondIsland.State.Owner=-1;secondIsland.State.Country=home.State.Country;
            mainland.State.Owner=-1;

            var force=BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,13,Sample(mainland.ClaimPoint));
            AssertPartial(force[0],firstIsland.ClaimPoint);AssertPartial(force[0],secondIsland.ClaimPoint);

            InvokeOffense(false);battle.Commands.Tick();
            foreach(var unit in force)Assert.That(unit.IsIdle,Is.True,"The first 24 probes cover only the two unreachable island candidates.");

            InvokeOffense(false);battle.Commands.Tick();
            yield return null;
            float deadline=Time.realtimeSinceStartup+2;
            while(force.Take(12).Any(unit=>unit.Agent.pathPending)&&Time.realtimeSinceStartup<deadline)yield return null;
            foreach(var unit in force.Take(12))
            {
                Assert.That(unit.Agent.pathStatus,Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(Vector3.Distance(unit.Agent.destination,mainland.ClaimPoint),Is.LessThan(5f),"The next decision must resume with the third, reachable candidate.");
            }
        }

        void InvokeOffense(bool relaxed)
        {
            typeof(SkirmishCommander).GetMethod("IssueOffensiveOrders",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(battle.Commander,new object[]{relaxed});
        }

        static void AssertPartial(Soldier unit,Vector3 destination)
        {
            var path=new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(unit.transform.position,destination,NavMesh.AllAreas,path),Is.True);
            Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathPartial));
        }

        static bool OnIsland(Vector3 point)
        {
            return IslandIndex(point)>=0;
        }

        static int IslandIndex(Vector3 point)
        {
            for(int island=0;island<MapLayout.Islands.Length;island++)if(MapLayout.IslandDistance(point.x,point.z,island)>=0)return island;
            return -1;
        }

        static Vector3 Sample(Vector3 point)
        {
            Assert.That(NavMesh.SamplePosition(point,out var hit,2f,NavMesh.AllAreas),Is.True,"Fixture point must be on the NavMesh.");
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
