using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Probe-owned human-move telemetry (RuntimeCommandProbe tooling). The command inbox and
    /// the soldier only hand it the applied command and the actor; every sample, stage
    /// aggregate and lifecycle count lives here, never in the production types.
    /// </summary>
    public static class HumanMoveProbe
    {
        /// <summary>Allocation-free aggregate of one report window. <see cref="Outstanding"/> is a live count.</summary>
        public struct Telemetry
        {
            public long Outstanding;
            public long RouteReadyCount, SpeedCount, RouteToSpeedCount, SpeedToDirectedCount;
            public double ApplyToRouteMilliseconds, ApplyToRouteMaxMilliseconds;
            public double SubmitToSpeedMilliseconds, SubmitToSpeedMaxMilliseconds;
            public double RouteToSpeedMilliseconds, RouteToSpeedMaxMilliseconds;
            public double SpeedToDirectedMilliseconds, SpeedToDirectedMaxMilliseconds;
        }

        /// <summary>The sampled move of one actor: a plain struct, never a per-tick allocation.</summary>
        public struct Sample
        {
            public bool Active, RouteResolved;
            public double SubmittedAt, PausedSecondsAtSubmit, AppliedActiveSeconds, RouteActiveSeconds, SpeedActiveSeconds;
            public Vector3 Destination;
        }

        static Telemetry telemetry;
        static long outstanding;

        /// <summary>Live lifecycle count of sampled moves not yet ended.</summary>
        public static long Outstanding => outstanding;

        /// <summary>Returns and clears the stage aggregates without touching <see cref="Outstanding"/>.</summary>
        public static Telemetry Consume()
        {
            var snapshot = telemetry;
            snapshot.Outstanding = outstanding;
            telemetry = default;
            return snapshot;
        }

        /// <summary>Active time of a sampled move: wall clock minus pauses observed by the diagnostics probe.</summary>
        public static double ActiveSeconds(BattleSession session, double submittedAt, double pausedSecondsAtSubmit) =>
            System.Math.Max(0, Time.realtimeSinceStartupAsDouble - submittedAt -
                ((session != null ? session.Commands.ObservedPausedSeconds() : 0) - pausedSecondsAtSubmit));

        /// <summary>What an applied order means for the move sample, judged before the motor takes the order.</summary>
        public enum MoveSampling { Ignore, CancelPrevious, Sample }

        /// <summary>
        /// Only orders issued while the agent is fully stationary are sampled. This
        /// avoids reporting old velocity as the response to a redirected movement.
        /// </summary>
        public static MoveSampling Prepare(IOrderable actor, in UnitCommand command)
        {
            if (command.PlayerId != 0 || command.Append) return MoveSampling.Ignore;
            if (command.Kind != UnitCommandKind.Move && command.Kind != UnitCommandKind.AttackMove) return MoveSampling.Ignore;
            return actor is Soldier soldier && soldier.Agent && soldier.Agent.enabled && soldier.Agent.isOnNavMesh &&
                !soldier.Agent.pathPending && !soldier.Agent.hasPath && soldier.Agent.velocity.sqrMagnitude < .0025f
                ? MoveSampling.Sample : MoveSampling.CancelPrevious;
        }

        /// <summary>Called once the order applied: a new sample replaces (and ends) the previous one.</summary>
        public static void Commit(IOrderable actor, in UnitCommand command, double submittedAt, double pausedSecondsAtSubmit, MoveSampling sampling)
        {
            if (sampling == MoveSampling.Ignore || !(actor is Soldier soldier)) return;
            End(soldier);
            if (sampling != MoveSampling.Sample) return;
            var agent = soldier.Agent;
            soldier.HumanMove = new Sample
            {
                Active = true,
                SubmittedAt = submittedAt,
                PausedSecondsAtSubmit = pausedSecondsAtSubmit,
                Destination = agent && agent.enabled ? agent.destination : new Vector3(command.X, command.Y, command.Z),
                AppliedActiveSeconds = ActiveSeconds(soldier.Session, submittedAt, pausedSecondsAtSubmit),
                RouteActiveSeconds = -1,
                SpeedActiveSeconds = -1
            };
            outstanding++;
        }

        /// <summary>One simulation-tick observation of route, speed and direction.</summary>
        public static void Tick(Soldier soldier)
        {
            var sample = soldier.HumanMove;
            if (!sample.Active) return;
            var session = soldier.Session;
            var agent = soldier.Agent;
            double activeSeconds = ActiveSeconds(session, sample.SubmittedAt, sample.PausedSecondsAtSubmit);
            // First observed non-pending state; this does not timestamp the actual solver completion.
            if (!agent.pathPending && !sample.RouteResolved)
            {
                sample.RouteResolved = true;
                sample.RouteActiveSeconds = activeSeconds;
                telemetry.RouteReadyCount++;
                Accumulate(activeSeconds - sample.AppliedActiveSeconds, ref telemetry.ApplyToRouteMilliseconds, ref telemetry.ApplyToRouteMaxMilliseconds);
            }
            Vector3 toward = sample.Destination - soldier.transform.position;
            toward.y = 0;
            Vector3 velocity = agent.velocity;
            velocity.y = 0;
            if (sample.SpeedActiveSeconds < 0 && velocity.sqrMagnitude > .04f)
            {
                sample.SpeedActiveSeconds = activeSeconds;
                telemetry.SpeedCount++;
                Accumulate(activeSeconds, ref telemetry.SubmitToSpeedMilliseconds, ref telemetry.SubmitToSpeedMaxMilliseconds);
                if (sample.RouteActiveSeconds >= 0)
                {
                    telemetry.RouteToSpeedCount++;
                    Accumulate(activeSeconds - sample.RouteActiveSeconds, ref telemetry.RouteToSpeedMilliseconds, ref telemetry.RouteToSpeedMaxMilliseconds);
                }
            }
            if (sample.RouteResolved && velocity.sqrMagnitude > .04f &&
                (toward.sqrMagnitude < .25f || Vector3.Dot(velocity, toward) > 0))
            {
                telemetry.SpeedToDirectedCount++;
                Accumulate(activeSeconds - sample.SpeedActiveSeconds, ref telemetry.SpeedToDirectedMilliseconds, ref telemetry.SpeedToDirectedMaxMilliseconds);
                End(soldier);
                return;
            }
            soldier.HumanMove = sample;
        }

        /// <summary>Ends the sample of one actor (the move finished, was replaced, or the actor left play).</summary>
        public static void End(Soldier soldier)
        {
            if (!soldier.HumanMove.Active) return;
            soldier.HumanMove = default;
            outstanding--;
        }

        static void Accumulate(double seconds, ref double total, ref double maximum)
        {
            double milliseconds = System.Math.Max(0, seconds) * 1000.0;
            total += milliseconds;
            if (milliseconds > maximum) maximum = milliseconds;
        }
    }
}
