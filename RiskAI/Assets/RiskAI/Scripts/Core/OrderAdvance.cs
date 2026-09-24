namespace RiskAI.Core
{
    /// <summary>Runs the next queued orders. Soldiers and ships share this loop; only the motor stays on the actor.</summary>
    public interface IQueuedOrderRunner
    {
        bool TryStartQueued(in UnitCommand command);
    }

    public static class OrderAdvance
    {
        /// <summary>Dequeues until one order starts. False when nothing queued could start.</summary>
        public static bool Drain(OrderQueue orders, IQueuedOrderRunner runner)
        {
            while (orders.TryDequeue(out var next))
                if (runner.TryStartQueued(next)) return true;
            return false;
        }

        /// <summary>
        /// Attack and AttackMove stay busy while a live target exists, for a soldier and a ship.
        /// Capture does not: a combat target is not part of the approach.
        /// </summary>
        public static bool MotorIdle(UnitCommandKind kind, bool hasLiveTarget)
        {
            if (!hasLiveTarget) return true;
            return kind != UnitCommandKind.Attack && kind != UnitCommandKind.AttackMove;
        }
    }
}
