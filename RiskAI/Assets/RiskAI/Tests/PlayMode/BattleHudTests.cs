using System.Collections;
using System.Linq;
using RiskAI.Core;
using RiskAI.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace RiskAI.Tests
{
    public sealed class BattleHudTests
    {
        Scene scene, previous;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;
        float previousTimeScale;
        BattleSession battle;
        BattleHud hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;
            previousMode=BattleSession.ModeForNewMatch;previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;previousTimeScale=Time.timeScale;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=19031;
            scene=SceneManager.CreateScene("Battle HUD gold refresh");SceneManager.SetActiveScene(scene);
            new GameObject("Battle HUD bootstrap").AddComponent<RiskBootstrap>();battle=BattleSession.Current;battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;hud=Object.FindFirstObjectByType<BattleHud>();
            yield return null;
        }

        [UnityTest]
        public IEnumerator GoldHeaderRefreshesAfterGrantWhilePausedBeforeNextSimulationTick()
        {
            var document=hud.GetComponent<UIDocument>();Assert.That(document,Is.Not.Null);
            var gold=document.rootVisualElement.Q<Label>("HUD gold");Assert.That(gold,Is.Not.Null,"The retained header needs a stable gold label for model-driven refresh verification.");
            long tickBefore=battle.Clock.TickCount;
            int granted=997;battle.Economy.Grant(0,granted-battle.Economy.Gold[0]);
            if(!battle.Paused)battle.TogglePause();
            yield return new WaitForSecondsRealtime(.12f);
            yield return null;

            Assert.That(battle.Clock.TickCount,Is.EqualTo(tickBefore),"The assertion must cover a model change before any next simulation tick.");
            Assert.That(gold.text,Does.StartWith(granted+" ORO"),"The retained header must read economy gold even while its simulation snapshot is paused.");
        }

        [UnityTest]
        public IEnumerator HelpSheetIsReplacedByActualVictoryResult()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            controller.HelpVisible=true;
            yield return null;

            // Establish the conquest condition through the public settlement state, then
            // drive the normal BattleWorld rule pass. Opponent guards remain alive, so
            // this specifically covers the timed 60-percent victory rather than a
            // synthetic Winner assignment or elimination shortcut.
            foreach(var town in battle.Towns)town.State.Owner=0;
            int steps=0;
            while(battle.Winner<0&&steps++<1000)
                battle.Clock.Advance(SimClock.StepSeconds,false,battle.World.Tick);
            Assert.That(battle.Winner,Is.EqualTo(0),"Fixture must reach victory through BattleSession.TickRules.");

            yield return null;
            var labels=hud.GetComponent<UIDocument>().rootVisualElement.Query<Label>().ToList();
            Assert.That(labels.Any(label=>label.text=="VICTORIA"),Is.True,
                "A modal already open as help must rebuild as the result sheet when the normal victory rule fires.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=previousTimeScale;UiViewport.ResetHudHeights();BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.ModeForNewMatch=previousMode;BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;
            SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
