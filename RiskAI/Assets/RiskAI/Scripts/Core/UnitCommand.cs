namespace RiskAI.Core
{
    public enum UnitCommandKind { Move, AttackMove, Attack, Stop, Hold, Patrol, Follow }
    // Data-only intent: mouse, touch and a future authenticated network adapter share this boundary.
    public readonly struct UnitCommand
    {
        public readonly int PlayerId, UnitId, TargetId;
        public readonly UnitCommandKind Kind;
        public readonly float X, Y, Z;
        public readonly bool Append;
        public UnitCommand(int playerId, int unitId, UnitCommandKind kind, float x=0, float y=0, float z=0, int targetId=0, bool append=false)
        { PlayerId=playerId; UnitId=unitId; Kind=kind; X=x; Y=y; Z=z; TargetId=targetId; Append=append; }
    }
}
