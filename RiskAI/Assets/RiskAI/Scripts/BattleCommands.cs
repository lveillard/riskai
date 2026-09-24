using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Validated local command inbox. Network authentication is intentionally outside this adapter.</summary>
    public sealed class BattleCommands
    {
        struct QueuedCommand
        {
            public readonly UnitCommand Command;
            public readonly double SubmittedAt, PausedSecondsAtSubmit;
            public QueuedCommand(UnitCommand command, double submittedAt, double pausedSecondsAtSubmit)
            { Command = command; SubmittedAt = submittedAt; PausedSecondsAtSubmit = pausedSecondsAtSubmit; }
        }

        readonly BattleSession session;
        public const int InboxLimit = 1024;
        readonly Queue<QueuedCommand> queue = new Queue<QueuedCommand>(256);
        readonly CommandResult[] results = new CommandResult[InboxLimit];
        int nextCommandId, resultCount;
        CommandTelemetry telemetry;
        // Live lifecycle count; unlike interval aggregates, ConsumeTelemetry does not reset it.
        long humanMoveOutstanding;
        bool telemetryPaused;
        double telemetryPauseStartedAt, observedPausedSeconds, pausedSecondsAtLastConsume;
        public int PendingCount => queue.Count;
        public long AppliedCount { get; private set; }
        public long RejectedCount { get; private set; }
        public string LastRejection { get; private set; }
        float nextFailureMessage;

        public BattleCommands(BattleSession battle) { session=battle; }

        /// <summary>Called by the diagnostics host; affects telemetry only, never command execution.</summary>
        internal void SetTelemetryPauseState(bool paused)
        {
            if (paused == telemetryPaused) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (paused) telemetryPauseStartedAt = now;
            else observedPausedSeconds += now - telemetryPauseStartedAt;
            telemetryPaused = paused;
        }

        /// <summary>Returns and clears low-cadence command telemetry without changing command totals.</summary>
        public CommandTelemetry ConsumeTelemetry()
        {
            var snapshot = telemetry;
            double pausedNow = ObservedPausedSeconds();
            snapshot.HumanMoveOutstanding = humanMoveOutstanding;
            snapshot.ObservedPauseMilliseconds = (pausedNow - pausedSecondsAtLastConsume) * 1000.0;
            pausedSecondsAtLastConsume = pausedNow;
            telemetry = default;
            return snapshot;
        }

        public bool Submit(UnitCommand command) => SubmitResult(command).Accepted;

        /// <summary>Validates synchronously and enqueues. The actor applies the order at the start of the next tick.</summary>
        public CommandResult SubmitResult(UnitCommand command)
        {
            command = command.WithCommandId(++nextCommandId);
            CommandResult result;
            if(session.Paused) result = Fail(command, "La partida está detenida.");
            else if(session.Winner>=0) result = Fail(command, "La batalla ha terminado.");
            else if(queue.Count>=InboxLimit) result = Fail(command, OrderQueue.FullError);
            else if(!Valid(command, true)) result = Fail(command, RejectionReason(command));
            else
            {
                double submittedAt = Time.realtimeSinceStartupAsDouble;
                queue.Enqueue(new QueuedCommand(command, submittedAt, ObservedPausedSeconds()));
                if (queue.Count > telemetry.MaxQueueDepth) telemetry.MaxQueueDepth = queue.Count;
                if (command.PlayerId == 0) telemetry.HumanSubmitted++; else telemetry.AiSubmitted++;
                result = CommandResult.Accept(command);
            }
            Remember(result);
            return result;
        }

        /// <summary>The latest result for a command id, if it is still in the ring.</summary>
        public bool TryGetResult(int commandId, out CommandResult result)
        {
            int available = resultCount < results.Length ? resultCount : results.Length;
            for (int i = 0; i < available; i++)
            {
                var candidate = results[(resultCount - 1 - i + results.Length) % results.Length];
                if (candidate.CommandId == commandId) { result = candidate; return true; }
            }
            result = default;
            return false;
        }

        public CommandResult SubmitResult(int playerId, int unitId, UnitCommandKind kind, float x = 0, float y = 0, float z = 0, int targetId = 0, bool append = false, string structureId = null, BuildingKind structureKind = BuildingKind.Settlement) =>
            SubmitResult(new UnitCommand(playerId, unitId, kind, x, y, z, targetId, append, structureId: structureId, structureKind: structureKind));

        void Remember(CommandResult result)
        {
            results[resultCount % results.Length] = result;
            resultCount++;
        }
        void Revise(CommandResult result)
        {
            int available = resultCount < results.Length ? resultCount : results.Length;
            for (int i = 0; i < available; i++)
            {
                int slot = (resultCount - 1 - i + results.Length) % results.Length;
                if (results[slot].CommandId != result.CommandId) continue;
                results[slot] = result;
                return;
            }
            Remember(result);
        }
        CommandResult Fail(UnitCommand command, string reason)
        {
            Reject(command, reason);
            return CommandResult.Reject(command, reason);
        }
        string RejectionReason(UnitCommand command)
        {
            var actor = session.FindTarget(command.UnitId) as IOrderable;
            if (actor != null && !string.IsNullOrEmpty(actor.OrderError)) return actor.OrderError;
            return OrderQueue.InvalidError;
        }
        bool Valid(UnitCommand command, bool plan)
        {
            var actor=session.FindTarget(command.UnitId) as IOrderable;
            if(actor!=null) actor.ClearOrderError();
            if(!PlayerRules.IsPlayer(command.PlayerId) || command.PlayerId>=session.PlayerCount || !Finite(command.X) || !Finite(command.Y) || !Finite(command.Z))return false;
            if(command.Kind<UnitCommandKind.Move || command.Kind>UnitCommandKind.Unload)return false;
            if(actor==null || !actor.IsAlive || actor.Team!=command.PlayerId)return false;
            if(command.HasPoint && actor.Type.Domain==UnitDomain.Static)return false;
            return actor.Authorize(command, plan);
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void Reject(UnitCommand command,string reason)
        {
            RejectedCount++;LastRejection=reason;
            if (command.PlayerId == 0) telemetry.HumanRejected++; else telemetry.AiRejected++;
            if(command.PlayerId==0 && Time.unscaledTime>=nextFailureMessage)
            {
                nextFailureMessage=Time.unscaledTime+1;session.Message(reason,MessageKind.Info);
                Debug.Log("RISKAI_ORDER_REJECTED: id="+command.UnitId+" kind="+command.Kind+" tick="+session.Clock.TickCount+" reason="+reason);
            }
        }
        public void Tick()
        {
            while(queue.Count>0)
            {
                var queued=queue.Dequeue();
                var command=queued.Command;
                if(!Valid(command,false)){FailDrain(command,"La unidad, el relevo o el objetivo cambió antes de aplicar la orden.");continue;}
                var actor=(IOrderable)session.FindTarget(command.UnitId);
                bool firstMoveEligible = actor.HumanMoveEligible(command);
                bool applied=actor.ApplyOrder(command);
                if(applied)
                {
                    AppliedCount++;
                    RecordApplied(command.PlayerId, Time.realtimeSinceStartupAsDouble - queued.SubmittedAt,
                        ObservedPausedSeconds() - queued.PausedSecondsAtSubmit);
                    actor.BeginHumanMove(command, queued.SubmittedAt, queued.PausedSecondsAtSubmit, firstMoveEligible);
                }
                else FailDrain(command, string.IsNullOrEmpty(actor.OrderError) ? "No se ha podido aplicar la orden." : actor.OrderError);
            }
        }

        void FailDrain(UnitCommand command, string reason)
        {
            Revise(CommandResult.Reject(command, reason));
            Reject(command, reason);
        }

        internal void RecordHumanFirstMotion(double submittedAt, double pausedSecondsAtSubmit)
        {
            double activeSeconds = Time.realtimeSinceStartupAsDouble - submittedAt -
                (ObservedPausedSeconds() - pausedSecondsAtSubmit);
            if (activeSeconds < 0) activeSeconds = 0;
            double milliseconds = activeSeconds * 1000.0;
            telemetry.HumanFirstMoveCount++;
            telemetry.HumanFirstMoveMilliseconds += milliseconds;
            if (milliseconds > telemetry.HumanFirstMoveMaxMilliseconds) telemetry.HumanFirstMoveMaxMilliseconds = milliseconds;
        }
        internal double HumanMoveActiveSeconds(double submittedAt, double pausedSecondsAtSubmit) =>
            System.Math.Max(0, Time.realtimeSinceStartupAsDouble - submittedAt - (ObservedPausedSeconds() - pausedSecondsAtSubmit));

        // Stages are simulation-tick observations, not exact NavMesh solver completion times.
        // Each interval is paired on one command; stage counts may fall in different report windows.
        internal void RecordHumanRouteReady(double applyToRouteSeconds)
        {
            telemetry.HumanRouteReadyCount++;
            AccumulateStage(applyToRouteSeconds, ref telemetry.HumanApplyToRouteMilliseconds, ref telemetry.HumanApplyToRouteMaxMilliseconds);
        }
        internal void RecordHumanSpeed(double submitToSpeedSeconds, double routeToSpeedSeconds)
        {
            telemetry.HumanSpeedCount++;
            AccumulateStage(submitToSpeedSeconds, ref telemetry.HumanSubmitToSpeedMilliseconds, ref telemetry.HumanSubmitToSpeedMaxMilliseconds);
            if (routeToSpeedSeconds >= 0)
            {
                telemetry.HumanRouteToSpeedCount++;
                AccumulateStage(routeToSpeedSeconds, ref telemetry.HumanRouteToSpeedMilliseconds, ref telemetry.HumanRouteToSpeedMaxMilliseconds);
            }
        }
        internal void RecordHumanSpeedToDirected(double seconds)
        {
            telemetry.HumanSpeedToDirectedCount++;
            AccumulateStage(seconds, ref telemetry.HumanSpeedToDirectedMilliseconds, ref telemetry.HumanSpeedToDirectedMaxMilliseconds);
        }
        static void AccumulateStage(double seconds, ref double total, ref double maximum)
        {
            double milliseconds = System.Math.Max(0, seconds) * 1000.0;
            total += milliseconds;
            if (milliseconds > maximum) maximum = milliseconds;
        }
        internal void RecordHumanMoveEnded() { humanMoveOutstanding--; }
        internal void RecordHumanFirstMoveEligible() { telemetry.HumanFirstMoveEligible++; humanMoveOutstanding++; }
        internal void RecordHumanFirstMoveCancelled() { telemetry.HumanFirstMoveCancelled++; }

        void RecordApplied(int playerId, double elapsedSeconds, double observedPauseSeconds)
        {
            // Active time excludes only pauses observed by RuntimeDiagnostics. The
            // reported observedPauseMs makes an incomplete observer visible in logs.
            double activeSeconds = elapsedSeconds - observedPauseSeconds;
            if (activeSeconds < 0) activeSeconds = 0;
            double milliseconds = activeSeconds * 1000.0;
            if (playerId == 0)
            {
                telemetry.HumanApplied++;
                telemetry.HumanSubmitToApplyMilliseconds += milliseconds;
                if (milliseconds > telemetry.HumanSubmitToApplyMaxMilliseconds) telemetry.HumanSubmitToApplyMaxMilliseconds = milliseconds;
            }
            else
            {
                telemetry.AiApplied++;
                telemetry.AiSubmitToApplyMilliseconds += milliseconds;
                if (milliseconds > telemetry.AiSubmitToApplyMaxMilliseconds) telemetry.AiSubmitToApplyMaxMilliseconds = milliseconds;
            }
        }
        double ObservedPausedSeconds() => observedPausedSeconds + (telemetryPaused ? Time.realtimeSinceStartupAsDouble - telemetryPauseStartedAt : 0);
    }

    /// <summary>Allocation-free aggregate returned by <see cref="BattleCommands.ConsumeTelemetry"/>.</summary>
    public struct CommandTelemetry
    {
        public int MaxQueueDepth;
        public long HumanSubmitted, AiSubmitted, HumanApplied, AiApplied, HumanRejected, AiRejected;
        public long HumanFirstMoveEligible, HumanFirstMoveCancelled, HumanFirstMoveCount;
        public double HumanSubmitToApplyMilliseconds, HumanSubmitToApplyMaxMilliseconds;
        public double AiSubmitToApplyMilliseconds, AiSubmitToApplyMaxMilliseconds;
        public double HumanFirstMoveMilliseconds, HumanFirstMoveMaxMilliseconds;
        public long HumanMoveOutstanding;
        public long HumanRouteReadyCount, HumanSpeedCount, HumanRouteToSpeedCount, HumanSpeedToDirectedCount;
        public double HumanApplyToRouteMilliseconds, HumanApplyToRouteMaxMilliseconds;
        public double HumanSubmitToSpeedMilliseconds, HumanSubmitToSpeedMaxMilliseconds;
        public double HumanRouteToSpeedMilliseconds, HumanRouteToSpeedMaxMilliseconds;
        public double HumanSpeedToDirectedMilliseconds, HumanSpeedToDirectedMaxMilliseconds;
        public double ObservedPauseMilliseconds;
    }
}
