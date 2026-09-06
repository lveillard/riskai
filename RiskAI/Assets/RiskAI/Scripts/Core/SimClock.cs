using System;

namespace RiskAI.Core
{
    /// <summary>A fixed simulation clock. Presentation time and pause belong to the caller.</summary>
    public sealed class SimClock
    {
        public const double StepSeconds = 1.0 / 20.0;
        public const int MaxStepsPerFrame = 8;
        double accumulator;
        public long TickCount { get; private set; }
        public double Elapsed => TickCount * StepSeconds;
        public float Alpha => (float)Math.Min(1, accumulator / StepSeconds);

        public int Advance(double frameSeconds, bool paused, Action<float> step)
        {
            if (double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds) || frameSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(frameSeconds));
            if (paused) return 0;
            accumulator += frameSeconds;
            int count = 0;
            // Keep outstanding time after a hitch; never silently skip combat or economy ticks.
            while (accumulator + 1e-9 >= StepSeconds && count < MaxStepsPerFrame)
            {
                accumulator -= StepSeconds;
                TickCount++;
                count++;
                step((float)StepSeconds);
            }
            return count;
        }
    }
}
