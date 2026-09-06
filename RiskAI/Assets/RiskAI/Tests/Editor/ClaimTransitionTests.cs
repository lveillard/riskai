using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class ClaimRulesTests
    {
        [Test]
        public void LivingOwnerUnitBeatsCloserEnemy()
        {
            Assert.That(
                ClaimRules.BetterCandidate(0, 0, 16f, 12, 1, 1f, 3),
                Is.True,
                "A surviving owner unit protects the town before an opposing unit can replace it.");
        }

        [Test]
        public void NearestEnemyWinsWhenNoOwnerUnitExists()
        {
            Assert.That(ClaimRules.BetterCandidate(0, 1, 4f, 8, 1, 9f, 4), Is.True);
            Assert.That(ClaimRules.BetterCandidate(0, 1, 10f, 8, 1, 9f, 4), Is.False);
        }

        [Test]
        public void EqualDistanceUsesEntityIdForDeterministicSelection()
        {
            Assert.That(ClaimRules.BetterCandidate(0, 1, 9f, 7, 1, 9f, 11), Is.True);
            Assert.That(ClaimRules.BetterCandidate(0, 1, 9f, 13, 1, 9f, 11), Is.False);
        }

        [Test]
        public void EmptyOrInvalidCandidateCannotDisplaceAValidCandidate()
        {
            Assert.That(ClaimRules.BetterCandidate(0, 1, 1f, 0, 1, 2f, 5), Is.False);
            Assert.That(ClaimRules.BetterCandidate(0, 1, float.NaN, 9, 1, 2f, 5), Is.False);
            Assert.That(ClaimRules.BetterCandidate(0, 1, 2f, 5, 1, 2f, 0), Is.True);
        }
    }
}
