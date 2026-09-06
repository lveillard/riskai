using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class ClaimTransitionTests
    {
        [Test]
        public void ABlockedCandidateLosesAllProgress()
        {
            var transition = new ClaimTransition();

            Assert.That(transition.Advance(7, 1, false, .9f), Is.False);
            Assert.That(transition.Elapsed, Is.EqualTo(.9f).Within(.0001f));
            Assert.That(transition.Advance(7, 1, true, .1f), Is.False);
            Assert.That(transition.Elapsed, Is.Zero);
            Assert.That(transition.CandidateId, Is.Zero);
            Assert.That(transition.CandidateTeam, Is.EqualTo(-1));
            Assert.That(transition.Advance(7, 1, false, ClaimRules.ConversionSeconds), Is.True);
        }

        [Test]
        public void ReplacingTheCandidateRestartsTheTimer()
        {
            var transition = new ClaimTransition();

            Assert.That(transition.Advance(7, 1, false, .9f), Is.False);
            Assert.That(transition.Advance(9, 1, false, .4f), Is.False);
            Assert.That(transition.CandidateId, Is.EqualTo(9));
            Assert.That(transition.Elapsed, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(transition.Advance(9, 1, false, .85f), Is.True);
        }

        [Test]
        public void TheSameEligibleCandidateConvertsAtTheConfiguredDuration()
        {
            var transition = new ClaimTransition();

            Assert.That(transition.Advance(4, 0, false, ClaimRules.ConversionSeconds - .01f), Is.False);
            Assert.That(transition.Progress, Is.LessThan(1));
            Assert.That(transition.Advance(4, 0, false, .01f), Is.True);
            Assert.That(transition.Progress, Is.EqualTo(1).Within(.0001f));
        }
    }
}
