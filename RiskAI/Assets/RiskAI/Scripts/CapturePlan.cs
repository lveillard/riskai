using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Live capture target. Guardian, claim point and owner are read when the order runs, not when it was queued.</summary>
    static class CapturePlan
    {
        public readonly struct View
        {
            public readonly bool Found;
            public readonly int Owner;
            public readonly Vector3 Point;
            public readonly CombatTarget Guardian;
            public readonly Harbor Harbor;
            public readonly Settlement Town;

            public View(bool found, int owner, Vector3 point, CombatTarget guardian, Harbor harbor, Settlement town)
            {
                Found = found; Owner = owner; Point = point; Guardian = guardian; Harbor = harbor; Town = town;
            }
        }

        public static View Look(BattleSession session, in UnitCommand command)
        {
            var found = StructureLookup.Find(session, command);
            if (found.Zone == null) return default;
            int owner = command.StructureKind == BuildingKind.Harbor
                ? (found.Harbor ? found.Harbor.Owner : PlayerRules.NeutralOwner)
                : (found.Town ? found.Town.State.Owner : PlayerRules.NeutralOwner);
            return new View(true, owner, found.Point, found.Zone.Guardian, found.Harbor, found.Town);
        }

        public static bool HostileGuardian(in View view, int team) =>
            view.Guardian && view.Guardian.IsAlive && view.Guardian.CanBeAttacked && view.Guardian.Team != team;
    }
}
