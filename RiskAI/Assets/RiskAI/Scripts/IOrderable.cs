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
        /// <summary>Shared aim checks, post release, then <see cref="Reach"/>. Does not start the order.</summary>
        bool Authorize(in UnitCommand command, bool commitRelease);
        /// <summary>Leave a bound post. The relief message is only for this failure.</summary>
        bool ReleasePost(in UnitCommand command, bool commitRelease, out string error);
        /// <summary>Motor only: a NavMesh point, or a sea path. Aim checks have already passed.</summary>
        bool Reach(in UnitCommand command, bool commitRelease, out string error);
        bool ApplyOrder(in UnitCommand command);
        bool HumanMoveEligible(in UnitCommand command);
        void BeginHumanMove(in UnitCommand command, double submittedAt, double pausedAtSubmit, bool eligible);
        /// <summary>Drawn legs from <see cref="Orders"/>: the active order, then the queue.</summary>
        int OrderLegCount { get; }
        Vector3 OrderLegPoint(int index);
        UnitCommandKind OrderLegKind(int index);
        /// <summary>The active leg's real polyline (navmesh corners or the sea route). Later legs are straight.</summary>
        int ActivePathCount { get; }
        Vector3 ActivePathPoint(int index);
        void RefreshActivePath();
    }
}
