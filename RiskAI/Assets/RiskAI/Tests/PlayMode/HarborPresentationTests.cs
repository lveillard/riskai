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
            var doorway=view.GetComponentsInChildren<Transform>(true).First(item=>item.name=="Training door pivot");
            Assert.That(view.GetComponentsInChildren<Transform>(true).Any(item=>item.name=="Training gate glow"),Is.True);
            Assert.That(view.GetComponentsInChildren<Transform>(true).Any(item=>item.name.Contains("hammer")||item.name.Contains("pennant")||item.name.Contains("illuminated door")),Is.False,
                "Training illuminates the existing entrance rather than adding a free-standing tool, banner, or duplicate door.");
            Quaternion beforePause=doorway.localRotation;battle.TogglePause();harbor.SimTick(.2f);
            Assert.That(doorway.localRotation,Is.EqualTo(beforePause),"Presentation advances only through unpaused simulation ticks.");
            battle.TogglePause();

            harbor.State.Owner=1;harbor.SimTick(.1f);
            Assert.That(harbor.QueueCount,Is.Zero);
            Assert.That(battle.Economy.Gold[0],Is.EqualTo(cost*(Harbor.QueueCapacity+1)),"Capture refunds every accepted naval order exactly once.");
            Assert.That(view.Active,Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrainingSpillsWarmLightForEitherQueueAndResetsAcrossReuse()
        {
            var harbor=naval.Harbors.First(item=>item.Owner==0&&!item.IsImportedPort);
            var view=harbor.GetComponentInChildren<BuildingTrainingView>(true);
            view.SetActivity(true,false,1f);
            var spill=view.GetComponentsInChildren<MeshRenderer>().First(item=>item.name=="Training threshold light spill");
            var opening=view.GetComponentsInChildren<MeshRenderer>().First(item=>item.name=="Training doorway glow");
            var warm=opening.sharedMaterial;
            Assert.That(warm.color.r,Is.GreaterThan(warm.color.b));
            var mesh=spill.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.bounds.max.z,Is.GreaterThan(2f),"Light extends out from the threshold into the approach.");
            Assert.That(mesh.colors.Any(color=>color.a==0),Is.True,"Spill fades at its edge.");
            var openingPosition=opening.transform.position;
            view.SetActivity(false,true,4f);
            Assert.That(opening.sharedMaterial,Is.SameAs(warm),"Naval-only queues use the same warm entrance light.");
            Assert.That(opening.transform.position,Is.EqualTo(openingPosition),"Breathing must not move light into the opaque gate.");
            view.SetActivity(false,false,4f);
            Assert.That(view.gameObject.activeSelf,Is.False);
            view.SetActivity(true,true,0f);
            Assert.That(view.gameObject.activeSelf,Is.True);
            Assert.That(view.GetComponentsInChildren<MeshRenderer>().Count(item=>item.name==spill.name),Is.EqualTo(1),"Reusing the cue does not accumulate spill geometry.");
            view.SetActivity(false,false,0f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrainingCuesUseAuthoredScaledEntrancesWithoutMovingSpawnEntries()
        {
            var town=battle.Towns.First(item=>item.State.Owner==0);
            var townAnchor=town.GetComponentInChildren<BuildingEntranceAnchor>(true);
            var townArt=town.GetComponentsInChildren<Transform>(true).First(item=>item.name.StartsWith(town.name+" ")&&item.name.EndsWith("architecture"));
            var townCue=town.GetComponentInChildren<BuildingTrainingView>(true);
            Assert.That(townAnchor,Is.Not.Null);Assert.That(townCue,Is.Not.Null);
            var townLocal=townArt.InverseTransformPoint(townAnchor.Position);
            Assert.That(townLocal.x,Is.EqualTo(0).Within(.001f));
            Assert.That(townLocal.z,Is.EqualTo(-1.89f).Within(.001f));
            Assert.That(Vector3.Dot(townAnchor.Outward,townArt.TransformDirection(Vector3.back).normalized),Is.GreaterThan(.999f));
            Assert.That(Vector3.Distance(townCue.transform.position,townAnchor.Position),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(town.DefaultLandEntry,townAnchor.Position),Is.GreaterThan(2f),"The art anchor must not redefine the gameplay spawn entry.");

            var harbor=naval.Harbors.First(item=>item.Owner==0&&!item.IsImportedPort);
            var harborAnchor=harbor.GetComponentInChildren<BuildingEntranceAnchor>(true);
            var harborArt=harbor.GetComponentsInChildren<Transform>(true).First(item=>item.name=="Harbor architecture");
            var harborCue=harbor.GetComponentInChildren<BuildingTrainingView>(true);
            Assert.That(harborAnchor,Is.Not.Null);Assert.That(harborCue,Is.Not.Null);
            var harborLocal=harborArt.InverseTransformPoint(harborAnchor.Position);
            Assert.That(harborLocal.x,Is.EqualTo(-3f).Within(.001f));
            Assert.That(harborLocal.z,Is.EqualTo(-1.55f).Within(.001f));
            Assert.That(Vector3.Dot(harborAnchor.Outward,harborArt.TransformDirection(Vector3.back).normalized),Is.GreaterThan(.999f));
            Assert.That(Vector3.Distance(harborCue.transform.position,harborAnchor.Position),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(harbor.LandEntry,harborAnchor.Position),Is.GreaterThan(2f),"The harbor cue must not move its land spawn entry.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HarborKeepsSmallClaimRingAndEntriesUseNavMesh()
        {
            var harbor=naval.Harbors.First(item=>item.Owner==0&&!item.IsImportedPort);
            var zone=naval.EmbarkZones.First(item=>item.Harbor==harbor);
            Assert.That(zone.GetComponentsInChildren<LineRenderer>(true),Is.Empty);
            Assert.That(harbor.GetComponentsInChildren<LineRenderer>(true).Any(item=>item.enabled),Is.True,"The smaller harbor claim/guard ring remains visible.");
            harbor.Select(true);
            Assert.That(zone.GetComponentsInChildren<LineRenderer>(true),Is.Empty,"Selecting a port uses the common building ring, without a loading radius overlay.");
            harbor.Select(false);

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
