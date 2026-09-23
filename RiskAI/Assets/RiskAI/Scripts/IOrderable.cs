using RiskAI.Core;

namespace RiskAI
{
    /// <summary>
    /// An actor that accepts <see cref="UnitCommand"/>. Soldiers and ships both implement it;
    /// the command inbox never branches on the actor class.
    /// </summary>
    public interface IOrderable
    {
        int EntityId { get; }
        int Team { get; }
        bool IsAlive { get; }
        bool IsGarrison { get; }
        ref readonly UnitType Type { get; }
        string OrderError { get; }
        /// <summary>Reject, or release a post when <paramref name="commitRelease"/> is set. Does not start the order.</summary>
        bool Authorize(in UnitCommand command, bool commitRelease);
        bool ApplyOrder(in UnitCommand command);
        bool HumanMoveEligible(in UnitCommand command);
        void BeginHumanMove(in UnitCommand command, double submittedAt, double pausedAtSubmit, bool eligible);
    }
}
