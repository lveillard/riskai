using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class AiPacingTests
    {
        Scene previous, scene;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
            BattleSession.SeedForNewMatch = 9182;
            scene = SceneManager.CreateScene("AI pacing");
            SceneManager.SetActiveScene(scene);
            new GameObject("AI pacing bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator RelaxedDelaysRecruitmentAndKeepsTheOneArcherPerPostStart()
        {
            battle.AiEnabled = true;
            int startingGold = battle.Economy.Gold[1];
            var garrisonPositions = battle.Towns.Where(t => t.State.Owner == 1 && t.Defender).Select(t => new { Unit = t.Defender, Position = t.Defender.transform.position }).ToArray();
            Assert.That(battle.Difficulty, Is.EqualTo(BattleSession.AiDifficulty.Relaxed));
            Assert.That(battle.Economy.Gold[0], Is.EqualTo(battle.Economy.Gold[1]));
            Assert.That(battle.Population(0), Is.EqualTo(battle.Population(1)));
            Assert.That(MapLayout.Towns.Length, Is.EqualTo(18));
            Assert.That(MapLayout.Countries.Length, Is.EqualTo(9));
            Assert.That(battle.Towns.Count(town => town.State.Owner == 0), Is.EqualTo(2));
            Assert.That(battle.Towns.Count(town => town.State.Owner == 1), Is.EqualTo(2));
            Assert.That(battle.Towns.Count(town => town.State.Owner < 0), Is.EqualTo(14));
            var posts = battle.Towns.Select(town => new { Owner = town.State.Owner, Defender = town.Defender })
                .Concat(NavalWorld.Current.Harbors.Select(harbor => new { Owner = harbor.Owner, Defender = harbor.Defender }))
                .ToArray();
            Assert.That(posts.Length, Is.EqualTo(MapLayout.Towns.Length + NavalWorld.Current.Harbors.Count));
            Assert.That(posts.Length, Is.EqualTo(25));
            Assert.That(battle.Units.Count, Is.EqualTo(posts.Length));
            Assert.That(posts.All(post => post.Defender && post.Defender.Kind == UnitKind.Archer && post.Defender.IsGarrison), Is.True);
            Assert.That(posts.All(post => post.Defender.Team == (post.Owner >= 0 ? post.Owner : PlayerRules.NeutralTeam)), Is.True);
            Assert.That(posts.Select(post => post.Defender).Distinct().Count(), Is.EqualTo(posts.Length));
            Assert.That(battle.Units.All(unit => unit.IsGarrison), Is.True);
            Assert.That(NavalWorld.Current.Ships, Is.Empty);

            Time.timeScale = 10;
            yield return ReachBattleTime(29);
            Assert.That(battle.Economy.Gold[1], Is.EqualTo(startingGold));
            Assert.That(battle.Towns.All(t => t.QueueCount == 0), Is.True);
            Assert.That(battle.Units.Where(u => u && u.Team == 1 && u.IsAlive).All(u => !u.CurrentTarget && (!u.Agent || !u.Agent.hasPath)), Is.True);
            Assert.That(garrisonPositions.All(item => item.Unit && Vector3.Distance(item.Unit.transform.position, item.Position) < .05f), Is.True);
        }

        [UnityTest]
        public IEnumerator RelaxedFirstTickQueuesAtMostOneTown()
        {
            battle.AiEnabled = true;
            Time.timeScale = 10;
            int startingPopulation = battle.Population(1);
            int startingGold = battle.Economy.Gold[1];
            yield return ReachBattleTime(30.2f); // Just after the first 30-second AI tick.
            int queued = battle.Towns.Sum(t => t.QueueCount);
            int trained = battle.Population(1) - startingPopulation;
            Assert.That(startingGold - battle.Economy.Gold[1], Is.EqualTo(1));
            Assert.That(queued + trained, Is.EqualTo(1));
            Assert.That(battle.Towns.Count(t => t.QueueCount > 0), Is.LessThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator SeededDefensePhaseStillDispatchesAMobileReserveToAContestedPost()
        {
            battle.AiEnabled = true;
            var town = battle.Towns.First(t => t.State.Owner == 1);
            var reserve = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, town.ClaimPoint + Vector3.forward * 12);
            var invader = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman, town.ClaimPoint);
            Assert.That(reserve.IsGarrison, Is.False);
            Assert.That(invader.IsGarrison, Is.False);

            while (battle.BattleTime < 1f)
                battle.Clock.Advance(SimClock.StepSeconds, false, battle.World.Tick);

            Assert.That(town.State.Contested, Is.True);
            Assert.That(reserve.IsIdle, Is.False, "The phased defense pass must still give the reserve an order.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CommanderMobilizesItsWaitingCountryReinforcements()
        {
            var camp=battle.Camps.First(c=>c&&battle.Economy.CountryOwner(c.Country)==1);
            var reserves=new Soldier[3];
            for(int i=0;i<reserves.Length;i++)
            {
                reserves[i]=BattleTestScenario.Mobile(battle,1,UnitKind.Archer,camp.SpawnPoint+Vector3.right*i*1.2f);
                camp.ApplyRally(reserves[i]);
                Assert.That(reserves[i].IsHolding,Is.True);
            }
            typeof(SkirmishCommander).GetMethod("IssueOffensiveOrders",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(battle.Commander,new object[]{false});
            battle.Commands.Tick();
            Assert.That(reserves.Any(unit=>!unit.IsHolding&&!unit.IsIdle),Is.True,"The AI must be able to assign an offensive order to troops waiting at its own camp.");
            yield return null;
        }

        IEnumerator ReachBattleTime(float target)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (battle.BattleTime < target && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.BattleTime, Is.GreaterThanOrEqualTo(target));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
