using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    /// <summary>
    /// Engine side of <see cref="UnitRules"/>: the same measurement, target filter, line of sight
    /// and acquisition for soldiers, ships and posts. Each actor passes its own type and origin;
    /// nothing here depends on the actor class.
    /// </summary>
    public static class UnitTargeting
    {
        /// <summary>Distance by a range measure from <paramref name="from"/> to the target.</summary>
        public static float Distance(RangeMeasure measure, Vector3 from, float ownBody, CombatTarget target)
        {
            Vector3 point = UnitRules.MeasuresToApproachPoint(measure) ? target.ApproachPoint(from) : target.transform.position;
            return UnitRules.Measure(measure, point.x - from.x, point.y - from.y, point.z - from.z, ownBody, target.Type.BodyRadius);
        }

        /// <summary>Weapon distance, as the attacker's weapon measures it.</summary>
        public static float WeaponDistance(CombatTarget attacker, in WeaponProfile weapon, CombatTarget target) =>
            Distance(weapon.Measure, attacker.transform.position, attacker.Type.BodyRadius, target);

        public static UnitRelation Relation(int team, CombatTarget self, CombatTarget target) =>
            UnitRules.Relation(team, target.Team, target == self, PlayerRules.NeutralTeam);

        /// <summary>A live, attackable target that this weapon's target flags admit (never self or an ally).</summary>
        public static bool CanTarget(CombatTarget self, int team, in WeaponProfile weapon, CombatTarget candidate)
        {
            if (!candidate || !candidate.CanBeAttacked) return false;
            var relation = Relation(team, self, candidate);
            return weapon.IsValid && relation != UnitRelation.Self && relation != UnitRelation.Ally &&
                   UnitRules.Allows(weapon.TargetMask, UnitRules.TargetClass(candidate.Type), relation);
        }

        /// <summary>Line of sight by the acquisition policy: terrain ray between aim points, or a NavMesh ray to the approach point.</summary>
        public static bool Visible(UnitVisibility visibility, CombatTarget self, CombatTarget target)
        {
            if (!target) return false;
            if (visibility == UnitVisibility.TerrainRay)
            {
                Vector3 from = self.AimPoint, to = target.AimPoint, delta = to - from;
                return delta.sqrMagnitude < .001f || !Physics.Raycast(from, delta.normalized, delta.magnitude, 1 << MapLayout.TerrainLayer, QueryTriggerInteraction.Ignore);
            }
            return !NavMesh.Raycast(self.transform.position, target.ApproachPoint(self.transform.position), out _, NavMesh.AllAreas);
        }

        /// <summary>
        /// Automatic acquisition shared by every actor: data radius and measure, target flags, line of
        /// sight, optional leash around an anchor, pressure spreading and the data tie-break.
        /// </summary>
        public static CombatTarget Acquire(BattleSession session, CombatTarget self, int team, in UnitType type, float radius,
            bool leashed, Vector3 anchor, float leash, List<CombatTarget> buffer)
        {
            ref readonly var acquisition = ref type.Acquisition;
            ref readonly var weapon = ref self.AttackWeapon;
            Vector3 origin = self.transform.position;
            CombatTarget best = null; float bestScore = float.MaxValue;
            session.Spatial.Query(origin, radius + acquisition.QueryPadding, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                var candidate = buffer[i];
                if (!CanTarget(self, team, weapon, candidate)) continue;
                float distance = Distance(acquisition.Measure, origin, type.BodyRadius, candidate);
                if (distance > radius) continue;
                float score = UnitRules.AcquireScore(acquisition, distance, acquisition.PressureBias > 0 ? session.Spatial.Pressure(team, candidate) : 0);
                if (!UnitRules.BetterCandidate(acquisition, score, candidate.EntityId, bestScore, best ? best.EntityId : 0, best)) continue;
                if (!Visible(acquisition.Visibility, self, candidate)) continue;
                if (leashed && Vector3.Distance(anchor, candidate.ApproachPoint(anchor)) > leash) continue;
                best = candidate; bestScore = score;
            }
            return best;
        }
    }
}
