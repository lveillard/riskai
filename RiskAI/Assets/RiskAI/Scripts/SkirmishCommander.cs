using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
namespace RiskAI
{
    public interface ICommander { void Tick(float delta); }
    // Tactical policy remains deliberately modest; a visibility service can replace its world view.
    public sealed class SkirmishCommander : ICommander
    {
        const float DefenseDecisionSeconds = .75f;
        const float DefenseRadius = 12f;
        const float DefenseDispatchRadius = 32f;
        readonly BattleSession session;
        readonly Dictionary<int, int> defenseAssignments = new Dictionary<int, int>(32);
        readonly List<DefenseSite> threats = new List<DefenseSite>(32);
        readonly List<CombatTarget> nearby = new List<CombatTarget>(64);
        readonly List<Soldier> defenders = new List<Soldier>(8);
        readonly List<int> staleAssignments = new List<int>(16);
        float nextDecision;
        float nextDefenseDecision;
        public SkirmishCommander(BattleSession battle) { session=battle; nextDecision=battle.AiFirstRecruitmentTime; nextDefenseDecision=0; }
        public void Tick(float delta)
        {
            if (!session.AiEnabled || session.Paused || session.Winner >= 0) return;
            if (session.BattleTime >= nextDefenseDecision)
            {
                nextDefenseDecision = session.BattleTime + DefenseDecisionSeconds;
                DecideDefense();
            }
            if (session.BattleTime < nextDecision) return;
            nextDecision = session.BattleTime + session.AiInterval;
            Decide();
        }
        void DecideDefense()
        {
            threats.Clear();
            foreach (var town in session.Towns)
            {
                if (!town || town.State.Owner != 1) continue;
                AddThreat(town.Defense.EntityId, town.ClaimPoint, town.State.Capture, town.State.Contested);
            }
            if (session.Naval)
                foreach (var harbor in session.Naval.Harbors)
                {
                    if (!harbor || harbor.Owner != 1) continue;
                    AddThreat(harbor.Defense.EntityId, harbor.Landing, harbor.State.Capture, harbor.State.Contested);
                }
            if (threats.Count == 0) { defenseAssignments.Clear(); return; }
            threats.Sort((a, b) => a.Priority == b.Priority ? a.Key.CompareTo(b.Key) : b.Priority.CompareTo(a.Priority));
            RemoveStaleAssignments();

            int mobile = 0;
            foreach (var unit in session.Units) if (IsMobileDefender(unit)) mobile++;
            int reserve = mobile>=4 ? Mathf.Max(1,Mathf.CeilToInt(mobile*.25f)) : 0;
            int assigned = CountAssignments();
            for (int i = 0; i < threats.Count; i++)
            {
                var threat = threats[i];
                int wanted = Mathf.Clamp(threat.EnemyStrength + 1, 1, 4);
                int already = CountAssignments(threat.Key);
                defenders.Clear();
                while (already < wanted && assigned < mobile - reserve)
                {
                    var unit = NearestUnassigned(threat.Point);
                    if (!unit) break;
                    defenseAssignments[unit.EntityId] = threat.Key;
                    defenders.Add(unit);
                    already++; assigned++;
                }
                if (defenders.Count > 0) BattleSession.GiveFormation(defenders, threat.Point, true, false);
            }
        }
        void AddThreat(int key, Vector3 point, float capture, bool contested)
        {
            session.Spatial.Query(point, DefenseRadius, nearby);
            int enemies = 0, friendlies = 0;
            for (int i = 0; i < nearby.Count; i++)
            {
                var unit = nearby[i] as Soldier;
                if (!unit || !unit.IsAlive || (unit.transform.position-point).sqrMagnitude>DefenseRadius*DefenseRadius) continue;
                if (unit.Team == 0) enemies++;
                else if (unit.Team == 1) friendlies++;
            }
            if (enemies == 0 && !contested && capture <= 0) return;
            if (!contested && capture <= 0 && enemies < Mathf.Max(1, friendlies)) return;
            float priority = enemies - friendlies + (contested ? 5f : 0) + capture * 4f;
            threats.Add(new DefenseSite { Key=key, Point=point, EnemyStrength=enemies, FriendlyStrength=friendlies, Priority=priority });
        }
        void RemoveStaleAssignments()
        {
            staleAssignments.Clear();
            foreach (var assignment in defenseAssignments)
            {
                var unit = session.FindTarget(assignment.Key) as Soldier;
                if (!IsMobileDefender(unit) || !HasThreat(assignment.Value)) staleAssignments.Add(assignment.Key);
            }
            for (int i = 0; i < staleAssignments.Count; i++) defenseAssignments.Remove(staleAssignments[i]);
        }
        bool HasThreat(int key)
        {
            for (int i = 0; i < threats.Count; i++) if (threats[i].Key == key) return true;
            return false;
        }
        int CountAssignments()
        {
            int count=0;
            foreach (var assignment in defenseAssignments) if (HasThreat(assignment.Value)) count++;
            return count;
        }
        int CountAssignments(int key)
        {
            int count=0;
            foreach (var assignment in defenseAssignments) if (assignment.Value == key) count++;
            return count;
        }
        Soldier NearestUnassigned(Vector3 point)
        {
            Soldier best=null;float distance=float.MaxValue;
            foreach (var unit in session.Units)
            {
                if (!IsMobileDefender(unit) || defenseAssignments.ContainsKey(unit.EntityId)) continue;
                float next=Vector3.SqrMagnitude(unit.transform.position-point);
                if (next > DefenseDispatchRadius * DefenseDispatchRadius) continue;
                if (next<distance || next==distance && (!best || unit.EntityId<best.EntityId)) { best=unit;distance=next; }
            }
            return best;
        }
        static bool IsMobileDefender(Soldier unit) => unit && unit.Team == 1 && unit.IsAlive && !unit.IsGarrison && unit.isActiveAndEnabled && unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh;
        struct DefenseSite
        {
            public int Key;
            public Vector3 Point;
            public int EnemyStrength;
            public int FriendlyStrength;
            public float Priority;
        }
        void Decide()
        {
            bool relaxed = session.Difficulty == BattleSession.AiDifficulty.Relaxed;
            bool recruited = false;
            foreach (var town in session.Towns.Where(t => t.State.Owner == 1))
            {
                if (relaxed && recruited) continue;
                if (town.QueueCount < 2 && session.Population(1) < 45)
                {
                    var kind = (UnitKind)(Mathf.FloorToInt(session.BattleTime/session.AiInterval)%6);
                    if (town.Recruit(kind,1) == null) recruited = true;
                }
            }
            if (session.BattleTime < session.AiFirstOffensiveTime) return;
            var active = session.Units.Where(u => u && u.Team == 1 && u.IsAlive && u.isActiveAndEnabled && u.Agent && u.Agent.enabled && u.IsIdle && !defenseAssignments.ContainsKey(u.EntityId)).ToList();
            if (active.Count < 5) return;
            var available = active;
            if (relaxed)
            {
                var infantry = active.Where(IsInfantry).Take(6).ToList();
                if (infantry.Count < 6) return;
                available = active.Where(u => !infantry.Contains(u)).Take(10).ToList();
                if (available.Count == 0) return;
            }
            Vector3 center = available.Aggregate(Vector3.zero, (sum, u) => sum + u.transform.position) / available.Count;
            bool neutralsRemain=session.Towns.Any(t=>t.State.Owner<0);
            var target = session.Towns.Where(t => t.State.Owner != 1 && (session.BattleTime > 100 || t.State.Owner < 0 || !neutralsRemain))
                .OrderByDescending(t => t.State.Country >= 0 && session.Towns.Any(x => x.State.Country == t.State.Country && x.State.Owner == 1))
                .ThenBy(t => Vector3.SqrMagnitude(t.transform.position - center)).FirstOrDefault();
            if (target) BattleSession.GiveFormation(available, target.ClaimPoint, true, false);
        }
        static bool IsInfantry(Soldier unit) => unit.Kind == UnitKind.Footman || unit.Kind == UnitKind.Guard;
    }
}
