using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Reusable broad phase; precise distances and visibility remain caller policies.</summary>
    public sealed class SpatialTargetIndex
    {
        public const float CellSize = 8;
        readonly Dictionary<long, List<CombatTarget>> cells = new Dictionary<long, List<CombatTarget>>(256);
        readonly Dictionary<long, int> pressure = new Dictionary<long, int>(256);
        public int LastCandidateCount { get; private set; }
        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
        static long PressureKey(int team, int entityId) => Key(team, entityId);

        public void Rebuild(IReadOnlyList<CombatTarget> targets, IReadOnlyList<Soldier> units)
        {
            foreach (var cell in cells.Values) cell.Clear();
            pressure.Clear();
            for (int i = 0; i < targets.Count; i++) Add(targets[i]);
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit || !unit.IsAlive || !unit.CurrentTarget || !unit.CurrentTarget.IsAlive) continue;
                long key = PressureKey(unit.Team, unit.CurrentTarget.EntityId);
                pressure.TryGetValue(key, out int count);
                pressure[key] = count + 1;
            }
        }

        public void Add(CombatTarget target)
        {
            if (!target || !target.IsAlive) return;
            var p = target.transform.position;
            long key = Key(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
            if (!cells.TryGetValue(key, out var cell)) cells.Add(key, cell = new List<CombatTarget>(16));
            cell.Add(target);
        }

        public int Pressure(int team, CombatTarget target)
        {
            return target && pressure.TryGetValue(PressureKey(team, target.EntityId), out int count) ? count : 0;
        }

        public void Query(Vector3 center, float radius, List<CombatTarget> results)
        {
            results.Clear();
            int minX = Mathf.FloorToInt((center.x - radius) / CellSize);
            int maxX = Mathf.FloorToInt((center.x + radius) / CellSize);
            int minZ = Mathf.FloorToInt((center.z - radius) / CellSize);
            int maxZ = Mathf.FloorToInt((center.z + radius) / CellSize);
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                    if (cells.TryGetValue(Key(x, z), out var cell))
                        for (int i = 0; i < cell.Count; i++)
                            if (cell[i] && cell[i].IsAlive) results.Add(cell[i]);
            LastCandidateCount = results.Count;
        }
    }
}
