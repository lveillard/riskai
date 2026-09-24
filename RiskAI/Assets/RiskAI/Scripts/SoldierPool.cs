using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
namespace RiskAI
{
    /// <summary>Scene-owned team/kind pools. Every rent receives a fresh simulation identity.</summary>
    public sealed class SoldierPool
    {
        struct Retiring { public Soldier Unit; public float At, DiedAt; }
        /// <summary>Visible corpse time with presentation: Death clip, hold, then a one second sink.</summary>
        public const float CorpseSeconds = 3f;
        /// <summary>A corpse at least this old may be recycled early when no pooled actor is idle.</summary>
        public const float MinimumCorpseSeconds = 1.2f;
        /// <summary>Pool return delay for a death. Headless fixtures use a short delay.</summary>
        public static float CorpseDelay(BattleSession battle, bool animated) =>
            battle && battle.Feedback != null && battle.Feedback.Enabled ? CorpseSeconds : animated ? 1.4f : 0;
        readonly BattleSession session;
        readonly Transform root;
        readonly Dictionary<int, Stack<Soldier>> idle = new Dictionary<int, Stack<Soldier>>();
        readonly List<Retiring> retiring = new List<Retiring>(128);
        public int CreatedCount { get; private set; }
        public int ReusedCount { get; private set; }
        public SoldierPool(BattleSession battle)
        {
            session = battle; root = new GameObject("Soldier pool").transform;
            root.SetParent(battle.transform, false);
        }
        // Keyed by the catalog's dense type index, never by the enum ordinal.
        static int Key(int team, UnitKind kind) => team * UnitCatalog.Count + UnitCatalog.Get(kind).Index;
        public Soldier Rent(int team, UnitKind kind, Vector3 point)
        {
            Soldier unit = null;
            if (idle.TryGetValue(Key(team, kind), out var stack))
                while (stack.Count > 0 && !unit) unit = stack.Pop();
            if (!unit) unit = TakeOldCorpse(team, kind);
            if (!unit)
            {
                var go = new GameObject(UnitCatalog.Get(kind).Name); go.transform.SetParent(root, false);
                go.transform.position = point; unit = go.AddComponent<Soldier>(); CreatedCount++;
            }
            else ReusedCount++;
            unit.transform.SetParent(root, true); unit.transform.position = point; unit.transform.localScale = Vector3.one; unit.gameObject.SetActive(true);
            unit.Initialize(session, team, kind); return unit;
        }
        public void Retire(Soldier unit, float delay) => retiring.Add(new Retiring { Unit = unit, At = session.BattleTime + delay, DiedAt = session.BattleTime });
        // Corpses are cosmetic: reuse the oldest one of this team/kind rather than
        // growing the pool while bodies are still sinking.
        Soldier TakeOldCorpse(int team, UnitKind kind)
        {
            int best = -1;
            for (int i = 0; i < retiring.Count; i++)
            {
                var item = retiring[i];
                if (!item.Unit || item.Unit.Team != team || item.Unit.Kind != kind || item.At - item.DiedAt < MinimumCorpseSeconds) continue;
                if (session.BattleTime - item.DiedAt < MinimumCorpseSeconds) continue;
                if (best < 0 || item.DiedAt < retiring[best].DiedAt) best = i;
            }
            if (best < 0) return null;
            var unit = retiring[best].Unit; retiring.RemoveAt(best);
            return unit;
        }
        public void Tick()
        {
            for (int i = retiring.Count - 1; i >= 0; i--)
            {
                var item = retiring[i]; if (session.BattleTime < item.At) continue;
                retiring.RemoveAt(i); if (!item.Unit) continue;
                item.Unit.gameObject.SetActive(false); item.Unit.transform.SetParent(root, true);
                int key = Key(item.Unit.Team, item.Unit.Kind);
                if (!idle.TryGetValue(key, out var stack)) idle.Add(key, stack = new Stack<Soldier>());
                stack.Push(item.Unit);
            }
        }
    }
}
