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

        public static UnitCommandKind Kind(OrderQueue orders, int index)
        {
            orders.Leg(index, out _, out _, out _, out var kind);
            return (UnitCommandKind)kind;
        }

        public static void Publish(OrderQueue orders, bool active, UnitCommandKind kind, Vector3 point) =>
            orders.Publish(active, kind, point.x, point.y, point.z);
    }
}
