using System;
using NUnit.Framework;
using RiskAI.Core;
namespace RiskAI.Tests
{
 public sealed class ReforgedCombatTests
 {
  [Test] public void DiceUseTheirActualDiscreteDistributionAndSeedRepeats()
  {
   var first=new Random(391);var second=new Random(391);var profile=BattleRules.Profile(UnitKind.Archer);
   int[] frequencies=new int[7];
   for(int i=0;i<16000;i++)
   {
    float value=profile.RollDamage(first);Assert.That(value,Is.EqualTo(profile.RollDamage(second)));
    Assert.That(value,Is.InRange(17,23));frequencies[(int)value-17]++;
   }
   Assert.That(frequencies[3],Is.GreaterThan(frequencies[0]*3),"2d4 must favour central results; a flat random min/max roll is not equivalent.");
  }
  [Test] public void TowerHitsDependOnArmorAndSiegeCountersFortifications()
  {
   float lightHit=CombatRules.ResolveDamage(54,AttackKind.Piercing,ArmorKind.Medium,0);
   float heavyHit=CombatRules.ResolveDamage(54,AttackKind.Piercing,ArmorKind.Heavy,7);
   Assert.That(Math.Ceiling(200/lightHit),Is.EqualTo(5));
   Assert.That(Math.Ceiling(650/heavyHit),Is.EqualTo(18));
   Assert.That(CombatRules.ResolveDamage(25,AttackKind.Siege,ArmorKind.Fortified,3),Is.GreaterThan(CombatRules.ResolveDamage(25,AttackKind.Piercing,ArmorKind.Fortified,3)*4));
   Assert.That(CombatRules.ResolveDamage(100,AttackKind.Normal,ArmorKind.Heavy,-5),Is.EqualTo(126.6096).Within(.001));
  }
 }
}

