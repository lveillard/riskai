using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Automatic support behavior for Medic soldiers. Healing is deliberately a local
    /// gameplay rule: there is no mana pool or resource cost in this version.
    /// </summary>
    public sealed class MedicSupport : MonoBehaviour
    {
        public const float HealRadius = 8f;
        public const float MaxVerticalDelta = 3f;
        public const float HealAmount = 15f;
        public const float CastInterval = 1f;

        public int CastCount { get; private set; }
        public float TotalHealing { get; private set; }

        Soldier self;
        BattleSession session;
        float nextCastTime;

        /// <summary>Initializes the support behavior after the owning Soldier is ready.</summary>
        public void Initialize(Soldier owner, BattleSession battle)
        {
            self = owner;
            session = battle;
            nextCastTime = Time.time + CastInterval;
        }

        void Update()
        {
            if (!self || self.Kind != Core.UnitKind.Medic || !self.IsAlive || !self.isActiveAndEnabled ||
                !session || session.Paused || session.Winner >= 0 || Time.time < nextCastTime)
                return;

            nextCastTime = Time.time + CastInterval;
            var target = FindMostInjuredAlly();
            if (!target) return;

            float healed = target.Heal(HealAmount);
            if (healed <= 0) return;

            CastCount++;
            TotalHealing += healed;
            VisualFactory.Impact(target.AimPoint, new Color(.72f, .96f, .36f), .25f);
        }

        Soldier FindMostInjuredAlly()
        {
            Soldier best = null;
            float greatestDeficit = 0;
            Vector3 origin = self.transform.position;

            foreach (var candidate in session.Units)
            {
                if (!candidate || candidate.Team != self.Team || !candidate.IsAlive || !candidate.isActiveAndEnabled)
                    continue;
                if (!candidate.Agent || !candidate.Agent.enabled || !candidate.Agent.isOnNavMesh)
                    continue;

                float deficit = candidate.MaxHealth - candidate.Health;
                if (deficit <= 0) continue;

                Vector3 delta = candidate.transform.position - origin;
                if (Mathf.Abs(delta.y) >= MaxVerticalDelta ||
                    new Vector2(delta.x, delta.z).sqrMagnitude > HealRadius * HealRadius)
                    continue;
                if (!HasLineOfSight(candidate)) continue;

                if (deficit > greatestDeficit)
                {
                    best = candidate;
                    greatestDeficit = deficit;
                }
            }

            return best;
        }

        bool HasLineOfSight(Soldier target)
        {
            Vector3 from = self.AimPoint;
            Vector3 to = target.AimPoint;
            Vector3 delta = to - from;
            return delta.sqrMagnitude < .001f ||
                   !Physics.Raycast(from, delta.normalized, delta.magnitude,
                       1 << MapLayout.TerrainLayer, QueryTriggerInteraction.Ignore);
        }
    }
}
