using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class HarborEmbarkIndicatorTests
    {
        Scene previousScene,createdScene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers,previousSeed;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene=SceneManager.GetActiveScene();
            previousMap=BattleSession.MapForNewMatch;
            previousLayout=BattleSession.LayoutForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;
            previousSeed=BattleSession.SeedForNewMatch;
            previousMode=BattleSession.ModeForNewMatch;
            yield break;
        }

        [UnityTest]
        public IEnumerator PortsUseCommonSelectionWithoutExtraLoadingRingsAcrossScenarios()
        {
            foreach(var map in new[]{ScenarioMap.Classic,ScenarioMap.Riverlands,ScenarioMap.Europe,ScenarioMap.NewWorld})
            {
                BattleSession.MapForNewMatch=map;
                BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
                BattleSession.PlayerCountForNewMatch=2;
                BattleSession.SeedForNewMatch=59201;
                BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
                createdScene=SceneManager.CreateScene("Embark indicator "+map);
                SceneManager.SetActiveScene(createdScene);
                new GameObject("Embark indicator bootstrap").AddComponent<RiskBootstrap>();

                // Before the first frame, including disabled objects: the unwanted
                // loading ring must not be recreated by either map construction path.
                var naval=NavalWorld.Current;
                Assert.That(naval,Is.Not.Null);
                Assert.That(naval.EmbarkZones,Is.Not.Empty,map+" needs ports to cover the shared path.");
                foreach(var zone in naval.EmbarkZones)
                {
                    Assert.That(zone.GetComponentsInChildren<LineRenderer>(true),Is.Empty,map+" must not create an extra loading ring.");
                    Assert.That(zone.Contains(zone.Center),Is.True,"Removing a visual must preserve the boarding area.");
                    Assert.That(zone.Contains(zone.Center+Vector3.right*(NavalEmbarkZone.Radius+.1f)),Is.False);
                }

                var selected=naval.EmbarkZones[0];
                var controller=BattleSession.Current.GetComponent<RtsController>();
                Assert.That(controller,Is.Not.Null);
                controller.SelectHarbor(selected.Harbor);
                Assert.That(selected.Harbor.Selected,Is.True);
                foreach(var zone in naval.EmbarkZones)
                    Assert.That(zone.GetComponentsInChildren<LineRenderer>(true),Is.Empty,"Selection cannot reintroduce the blue radius.");
                var building=selected.Harbor.IsImportedPort?(Component)selected.Harbor.LinkedTown:selected.Harbor;
                var selectionRing=System.Array.Find(building.GetComponentsInChildren<LineRenderer>(),ring=>ring.name=="Whole building selection");
                Assert.That(selectionRing,Is.Not.Null,"The shared building selection remains available.");
                yield return null; // Settlement owns its retained presentation update.
                Assert.That(selectionRing.enabled,Is.True);
                controller.ClearSelectedBuildings();
                yield return null;
                Assert.That(selectionRing.enabled,Is.False);

                yield return SceneManager.UnloadSceneAsync(createdScene);
                createdScene=default;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap;
            BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.PlayerCountForNewMatch=previousPlayers;
            BattleSession.SeedForNewMatch=previousSeed;
            BattleSession.ModeForNewMatch=previousMode;
            MapLayout.Configure(previousMap);
            SceneManager.SetActiveScene(previousScene);
            if(createdScene.IsValid()&&createdScene.isLoaded)yield return SceneManager.UnloadSceneAsync(createdScene);
        }
    }
}
