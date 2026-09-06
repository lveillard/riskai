using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class EconomyCountryTests
    {
        [Test]
        public void FragmentedCountryPaysNoTownIncomeUntilBothCitiesAreOwned()
        {
            var economy = new Economy();
            var first = new TownState("a", 0, 0, 3);
            var second = new TownState("b", 1, 0, 3);
            economy.Towns.Add(first); economy.Towns.Add(second);
            Assert.That(economy.CountryOwner(3), Is.EqualTo(-1));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome));
            second.Owner = 0;
            Assert.That(economy.CountryOwner(3), Is.EqualTo(0));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome + BattleRules.TownIncome * 2 + economy.RegionBonuses[0]));
            second.Owner = 1;
            Assert.That(economy.CountryOwner(3), Is.EqualTo(-1));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome));
        }

        [Test]
        public void MixedAndLegacyTownsRemainDistinguishable()
        {
            var economy = new Economy();
            economy.Towns.Add(new TownState("legacy", 0, 0));
            economy.Towns.Add(new TownState("mixed-a", 0, 0, 4));
            economy.Towns.Add(new TownState("mixed-b", -1, 0, 4));
            Assert.That(economy.CountryOwner(4), Is.EqualTo(-1));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome + BattleRules.TownIncome));
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
    }
}
