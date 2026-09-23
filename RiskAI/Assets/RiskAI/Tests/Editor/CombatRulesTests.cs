using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class CombatRulesTests
    {
        [Test]
        public void DamageBonusesFollowWarcraftTableOrder()
        {
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Normal, ArmorKind.Light), Is.EqualTo(1f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Normal, ArmorKind.Medium), Is.EqualTo(1.5f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Normal, ArmorKind.Heavy), Is.EqualTo(1f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Normal, ArmorKind.Fortified), Is.EqualTo(.7f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Normal, ArmorKind.Unarmored), Is.EqualTo(1f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Piercing, ArmorKind.Light), Is.EqualTo(2f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Piercing, ArmorKind.Medium), Is.EqualTo(.75f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Piercing, ArmorKind.Heavy), Is.EqualTo(1f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Piercing, ArmorKind.Fortified), Is.EqualTo(.35f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Piercing, ArmorKind.Unarmored), Is.EqualTo(1.5f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Siege, ArmorKind.Light), Is.EqualTo(1f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Siege, ArmorKind.Medium), Is.EqualTo(.5f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Siege, ArmorKind.Heavy), Is.EqualTo(1f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Siege, ArmorKind.Fortified), Is.EqualTo(1.5f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Siege, ArmorKind.Unarmored), Is.EqualTo(1.5f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Magic, ArmorKind.Light), Is.EqualTo(1.5f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Magic, ArmorKind.Medium), Is.EqualTo(.75f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Magic, ArmorKind.Heavy), Is.EqualTo(2f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Magic, ArmorKind.Fortified), Is.EqualTo(.35f));
            Assert.That(CombatRules.DamageMultiplier(AttackKind.Magic, ArmorKind.Unarmored), Is.EqualTo(1f));
        }

        [Test]
        public void ArmorReducesBySixPercentPerPoint()
        {
            Assert.That(CombatRules.ArmorMultiplier(0), Is.EqualTo(1f));
            Assert.That(CombatRules.ArmorMultiplier(2), Is.EqualTo(1f / 1.12f).Within(.0001f));
            Assert.That(CombatRules.ResolveDamage(24, AttackKind.Piercing, ArmorKind.Fortified, 3), Is.EqualTo(24f * .35f / 1.18f).Within(.0001f));
            Assert.That(CombatRules.ResolveDamage(-1, AttackKind.Normal, ArmorKind.Light, 0), Is.Zero);
        }

        [Test]
        public void UphillMissChanceOnlyAppliesToPiercingAtTheThreshold()
        {
            Assert.That(CombatRules.UphillMissChance(AttackKind.Piercing, 2.49f), Is.Zero);
            Assert.That(CombatRules.UphillMissChance(AttackKind.Piercing, 2.5f), Is.EqualTo(.25f));
            Assert.That(CombatRules.UphillMissChance(AttackKind.Piercing, 9f), Is.EqualTo(.25f));
            Assert.That(CombatRules.UphillMissChance(AttackKind.Normal, 9f), Is.Zero);
            Assert.That(CombatRules.UphillMissChance(AttackKind.Siege, 9f), Is.Zero);
            Assert.That(CombatRules.UphillMissChance(AttackKind.Magic, 9f), Is.Zero);
        }

        [Test]
        public void ExtractedReforgedProfilesUseMapAttackAndDefenseTypes()
        {
            var rifleman = UnitCatalog.Get(UnitKind.Archer);
            var knight = UnitCatalog.Get(UnitKind.Knight);
            var mortar = UnitCatalog.Get(UnitKind.Mortar);
            var medic = UnitCatalog.Get(UnitKind.Medic);
            Assert.That(rifleman.Weapon.Cooldown, Is.EqualTo(1.6f)); Assert.That(rifleman.ArmorType, Is.EqualTo(ArmorKind.Light));
            Assert.That(rifleman.Weapon.AttackPoint, Is.EqualTo(.17f)); Assert.That(rifleman.Weapon.Backswing, Is.EqualTo(.7f));
            Assert.That(knight.Weapon.Range, Is.EqualTo(2f)); Assert.That(knight.Weapon.Cooldown, Is.EqualTo(1.36f));
            Assert.That(knight.Weapon.AttackPoint, Is.EqualTo(.66f)); Assert.That(knight.Weapon.Backswing, Is.EqualTo(.44f));
            Assert.That(mortar.ArmorType, Is.EqualTo(ArmorKind.Medium)); Assert.That(mortar.Weapon.AttackPoint, Is.EqualTo(1f)); Assert.That(mortar.Weapon.Backswing, Is.EqualTo(1.1f));
            Assert.That(medic.AttackType, Is.EqualTo(AttackKind.Piercing)); Assert.That(medic.ArmorType, Is.EqualTo(ArmorKind.Light)); Assert.That(medic.Weapon.AttackPoint, Is.EqualTo(.59f)); Assert.That(medic.Weapon.Backswing, Is.EqualTo(.58f));
            Assert.That(UnitCatalog.Get(UnitKind.Knight).Name, Is.EqualTo("Caballero"));
            Assert.That(UnitCatalog.Get(UnitKind.Knight).Role, Is.EqualTo("Caballer\u00eda pesada"));
            var privateMarine = UnitCatalog.Get(UnitKind.MarinePrivate);
            var major = UnitCatalog.Get(UnitKind.MarineMajor);
            var general = UnitCatalog.Get(UnitKind.MarineGeneral);
            Assert.That(privateMarine.Weapon.MinimumDamage, Is.EqualTo(18)); Assert.That(privateMarine.Weapon.MaximumDamage, Is.EqualTo(24)); Assert.That(privateMarine.Weapon.AttackPoint, Is.EqualTo(.17f));
            Assert.That(major.MaxHealth, Is.EqualTo(650)); Assert.That(major.Armor, Is.EqualTo(6));
            Assert.That(major.Weapon.AttackPoint, Is.EqualTo(.66f));
            Assert.That(general.MaxHealth, Is.EqualTo(800)); Assert.That(general.Cost, Is.EqualTo(10)); Assert.That(general.Points, Is.EqualTo(10)); Assert.That(general.Weapon.AttackPoint, Is.EqualTo(.66f));
            Assert.That(ProductionHotkeys.Hotkey(UnitKind.MarinePrivate), Is.EqualTo("Q"), "Cheapest harbor product takes the first grid cell.");
            Assert.That(UnitCatalog.Get(UnitKind.MarinePrivate).Weapon.Ranged, Is.True); Assert.That(UnitCatalog.Get(UnitKind.MarineMajor).Weapon.Ranged, Is.False);
        }

        [Test]
        public void MortarUsesSiegeProfileAndCanRecruitWithoutUpgrade()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Cost,Is.EqualTo(3));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).TrainSeconds,Is.EqualTo(1f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).MaxHealth,Is.EqualTo(350f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Speed,Is.EqualTo(4.6f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Weapon.AverageDamage,Is.EqualTo(25f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Weapon.Cooldown,Is.EqualTo(3.5f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Weapon.AttackPoint,Is.EqualTo(1f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Weapon.Range,Is.EqualTo(18f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Weapon.Ranged,Is.True);
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Level,Is.EqualTo(1));
            Assert.That(ProductionHotkeys.Hotkey(UnitKind.Mortar),Is.EqualTo("R"));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Model,Is.EqualTo("Mortar"));
        }
    }
}
