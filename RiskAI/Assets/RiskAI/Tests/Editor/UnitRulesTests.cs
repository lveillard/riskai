using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    /// <summary>The shared combat rules: every unit type uses them, differences are data.</summary>
    public sealed class UnitRulesTests
    {
        [Test]
        public void MeasuresReproduceTheFourLegacyFormulas()
        {
            // 3 m right, 4 m forward, 12 m up.
            Assert.That(UnitRules.Measure(RangeMeasure.CenterToApproach, 3, 12, 4, .3f, .2f), Is.EqualTo(13f).Within(1e-5f));
            Assert.That(UnitRules.Measure(RangeMeasure.BodyEdges, 3, 12, 4, .3f, .2f), Is.EqualTo(12.5f).Within(1e-5f));
            Assert.That(UnitRules.Measure(RangeMeasure.ToHull, 3, 12, 4, .3f, .2f), Is.EqualTo(5f).Within(1e-5f));
            Assert.That(UnitRules.Measure(RangeMeasure.CenterToCenter, 3, 12, 4, .3f, .2f), Is.EqualTo(5f).Within(1e-5f));
            Assert.That(UnitRules.MeasuresToApproachPoint(RangeMeasure.CenterToCenter), Is.False);
            Assert.That(UnitRules.MeasuresToApproachPoint(RangeMeasure.ToHull), Is.True);
        }

        [Test]
        public void EveryTypeMeasuresAsUnitsJsonSays()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Knight).Weapon.Measure, Is.EqualTo(RangeMeasure.BodyEdges));
            Assert.That(UnitCatalog.Get(UnitKind.Archer).Weapon.Measure, Is.EqualTo(RangeMeasure.CenterToApproach));
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).Weapon.Measure, Is.EqualTo(RangeMeasure.ToHull));
            Assert.That(UnitCatalog.Get(UnitKind.Tower).TownWeapon.Measure, Is.EqualTo(RangeMeasure.CenterToCenter));
            // Melee acquires to the approach point, not body edge to body edge.
            Assert.That(UnitCatalog.Get(UnitKind.Knight).Acquisition.Measure, Is.EqualTo(RangeMeasure.CenterToApproach));
        }

        [Test]
        public void MeleeReachHasHysteresisAndRangedDoesNot()
        {
            ref readonly var knight = ref UnitCatalog.Get(UnitKind.Knight).Weapon;
            Assert.That(UnitRules.Reach(knight, false), Is.EqualTo(knight.Range - .2f).Within(1e-6f));
            Assert.That(UnitRules.Reach(knight, true), Is.EqualTo(knight.Range));
            ref readonly var archer = ref UnitCatalog.Get(UnitKind.Archer).Weapon;
            Assert.That(UnitRules.Reach(archer, false), Is.EqualTo(archer.Range));
            ref readonly var mortar = ref UnitCatalog.Get(UnitKind.Mortar).Weapon;
            Assert.That(UnitRules.TooClose(mortar, 4.9f), Is.True);
            Assert.That(UnitRules.StrikeLands(mortar, 18.5f), Is.True, "strike tolerance .55");
            Assert.That(UnitRules.StrikeLands(mortar, 18.6f), Is.False);
        }

        [Test]
        public void AcquisitionRadiusLeashAndTieBreakAreData()
        {
            ref readonly var footman = ref UnitCatalog.Get(UnitKind.Footman).Acquisition;
            Assert.That(UnitRules.AcquireRadius(footman, false, false), Is.EqualTo(7.5f));
            Assert.That(UnitRules.AcquireRadius(footman, false, true), Is.EqualTo(5f));
            Assert.That(UnitRules.AcquireRadius(footman, true, false), Is.EqualTo(.9f));
            Assert.That(UnitRules.Leash(footman, true), Is.EqualTo(7f));
            Assert.That(UnitRules.AcquireScore(footman, 3, 2), Is.EqualTo(3 + 2 * .48f).Within(1e-6f));
            Assert.That(UnitRules.BetterCandidate(footman, 3, 5, 3, 9, true), Is.True, "soldiers prefer the lower id on a tie");
            ref readonly var frigate = ref UnitCatalog.Get(UnitKind.Frigate).Acquisition;
            Assert.That(UnitRules.BetterCandidate(frigate, 3, 5, 3, 9, true), Is.False, "ships keep the first candidate found");
            Assert.That(frigate.HasLeash, Is.False);
        }

        [Test]
        public void TargetFlagsAdmitClassesAndRelations()
        {
            var frigate = UnitCatalog.Get(UnitKind.Frigate).Weapon;
            // Warship splash: debris/ground/structure/wall on enemies and neutrals, never allies.
            Assert.That(UnitRules.Allows(frigate.SplashTargets, WeaponTargetMask.Ground, UnitRelation.Enemy), Is.True);
            Assert.That(UnitRules.Allows(frigate.SplashTargets, WeaponTargetMask.Ground, UnitRelation.Ally), Is.False);
            // Mortar splash has no relation bits: it hurts allies too, but never the Mage-only class filter.
            var mortar = UnitCatalog.Get(UnitKind.Mortar).Weapon;
            Assert.That(UnitRules.Allows(mortar.SplashTargets, WeaponTargetMask.Ground | WeaponTargetMask.Soldier, UnitRelation.Ally), Is.True);
            Assert.That(UnitRules.Allows(mortar.SplashTargets, WeaponTargetMask.Ground, UnitRelation.Self), Is.False);
            var mage = UnitCatalog.Get(UnitKind.Mage).Weapon;
            Assert.That(UnitRules.Allows(mage.SplashTargets, WeaponTargetMask.Ground, UnitRelation.Enemy), Is.False, "the mage splash only hits soldiers");
            Assert.That(UnitRules.Allows(mage.SplashTargets, WeaponTargetMask.Ground | WeaponTargetMask.Soldier, UnitRelation.Neutral), Is.True);
            // A melee soldier can attack a ship as it always could; posts are never targets.
            var knight = UnitCatalog.Get(UnitKind.Knight).Weapon;
            Assert.That(UnitRules.CanAttack(knight, UnitCatalog.Get(UnitKind.Frigate), UnitRelation.Enemy), Is.True);
            Assert.That(UnitRules.CanAttack(knight, UnitCatalog.Get(UnitKind.Tower), UnitRelation.Enemy), Is.False);
            Assert.That(UnitRules.CanAttack(knight, UnitCatalog.Get(UnitKind.Footman), UnitRelation.Ally), Is.False);
            Assert.That(UnitRules.CanAttack(UnitCatalog.Get(UnitKind.Transport).Weapon, UnitCatalog.Get(UnitKind.Footman), UnitRelation.Enemy), Is.False);
        }

        [Test]
        public void RelationsFollowTeams()
        {
            Assert.That(UnitRules.Relation(1, 1, true, PlayerRules.NeutralTeam), Is.EqualTo(UnitRelation.Self));
            Assert.That(UnitRules.Relation(1, 1, false, PlayerRules.NeutralTeam), Is.EqualTo(UnitRelation.Ally));
            Assert.That(UnitRules.Relation(1, PlayerRules.NeutralTeam, false, PlayerRules.NeutralTeam), Is.EqualTo(UnitRelation.Neutral));
            Assert.That(UnitRules.Relation(1, 2, false, PlayerRules.NeutralTeam), Is.EqualTo(UnitRelation.Enemy));
        }
    }
}
