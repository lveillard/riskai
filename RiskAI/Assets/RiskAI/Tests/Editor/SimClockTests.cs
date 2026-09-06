using System;
using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class SimClockTests
    {
        [Test]
        public void PartitioningFrameTimeProducesTheSameTickCount()
        {
            var whole = new SimClock();
            var partitioned = new SimClock();

            Assert.That(whole.Advance(.2, false, _ => { }), Is.EqualTo(4));
            Assert.That(partitioned.Advance(.1, false, _ => { }), Is.EqualTo(2));
            Assert.That(partitioned.Advance(.1, false, _ => { }), Is.EqualTo(2));
            Assert.That(partitioned.TickCount, Is.EqualTo(whole.TickCount));
            Assert.That(partitioned.Elapsed, Is.EqualTo(whole.Elapsed).Within(1e-12));
        }

        [Test]
        public void PausedTimeDoesNotAccumulate()
        {
            var clock = new SimClock();

            Assert.That(clock.Advance(SimClock.StepSeconds * 4, true, _ =>
            {
                Assert.Fail("A paused clock must not invoke its step callback.");
            }), Is.Zero);
            Assert.That(clock.TickCount, Is.Zero);
            Assert.That(clock.Advance(SimClock.StepSeconds * .99, false, _ => { }), Is.Zero);
            Assert.That(clock.Advance(SimClock.StepSeconds * .01, false, _ => { }), Is.EqualTo(1));
        }

        [Test]
        public void HitchDebtIsRetainedAcrossThePerFrameStepCap()
        {
            var clock = new SimClock();

            Assert.That(clock.Advance(SimClock.StepSeconds * 20, false, _ => { }),
                Is.EqualTo(SimClock.MaxStepsPerFrame));
            Assert.That(clock.Advance(0, false, _ => { }),
                Is.EqualTo(SimClock.MaxStepsPerFrame));
            Assert.That(clock.Advance(0, false, _ => { }), Is.EqualTo(4));
            Assert.That(clock.TickCount, Is.EqualTo(20));
        }

        [Test]
        public void InvalidFrameTimeIsRejected()
        {
            var clock = new SimClock();
            Action<double> advance = value => clock.Advance(value, false, _ => { });

            Assert.Throws<ArgumentOutOfRangeException>(() => advance(-.01));
            Assert.Throws<ArgumentOutOfRangeException>(() => advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => advance(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => advance(double.NegativeInfinity));
            Assert.That(clock.TickCount, Is.Zero);
        }
    }
}
