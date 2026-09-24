using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>The one reader for a unit's drawn order legs. Soldiers and ships both call it.</summary>
    public static class OrderLegView
    {
        public static int Count(OrderQueue orders) => orders.LegCount;

        public static Vector3 Point(OrderQueue orders, int index)
        {
            orders.Leg(index, out var x, out var y, out var z, out _);
            return new Vector3(x, y, z);
        }

        /// <summary>Attack and Follow legs follow the live target. Other legs keep the stored point.</summary>
        public static Vector3 Point(OrderQueue orders, int index, BattleSession session, bool hasActive, in UnitCommand active)
        {
            if (session != null && TryCommand(orders, index, hasActive, active, out var command)
                && (command.Kind == UnitCommandKind.Attack || command.Kind == UnitCommandKind.Follow))
            {
                var target = session.FindTarget(command.TargetId);
                if (target) return target.transform.position;
            }
            return Point(orders, index);
        }

        static bool TryCommand(OrderQueue orders, int index, bool hasActive, in UnitCommand active, out UnitCommand command)
        {
            if (index == 0 && hasActive) { command = active; return true; }
            return orders.TryAt(hasActive ? index - 1 : index, out command);
        }

        public static UnitCommandKind Kind(OrderQueue orders, int index)
        {
            orders.Leg(index, out _, out _, out _, out var kind);
            return (UnitCommandKind)kind;
        }

        public static void Publish(OrderQueue orders, bool active, UnitCommandKind kind, Vector3 point) =>
            orders.Publish(active, kind, point.x, point.y, point.z);
    }
}
