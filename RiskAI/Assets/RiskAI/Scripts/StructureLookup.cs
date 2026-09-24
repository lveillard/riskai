using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    readonly struct StructureRef
    {
        public readonly CityClaimZone Zone;
        public readonly Vector3 Point;
        public readonly Harbor Harbor;
        public readonly Settlement Town;
        public StructureRef(CityClaimZone zone, Vector3 point, Harbor harbor, Settlement town)
        { Zone = zone; Point = point; Harbor = harbor; Town = town; }
    }

    /// <summary>Resolves a capture command's building id to the live town or harbor.</summary>
    static class StructureLookup
    {
        public static StructureRef Find(BattleSession session, in UnitCommand command)
        {
            if (!session || string.IsNullOrEmpty(command.StructureId)) return default;
            if (command.StructureKind == BuildingKind.Harbor)
            {
                var harbor = Harbor(session, command.StructureId);
                return harbor && harbor.ClaimZone != null
                    ? new StructureRef(harbor.ClaimZone, harbor.ClaimZone.Center, harbor, harbor.LinkedTown) : default;
            }
            var town = Town(session, command.StructureId);
            return town && town.ClaimZone != null
                ? new StructureRef(town.ClaimZone, town.ClaimPoint, town.Port, town) : default;
        }

        public static Harbor Harbor(BattleSession session, string id)
        {
            var naval = session ? session.Naval : null;
            if (!naval || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < naval.Harbors.Count; i++)
            {
                var harbor = naval.Harbors[i];
                if (harbor && harbor.BuildingId.LocalId == id) return harbor;
            }
            return null;
        }

        public static Settlement Town(BattleSession session, string id)
        {
            if (!session || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < session.Towns.Count; i++)
            {
                var town = session.Towns[i];
                if (town && town.BuildingId.LocalId == id) return town;
            }
            return null;
        }
    }
}
