using RiskAI.Core;

namespace RiskAI
{
    public sealed partial class RtsController
    {
        PlayerBuildingCommands buildingCommands;
        /// <summary>Common local command boundary for retained UI, hotkeys and device adapters.</summary>
        public string ExecuteBuilding(PlayerBuildingIntent intent)
        {
            if(buildingCommands==null)buildingCommands=new PlayerBuildingCommands(session);
            return buildingCommands.Execute(0,intent);
        }
        public void CancelTraining(Settlement town,int index)
        {
            if(town)Feedback(ExecuteBuilding(PlayerBuildingIntent.CancelLand(town.BuildingId,index)));
        }
        public void CancelTraining(Harbor harbor,int index,bool naval)
        {
            if(harbor)Feedback(ExecuteBuilding(PlayerBuildingIntent.CancelTraining(harbor.BuildingId,naval?ProductionQueueChannel.Naval:ProductionQueueChannel.Land,index)));
        }
        public void ClearCampRally()
        {
            if(SelectedCamp)Feedback(ExecuteBuilding(PlayerBuildingIntent.ClearRally(SelectedCamp.BuildingId)));
        }
    }
}
