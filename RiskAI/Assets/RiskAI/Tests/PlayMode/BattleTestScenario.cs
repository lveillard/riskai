using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    /// <summary>Explicit mobile actors for tests. Bootstrap posts stay garrisoned.</summary>
    static class BattleTestScenario
    {
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

        public static Ship Ship(NavalWorld naval, int team, ShipKind kind, Vector3 position)
        {
            var ship = naval.Spawn(team, kind, position);
            Assert.That(ship, Is.Not.Null, "The test fixture must spawn a ship in clear water.");
            return ship;
        }
    }
}
