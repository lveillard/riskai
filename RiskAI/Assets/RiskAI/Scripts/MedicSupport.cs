using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Automatic Ahea autocast for Medic soldiers. Each heal spends source mana
    /// (hmpr 200 max / 75 initial, h00E regeneration 1.5 per second).
    /// </summary>
    public sealed class MedicSupport : MonoBehaviour, IManaUser
    {
        // Ahea: 250 native range, 25 points, 5 mana and a one-second cooldown.
        public const float HealRadius = Core.SupportAbilities.HealRange;
        public const float MaxVerticalDelta = 3f;
        public const float HealAmount = Core.SupportAbilities.HealAmount;
        public const float CastInterval = Core.SupportAbilities.HealCooldown;
        public const float ManaCost = Core.SupportAbilities.HealManaCost;

        readonly Core.ManaPool mana = new Core.ManaPool();
        public Core.ManaPool Mana => mana;
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
            mana.Reset(Core.SupportAbilities.Mana(owner.Kind));
        }

        public bool SimTick(float delta)
        {
            if (!self || self.Kind != Core.UnitKind.Medic || !self.IsAlive || !self.isActiveAndEnabled ||
                !session || session.Paused || session.Winner >= 0)
                return false;
            mana.Tick(delta);
            if (session.BattleTime < nextCastTime || !mana.CanSpend(ManaCost)) return false;

            nextCastTime = session.BattleTime + CastInterval;
            var target = FindMostInjuredAlly();
            if (!target) return false;

            float healed = target.Heal(HealAmount);
            if (healed <= 0) return false;
            mana.TrySpend(ManaCost);

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
                // Ahea targets organic units only; h00M/h01A are mechanical.
                if (Core.BattleRules.Mechanical(candidate.Kind)) continue;
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
