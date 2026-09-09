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
        public IEnumerator RelaxedFirstRecruitLeavesPromptlyThenResumesNormalCadence()
        {
            yield return VerifyOpeningCadence(BattleSession.AiDifficulty.Relaxed);
        }

        [UnityTest]
        public IEnumerator StandardFirstRecruitLeavesPromptlyThenResumesNormalCadence()
        {
            yield return VerifyOpeningCadence(BattleSession.AiDifficulty.Standard);
        }

        IEnumerator VerifyOpeningCadence(BattleSession.AiDifficulty difficulty)
        {
            typeof(BattleSession).GetProperty("Difficulty").SetValue(battle,difficulty);
            var commander=new SkirmishCommander(battle,1);
            var home=battle.Towns.First(town=>town.State.Owner==1&&!OnIsland(town.ClaimPoint));
            var path=new NavMeshPath();
            var target=battle.Towns.First(town=>town!=home&&!OnIsland(town.ClaimPoint)&&
                Vector3.Distance(home.Rally,town.ClaimPoint)>20&&
                NavMesh.CalculatePath(home.Rally,town.ClaimPoint,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete);
            foreach(var town in battle.Towns)town.State.Owner=town==target?-1:1;
            foreach(var unit in battle.Units)if(unit&&unit.Team!=1)unit.gameObject.SetActive(false);
            battle.Economy.Gold[1]=0;
            var guards=battle.Units.Where(unit=>unit&&unit.Team==1&&unit.IsGarrison).ToArray();
            // A ready mobile recruit isolates dispatch timing from training and
            // combat. Opening recruitment itself is covered by the 16-player test.
            var recruit=BattleTestScenario.Mobile(battle,1,UnitKind.Archer,home.Rally);
            battle.AiEnabled=true;
            long before=battle.Commands.AppliedCount;
            float started=battle.BattleTime;
            while(battle.Commands.AppliedCount==before&&battle.BattleTime<started+3)
                AdvanceCommander(commander);
            Assert.That(battle.Commands.AppliedCount,Is.GreaterThan(before));
            Assert.That(battle.BattleTime-started,Is.LessThan(3));
            Assert.That(recruit.IsIdle,Is.False,"Both difficulties dispatch their first ready recruit promptly.");
            Assert.That(guards.All(unit=>unit.IsGarrison),Is.True,"Opening orders cannot empty guarded posts.");

            var secondWave=BattleTestScenario.MobileArmy(battle,1,UnitKind.Archer,3,home.Rally);
            long afterOpening=battle.Commands.AppliedCount;
            float nextDecision=(float)typeof(SkirmishCommander).GetField("nextDecision",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(commander);
            while(battle.BattleTime+SimClock.StepSeconds<nextDecision)
                AdvanceCommander(commander);
            Assert.That(battle.Commands.AppliedCount,Is.EqualTo(afterOpening),"Fast offensive polling ends after the first dispatch.");
            Assert.That(secondWave.All(unit=>unit.IsIdle),Is.True);
            while(battle.Commands.AppliedCount==afterOpening&&battle.BattleTime<nextDecision+1)
                AdvanceCommander(commander);
            Assert.That(battle.Commands.AppliedCount,Is.GreaterThan(afterOpening),"Later waves use the regular difficulty cadence.");
            battle.AiEnabled=false;
            yield return null;
        }

        void AdvanceCommander(SkirmishCommander commander)
        {
            battle.Clock.Advance(SimClock.StepSeconds,false,delta=>{commander.Tick(delta);battle.Commands.Tick();});
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
        public IEnumerator ReachabilityCursorSurvivesMobileRosterChurn()
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

            // Replace a unit that was part of the bounded probe set. The target
            // ranking is unchanged, but the mobile roster (and entity IDs) is not.
            // The next decision must continue at the saved candidate cursor.
            force[11].gameObject.SetActive(false);
            var replacement=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,Sample(mainland.ClaimPoint));

            // Once the opening grace period has elapsed, an ownership update
            // must not rewind the cursor while the ordered candidate identities
            // remain the same.
            battle.Clock.Advance(101f,false,_=>{});
            mainland.State.Owner=0;

            InvokeOffense(false);battle.Commands.Tick();
            Assert.That(replacement.IsIdle,Is.True,"The newly recruited unit is the reserved roster tail, not part of this wave.");
            yield return null;
            float deadline=Time.realtimeSinceStartup+2;
            var commanded=force.Take(11).Concat(new[]{force[12]}).ToArray();
            while(commanded.Any(unit=>unit.Agent.pathPending)&&Time.realtimeSinceStartup<deadline)yield return null;
            foreach(var unit in commanded)
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
