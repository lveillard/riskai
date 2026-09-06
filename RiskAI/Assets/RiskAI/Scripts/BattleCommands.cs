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
        readonly Queue<QueuedCommand> queue = new Queue<QueuedCommand>(256);
        CommandTelemetry telemetry;
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
            snapshot.ObservedPauseMilliseconds = (pausedNow - pausedSecondsAtLastConsume) * 1000.0;
            pausedSecondsAtLastConsume = pausedNow;
            telemetry = default;
            return snapshot;
        }

        public bool Submit(UnitCommand command)
        {
            if(session.Paused || session.Winner>=0 || queue.Count>=1024 || !Valid(command))
            {
                var unit=session.FindTarget(command.UnitId) as Soldier;
                Reject(command,unit&&unit.IsGarrison?"Defensor retenido: recluta una tropa móvil para dar órdenes.":"La orden ya no es válida para esa unidad o su objetivo.");return false;
            }
            double submittedAt = Time.realtimeSinceStartupAsDouble;
            queue.Enqueue(new QueuedCommand(command, submittedAt, ObservedPausedSeconds()));
            if (queue.Count > telemetry.MaxQueueDepth) telemetry.MaxQueueDepth = queue.Count;
            if (command.PlayerId == 0) telemetry.HumanSubmitted++; else telemetry.AiSubmitted++;
            return true;
        }
        bool Valid(UnitCommand command)
        {
            if(!PlayerRules.IsPlayer(command.PlayerId) || command.PlayerId>=session.PlayerCount || !Finite(command.X) || !Finite(command.Y) || !Finite(command.Z))return false;
            if(command.Kind<UnitCommandKind.Move || command.Kind>UnitCommandKind.Follow)return false;
            var unit=session.FindTarget(command.UnitId) as Soldier;
            if(!unit || !unit.IsAlive || unit.Team!=command.PlayerId || unit.IsGarrison)return false;
            if(command.Kind!=UnitCommandKind.Attack && command.Kind!=UnitCommandKind.Follow)return true;
            var target=session.FindTarget(command.TargetId);
            if(!target || !target.IsAlive)return false;
            return command.Kind==UnitCommandKind.Attack ? target.CanBeAttacked && target.Team!=unit.Team : target is Soldier && target.Team==unit.Team && target!=unit;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void Reject(UnitCommand command,string reason)
        {
            RejectedCount++;LastRejection=reason;
            if (command.PlayerId == 0) telemetry.HumanRejected++; else telemetry.AiRejected++;
            if(command.PlayerId==0 && Time.unscaledTime>=nextFailureMessage)
            {
                nextFailureMessage=Time.unscaledTime+1;session.Message(reason);
                Debug.Log("RISKAI_ORDER_REJECTED: id="+command.UnitId+" kind="+command.Kind+" tick="+session.Clock.TickCount+" reason="+reason);
            }
        }
        public void Tick()
        {
            while(queue.Count>0)
            {
                var queued=queue.Dequeue();
                var command=queued.Command;
                if(!Valid(command)){Reject(command,"La unidad o el objetivo cambió antes de aplicar la orden.");continue;}
                var unit=(Soldier)session.FindTarget(command.UnitId);
                bool directHumanMove = command.PlayerId == 0 && !command.Append &&
                    (command.Kind == UnitCommandKind.Move || command.Kind == UnitCommandKind.AttackMove);
                bool firstMoveEligible = directHumanMove && unit.CanBeginHumanMoveTelemetry;
                var point=new Vector3(command.X,command.Y,command.Z);
                bool applied=true;
                switch(command.Kind)
                {
                    case UnitCommandKind.Move: applied=unit.TryMoveTo(point,false,command.Append); break;
                    case UnitCommandKind.AttackMove: applied=unit.TryMoveTo(point,true,command.Append); break;
                    case UnitCommandKind.Patrol: applied=unit.Patrol(point,command.Append); break;
                    case UnitCommandKind.Attack: unit.Attack(session.FindTarget(command.TargetId)); break;
                    case UnitCommandKind.Follow: unit.Follow(session.FindTarget(command.TargetId) as Soldier); break;
                    case UnitCommandKind.Stop: unit.Stop(); break;
                    case UnitCommandKind.Hold: unit.HoldPosition(); break;
                }
                if(applied)
                {
                    AppliedCount++;
                    RecordApplied(command.PlayerId, Time.realtimeSinceStartupAsDouble - queued.SubmittedAt,
                        ObservedPausedSeconds() - queued.PausedSecondsAtSubmit);
                    if (directHumanMove) unit.BeginHumanMoveTelemetry(queued.SubmittedAt, queued.PausedSecondsAtSubmit, firstMoveEligible, point);
                }
                else Reject(command,unit.LastMoveError??"No se ha podido aplicar la orden.");
            }
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
        internal void RecordHumanFirstMoveEligible() { telemetry.HumanFirstMoveEligible++; }
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
        public double ObservedPauseMilliseconds;
    }
}
