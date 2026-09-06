using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class HarborPresentationTests
    {
        Scene previousScene;
        Scene scene;
        BattleSession battle;
        NavalWorld naval;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers;
        int previousSeed;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene=SceneManager.GetActiveScene();
            previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;previousMode=BattleSession.ModeForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=59201;
            scene=SceneManager.CreateScene("Harbor presentation");SceneManager.SetActiveScene(scene);
            new GameObject("Harbor presentation bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;naval=NavalWorld.Current;
            var controller=Object.FindFirstObjectByType<RtsController>();if(controller)controller.enabled=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator NavalQueueHasFiveSlotsRefundsOnCaptureAndDrivesOnePausableView()
        {
            var harbor=naval.Harbors.First(item=>item.Owner==0&&!item.IsImportedPort);
            int cost=Harbor.Cost(ShipKind.Galley);battle.Economy.Gold[0]=cost*(Harbor.QueueCapacity+1);
            var view=harbor.GetComponentInChildren<BuildingTrainingView>(true);
            Assert.That(view,Is.Not.Null);Assert.That(view.Active,Is.False);

            for(int i=0;i<Harbor.QueueCapacity;i++)Assert.That(harbor.Buy(ShipKind.Galley),Is.Null);
            int afterFive= battle.Economy.Gold[0];
            Assert.That(afterFive,Is.EqualTo(cost));
            Assert.That(harbor.Buy(ShipKind.Galley),Is.Not.Null);
            Assert.That(harbor.QueueCount,Is.EqualTo(Harbor.QueueCapacity));
            Assert.That(battle.Economy.Gold[0],Is.EqualTo(afterFive));

            harbor.SimTick(.1f);
            Assert.That(view.Active,Is.True);
            var hammer=view.GetComponentsInChildren<Transform>(true).First(item=>item.name=="Training hammer pivot");
            Quaternion beforePause=hammer.localRotation;battle.TogglePause();harbor.SimTick(.2f);
            Assert.That(hammer.localRotation,Is.EqualTo(beforePause),"Presentation advances only through unpaused simulation ticks.");
            battle.TogglePause();

            harbor.State.Owner=1;harbor.SimTick(.1f);
            Assert.That(harbor.QueueCount,Is.Zero);
            Assert.That(battle.Economy.Gold[0],Is.EqualTo(cost*(Harbor.QueueCapacity+1)),"Capture refunds every accepted naval order exactly once.");
            Assert.That(view.Active,Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmbarkRingOnlyShowsForSelectedHarborAndEntriesUseNavMesh()
        {
            var harbor=naval.Harbors.First(item=>item.Owner==0&&!item.IsImportedPort);
            var zone=naval.EmbarkZones.First(item=>item.Harbor==harbor);
            var embarkRing=zone.GetComponentInChildren<LineRenderer>(true);
            Assert.That(embarkRing,Is.Not.Null);Assert.That(embarkRing.enabled,Is.False);
            Assert.That(harbor.GetComponentsInChildren<LineRenderer>(true).Any(item=>item.enabled),Is.True,"The smaller harbor claim/guard ring remains visible.");
            harbor.Select(true);Assert.That(embarkRing.enabled,Is.True);
            harbor.Select(false);Assert.That(embarkRing.enabled,Is.False);

            var town=battle.Towns.First(item=>item.State.Owner==0);
            Assert.That(NavMesh.SamplePosition(town.DefaultLandEntry,out var townEntry,2f,NavMesh.AllAreas),Is.True);
            Assert.That(Vector3.Distance(town.Rally,townEntry.position),Is.LessThan(.2f),"The initial rally should be at the town's south-gate spawn entry.");
            Assert.That(NavMesh.SamplePosition(harbor.LandEntry,out var harborEntry,2f,NavMesh.AllAreas),Is.True);
            Assert.That(Vector3.Distance(harborEntry.position,harbor.Landing),Is.GreaterThan(1f));
            Assert.That(Vector3.Distance(harborEntry.position,harbor.Berth),Is.GreaterThan(1f));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;BattleSession.ModeForNewMatch=previousMode;
            BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;
            SceneManager.SetActiveScene(previousScene);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
