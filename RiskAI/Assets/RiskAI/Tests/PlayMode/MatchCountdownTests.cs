using System.Collections;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class MatchCountdownTests
    {
        [UnityTest] public IEnumerator CountdownBlocksSimulationAndOrdersThenStartsFromZero()
        {
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("Match countdown");
            SceneManager.SetActiveScene(scene);
            var battle=new GameObject("Countdown session").AddComponent<BattleSession>();battle.AiEnabled=false;
            float previousScale=Time.timeScale;
            try
            {
                Time.timeScale=0;
                battle.BeginStartCountdown(.35f);
                Assert.That(battle.IsStarting,Is.True);
                Assert.That(battle.Paused,Is.True);
                Assert.That(battle.Commands.Submit(new UnitCommand(0,123,UnitCommandKind.Move)),Is.False);
                battle.TogglePause();
                Assert.That(battle.Paused,Is.True,"Pause cannot skip the start countdown.");
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(battle.IsStarting,Is.True);
                Assert.That(battle.Clock.TickCount,Is.Zero);
                Assert.That(battle.Economy.ElapsedInRound,Is.Zero);
                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(battle.IsStarting,Is.False,"The countdown uses real time, independent of simulation speed.");
                Assert.That(battle.Paused,Is.False);
                Assert.That(battle.Clock.TickCount,Is.Zero,"Countdown time must never become simulation ticks.");
                Time.timeScale=1;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(battle.Clock.TickCount,Is.GreaterThan(0));
            }
            finally { Time.timeScale=previousScale;SceneManager.SetActiveScene(previous); }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
