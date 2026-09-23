using UnityEngine;
using RiskAI.Core;

namespace RiskAI
{
    /// <summary>
    /// Automatic Ahea autocast for Medic soldiers. Each heal spends source mana
    /// (hmpr 200 max / 75 initial, h00E regeneration 1.5 per second).
    /// </summary>
    public sealed class MedicSupport : MonoBehaviour, IManaUser
    {
        // Range, amount, mana, cooldown and rescan throttle come from the owner's units.json heal.
        HealProfile heal;

        readonly Core.ManaPool mana = new Core.ManaPool();
        public Core.ManaPool Mana => mana;
        public int CastCount { get; private set; }
        public float TotalHealing { get; private set; }
        public long LastCastTick { get; private set; } = -1;

        Soldier self;
        BattleSession session;
        float nextCastTime;
        float nextScanTime;
        readonly System.Collections.Generic.List<CombatTarget> nearby = new System.Collections.Generic.List<CombatTarget>(32);

        /// <summary>Initializes the support behavior after the owning Soldier is ready.</summary>
        public void Initialize(Soldier owner, BattleSession battle)
        {
            self = owner;
            session = battle;
            heal = owner.Type.Heal;
            nextCastTime = session.BattleTime + heal.Cooldown;
            nextScanTime = session.BattleTime;
            CastCount=0;TotalHealing=0;LastCastTick=-1;
            mana.Reset(owner.Type.Mana);
        }

        public bool SimTick(float delta)
        {
            if (!self || !heal.Enabled || !self.IsAlive || !self.isActiveAndEnabled ||
                !session || session.Paused || session.Winner >= 0)
                return false;
            mana.Tick(delta);
            if (session.BattleTime < nextCastTime || session.BattleTime < nextScanTime || !mana.CanSpend(heal.ManaCost)) return false;

            var target = FindMostInjuredAlly();
            if (!target) { nextScanTime = session.BattleTime + heal.Rescan; return false; }

            float healed = target.Heal(heal.Amount);
            if (healed <= 0) { nextScanTime = session.BattleTime + heal.Rescan; return false; }
            nextCastTime = session.BattleTime + heal.Cooldown;
            mana.TrySpend(heal.ManaCost);

            CastCount++;
            TotalHealing += healed;
            LastCastTick=session.Clock.TickCount;
            if(session.Combat.PresentationEnabled)VisualFactory.Impact(target.AimPoint, new Color(.72f, .96f, .36f), .25f);
            return true;
        }

        Soldier FindMostInjuredAlly()
        {
            Soldier best = null;
            float greatestDeficit = 0;
            Vector3 origin = self.transform.position;

            session.Spatial.Query(origin,heal.Range,nearby);
            foreach (var entity in nearby)
            {
                var candidate=entity as Soldier;
                if (!candidate || candidate.Team != self.Team || !candidate.IsAlive || !candidate.isActiveAndEnabled)
                    continue;
                // Ahea targets organic units only; h00M/h01A are mechanical.
                if (heal.OrganicOnly && candidate.Type.Mechanical) continue;
                if (!candidate.Agent || !candidate.Agent.enabled || !candidate.Agent.isOnNavMesh)
                    continue;

                float deficit = candidate.MaxHealth - candidate.Health;
                if (deficit <= 0) continue;

                Vector3 delta = candidate.transform.position - origin;
                if (Mathf.Abs(delta.y) >= heal.MaxVerticalDelta ||
                    new Vector2(delta.x, delta.z).sqrMagnitude > heal.Range * heal.Range)
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
