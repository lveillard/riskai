using System;

namespace RiskAI.Core
{
    public enum WeaponDelivery
    {
        Instant,
        Missile,
        Artillery,
        MissileSplash
    }

    public enum WeaponTargeting
    {
        Target,
        LaunchPoint
    }

    [Flags]
    public enum WeaponTargetMask
    {
        None = 0,
        Air = 1 << 0,
        Debris = 1 << 1,
        Ground = 1 << 2,
        Item = 1 << 3,
        Structure = 1 << 4,
        Ward = 1 << 5,
        Self = 1 << 6,
        Tree = 1 << 7,
        Wall = 1 << 8,
        Enemy = 1 << 9,
        Neutral = 1 << 10,
        Ally = 1 << 11,
        // Runtime-only classification used to preserve the local Mage profile.
        Soldier = 1 << 12
    }

    /// <summary>Source-facing weapon delivery data, independent from the damage bonus category.</summary>
    public readonly struct WeaponProfile
    {
        public readonly AttackKind DamageType;
        public readonly WeaponDelivery Delivery;
        public readonly WeaponTargeting Targeting;
        public readonly float ProjectileSpeed;
        public readonly float FullDamageRadius;
        public readonly float MediumDamageRadius;
        public readonly float SmallDamageRadius;
        public readonly float MediumDamageFactor;
        public readonly float SmallDamageFactor;
        public readonly float MinimumFlightTime;
        public readonly float MaximumFlightTime;
        public readonly WeaponTargetMask SplashTargets;
        public readonly string SourceRawId;
        readonly bool initialized;

        public bool IsValid => initialized && (Delivery == WeaponDelivery.Instant || ProjectileSpeed > 0);
        public bool IsProjectile => Delivery != WeaponDelivery.Instant;
        public bool HasSplash => SmallDamageRadius > 0;
        public bool TracksTarget => Targeting == WeaponTargeting.Target;

        public WeaponProfile(
            AttackKind damageType,
            WeaponDelivery delivery,
            float projectileSpeed = 0,
            WeaponTargeting targeting = WeaponTargeting.Target,
            float fullDamageRadius = 0,
            float mediumDamageRadius = 0,
            float smallDamageRadius = 0,
            float mediumDamageFactor = 0,
            float smallDamageFactor = 0,
            float minimumFlightTime = 0,
            float maximumFlightTime = float.PositiveInfinity,
            WeaponTargetMask splashTargets = WeaponTargetMask.None,
            string sourceRawId = null)
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

            DamageType = damageType;
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
            SourceRawId = sourceRawId;
            initialized = true;
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

    /// <summary>Delivery fields for the source IDs currently represented by runtime actors.</summary>
    public static class SourceWeapons
    {
        const WeaponTargetMask MortarSplash = WeaponTargetMask.Tree | WeaponTargetMask.Ground | WeaponTargetMask.Structure;
        const WeaponTargetMask WarshipSplash = WeaponTargetMask.Debris | WeaponTargetMask.Enemy |
            WeaponTargetMask.Ground | WeaponTargetMask.Neutral | WeaponTargetMask.Structure | WeaponTargetMask.Wall;

        public static float AcquisitionRange(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Archer:
                case UnitKind.MarinePrivate:
                    return 12; // h00B/h012/h00R: 600 native.
                case UnitKind.Guard:
                case UnitKind.MarineMajor:
                case UnitKind.MarineGeneral:
                    return 10; // h00G/h014/h015: 500 native.
                case UnitKind.Medic:
                    return 8; // h00E explicitly overrides acquisition to 400 native.
                case UnitKind.Mortar:
                    return 18; // h00H explicitly overrides acquisition to 900 native.
                case UnitKind.Mage:
                    return 11; // Preserve the local profile's former range + 1 behavior.
                default:
                    return 0;
            }
        }

        public static WeaponProfile For(UnitKind kind, AttackKind damageType)
        {
            switch (kind)
            {
                // The source weapon type is instant for h00B and both deployed marine-private IDs.
                case UnitKind.Archer:
                    return new WeaponProfile(damageType, WeaponDelivery.Instant, sourceRawId: "h00B");
                case UnitKind.MarinePrivate:
                    return new WeaponProfile(damageType, WeaponDelivery.Instant, sourceRawId: "h012/h00R");
                // h00E speed 1100 uses the project's established native-distance / 50 scale.
                // Its homing inheritance is not certified for 2.0.2, so Target preserves runtime behavior.
                case UnitKind.Medic:
                    return new WeaponProfile(damageType, WeaponDelivery.Missile, 22, WeaponTargeting.Target,
                        sourceRawId: "h00E");
                // h00H supplies an explicit area-target mask and medium factor. Its artillery
                // delivery, speed, radii and outer factor agree in both owned classic sources.
                case UnitKind.Mortar:
                    return new WeaponProfile(damageType, WeaponDelivery.Artillery, 18, WeaponTargeting.LaunchPoint,
                        .5f, 3, 5, .35f, .1f, splashTargets: MortarSplash, sourceRawId: "h00H");
                // These prototype-only identities retain an explicit version of their former behavior.
                case UnitKind.Mage:
                    return new WeaponProfile(damageType, WeaponDelivery.Missile, 25, WeaponTargeting.Target,
                        0, 2.4f, 2.4f, .5f, .5f, .15f, .6f,
                        WeaponTargetMask.Soldier | WeaponTargetMask.Enemy | WeaponTargetMask.Neutral,
                        "local-mage");
                default:
                    return new WeaponProfile(damageType, WeaponDelivery.Instant, sourceRawId: "local-melee");
            }
        }

        public static WeaponProfile For(NavalUnitKind kind, AttackKind damageType)
        {
            switch (kind)
            {
                case NavalUnitKind.Galley:
                    // h00W explicitly supplies speed and splash targets. Its radii and factors
                    // remain TFT-only historical candidates retained by the deployed profile.
                    // Homing is unresolved, so Target preserves the deployed behavior.
                    return new WeaponProfile(damageType, WeaponDelivery.MissileSplash, 22, WeaponTargeting.Target,
                        .5f, .7f, 1, .3f, .1f, splashTargets: WarshipSplash, sourceRawId: "h00W");
                default:
                    return default;
            }
        }

        // h00N explicitly sets MissileHoming=1. h00O has no resolved homing field;
        // Target preserves its current behavior without presenting it as source proof.
        public static readonly WeaponProfile MilitaryBase = new WeaponProfile(
            AttackKind.Piercing, WeaponDelivery.Missile, 32, WeaponTargeting.Target, sourceRawId: "h00N");
        public static readonly WeaponProfile Shipyard = new WeaponProfile(
            AttackKind.Piercing, WeaponDelivery.Missile, 32, WeaponTargeting.Target, sourceRawId: "h00O");
    }
}
