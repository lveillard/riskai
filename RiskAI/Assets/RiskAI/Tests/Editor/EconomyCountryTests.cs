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
    }
}
