using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// An actor that accepts <see cref="UnitCommand"/>. Soldiers and ships both implement it;
    /// the command inbox and the route drawer never branch on the actor class.
    /// </summary>
    public interface IOrderable
    {
        int EntityId { get; }
        int Team { get; }
        bool IsAlive { get; }
        bool IsGarrison { get; }
        bool Selected { get; }
        ref readonly UnitType Type { get; }
        string OrderError { get; }
        OrderQueue Orders { get; }
        void ClearOrderError();
        /// <summary>Shared aim checks, then <see cref="Reach"/>. Does not release a post and does not start the order.</summary>
        bool Authorize(ref UnitCommand command, bool plan);
        /// <summary>Leave a bound post. Commit only when the order is about to start. The relief message is only for this failure.</summary>
        bool ReleasePost(in UnitCommand command, bool commitRelease, out string error);
        /// <summary>Motor only. <paramref name="plan"/> builds the sea path once; a cheap check does not.</summary>
        bool Reach(in UnitCommand command, bool plan, out string error);
        bool ApplyOrder(in UnitCommand command);
        /// <summary>Drawn legs from <see cref="Orders"/>: the active order, then the queue.</summary>
        int OrderLegCount { get; }
        Vector3 OrderLegPoint(int index);
        UnitCommandKind OrderLegKind(int index);
        /// <summary>The active leg's real polyline (navmesh corners or the sea route). Later legs are straight.</summary>
        int ActivePathCount { get; }
        Vector3 ActivePathPoint(int index);
        void RefreshActivePath();
        /// <summary>Where this motor goes for a place click: the claim or landing it was given, or the berth.</summary>
        Vector3 PlacePoint(Vector3 claim, Harbor harbor);
        /// <summary>The motor can take a player order: on the navmesh, or afloat.</summary>
        bool MotorReady { get; }
    }
}
