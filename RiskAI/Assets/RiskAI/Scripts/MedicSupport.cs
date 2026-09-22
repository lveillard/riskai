using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Automatic support behavior for Medic soldiers. Healing is deliberately a local
    /// gameplay rule: there is no mana pool or resource cost in this version.
    /// </summary>
    public sealed class MedicSupport : MonoBehaviour
    {
        // Ahea: 250 native range, 25 points and a one-second cooldown.
        public const float HealRadius = 5f;
        public const float MaxVerticalDelta = 3f;
        public const float HealAmount = 25f;
        public const float CastInterval = 1f;

        public int CastCount { get; private set; }
        public float TotalHealing { get; private set; }
        public long LastCastTick { get; private set; } = -1;

        Soldier self;
        BattleSession session;
        float nextCastTime;
        readonly System.Collections.Generic.List<CombatTarget> nearby = new System.Collections.Generic.List<CombatTarget>(32);

        /// <summary>Initializes the support behavior after the owning Soldier is ready.</summary>
        public void Initialize(Soldier owner, BattleSession battle)
        {
            self = owner;
            session = battle;
            nextCastTime = session.BattleTime + CastInterval;
            CastCount=0;TotalHealing=0;LastCastTick=-1;
        }

        public bool SimTick(float delta)
        {
            if (!self || self.Kind != Core.UnitKind.Medic || !self.IsAlive || !self.isActiveAndEnabled ||
                !session || session.Paused || session.Winner >= 0 || session.BattleTime < nextCastTime)
                return false;

            nextCastTime = session.BattleTime + CastInterval;
            var target = FindMostInjuredAlly();
            if (!target) return false;

            float healed = target.Heal(HealAmount);
            if (healed <= 0) return false;

            CastCount++;
            TotalHealing += healed;
            LastCastTick=session.Clock.TickCount;
            VisualFactory.Impact(target.AimPoint, new Color(.72f, .96f, .36f), .25f);
            return true;
        }

        Soldier FindMostInjuredAlly()
        {
            Soldier best = null;
            float greatestDeficit = 0;
            Vector3 origin = self.transform.position;

            session.Spatial.Query(origin,HealRadius,nearby);
            foreach (var entity in nearby)
            {
                var candidate=entity as Soldier;
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
