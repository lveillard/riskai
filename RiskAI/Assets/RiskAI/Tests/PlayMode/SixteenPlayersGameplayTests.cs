using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class SixteenPlayersGameplayTests
    {
        Scene previous, scene;
        BattleSession battle;
        int savedPlayerCount, savedSeed;
        BattleSession.StartLayout savedLayout;
        BattleSession.AiDifficulty savedDifficulty;
        BattleSession.VictoryMode savedMode;
        ScenarioMap savedMap;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            savedPlayerCount = BattleSession.PlayerCountForNewMatch;
            savedSeed = BattleSession.SeedForNewMatch;
            savedLayout = BattleSession.LayoutForNewMatch;
            savedDifficulty = BattleSession.DifficultyForNewMatch;
            savedMode = BattleSession.ModeForNewMatch;
            savedMap = BattleSession.MapForNewMatch;
            BattleSession.PlayerCountForNewMatch = PlayerRules.MaxPlayers;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
            BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EuropeAndNewWorldGiveEveryPlayerAnIndependentOpening()
        {
            yield return VerifyScenario(ScenarioMap.Europe, 212, 69, 44, 160212);
            yield return VerifyScenario(ScenarioMap.NewWorld, 293, 100, 59, 160293);
        }

        IEnumerator VerifyScenario(ScenarioMap map, int cities, int countries, int ports, int seed)
        {
            BattleSession.MapForNewMatch = map;
            BattleSession.SeedForNewMatch = seed;
            scene = SceneManager.CreateScene("16 players " + map);
            SceneManager.SetActiveScene(scene);
            new GameObject("16 players bootstrap").AddComponent<RiskBootstrap>();
            yield return null;
            battle = BattleSession.Current;

            Assert.That(battle.PlayerCount, Is.EqualTo(PlayerRules.MaxPlayers));
            Assert.That(battle.Commanders.Count, Is.EqualTo(15));
            Assert.That(MapLayout.Towns.Length, Is.EqualTo(cities));
            Assert.That(MapLayout.Countries.Length, Is.EqualTo(countries));
            Assert.That(battle.Towns.Count(town => town.IsPort), Is.EqualTo(ports));
            Assert.That(battle.Economy.Gold, Is.All.EqualTo(BattleRules.StartingGold));
            for (int team = 0; team < PlayerRules.MaxPlayers; team++)
            {
                Assert.That(battle.Towns.Any(town => town.State.Owner == team), Is.True, "Player " + team + " needs a city.");
                Assert.That(battle.Towns.Any(town => town.State.Owner == team && !town.IsPort), Is.True, "Player " + team + " needs a recruitable city.");
                Assert.That(battle.RecruitmentPopulation(team), Is.Zero, "Starting posts are garrisons, not free mobile troops.");
            }
            int neutralCities = cities % PlayerRules.MaxPlayers;
            var neutralPosts = battle.Towns.Where(town => town.State.Owner == PlayerRules.NeutralOwner).ToArray();
            Assert.That(neutralPosts.Length, Is.EqualTo(neutralCities));
            Assert.That(neutralPosts.All(town => town.Defender && town.Defender.Team == PlayerRules.NeutralTeam), Is.True);
            Assert.That(Enumerable.Range(0, PlayerRules.MaxPlayers).Select(VisualFactory.TeamColor).Distinct().Count(), Is.EqualTo(PlayerRules.MaxPlayers));

            AdvanceTo(35f);
            Assert.That(battle.RecruitmentPopulation(0), Is.Zero, "Only AI commanders may receive their opening order automatically.");
            for (int team = 1; team < PlayerRules.MaxPlayers; team++)
            {
                Assert.That(battle.RecruitmentPopulation(team), Is.EqualTo(1), "AI " + team + " must spend its own four-gold opening on one mobile unit.");
                Assert.That(battle.Economy.Gold[team], Is.LessThan(BattleRules.StartingGold));
            }

            foreach (var town in battle.Towns)
                if (town.State.Owner == 1) town.State.Owner = 0;
            foreach (var unit in battle.Units.Where(unit => unit && unit.Team == 1).ToArray())
                unit.TakeDamage(10000, PlayerRules.NeutralTeam);
            for (int team = 2; team < PlayerRules.MaxPlayers; team++)
                Assert.That(battle.Units.Any(unit => unit && unit.IsAlive && unit.Team == team), Is.True, "Other opponents must still be present.");
            AdvanceTo(56f);
            Assert.That(battle.Winner, Is.EqualTo(-1), "Eliminating one rival cannot end a 16-player match while fourteen rivals remain.");

            int heldCities = battle.VictoryTarget - 1;
            for (int city = 0; city < battle.Towns.Count; city++)
            {
                battle.Towns[city].State.Owner = city < heldCities ? 0 : PlayerRules.NeutralOwner;
                battle.Towns[city].enabled = false;
            }
            AdvanceTo(56f + .25f);
            Assert.That(battle.VictoryProgress[0], Is.Zero,
                "Losing one city below the conquest target must reset the current hold immediately.");

            foreach (var town in battle.Towns) town.State.Owner = 0;
            AdvanceTo(56f + .25f + BattleRules.VictoryHoldSeconds + 1);
            Assert.That(battle.Winner, Is.EqualTo(0), "Holding the conquest target must win even while other armies remain alive.");

            yield return UnloadScenario();
        }

        void AdvanceTo(float target)
        {
            while (battle.BattleTime < target)
                battle.Clock.Advance(SimClock.StepSeconds, false, battle.World.Tick);
        }

        IEnumerator UnloadScenario()
        {
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            battle = null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return UnloadScenario();
            BattleSession.PlayerCountForNewMatch = savedPlayerCount;
            BattleSession.SeedForNewMatch = savedSeed;
            BattleSession.LayoutForNewMatch = savedLayout;
            BattleSession.DifficultyForNewMatch = savedDifficulty;
            BattleSession.ModeForNewMatch = savedMode;
            BattleSession.MapForNewMatch = savedMap;
        }
    }
}
