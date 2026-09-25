using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// The one AI power metric for every actor, land or sea. The Lanchester value comes from
    /// AiUnitAnalysis and the health weighting is applied exactly here, once.
    /// </summary>
    public static class AiPower
    {
        public static float Power(CombatTarget actor)
        {
            if (!actor || !actor.IsAlive) return 0;
            return AiUnitAnalysis.Value(actor.Type) * Mathf.Clamp01(actor.Health / Mathf.Max(1f, actor.MaxHealth));
        }
    }
}
