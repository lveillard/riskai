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
            public readonly CityClaimZone Zone;

            public View(bool found, int owner, Vector3 point, CombatTarget guardian, Harbor harbor, Settlement town, CityClaimZone zone)
            {
                Found = found; Owner = owner; Point = point; Guardian = guardian; Harbor = harbor; Town = town; Zone = zone;
            }
        }

        public static View Look(BattleSession session, in UnitCommand command)
        {
            var found = StructureLookup.Find(session, command);
            if (found.Zone == null) return default;
            return From(found.Zone, found.Harbor, found.Town, found.Point);
        }

        /// <summary>Re-reads the owner of a zone resolved when the order started. A destroyed zone is looked up again.</summary>
        public static View Fresh(BattleSession session, in UnitCommand command, View cached)
        {
            if (cached.Zone != null) return From(cached.Zone, cached.Harbor, cached.Town, cached.Point);
            return Look(session, command);
        }

        static View From(CityClaimZone zone, Harbor harbor, Settlement town, Vector3 point)
        {
            int owner = harbor ? harbor.Owner : town ? town.State.Owner : PlayerRules.NeutralOwner;
            return new View(true, owner, point, zone.Guardian, harbor, town, zone);
        }

        public static bool HostileGuardian(in View view, int team) =>
            view.Guardian && view.Guardian.IsAlive && view.Guardian.CanBeAttacked && view.Guardian.Team != team;
    }
}
