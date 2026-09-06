using System.Collections.Generic;

namespace RiskAI
{
    /// <summary>One ordered tick for gameplay. NavMesh remains an explicit Unity movement adapter.</summary>
    public sealed class BattleWorld
    {
        readonly BattleSession session;
        readonly List<Soldier> units = new List<Soldier>(256);
        readonly List<Ship> ships = new List<Ship>(32);
        public BattleWorld(BattleSession battle) { session = battle; }

        public void Tick(float delta)
        {
            if (session.Paused || session.Winner >= 0) return;
            session.Commands.Tick();
            session.Spatial.Rebuild(session.Targets, session.Units);
            units.Clear(); units.AddRange(session.Units);
            for (int i = 0; i < units.Count; i++)
                if (units[i] && units[i].IsAlive && units[i].isActiveAndEnabled) units[i].SimTick(delta);
            for (int i = 0; i < session.Towers.Count; i++)
                if (session.Towers[i] && session.Towers[i].isActiveAndEnabled) session.Towers[i].SimTick(delta);
            var naval = session.Naval;
            if (naval)
            {
                ships.Clear(); ships.AddRange(naval.Ships);
                for (int i = 0; i < ships.Count; i++) if (ships[i] && ships[i].IsAlive && ships[i].isActiveAndEnabled) ships[i].SimTick(delta);
            }
            // Ships move inside this tick. Impact/claim queries must observe those new cells.
            session.Spatial.Rebuild(session.Targets, session.Units);
            session.Combat.Tick(delta);
            for (int i = 0; i < session.Towns.Count; i++) if (session.Towns[i] && session.Towns[i].isActiveAndEnabled) session.Towns[i].SimTick(delta);
            if (naval)
                for (int i = 0; i < naval.Harbors.Count; i++) if (naval.Harbors[i] && naval.Harbors[i].isActiveAndEnabled) naval.Harbors[i].SimTick(delta);
            session.TickRules(delta);
            session.Commander.Tick(delta);
            if (naval) naval.SimTick(delta);
            session.SoldierPool.Tick();
        }
    }
}
