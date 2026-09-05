using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class StartingAllocationTests
    {
        [Test]
        public void WholeCountriesIsSeededBalancedAndLeavesFourNeutralCountries()
        {
            var first = StartingAllocation.GeneratePrototype(37);
            var second = StartingAllocation.GeneratePrototype(37);

            Assert.That(first.CityOwners, Is.EqualTo(second.CityOwners));
            Assert.That(first.CountryOwners, Is.EqualTo(second.CountryOwners));
            Assert.That(first.CountCitiesForPlayer(0), Is.EqualTo(2));
            Assert.That(first.CountCitiesForPlayer(1), Is.EqualTo(2));

            var neutralCountries = 0;
            for (var country = 0; country < first.CountryOwners.Length; country++)
            {
                if (first.CountryOwners[country] == StartingAllocationPlan.NeutralOwner) neutralCountries++;
                else Assert.That(first.CountryOwners[country], Is.EqualTo(0).Or.EqualTo(1));
            }
            Assert.That(neutralCountries, Is.EqualTo(4));

            for (var city = 0; city < first.CityOwners.Length; city++)
                Assert.That(first.CityOwners[city], Is.EqualTo(first.CountryOwners[city / 2]));
        }

        [Test]
        public void IndividualCitiesKeepsBothPlayersAtSixCities()
        {
            var plan = StartingAllocation.GeneratePrototype(37, StartingAllocationMode.IndividualCities);

            Assert.That(plan.CountCitiesForPlayer(0), Is.EqualTo(6));
            Assert.That(plan.CountCitiesForPlayer(1), Is.EqualTo(6));
            for (var city = 0; city < plan.CityOwners.Length; city++)
                Assert.That(plan.CityOwners[city], Is.EqualTo(0).Or.EqualTo(1));
            for (var country = 0; country < plan.CountryOwners.Length; country++)
                Assert.That(plan.CountryOwners[country], Is.EqualTo(0).Or.EqualTo(1).Or.EqualTo(StartingAllocationPlan.MixedOwner));
        }

        [Test]
        public void IndividualCitiesLeavesIntegerDivisionRemainderNeutral()
        {
            var thirteenCities = new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6 };
            var plan = StartingAllocation.Generate(37, thirteenCities, 2, StartingAllocationMode.IndividualCities);

            Assert.That(plan.CountCitiesForPlayer(0), Is.EqualTo(6));
            Assert.That(plan.CountCitiesForPlayer(1), Is.EqualTo(6));
            var neutralCities = 0;
            for (var city = 0; city < plan.CityOwners.Length; city++)
                if (plan.CityOwners[city] == StartingAllocationPlan.NeutralOwner) neutralCities++;
            Assert.That(neutralCities, Is.EqualTo(1));
        }

        [Test]
        public void InvalidTopologyIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => StartingAllocation.Generate(1, new[] { 0, 2 }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => StartingAllocation.Generate(1, new[] { 0, 0 }, 0));
        }
    }
}
