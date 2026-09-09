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
            var rifleman = ReforgedProfiles.Units[(int)UnitKind.Archer];
            var knight = ReforgedProfiles.Units[(int)UnitKind.Guard];
            var mortar = ReforgedProfiles.Units[(int)UnitKind.Mortar];
            var medic = ReforgedProfiles.Units[(int)UnitKind.Medic];
            Assert.That(rifleman.Cooldown, Is.EqualTo(1.6f)); Assert.That(rifleman.Defense, Is.EqualTo(ArmorKind.Light));
            Assert.That(rifleman.AttackPoint, Is.EqualTo(.17f)); Assert.That(rifleman.Backswing, Is.EqualTo(.7f));
            Assert.That(knight.Range, Is.EqualTo(2f)); Assert.That(knight.Cooldown, Is.EqualTo(1.36f));
            Assert.That(knight.AttackPoint, Is.EqualTo(.66f)); Assert.That(knight.Backswing, Is.EqualTo(.44f));
            Assert.That(mortar.Defense, Is.EqualTo(ArmorKind.Medium)); Assert.That(mortar.AttackPoint, Is.EqualTo(1f)); Assert.That(mortar.Backswing, Is.EqualTo(1.1f));
            Assert.That(medic.Attack, Is.EqualTo(AttackKind.Piercing)); Assert.That(medic.Defense, Is.EqualTo(ArmorKind.Light)); Assert.That(medic.AttackPoint, Is.EqualTo(.59f)); Assert.That(medic.Backswing, Is.EqualTo(.58f));
            Assert.That(BattleRules.Name(UnitKind.Guard), Is.EqualTo("Caballero"));
            Assert.That(BattleRules.Role(UnitKind.Guard), Is.EqualTo("Caballer\u00eda pesada"));
            var privateMarine = ReforgedProfiles.Units[(int)UnitKind.MarinePrivate];
            var major = ReforgedProfiles.Units[(int)UnitKind.MarineMajor];
            var general = ReforgedProfiles.Units[(int)UnitKind.MarineGeneral];
            Assert.That(privateMarine.MinimumDamage, Is.EqualTo(18)); Assert.That(privateMarine.MaximumDamage, Is.EqualTo(24)); Assert.That(privateMarine.AttackPoint, Is.EqualTo(.17f));
            Assert.That(major.Health, Is.EqualTo(650)); Assert.That(major.Armor, Is.EqualTo(6));
            Assert.That(major.AttackPoint, Is.EqualTo(.66f));
            Assert.That(general.Health, Is.EqualTo(800)); Assert.That(general.Cost, Is.EqualTo(10)); Assert.That(general.PointValue, Is.EqualTo(10)); Assert.That(general.AttackPoint, Is.EqualTo(.66f));
            Assert.That(BattleRules.Hotkey(UnitKind.MarinePrivate), Is.EqualTo("V"));
            Assert.That(BattleRules.Ranged(UnitKind.MarinePrivate), Is.True); Assert.That(BattleRules.Ranged(UnitKind.MarineMajor), Is.False);
        }

        [Test]
        public void MortarUsesSiegeProfileAndCanRecruitWithoutUpgrade()
        {
            Assert.That(BattleRules.Cost(UnitKind.Mortar),Is.EqualTo(3));
            Assert.That(BattleRules.TrainTime(UnitKind.Mortar),Is.EqualTo(1f));
            Assert.That(BattleRules.Health(UnitKind.Mortar),Is.EqualTo(350f));
            Assert.That(BattleRules.Speed(UnitKind.Mortar),Is.EqualTo(4.6f));
            Assert.That(BattleRules.Damage(UnitKind.Mortar),Is.EqualTo(25f));
            Assert.That(BattleRules.AttackInterval(UnitKind.Mortar),Is.EqualTo(3.5f));
            Assert.That(BattleRules.AttackPoint(UnitKind.Mortar),Is.EqualTo(1f));
            Assert.That(BattleRules.Range(UnitKind.Mortar),Is.EqualTo(18f));
            Assert.That(BattleRules.Ranged(UnitKind.Mortar),Is.True);
            Assert.That(BattleRules.RequiredLevel(UnitKind.Mortar),Is.EqualTo(1));
            Assert.That(BattleRules.Hotkey(UnitKind.Mortar),Is.EqualTo("R"));
            Assert.That(BattleRules.Model(UnitKind.Mortar),Is.EqualTo("Mortar"));
        }
    }
}
