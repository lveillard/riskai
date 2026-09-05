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
        public void MortarUsesSiegeProfileAndLevelTwoQueueRules()
        {
            Assert.That(BattleRules.Cost(UnitKind.Mortar),Is.EqualTo(60));
            Assert.That(BattleRules.TrainTime(UnitKind.Mortar),Is.EqualTo(6f));
            Assert.That(BattleRules.Health(UnitKind.Mortar),Is.EqualTo(350f));
            Assert.That(BattleRules.Speed(UnitKind.Mortar),Is.EqualTo(4.6f));
            Assert.That(BattleRules.Damage(UnitKind.Mortar),Is.EqualTo(25f));
            Assert.That(BattleRules.AttackInterval(UnitKind.Mortar),Is.EqualTo(3.5f));
            Assert.That(BattleRules.Range(UnitKind.Mortar),Is.EqualTo(18f));
            Assert.That(BattleRules.Ranged(UnitKind.Mortar),Is.True);
            Assert.That(BattleRules.RequiredLevel(UnitKind.Mortar),Is.EqualTo(2));
            Assert.That(BattleRules.Role(UnitKind.Mortar),Is.EqualTo("Asedio · contra fortificaciones"));
            Assert.That(BattleRules.Hotkey(UnitKind.Mortar),Is.EqualTo("R"));
            Assert.That(BattleRules.Model(UnitKind.Mortar),Is.EqualTo("Mortar"));
        }
    }
}
