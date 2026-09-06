using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class EconomyCountryTests
    {
        [Test]
        public void FragmentedCountryPaysEachOwnedCityWithoutRequiringFullOwnership()
        {
            var economy = new Economy();
            var first = new TownState("a", 0, 0, 3);
            var second = new TownState("b", 1, 0, 3);
            economy.Towns.Add(first); economy.Towns.Add(second);
            Assert.That(economy.CountryOwner(3), Is.EqualTo(-1));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome + BattleRules.TownIncome));
            second.Owner = 0;
            Assert.That(economy.CountryOwner(3), Is.EqualTo(0));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome + BattleRules.TownIncome * 2));
            second.Owner = 1;
            Assert.That(economy.CountryOwner(3), Is.EqualTo(-1));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome + BattleRules.TownIncome));
        }

        [Test]
        public void MixedAndLegacyTownsRemainDistinguishable()
        {
            var economy = new Economy();
            economy.Towns.Add(new TownState("legacy", 0, 0));
            economy.Towns.Add(new TownState("mixed-a", 0, 0, 4));
            economy.Towns.Add(new TownState("mixed-b", -1, 0, 4));
            Assert.That(economy.CountryOwner(4), Is.EqualTo(-1));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome + BattleRules.TownIncome * 2));
        }

        [Test]
        public void CountryQueriesReflectMutableOwnershipAndTopology()
        {
            var economy = new Economy();
            var first = new TownState("a", 0, 0, 5);
            var second = new TownState("b", 0, 0, 5);
            economy.Towns.Add(first);
            economy.Towns.Add(second);

            Assert.That(economy.CountryOwner(5), Is.EqualTo(0));
            second.Owner = 1;
            Assert.That(economy.CountryOwner(5), Is.EqualTo(-1));

            economy.Towns.Remove(second);
            Assert.That(economy.CountryOwner(5), Is.EqualTo(0));
            first.Country = 7;
            Assert.That(economy.CountryOwner(5), Is.EqualTo(-1));
            Assert.That(economy.CountryOwner(7), Is.EqualTo(0));
            economy.Towns.Add(new TownState("c", 1, 0, 6));
            Assert.That(economy.CountryOwner(6), Is.EqualTo(1));
        }

        [Test]
        public void SourceCountryReinforcementUsesCeilingHalfCityCreditAndFivePointCap()
        {
            Assert.That(BattleRules.CountryReinforcementPointCapPerCity, Is.EqualTo(5));
            Assert.That(BattleRules.CountryReinforcementStepSeconds, Is.EqualTo(.5f));
            Assert.That(BattleRules.CountryReinforcementPointsPerRound(0), Is.Zero);
            Assert.That(BattleRules.CountryReinforcementPointsPerRound(1), Is.EqualTo(1));
            Assert.That(BattleRules.CountryReinforcementPointsPerRound(2), Is.EqualTo(1));
            Assert.That(BattleRules.CountryReinforcementPointsPerRound(3), Is.EqualTo(2));
        }

        [Test]
        public void GrantAndRefundValidateTeamsAndAmounts()
        {
            var economy = new Economy();

            Assert.That(economy.Grant(0, 30), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 30));
            Assert.That(economy.Refund(0, 10), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 40));
            Assert.That(economy.Grant(2, 1), Is.False);
            Assert.That(economy.Refund(-1, 1), Is.False);
            Assert.That(economy.Grant(0, -1), Is.False);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 40));
        }

        [Test]
        public void BountyRetainsQuarterPointFractionsUntilWholeGold()
        {
            var economy = new Economy();

            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold));
            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold));
            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 1));
            Assert.That(economy.GrantBounty(0, 3), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 1));
            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 2));
            Assert.That(economy.GrantBounty(2, 1), Is.False);
            Assert.That(economy.GrantBounty(0, -1), Is.False);
        }

        [Test]
        public void BountyUsesPointValueIndependentlyOfPurchasePrice()
        {
            var profile = new UnitProfile(200, 17, 1, 4, 1, 1, 5, 2,
                AttackKind.Normal, ArmorKind.Heavy, 12, 1, "Independent price fixture", 2);
            var economy = new Economy();
            economy.GrantBounty(0, profile.PointValue);
            economy.GrantBounty(0, profile.PointValue);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 1));
            Assert.That(profile.Cost, Is.EqualTo(12));
        }

        [Test]
        public void SourceGoldCostsAndNavalProfilesAreNotScaledByPrototypeMultiplier()
        {
            var economy = new Economy();
            Assert.That(BattleRules.Cost(UnitKind.Footman), Is.EqualTo(1));
            Assert.That(BattleRules.Cost(UnitKind.Archer), Is.EqualTo(1));
            Assert.That(BattleRules.Cost(UnitKind.Guard), Is.EqualTo(5));
            Assert.That(BattleRules.Cost(UnitKind.Mage), Is.EqualTo(4));
            Assert.That(BattleRules.Cost(UnitKind.Mortar), Is.EqualTo(3));
            Assert.That(BattleRules.Cost(UnitKind.Medic), Is.EqualTo(2));
            Assert.That(economy.RegionBonuses, Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(ReforgedProfiles.Tower.Health, Is.EqualTo(550));
            Assert.That(ReforgedProfiles.CapturableTower.BaseDamage, Is.EqualTo(45));
            Assert.That(ReforgedProfiles.CapturableTower.Dice, Is.EqualTo(1));
            Assert.That(ReforgedProfiles.CapturableTower.Sides, Is.EqualTo(5));
            Assert.That(ReforgedProfiles.CapturableTower.Range, Is.EqualTo(13f));
            Assert.That(ReforgedProfiles.CapturableTower.Cooldown, Is.EqualTo(.9f));
            Assert.That(NavalProfiles.Galley.Health, Is.EqualTo(400));
            Assert.That(NavalProfiles.Galley.MinimumDamage, Is.EqualTo(31));
            Assert.That(NavalProfiles.Galley.MaximumDamage, Is.EqualTo(45));
            Assert.That(NavalProfiles.Galley.Cooldown, Is.EqualTo(1.5f));
            var first=new System.Random(16016);var replay=new System.Random(16016);
            for(int i=0;i<64;i++)
            {
                float damage=NavalProfiles.Galley.RollDamage(first);
                Assert.That(damage,Is.InRange(31f,45f));
                Assert.That(damage,Is.EqualTo(NavalProfiles.Galley.RollDamage(replay)));
            }
            Assert.That(NavalProfiles.Transport.Cost, Is.EqualTo(2));
            Assert.That(NavalProfiles.Transport.Speed, Is.EqualTo(6.8f));
            Assert.That(NavalProfiles.Transport.Armor, Is.Zero);
        }
    }
}
