using System;

namespace RiskAI.Core
{
    // WeaponDelivery, WeaponTargeting and WeaponTargetMask are generated from units.schema.ts.
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
                case UnitKind.Knight:
                case UnitKind.MarineMajor:
                case UnitKind.MarineGeneral:
                    return 10; // h00G/h014/h015: 500 native.
                case UnitKind.Medic:
                    return 8; // h00E explicitly overrides acquisition to 400 native.
                case UnitKind.Mortar:
                    return 18; // h00H explicitly overrides acquisition to 900 native.
                case UnitKind.EliteRifleman:
                    return 12; // h00F inherits hrif uacq=600.
                case UnitKind.Roarer:
                    return 8;  // h00I explicitly overrides acquisition to 400 native.
                case UnitKind.ArmyGeneral:
                case UnitKind.Tank:
                    return 10; // h00J inherits hkni 500; h01A inherits hfoo 500.
                case UnitKind.Artillery:
                    return 20; // h00M explicitly overrides acquisition to 1000 native.
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
                // h00B inherits the Rifleman's instant weapon, but the runtime actor is
                // presented as a crossbowman. Use its authored ua1z=1800 as a real bolt
                // speed (1800 / 50 = 36) so damage coincides with visible contact.
                case UnitKind.Archer:
                    return new WeaponProfile(damageType, WeaponDelivery.Missile, 36, WeaponTargeting.Target,
                        sourceRawId: "h00B-crossbow-adaptation");
                // The firearm identity keeps the source Rifleman/Marine instant delivery.
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
                // h00F keeps the inherited Rifleman instant firearm (ua1w=instant).
                case UnitKind.EliteRifleman:
                    return new WeaponProfile(damageType, WeaponDelivery.Instant, sourceRawId: "h00F");
                // h00I explicitly overrides ua1z=900 (18 Unity/s); homing is inherited like h00E.
                case UnitKind.Roarer:
                    return new WeaponProfile(damageType, WeaponDelivery.Missile, 18, WeaponTargeting.Target,
                        sourceRawId: "h00I");
                // h00M explicitly sets artillery, ua1z=900, areas 25/100/170 native, factors .35/.1
                // and the same tree/ground/structure splash mask as the Mortar.
                case UnitKind.Artillery:
                    return new WeaponProfile(damageType, WeaponDelivery.Artillery, 18, WeaponTargeting.LaunchPoint,
                        .5f, 2, 3.4f, .35f, .1f, splashTargets: MortarSplash, sourceRawId: "h00M");
                // h01A overrides ua1w=msplash and ua1z=1000 but inherits no hfoo splash areas,
                // so the source weapon resolves as a single-target missile.
                case UnitKind.Tank:
                    return new WeaponProfile(damageType, WeaponDelivery.MissileSplash, 20, WeaponTargeting.Target,
                        sourceRawId: "h01A");
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
                case NavalUnitKind.Frigate:
                    // h00W explicitly supplies speed and splash targets. Its radii and factors
                    // remain TFT-only historical candidates retained by the deployed profile.
                    // Homing is unresolved, so Target preserves the deployed behavior.
                    return new WeaponProfile(damageType, WeaponDelivery.MissileSplash, 22, WeaponTargeting.Target,
                        .5f, .7f, 1, .3f, .1f, splashTargets: WarshipSplash, sourceRawId: "h00W");
                // h00U/h001 explicitly set ua1z=1000; inherited hdes msplash radii/factors match h00W.
                case NavalUnitKind.Warship:
                    return new WeaponProfile(damageType, WeaponDelivery.MissileSplash, 20, WeaponTargeting.Target,
                        .5f, .7f, 1, .3f, .1f, splashTargets: WarshipSplash, sourceRawId: "h00U");
                case NavalUnitKind.Battleship:
                    return new WeaponProfile(damageType, WeaponDelivery.MissileSplash, 20, WeaponTargeting.Target,
                        .5f, .7f, 1, .3f, .1f, splashTargets: WarshipSplash, sourceRawId: "h001");
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
