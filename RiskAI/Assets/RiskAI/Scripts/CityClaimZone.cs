using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Unit-only city claim footprint and defender selection.</summary>
    public sealed class CityClaimZone
    {
        public const float DefaultHalfExtent = 1.1f;
        public const float VerticalExtent = 1.25f;
        public Vector3 Center { get; }
        public float HalfExtent { get; }
        public Soldier Defender { get; private set; }
        public bool Contested { get; private set; }

        public CityClaimZone(Vector3 center, float halfExtent = DefaultHalfExtent)
        {
            Center = center;
            HalfExtent = halfExtent;
        }

        /// <summary>Returns the owner after applying the current footprint state.</summary>
        public int Step(IEnumerable<Soldier> soldiers, int owner)
        {
            var eligible = soldiers == null ? new List<Soldier>() : soldiers.Where(IsEligible).ToList();
            var inside = eligible.Where(IsInside).ToList();
            var current = Defender;
            if (current && IsEligible(current) && IsInside(current))
            {
                Contested = inside.Any(unit => unit.Team >= 0 && unit.Team <= 1 && unit.Team != current.Team);
                return owner;
            }

            Defender = null;
            var replacement = owner >= 0 && owner <= 1
                ? inside.Where(unit => unit.Team == owner).OrderBy(DistanceToCenter).FirstOrDefault()
                : null;
            Defender = replacement ?? inside.OrderBy(DistanceToCenter).FirstOrDefault();
            if (!Defender)
            {
                Contested = false;
                return owner;
            }

            Contested = inside.Any(unit => unit.Team >= 0 && unit.Team <= 1 && unit.Team != Defender.Team);
            return Defender.Team >= 0 && Defender.Team <= 1 ? Defender.Team : owner;
        }

        public void SetDefender(Soldier defender)
        {
            Defender = IsEligible(defender) ? defender : null;
            Contested = false;
        }

        bool IsInside(Soldier unit)
        {
            Vector3 position = unit.transform.position;
            return Mathf.Abs(position.x - Center.x) <= HalfExtent &&
                   Mathf.Abs(position.z - Center.z) <= HalfExtent &&
                   Mathf.Abs(position.y - Center.y) <= VerticalExtent;
        }

        float DistanceToCenter(Soldier unit)
        {
            Vector3 delta = unit.transform.position - Center;
            return delta.sqrMagnitude;
        }

        static bool IsEligible(Soldier unit)
        {
            return unit && unit.isActiveAndEnabled && unit.IsAlive && unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh &&
                   unit.Team >= 0 && unit.Team <= 2;
        }
    }
}
