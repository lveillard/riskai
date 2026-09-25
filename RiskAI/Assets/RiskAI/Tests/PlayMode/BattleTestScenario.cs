using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    /// <summary>Explicit mobile actors for tests. Bootstrap posts stay garrisoned.</summary>
    static class BattleTestScenario
    {
        /// <summary>
        /// Every new-match setting a fixture depends on. A fixture pins them in SetUp and restores them
        /// in TearDown, so it never inherits another fixture's map or the default random seed and layout.
        /// </summary>
        public readonly struct PinnedMatch
        {
            readonly ScenarioMap map; readonly BattleSession.VictoryMode mode; readonly BattleSession.StartLayout layout;
            readonly BattleSession.AiDifficulty difficulty; readonly int seed, players;

            PinnedMatch(bool _)
            {
                map = BattleSession.MapForNewMatch; mode = BattleSession.ModeForNewMatch; layout = BattleSession.LayoutForNewMatch;
                difficulty = BattleSession.DifficultyForNewMatch; seed = BattleSession.SeedForNewMatch; players = BattleSession.PlayerCountForNewMatch;
            }

            public static PinnedMatch Pin(ScenarioMap map, int seed, BattleSession.StartLayout layout = BattleSession.StartLayout.Fixed, int players = PlayerRules.MaxPlayers)
            {
                var previous = new PinnedMatch(true);
                BattleSession.MapForNewMatch = map; BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
                BattleSession.LayoutForNewMatch = layout; BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
                BattleSession.SeedForNewMatch = seed; BattleSession.PlayerCountForNewMatch = players;
                return previous;
            }

            public void Restore()
            {
                BattleSession.MapForNewMatch = map; BattleSession.ModeForNewMatch = mode; BattleSession.LayoutForNewMatch = layout;
                BattleSession.DifficultyForNewMatch = difficulty; BattleSession.SeedForNewMatch = seed; BattleSession.PlayerCountForNewMatch = players;
            }
        }

        public static Soldier Mobile(BattleSession battle, int team, UnitKind kind, Vector3 position)
        {
            var unit = battle.Spawn(team, kind, position);
            Assert.That(unit, Is.Not.Null, "The test fixture must spawn a mobile soldier on the NavMesh.");
            Assert.That(unit.IsGarrison, Is.False);
            return unit;
        }

        public static Soldier[] MobileArmy(BattleSession battle, int team, UnitKind kind, int count, Vector3 center)
        {
            var army = new Soldier[count];
            for (int i = 0; i < count; i++)
                army[i] = Mobile(battle, team, kind, center + new Vector3((i % 4) * 1.2f, 0, (i / 4) * 1.2f));
            return army;
        }

        public static Ship Ship(NavalWorld naval, int team, UnitKind kind, Vector3 position)
        {
            var ship = naval.Spawn(team, kind, position);
            Assert.That(ship, Is.Not.Null, "The test fixture must spawn a ship in clear water.");
            return ship;
        }
    }
}
