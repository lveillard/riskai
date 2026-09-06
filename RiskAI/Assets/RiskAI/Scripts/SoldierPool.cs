using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
namespace RiskAI
{
    /// <summary>Scene-owned team/kind pools. Every rent receives a fresh simulation identity.</summary>
    public sealed class SoldierPool
    {
        struct Retiring { public Soldier Unit; public float At; }
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
        static int Key(int team, UnitKind kind) => team * 32 + (int)kind;
        public Soldier Rent(int team, UnitKind kind, Vector3 point)
        {
            Soldier unit = null;
            if (idle.TryGetValue(Key(team, kind), out var stack))
                while (stack.Count > 0 && !unit) unit = stack.Pop();
            if (!unit)
            {
                var go = new GameObject(BattleRules.Name(kind)); go.transform.SetParent(root, false);
                go.transform.position = point; unit = go.AddComponent<Soldier>(); CreatedCount++;
            }
            else ReusedCount++;
            unit.transform.SetParent(root, true); unit.transform.position = point; unit.gameObject.SetActive(true);
            unit.Initialize(session, team, kind); return unit;
        }
        public void Retire(Soldier unit, float delay) => retiring.Add(new Retiring { Unit = unit, At = session.BattleTime + delay });
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
