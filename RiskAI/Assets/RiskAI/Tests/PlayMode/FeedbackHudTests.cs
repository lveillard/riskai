using System.Collections;
using System.Linq;
using RiskAI.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace RiskAI.Tests
{
    public sealed class FeedbackHudTests
    {
        Scene scene, previous;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers, previousSeed;
        BattleSession battle;
        BattleHud hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=19031;
            GameText.Set(GameLanguage.Spanish);
            scene=SceneManager.CreateScene("Battle feedback overlay");SceneManager.SetActiveScene(scene);
            new GameObject("Battle feedback bootstrap").AddComponent<RiskBootstrap>();battle=BattleSession.Current;battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;hud=Object.FindFirstObjectByType<BattleHud>();
            yield return null;
        }

        VisualElement Overlay() => GameObject.Find("Battle HUD feedback").GetComponent<UIDocument>().rootVisualElement;

        [UnityTest]
        public IEnumerator RecentMessagesStackNewestFirstInTheOverlay()
        {
            battle.Message("primero");battle.Message("segundo");battle.Message("tercero");
            yield return null;yield return null;
            var root=Overlay();
            Assert.That(root.Q<Label>("HUD message 0").text,Is.EqualTo("tercero"));
            Assert.That(root.Q<Label>("HUD message 1").text,Is.EqualTo("segundo"));
            Assert.That(root.Q<Label>("HUD message 2").text,Is.EqualTo("primero"));
            Assert.That(root.Q<Label>("HUD message 0").resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
            Assert.That(battle.Messages[0],Is.EqualTo("tercero"),"The simulation message list keeps its contract.");
        }

        [UnityTest]
        public IEnumerator AlertsAndChatStayOutOfSimulationMessages()
        {
            int before=battle.Messages.Count;
            battle.Feedback.Post("¡Te atacan!",MessageKind.Attack,0,Vector3.zero);
            Assert.That(hud.Chat.Submit(0,"  a por ellos "),Is.True);
            yield return null;yield return null;
            Assert.That(battle.Messages.Count,Is.EqualTo(before));
            Assert.That(battle.Feedback.Log[0].Text,Is.EqualTo("Tú: a por ellos"));
            Assert.That(battle.Feedback.Log[0].Kind,Is.EqualTo(MessageKind.Chat));
            Assert.That(Overlay().Q<Label>("HUD message 1").text,Is.EqualTo("¡Te atacan!"));
        }

        [UnityTest]
        public IEnumerator EnemyDamageRaisesOneThrottledAlertAndSpaceTargetIsRecorded()
        {
            var victim=battle.Units.First(unit=>unit&&unit.Team==0&&unit.IsAlive);
            var enemy=battle.Units.First(unit=>unit&&unit.Team!=0&&unit.IsAlive);
            victim.ReceiveAttack(1,AttackKind.Normal,enemy.Team,enemy);
            victim.ReceiveAttack(1,AttackKind.Normal,enemy.Team,enemy);
            yield return null;
            Assert.That(battle.Feedback.Alerts.HasAlert,Is.True);
            Assert.That(Vector3.Distance(battle.Feedback.Alerts.LastPosition,victim.transform.position),Is.LessThan(.01f));
            Assert.That(battle.Feedback.Alerts.TryRaise(victim.transform.position,Time.unscaledTime),Is.False,"The same area is throttled.");
        }

        [UnityTest]
        public IEnumerator IncomeTweensTheGoldLabelToTheNewBalance()
        {
            var document=hud.GetComponent<UIDocument>();
            var gold=document.rootVisualElement.Q<Label>("HUD gold");
            int start=battle.Economy.Gold[0];
            battle.Economy.Grant(0,40);
            // Reproduce the round-income notification the session raises after Economy.Advance.
            typeof(BattleFeedback).GetMethod("RaiseIncome",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(battle.Feedback,new object[]{0,40});
            float began=Time.unscaledTime;
            yield return null;
            int shown=int.Parse(gold.text.Split(' ')[0]);
            if(Time.unscaledTime-began<GoldTween.Seconds*.8f)
                Assert.That(shown,Is.LessThan(start+40),"The label counts up instead of jumping.");
            yield return new WaitForSecondsRealtime(GoldTween.Seconds+.2f);
            Assert.That(gold.text,Does.StartWith((start+40)+" ORO"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UiViewport.ResetHudHeights();GameText.Set(GameLanguage.English);BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;
            SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
