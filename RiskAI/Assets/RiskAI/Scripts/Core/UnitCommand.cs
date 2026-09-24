namespace RiskAI.Core
{
    public enum UnitCommandKind { Move, AttackMove, Attack, Stop, Hold, Patrol, Follow, Capture, Embark, Unload }

    /// <summary>Data-only intent shared by mouse, touch and a future network adapter.</summary>
    public readonly struct UnitCommand
    {
        public readonly int PlayerId, UnitId, TargetId, CommandId;
        public readonly UnitCommandKind Kind;
        public readonly float X, Y, Z;
        public readonly bool Append;
        /// <summary>Stable building id for <see cref="UnitCommandKind.Capture"/> (town or harbor local id).</summary>
        public readonly string StructureId;
        public readonly BuildingKind StructureKind;

        public UnitCommand(int playerId, int unitId, UnitCommandKind kind, float x = 0, float y = 0, float z = 0, int targetId = 0, bool append = false, int commandId = 0, string structureId = null, BuildingKind structureKind = BuildingKind.Settlement)
        {
            PlayerId = playerId; UnitId = unitId; Kind = kind; X = x; Y = y; Z = z; TargetId = targetId; Append = append;
            CommandId = commandId; StructureId = structureId; StructureKind = structureKind;
        }

        public UnitCommand WithCommandId(int commandId) =>
            new UnitCommand(PlayerId, UnitId, Kind, X, Y, Z, TargetId, Append, commandId, StructureId, StructureKind);

        public UnitCommand WithAppend(bool append) =>
            new UnitCommand(PlayerId, UnitId, Kind, X, Y, Z, TargetId, append, CommandId, StructureId, StructureKind);

        public bool HasPoint => Kind == UnitCommandKind.Move || Kind == UnitCommandKind.AttackMove || Kind == UnitCommandKind.Patrol || Kind == UnitCommandKind.Unload;
    }

    /// <summary>One submit outcome, kept by command id for the UI and the AI.</summary>
    public readonly struct CommandResult
    {
        public readonly int CommandId, UnitId;
        public readonly UnitCommandKind Kind;
        public readonly bool Accepted;
        public readonly string Error;
        public CommandResult(int commandId, int unitId, UnitCommandKind kind, bool accepted, string error)
        { CommandId = commandId; UnitId = unitId; Kind = kind; Accepted = accepted; Error = error; }
        public static CommandResult Accept(in UnitCommand command) => new CommandResult(command.CommandId, command.UnitId, command.Kind, true, null);
        public static CommandResult Reject(in UnitCommand command, string error) => new CommandResult(command.CommandId, command.UnitId, command.Kind, false, error);
    }
}
