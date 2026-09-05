using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class RiskReferenceRulesTests
    {
        [Test]
        public void ComputeSpawnAmountCapsStepAndReturnsZeroAtCap()
        {
            Assert.That(RiskReferenceRules.ComputeSpawnAmount(0, 25, 5), Is.EqualTo(5));
            Assert.That(RiskReferenceRules.ComputeSpawnAmount(23, 25, 5), Is.EqualTo(2));
            Assert.That(RiskReferenceRules.ComputeSpawnAmount(25, 25, 5), Is.Zero);
            Assert.That(RiskReferenceRules.ComputeSpawnAmount(30, 25, 5), Is.Zero);
        }

        [Test]
        public void CalculateCityCountWinCeilsSixtyPercentAndFloorsOvertime()
        {
            Assert.That(RiskReferenceRules.CalculateCityCountWin(7, .6), Is.EqualTo(5));
            Assert.That(RiskReferenceRules.CalculateCityCountWin(100, .6), Is.EqualTo(60));
            Assert.That(RiskReferenceRules.CalculateCityCountWin(100, .6, 5), Is.EqualTo(55));
            Assert.That(RiskReferenceRules.CalculateCityCountWin(3, .6, 100), Is.EqualTo(1));
        }

        [Test]
        public void ReferenceRulesRejectInvalidInputs()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RiskReferenceRules.ComputeSpawnAmount(-1, 5, 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RiskReferenceRules.CalculateCityCountWin(10, 1.1));
        }
    }
}
