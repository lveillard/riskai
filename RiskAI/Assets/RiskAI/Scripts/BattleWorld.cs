using System.Collections.Generic;
using System.Diagnostics;

namespace RiskAI
{
    /// <summary>One ordered tick for gameplay. NavMesh remains an explicit Unity movement adapter.</summary>
    public sealed class BattleWorld
    {
        readonly BattleSession session;
        readonly List<Soldier> units = new List<Soldier>(256);
        readonly List<Ship> ships = new List<Ship>(32);
        BattleWorldTelemetry telemetry;
        static readonly double TimestampToMilliseconds = 1000.0 / Stopwatch.Frequency;

        public BattleWorld(BattleSession battle) { session = battle; }

        /// <summary>Returns and clears the coarse timing window. Intended for low-cadence diagnostics only.</summary>
        public BattleWorldTelemetry ConsumeTelemetry()
        {
            var snapshot = telemetry;
            telemetry = default;
            return snapshot;
        }

        public void Tick(float delta)
        {
            if (session.Paused || session.Winner >= 0) return;
            long tickStarted = Stopwatch.GetTimestamp();
            long phaseStarted = tickStarted;

            session.Commands.Tick();
            AddPhase(ref telemetry.CommandsMilliseconds, ref telemetry.CommandsMaxMilliseconds, phaseStarted);

            phaseStarted = Stopwatch.GetTimestamp();
            session.Spatial.Rebuild(session.Targets, session.Units);
            AddPhase(ref telemetry.SpatialMilliseconds, ref telemetry.SpatialMaxMilliseconds, phaseStarted);

            phaseStarted = Stopwatch.GetTimestamp();
            units.Clear(); units.AddRange(session.Units);
            for (int i = 0; i < units.Count; i++)
                if (units[i] && units[i].IsAlive && units[i].isActiveAndEnabled) units[i].SimTick(delta);
            AddPhase(ref telemetry.SoldiersMilliseconds, ref telemetry.SoldiersMaxMilliseconds, phaseStarted);

            phaseStarted = Stopwatch.GetTimestamp();
            for (int i = 0; i < session.Towers.Count; i++)
                if (session.Towers[i] && session.Towers[i].isActiveAndEnabled) session.Towers[i].SimTick(delta);
            AddPhase(ref telemetry.TowersMilliseconds, ref telemetry.TowersMaxMilliseconds, phaseStarted);

            var naval = session.Naval;
            phaseStarted = Stopwatch.GetTimestamp();
            if (naval)
            {
                ships.Clear(); ships.AddRange(naval.Ships);
                for (int i = 0; i < ships.Count; i++)
                    if (ships[i] && ships[i].IsAlive && ships[i].isActiveAndEnabled) ships[i].SimTick(delta);
            }
            AddPhase(ref telemetry.ShipsMilliseconds, ref telemetry.ShipsMaxMilliseconds, phaseStarted);

            // Ships move inside this tick. Impact/claim queries must observe those new cells.
            phaseStarted = Stopwatch.GetTimestamp();
            session.Spatial.Rebuild(session.Targets, session.Units);
            AddPhase(ref telemetry.SpatialMilliseconds, ref telemetry.SpatialMaxMilliseconds, phaseStarted);

            phaseStarted = Stopwatch.GetTimestamp();
            session.Combat.Tick(delta);
            AddPhase(ref telemetry.CombatMilliseconds, ref telemetry.CombatMaxMilliseconds, phaseStarted);

            phaseStarted = Stopwatch.GetTimestamp();
            for (int i = 0; i < session.Towns.Count; i++)
                if (session.Towns[i] && session.Towns[i].isActiveAndEnabled) session.Towns[i].SimTick(delta);
            if (naval)
                for (int i = 0; i < naval.Harbors.Count; i++)
                    if (naval.Harbors[i] && naval.Harbors[i].isActiveAndEnabled) naval.Harbors[i].SimTick(delta);
            AddPhase(ref telemetry.ClaimsMilliseconds, ref telemetry.ClaimsMaxMilliseconds, phaseStarted);

            phaseStarted = Stopwatch.GetTimestamp();
            session.TickRules(delta);
            AddPhase(ref telemetry.RulesMilliseconds, ref telemetry.RulesMaxMilliseconds, phaseStarted);

            phaseStarted = Stopwatch.GetTimestamp();
            session.Commander.Tick(delta);
            AddPhase(ref telemetry.LandAiMilliseconds, ref telemetry.LandAiMaxMilliseconds, phaseStarted);
            long navalStarted = Stopwatch.GetTimestamp();
            if (naval) naval.SimTick(delta);
            AddPhase(ref telemetry.NavalAiMilliseconds, ref telemetry.NavalAiMaxMilliseconds, navalStarted);
            AddPhase(ref telemetry.AiMilliseconds, ref telemetry.AiMaxMilliseconds, phaseStarted);

            phaseStarted = Stopwatch.GetTimestamp();
            session.SoldierPool.Tick();
            AddPhase(ref telemetry.PoolMilliseconds, ref telemetry.PoolMaxMilliseconds, phaseStarted);

            double total = ElapsedMilliseconds(tickStarted);
            telemetry.TickCount++;
            telemetry.TotalMilliseconds += total;
            if (total > telemetry.MaxMilliseconds) telemetry.MaxMilliseconds = total;
        }

        static void AddPhase(ref double total, ref double maximum, long started)
        {
            double elapsed = ElapsedMilliseconds(started);
            total += elapsed;
            if (elapsed > maximum) maximum = elapsed;
        }
        static double ElapsedMilliseconds(long started) => (Stopwatch.GetTimestamp() - started) * TimestampToMilliseconds;
    }

    /// <summary>Allocation-free aggregate returned by <see cref="BattleWorld.ConsumeTelemetry"/>.</summary>
    public struct BattleWorldTelemetry
    {
        public int TickCount;
        public double TotalMilliseconds, MaxMilliseconds;
        public double CommandsMilliseconds, CommandsMaxMilliseconds;
        public double SpatialMilliseconds, SpatialMaxMilliseconds;
        public double SoldiersMilliseconds, SoldiersMaxMilliseconds;
        public double TowersMilliseconds, TowersMaxMilliseconds;
        public double ShipsMilliseconds, ShipsMaxMilliseconds;
        public double CombatMilliseconds, CombatMaxMilliseconds;
        public double ClaimsMilliseconds, ClaimsMaxMilliseconds;
        public double RulesMilliseconds, RulesMaxMilliseconds;
        public double AiMilliseconds, AiMaxMilliseconds;
        public double LandAiMilliseconds, LandAiMaxMilliseconds;
        public double NavalAiMilliseconds, NavalAiMaxMilliseconds;
        public double PoolMilliseconds, PoolMaxMilliseconds;
    }
}
