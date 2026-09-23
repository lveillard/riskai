using System;

namespace RiskAI.Core
{
    public enum BuildingKind { Settlement, Harbor, CountryCamp }
    public enum ProductionQueueChannel { Land, Naval }
    public enum RallyDestination { Land, Naval }
    // v0.30 removed BuildTower: city/harbor posts cannot be attacked, so a rebuild order was unreachable.
    public enum PlayerBuildingIntentKind { Recruit, CancelTraining, SetRally, ClearRally }

    /// <summary>Typed local building identity. Stable within its map roster and free of scene references.</summary>
    public readonly struct BuildingId : IEquatable<BuildingId>
    {
        public readonly BuildingKind Kind;
        public readonly string LocalId;
        public BuildingId(BuildingKind kind,string localId) { Kind=kind;LocalId=localId; }
        public bool IsValid => !string.IsNullOrEmpty(LocalId);
        public bool Equals(BuildingId other) => Kind==other.Kind&&string.Equals(LocalId,other.LocalId,StringComparison.Ordinal);
        public override bool Equals(object other) => other is BuildingId id&&Equals(id);
        public override int GetHashCode() => ((int)Kind*397)^(LocalId==null?0:StringComparer.Ordinal.GetHashCode(LocalId));
        public static bool operator ==(BuildingId left,BuildingId right) => left.Equals(right);
        public static bool operator !=(BuildingId left,BuildingId right) => !left.Equals(right);
    }

    /// <summary>ID-only building command data. It carries no Unity object or transport semantics.</summary>
    public readonly struct PlayerBuildingIntent
    {
        public readonly BuildingId BuildingId;
        public readonly PlayerBuildingIntentKind Kind;
        public readonly UnitKind Unit;
        public readonly int CancelIndex;
        public readonly ProductionQueueChannel QueueChannel;
        public readonly RallyDestination RallyDestination;
        public readonly float RallyX,RallyY,RallyZ;

        PlayerBuildingIntent(BuildingId buildingId,PlayerBuildingIntentKind kind,UnitKind unit,int cancelIndex,ProductionQueueChannel queueChannel,RallyDestination rallyDestination,float rallyX,float rallyY,float rallyZ)
        { BuildingId=buildingId;Kind=kind;Unit=unit;CancelIndex=cancelIndex;QueueChannel=queueChannel;RallyDestination=rallyDestination;RallyX=rallyX;RallyY=rallyY;RallyZ=rallyZ; }
        /// <summary>Recruits any unit type; hulls use the harbor's naval queue channel.</summary>
        public static PlayerBuildingIntent Recruit(BuildingId buildingId,UnitKind kind)
        {
            bool sea=UnitCatalog.Get(kind).Domain==UnitDomain.Sea;
            return new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.Recruit,kind,0,sea?ProductionQueueChannel.Naval:ProductionQueueChannel.Land,sea?RallyDestination.Naval:RallyDestination.Land,0,0,0);
        }
        public static PlayerBuildingIntent CancelTraining(BuildingId buildingId,ProductionQueueChannel channel,int index) => new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.CancelTraining,default,index,channel,RallyDestination.Land,0,0,0);
        public static PlayerBuildingIntent CancelLand(BuildingId buildingId,int index) => CancelTraining(buildingId,ProductionQueueChannel.Land,index);
        public static PlayerBuildingIntent CancelNaval(BuildingId buildingId,int index) => CancelTraining(buildingId,ProductionQueueChannel.Naval,index);
        public static PlayerBuildingIntent SetRally(BuildingId buildingId,RallyDestination destination,float x,float y,float z) => new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.SetRally,default,0,ProductionQueueChannel.Land,destination,x,y,z);
        public static PlayerBuildingIntent SetLandRally(BuildingId buildingId,float x,float y,float z) => SetRally(buildingId,RallyDestination.Land,x,y,z);
        public static PlayerBuildingIntent SetNavalRally(BuildingId buildingId,float x,float y,float z) => SetRally(buildingId,RallyDestination.Naval,x,y,z);
        public static PlayerBuildingIntent ClearRally(BuildingId buildingId) => new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.ClearRally,default,0,ProductionQueueChannel.Land,RallyDestination.Land,0,0,0);
    }
}
