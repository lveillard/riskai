using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
namespace RiskAI
{
    public interface ICommander { void Tick(float delta); }
    public sealed class CommanderGroup : ICommander
    {
        readonly List<SkirmishCommander> commanders = new List<SkirmishCommander>();
        public IReadOnlyList<SkirmishCommander> Commanders => commanders;
        public CommanderGroup(BattleSession session,int playerCount)
        {
            for(int player=1;player<playerCount;player++)commanders.Add(new SkirmishCommander(session,player));
        }
        public void Tick(float delta){for(int i=0;i<commanders.Count;i++)commanders[i].Tick(delta);}
    }
    // Tactical policy remains deliberately modest; a visibility service can replace its world view.
    public sealed class SkirmishCommander : ICommander
    {
        const float DefenseDecisionSeconds = .75f;
        const float DefenseRadius = 12f;
        const float DefenseDispatchRadius = 32f;
        const float OpeningRecruitmentWindow = .15f;
        readonly BattleSession session;
        readonly int team;
        readonly Dictionary<int, int> defenseAssignments = new Dictionary<int, int>(32);
        readonly List<DefenseSite> threats = new List<DefenseSite>(32);
        readonly List<CombatTarget> nearby = new List<CombatTarget>(64);
        readonly List<Soldier> defenders = new List<Soldier>(8);
        readonly List<int> staleAssignments = new List<int>(16);
        readonly List<RecruitmentCandidate> recruitmentSites = new List<RecruitmentCandidate>(16);
        readonly List<Soldier> active = new List<Soldier>(32);
        readonly List<Soldier> available = new List<Soldier>(12);
        readonly HashSet<int> ownedCountries = new HashSet<int>();
        readonly float decisionPhase;
        float nextDecision;
        float nextDefenseDecision;
        bool openingDecision = true;
        int recruitsOrdered;
        static readonly UnitKind[] RecruitmentCycle = {
            UnitKind.Archer, UnitKind.Archer, UnitKind.Footman,
            UnitKind.Archer, UnitKind.Guard, UnitKind.Mortar, UnitKind.Mage
        };
        public int Team => team;

        public SkirmishCommander(BattleSession battle,int player=1)
        {
            session=battle;
            team=player;
            // Let every AI spend its opening gold immediately, then distribute its
            // recurring work through a deterministic seed/team phase.
            nextDecision=battle.AiFirstRecruitmentTime+SeededPhase(battle.Seed,team,OpeningRecruitmentWindow,0xA341316Cu);
            decisionPhase=SeededPhase(battle.Seed,team,battle.AiInterval,0xC8013EA4u);
            nextDefenseDecision=SeededPhase(battle.Seed,team,DefenseDecisionSeconds,0xAD90777Du);
        }

        static float SeededPhase(int seed,int team,float interval,uint salt)
        {
            unchecked
            {
                uint state=(uint)seed^salt^(uint)team*0x9E3779B9u;
                state^=state>>16;state*=0x7FEB352Du;state^=state>>15;state*=0x846CA68Bu;state^=state>>16;
                return (state&0x00FFFFFFu)*(interval/16777216f);
            }
        }

        static float AdvanceSchedule(float scheduled,float interval,float now)
        {
            do scheduled+=interval; while(scheduled<=now);
            return scheduled;
        }

        public void Tick(float delta)
        {
            if (!session.AiEnabled || session.Paused || session.Winner >= 0) return;
            if (session.BattleTime >= nextDefenseDecision)
            {
                nextDefenseDecision=AdvanceSchedule(nextDefenseDecision,DefenseDecisionSeconds,session.BattleTime);
                DecideDefense();
            }
            if (session.BattleTime < nextDecision) return;
            Decide();
            if (openingDecision)
            {
                openingDecision=false;
                nextDecision=session.AiFirstRecruitmentTime+session.AiInterval+decisionPhase;
                if(nextDecision<=session.BattleTime)nextDecision=session.BattleTime+session.AiInterval;
            }
            else nextDecision=AdvanceSchedule(nextDecision,session.AiInterval,session.BattleTime);
        }

        void DecideDefense()
        {
            threats.Clear();
            foreach (var town in session.Towns)
            {
                if (!town || town.State.Owner != team) continue;
                AddThreat(town.Defense.EntityId, town.ClaimPoint, town.State.Capture, town.State.Contested);
            }
            if (session.Naval)
                foreach (var harbor in session.Naval.Harbors)
                {
                    if (!harbor || harbor.Owner != team) continue;
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
                if (unit.Team == team) friendlies++;
                else if (PlayerRules.IsPlayer(unit.Team)) enemies++;
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

        bool IsMobileDefender(Soldier unit) => unit && unit.Team == team && unit.IsAlive && !unit.IsGarrison && unit.isActiveAndEnabled && unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh;

        struct DefenseSite
        {
            public int Key;
            public Vector3 Point;
            public int EnemyStrength;
            public int FriendlyStrength;
            public float Priority;
        }

        struct RecruitmentCandidate
        {
            public Settlement Town;
            public float FrontierDistance;
        }

        void Decide()
        {
            bool relaxed = session.Difficulty == BattleSession.AiDifficulty.Relaxed;
            int mobile = 0;
            foreach (var unit in session.Units) if (IsMobileDefender(unit)) mobile++;
            int navalBudget = threats.Count == 0 && mobile >= 2 && session.Naval
                ? session.Naval.FirstFleetSavingsTargetFor(team) : 0;
            int purchases = relaxed ? 1 : 2;
            if(session.RecruitmentPopulation(team)<45)RefreshRecruitmentSites();
            for (int i = 0; i < purchases && session.RecruitmentPopulation(team) < 45; i++)
            {
                var town = RecruitmentSite();
                if (!town) break;
                var kind = RecruitmentCycle[recruitsOrdered % RecruitmentCycle.Length];
                // Four starting gold must produce a mobile opening, even when
                // the next preferred specialist is temporarily unaffordable.
                if (BattleRules.Cost(kind) > session.Economy.Gold[team]) kind = UnitKind.Archer;
                if (session.Economy.Gold[team] - BattleRules.Cost(kind) < navalBudget) break;
                if (town.Recruit(kind, team) != null) break;
                recruitsOrdered++;
            }
            if (session.BattleTime < session.AiFirstOffensiveTime) return;
            IssueOffensiveOrders(relaxed);
        }

        void RefreshRecruitmentSites()
        {
            recruitmentSites.Clear();
            foreach(var town in session.Towns)
            {
                if(!town || town.IsPort || town.State.Owner!=team)continue;
                float frontierDistance=10000;
                foreach(var other in session.Towns)
                    if(other && other.State.Owner!=team)
                        frontierDistance=Mathf.Min(frontierDistance,Vector3.Distance(town.ClaimPoint,other.ClaimPoint));
                recruitmentSites.Add(new RecruitmentCandidate { Town=town, FrontierDistance=frontierDistance });
            }
        }

        Settlement RecruitmentSite()
        {
            Settlement best = null;
            float bestScore = float.PositiveInfinity;
            for(int i=0;i<recruitmentSites.Count;i++)
            {
                var candidate=recruitmentSites[i];
                var town=candidate.Town;
                if (!town || town.QueueCount >= 2) continue;
                // Replenish threatened posts first, then the closest frontier.
                // Stable town order breaks ties without consuming combat RNG.
                float score = candidate.FrontierDistance + town.QueueCount * 20 - (HasThreat(town.Defense.EntityId) ? 1000 : 0);
                if (score < bestScore) { best = town; bestScore = score; }
            }
            return best;
        }

        void IssueOffensiveOrders(bool relaxed)
        {
            active.Clear();
            foreach(var unit in session.Units)
                if(IsMobileDefender(unit) && (unit.IsIdle || unit.IsHolding) && !defenseAssignments.ContainsKey(unit.EntityId))active.Add(unit);
            if(active.Count<(relaxed?2:3))return;

            int reserve=active.Count>=4?1:0;
            int count=Mathf.Min(active.Count-reserve,relaxed?8:12);
            available.Clear();
            Vector3 center=Vector3.zero;
            for(int i=0;i<count;i++){var unit=active[i];available.Add(unit);center+=unit.transform.position;}
            center/=available.Count;

            bool neutralsRemain=false;
            ownedCountries.Clear();
            foreach(var town in session.Towns)
            {
                if(town.State.Owner<0)neutralsRemain=true;
                if(town.State.Owner==team && town.State.Country>=0)ownedCountries.Add(town.State.Country);
            }

            Settlement target=null;
            bool targetJoinsCountry=false;
            float targetDistance=float.PositiveInfinity;
            foreach(var town in session.Towns)
            {
                if(town.State.Owner==team || (session.BattleTime<=100 && town.State.Owner>=0 && neutralsRemain))continue;
                bool joinsCountry=town.State.Country>=0&&ownedCountries.Contains(town.State.Country);
                float distance=Vector3.SqrMagnitude(town.transform.position-center);
                if(!target || (joinsCountry&&!targetJoinsCountry) || joinsCountry==targetJoinsCountry&&distance<targetDistance)
                {
                    target=town;targetJoinsCountry=joinsCountry;targetDistance=distance;
                }
            }
            if(target)BattleSession.GiveFormation(available,target.ClaimPoint,true,false);
        }
    }
}