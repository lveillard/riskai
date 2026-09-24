using System;

namespace RiskAI.Core
{
    // WeaponDelivery, WeaponTargeting and WeaponTargetMask are generated from units.schema.ts.

    /// <summary>
    /// One resolved attack from units.json: damage roll, timing, reach, delivery and splash.
    /// default(WeaponProfile) is "no weapon" (<see cref="IsValid"/> false).
    /// </summary>
    public readonly struct WeaponProfile
    {
        public readonly string SourceRawId;
        public readonly AttackKind DamageType;
        public readonly float Base;
        public readonly int Dice, Sides;
        public readonly float Cooldown, AttackPoint, Backswing, Range, MinRange;
        public readonly bool Ranged;
        public readonly RangeMeasure Measure;
        /// <summary>Melee hysteresis: path this far inside the reach / open the first blow this far inside it.</summary>
        public readonly float ApproachMargin, HoldMargin;
        /// <summary>Extra distance tolerated when a scheduled strike resolves.</summary>
        public readonly float StrikeTolerance;
        public readonly WeaponDelivery Delivery;
        public readonly WeaponTargeting Targeting;
        public readonly float ProjectileSpeed;
        public readonly float FullDamageRadius, MediumDamageRadius, SmallDamageRadius;
        public readonly float MediumDamageFactor, SmallDamageFactor;
        public readonly float MinimumFlightTime, MaximumFlightTime;
        public readonly WeaponTargetMask SplashTargets, TargetMask;
        public readonly bool Tracer;
        public readonly WeaponSound Sound;
        readonly bool initialized;

        public bool IsValid => initialized && (Delivery == WeaponDelivery.Instant || ProjectileSpeed > 0);
        public bool IsProjectile => Delivery != WeaponDelivery.Instant;
        public bool HasSplash => SmallDamageRadius > 0;
        public bool TracksTarget => Targeting == WeaponTargeting.Target;
        public float MinimumDamage => Base + Dice;
        public float MaximumDamage => Base + Dice * Sides;
        public float AverageDamage => Base + Dice * (Sides + 1) * .5f;
        public string DamageText => Dice > 0 ? MinimumDamage + "–" + MaximumDamage : AverageDamage.ToString();

        public WeaponProfile(string sourceRawId, AttackKind damageType, float baseDamage, int dice, int sides,
            float cooldown, float attackPoint, float backswing, float range, float minRange, bool ranged,
            RangeMeasure measure, float approachMargin, float holdMargin, float strikeTolerance,
            WeaponDelivery delivery, WeaponTargeting targeting, float projectileSpeed,
            float fullDamageRadius, float mediumDamageRadius, float smallDamageRadius,
            float mediumDamageFactor, float smallDamageFactor,
            float minimumFlightTime, float maximumFlightTime,
            WeaponTargetMask splashTargets, WeaponTargetMask targetMask, bool tracer, WeaponSound sound)
        {
            if (delivery != WeaponDelivery.Instant &&
                (projectileSpeed <= 0 || float.IsNaN(projectileSpeed) || float.IsInfinity(projectileSpeed)))
                throw new ArgumentOutOfRangeException(nameof(projectileSpeed));
            if (fullDamageRadius < 0 || mediumDamageRadius < fullDamageRadius || smallDamageRadius < mediumDamageRadius ||
                float.IsNaN(fullDamageRadius) || float.IsNaN(mediumDamageRadius) || float.IsNaN(smallDamageRadius))
                throw new ArgumentOutOfRangeException(nameof(fullDamageRadius));
            if (mediumDamageFactor < 0 || mediumDamageFactor > 1 || smallDamageFactor < 0 || smallDamageFactor > 1 ||
                float.IsNaN(mediumDamageFactor) || float.IsNaN(smallDamageFactor))
                throw new ArgumentOutOfRangeException(nameof(mediumDamageFactor));
            if (minimumFlightTime < 0 || float.IsNaN(minimumFlightTime) ||
                maximumFlightTime < minimumFlightTime || float.IsNaN(maximumFlightTime))
                throw new ArgumentOutOfRangeException(nameof(minimumFlightTime));

            SourceRawId = sourceRawId;
            DamageType = damageType;
            Base = baseDamage; Dice = dice; Sides = sides;
            Cooldown = cooldown; AttackPoint = attackPoint; Backswing = backswing;
            Range = range; MinRange = minRange; Ranged = ranged; Measure = measure;
            ApproachMargin = approachMargin; HoldMargin = holdMargin; StrikeTolerance = strikeTolerance;
            Delivery = delivery;
            Targeting = targeting;
            ProjectileSpeed = projectileSpeed;
            FullDamageRadius = fullDamageRadius;
            MediumDamageRadius = mediumDamageRadius;
            SmallDamageRadius = smallDamageRadius;
            MediumDamageFactor = mediumDamageFactor;
            SmallDamageFactor = smallDamageFactor;
            MinimumFlightTime = minimumFlightTime;
            MaximumFlightTime = maximumFlightTime;
            SplashTargets = splashTargets;
            TargetMask = targetMask;
            Tracer = tracer;
            Sound = sound;
            initialized = true;
        }

        public float RollDamage(Random random)
        {
            float result = Base;
            for (int i = 0; i < Dice; i++) result += random.Next(1, Sides + 1);
            return result;
        }

        public float FlightTime(float distance)
        {
            if (!IsProjectile) return 0;
            float duration = Math.Max(0, distance) / ProjectileSpeed;
            return Math.Min(MaximumFlightTime, Math.Max(MinimumFlightTime, duration));
        }

        public float SplashFactor(float distance)
        {
            if (!HasSplash || distance < 0 || float.IsNaN(distance)) return 0;
            if (distance <= FullDamageRadius) return 1;
            if (distance <= MediumDamageRadius) return MediumDamageFactor;
            return distance <= SmallDamageRadius ? SmallDamageFactor : 0;
        }
    }
}
