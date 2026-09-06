using System.Linq;
using RiskAI.Core;
using UnityEngine;
namespace RiskAI
{
    public interface ICommander { void Tick(float delta); }
    // Tactical policy remains deliberately modest; a visibility service can replace its world view.
    public sealed class SkirmishCommander : ICommander
    {
        readonly BattleSession session;
        float nextDecision;
        public SkirmishCommander(BattleSession battle) { session=battle; nextDecision=battle.AiFirstRecruitmentTime; }
        public void Tick(float delta)
        {
            if (!session.AiEnabled || session.Paused || session.Winner >= 0 || session.BattleTime < nextDecision) return;
            nextDecision = session.BattleTime + session.AiInterval;
            Decide();
        }
        void Decide()
        {
            bool relaxed = session.Difficulty == BattleSession.AiDifficulty.Relaxed;
            bool recruited = false;
            foreach (var town in session.Towns.Where(t => t.State.Owner == 1))
            {
                float upgradeTime = relaxed ? 120f : 70f;
                float towerTime = relaxed ? 150f : 90f;
                if(!town.Building && session.BattleTime>=upgradeTime && session.Economy.Gold[1]>=125 && town.IsCapital && town.State.Level==1)town.Upgrade(1);
                else if(!town.Building && session.BattleTime>=towerTime && session.Economy.Gold[1]>=100 && !town.Defense.IsAlive)town.BuildTower(1);
                if (relaxed && recruited) continue;
                if (town.QueueCount < 2 && session.Population(1) < 45)
                {
                    var kind = town.State.Level==2 ? (UnitKind)(Mathf.FloorToInt(session.BattleTime/session.AiInterval)%4) : Mathf.FloorToInt(session.BattleTime/session.AiInterval)%3==0 ? UnitKind.Archer : UnitKind.Footman;
                    if (town.Recruit(kind,1) == null) recruited = true;
                }
            }
            if (session.BattleTime < session.AiFirstOffensiveTime) return;
            var active = session.Units.Where(u => u && u.Team == 1 && u.IsAlive && u.isActiveAndEnabled && u.Agent && u.Agent.enabled && u.IsIdle).ToList();
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
