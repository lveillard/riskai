using System;

namespace RiskAI.Core
{
    /// <summary>Owner relation of a candidate target, as the Warcraft target flags see it.</summary>
    public enum UnitRelation { Self, Ally, Neutral, Enemy }

    /// <summary>
    /// The shared, engine-free combat rules for every unit type (land, sea, posts). Differences
    /// between units are data (range measure, reach margins, acquisition, masks); the motor
    /// (NavMesh or SeaNavigation) only supplies positions, paths and line of sight.
    /// </summary>
    public static class UnitRules
    {
        /// <summary>True when the measure starts at the target's approach point (hull/foundation surface), not its pivot.</summary>
        public static bool MeasuresToApproachPoint(RangeMeasure measure) => measure != RangeMeasure.CenterToCenter;

        /// <summary>
        /// Weapon distance from a delta (target point − attacker pivot). BodyEdges and CenterToApproach
        /// are 3D; ToHull and CenterToCenter are horizontal. BodyEdges subtracts both body radii.
        /// </summary>
        public static float Measure(RangeMeasure measure, float dx, float dy, float dz, float ownBody, float targetBody)
        {
            switch (measure)
            {
                case RangeMeasure.BodyEdges: return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) - ownBody - targetBody;
                case RangeMeasure.CenterToApproach: return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                default: return (float)Math.Sqrt(dx * dx + 0f * 0f + dz * dz);
            }
        }

        /// <summary>Reach for the next blow: a melee weapon closes HoldMargin inside its range before the first blow (hysteresis).</summary>
        public static float Reach(in WeaponProfile weapon, bool engaged) =>
            weapon.HoldMargin > 0 && !engaged ? Math.Max(.05f, weapon.Range - weapon.HoldMargin) : weapon.Range;

        /// <summary>A scheduled strike still lands inside range + tolerance and outside the minimum range.</summary>
        public static bool StrikeLands(in WeaponProfile weapon, float distance) =>
            distance <= weapon.Range + weapon.StrikeTolerance && distance >= weapon.MinRange;

        public static bool TooClose(in WeaponProfile weapon, float distance) => distance < weapon.MinRange;

        /// <summary>Centre distance at which a BodyEdges weapon engages a target of the given body radius.</summary>
        public static float EngageDistance(in UnitType attacker, float targetBody) =>
            attacker.BodyRadius + targetBody + Math.Max(.05f, attacker.Weapon.Range - attacker.Weapon.ApproachMargin);

        public static float AcquireRadius(in AcquisitionProfile acquisition, bool holding, bool neutralOwner) =>
            holding ? acquisition.RadiusHold : neutralOwner ? acquisition.RadiusNeutral : acquisition.RadiusHostile;

        public static float Leash(in AcquisitionProfile acquisition, bool neutralOwner) =>
            neutralOwner ? acquisition.LeashNeutral : acquisition.LeashHostile;

        /// <summary>
        /// After an attack-move reaches its goal, a target keeps the order only inside this
        /// distance of the goal. A leash from units.json wins; otherwise the acquisition radius.
        /// </summary>
        public static float AttackMoveHold(in AcquisitionProfile acquisition, bool neutralOwner) =>
            acquisition.HasLeash ? Leash(acquisition, neutralOwner) : AcquireRadius(acquisition, false, neutralOwner);

        /// <summary>Acquisition score: nearer first, spread by the attackers already on the candidate.</summary>
        public static float AcquireScore(in AcquisitionProfile acquisition, float distance, int pressure) =>
            distance + pressure * acquisition.PressureBias;

        /// <summary>Lower score wins; an equal score goes to the lower entity id or stays with the first candidate found.</summary>
        public static bool BetterCandidate(in AcquisitionProfile acquisition, float score, int entityId, float bestScore, int bestEntityId, bool hasBest) =>
            score < bestScore || acquisition.TieBreak == AcquisitionTieBreak.LowerEntityId && score == bestScore && (!hasBest || entityId < bestEntityId);

        /// <summary>Warcraft target class of a unit type: posts are structures, land units ground soldiers, hulls ground.</summary>
        public static WeaponTargetMask TargetClass(in UnitType type) =>
            type.Domain == UnitDomain.Static ? WeaponTargetMask.Structure
            : type.Domain == UnitDomain.Land ? WeaponTargetMask.Ground | WeaponTargetMask.Soldier
            : WeaponTargetMask.Ground;

        const WeaponTargetMask Classes = WeaponTargetMask.Air | WeaponTargetMask.Debris | WeaponTargetMask.Ground |
            WeaponTargetMask.Item | WeaponTargetMask.Structure | WeaponTargetMask.Ward | WeaponTargetMask.Tree |
            WeaponTargetMask.Wall | WeaponTargetMask.Soldier;
        const WeaponTargetMask Relations = WeaponTargetMask.Enemy | WeaponTargetMask.Neutral | WeaponTargetMask.Ally;

        /// <summary>
        /// Target-flag test shared by direct attacks and splash. A mask without class bits admits every
        /// class; without relation bits it admits every relation (an unrestricted splash hurts allies).
        /// </summary>
        public static bool Allows(WeaponTargetMask mask, WeaponTargetMask targetClass, UnitRelation relation)
        {
            if ((mask & Classes) != 0 && (mask & targetClass) == 0) return false;
            if (relation == UnitRelation.Self) return (mask & WeaponTargetMask.Self) != 0;
            if ((mask & Relations) == 0) return true;
            switch (relation)
            {
                case UnitRelation.Ally: return (mask & WeaponTargetMask.Ally) != 0;
                case UnitRelation.Neutral: return (mask & WeaponTargetMask.Neutral) != 0;
                default: return (mask & WeaponTargetMask.Enemy) != 0;
            }
        }

        /// <summary>Whether this weapon may attack a target of that type/relation (attackable types only).</summary>
        public static bool CanAttack(in WeaponProfile weapon, in UnitType target, UnitRelation relation) =>
            weapon.IsValid && target.CanBeAttacked && relation != UnitRelation.Self && relation != UnitRelation.Ally &&
            Allows(weapon.TargetMask, TargetClass(target), relation);

        /// <summary>How Shift and a busy unit treat one more order. Stop and Hold never queue.</summary>
        public enum OrderQueueAction { Start, Append, Clear }

        /// <summary>
        /// Shift appends while the unit is already carrying out an order. The first Shift order on an
        /// idle or holding unit starts immediately. Patrol and Follow are terminal once they start
        /// (they do not finish on their own); that is the actor's completion rule, not a different enqueue.
        /// </summary>
        public static OrderQueueAction Queue(UnitCommandKind kind, bool append, bool busy)
        {
            if (kind == UnitCommandKind.Stop || kind == UnitCommandKind.Hold) return OrderQueueAction.Clear;
            if (!append || !busy) return OrderQueueAction.Start;
            return OrderQueueAction.Append;
        }

        /// <summary>A direct attack advances the queue when its target dies. Attack-move keeps the point.</summary>
        public enum TargetLost { KeepDestination, Advance }

        public static TargetLost OnTargetLost(UnitCommandKind active) =>
            active == UnitCommandKind.Attack ? TargetLost.Advance : TargetLost.KeepDestination;

        /// <summary>Which commands a domain can carry. The motor still checks the point.</summary>
        public static bool KindAllowed(UnitDomain domain, UnitCommandKind kind)
        {
            switch (kind)
            {
                case UnitCommandKind.Move:
                case UnitCommandKind.AttackMove:
                case UnitCommandKind.Attack:
                case UnitCommandKind.Stop:
                case UnitCommandKind.Hold:
                case UnitCommandKind.Capture:
                    return domain == UnitDomain.Land || domain == UnitDomain.Sea;
                case UnitCommandKind.Patrol:
                case UnitCommandKind.Follow:
                case UnitCommandKind.Embark:
                    return domain == UnitDomain.Land;
                case UnitCommandKind.Unload:
                    return domain == UnitDomain.Sea;
                default:
                    return false;
            }
        }

        public static UnitRelation Relation(int ownTeam, int targetTeam, bool self, int neutralTeam)
        {
            if (self) return UnitRelation.Self;
            if (targetTeam == ownTeam) return UnitRelation.Ally;
            if (targetTeam == neutralTeam || targetTeam < 0) return UnitRelation.Neutral;
            return UnitRelation.Enemy;
        }
    }
}
