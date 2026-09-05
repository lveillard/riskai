// Adapted from references/wc3-risk-system/src/app/spawner/spawner-logic.ts
// and src/app/managers/victory-logic.ts (MIT; Copyright (c) 2019 trigger).
// See THIRD_PARTY_NOTICES.md at the repository root.
using System;

namespace RiskAI.Core
{
    /// <summary>Small engine-independent rules adapted from the local WC3 Risk reference.</summary>
    public static class RiskReferenceRules
    {
        /// <summary>Returns the number that fits in one spawn step and the remaining cap.</summary>
        public static int ComputeSpawnAmount(int currentCount, int maxPerPlayer, int perStep)
        {
            if (currentCount < 0) throw new ArgumentOutOfRangeException(nameof(currentCount));
            if (maxPerPlayer < 0) throw new ArgumentOutOfRangeException(nameof(maxPerPlayer));
            if (perStep < 0) throw new ArgumentOutOfRangeException(nameof(perStep));
            if (currentCount >= maxPerPlayer) return 0;
            return Math.Min(perStep, maxPerPlayer - currentCount);
        }

        /// <summary>Calculates the reference city threshold, including the overtime floor of one.</summary>
        public static int CalculateCityCountWin(int totalCities, double percentage, int numberOvertimeTurns = 0, int cityReductionPerTurn = 1)
        {
            if (totalCities < 0) throw new ArgumentOutOfRangeException(nameof(totalCities));
            if (percentage < 0 || percentage > 1 || double.IsNaN(percentage) || double.IsInfinity(percentage))
                throw new ArgumentOutOfRangeException(nameof(percentage));
            if (numberOvertimeTurns < 0) throw new ArgumentOutOfRangeException(nameof(numberOvertimeTurns));
            if (cityReductionPerTurn < 0) throw new ArgumentOutOfRangeException(nameof(cityReductionPerTurn));

            var threshold = (int)Math.Ceiling(totalCities * percentage);
            if (numberOvertimeTurns > 0)
                threshold -= cityReductionPerTurn * numberOvertimeTurns;
            return Math.Max(1, threshold);
        }
    }
}
