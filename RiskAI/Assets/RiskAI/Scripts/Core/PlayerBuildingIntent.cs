using System;

namespace RiskAI.Core
{
    public enum BuildingKind { Settlement, Harbor, CountryCamp }
    public enum ProductionQueueChannel { Land, Naval }
    public enum RallyDestination { Land, Naval }
    public enum PlayerBuildingIntentKind { RecruitUnit, BuyShip, CancelTraining, SetRally, ClearRally, BuildTower }

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
        public readonly NavalUnitKind Ship;
        public readonly int CancelIndex;
        public readonly ProductionQueueChannel QueueChannel;
        public readonly RallyDestination RallyDestination;
        public readonly float RallyX,RallyY,RallyZ;

        PlayerBuildingIntent(BuildingId buildingId,PlayerBuildingIntentKind kind,UnitKind unit,NavalUnitKind ship,int cancelIndex,ProductionQueueChannel queueChannel,RallyDestination rallyDestination,float rallyX,float rallyY,float rallyZ)
        { BuildingId=buildingId;Kind=kind;Unit=unit;Ship=ship;CancelIndex=cancelIndex;QueueChannel=queueChannel;RallyDestination=rallyDestination;RallyX=rallyX;RallyY=rallyY;RallyZ=rallyZ; }
        public static PlayerBuildingIntent Recruit(BuildingId buildingId,UnitKind kind) => new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.RecruitUnit,kind,default,0,ProductionQueueChannel.Land,RallyDestination.Land,0,0,0);
        public static PlayerBuildingIntent BuyShip(BuildingId buildingId,NavalUnitKind kind) => new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.BuyShip,default,kind,0,ProductionQueueChannel.Naval,RallyDestination.Naval,0,0,0);
        public static PlayerBuildingIntent CancelTraining(BuildingId buildingId,ProductionQueueChannel channel,int index) => new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.CancelTraining,default,default,index,channel,RallyDestination.Land,0,0,0);
        public static PlayerBuildingIntent CancelLand(BuildingId buildingId,int index) => CancelTraining(buildingId,ProductionQueueChannel.Land,index);
        public static PlayerBuildingIntent CancelNaval(BuildingId buildingId,int index) => CancelTraining(buildingId,ProductionQueueChannel.Naval,index);
        public static PlayerBuildingIntent SetRally(BuildingId buildingId,RallyDestination destination,float x,float y,float z) => new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.SetRally,default,default,0,ProductionQueueChannel.Land,destination,x,y,z);
        public static PlayerBuildingIntent SetLandRally(BuildingId buildingId,float x,float y,float z) => SetRally(buildingId,RallyDestination.Land,x,y,z);
        public static PlayerBuildingIntent SetNavalRally(BuildingId buildingId,float x,float y,float z) => SetRally(buildingId,RallyDestination.Naval,x,y,z);
        public static PlayerBuildingIntent ClearRally(BuildingId buildingId) => new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.ClearRally,default,default,0,ProductionQueueChannel.Land,RallyDestination.Land,0,0,0);
        public static PlayerBuildingIntent BuildTower(BuildingId buildingId) => new PlayerBuildingIntent(buildingId,PlayerBuildingIntentKind.BuildTower,default,default,0,ProductionQueueChannel.Land,RallyDestination.Land,0,0,0);
    }
}
