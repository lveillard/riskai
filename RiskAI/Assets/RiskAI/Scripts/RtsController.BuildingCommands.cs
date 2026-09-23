using RiskAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskAI
{
    public sealed partial class RtsController
    {
        // Physical keys of ProductionHotkeys.GridKeys, cell order Q W E R / A S D F / Z X C V.
        static readonly Key[] ProductionGridKeys={Key.Q,Key.W,Key.E,Key.R,Key.A,Key.S,Key.D,Key.F,Key.Z,Key.X,Key.C,Key.V};
        int productionPage;
        PlayerBuildingCommands buildingCommands;

        /// <summary>
        /// The command card the grid hotkeys drive: the primary harbor's when a harbor
        /// is primary, otherwise the selected own cities', otherwise any own harbor.
        /// False while units (or nothing, or only enemy buildings) are selected.
        /// </summary>
        public bool TryGetProductionCard(out ProductionBuilding card)
        {
            // Runs every frame from the keyboard handler: plain loops, no LINQ allocation.
            bool city=false,harbor=false;
            foreach(var town in selectedTowns)if(town&&town.State.Owner==0){city=true;break;}
            foreach(var port in selectedHarbors)if(port&&port.Owner==0){harbor=true;break;}
            card=harbor&&(SelectedHarbor&&SelectedHarbor.Owner==0||!city)?ProductionBuilding.Harbor:ProductionBuilding.City;
            return city||harbor;
        }

        /// <summary>Visible page of a card; only cards with more than twelve products page.</summary>
        public int ProductionPage(ProductionBuilding card) =>
            Mathf.Clamp(productionPage,0,ProductionHotkeys.PageCount(card)-1);

        public void NextProductionPage(ProductionBuilding card)
        {
            int pages=ProductionHotkeys.PageCount(card);
            productionPage=pages>1?(ProductionPage(card)+1)%pages:0;
        }

        /// <summary>Same action as clicking that cell: page switch (V on overflowing cards) or a purchase.</summary>
        public void TriggerProductionCell(ProductionBuilding card,int cell)
        {
            if(ProductionHotkeys.PageCount(card)>1&&cell==ProductionHotkeys.PageCell){NextProductionPage(card);return;}
            if(ProductionHotkeys.TryFind(card,ProductionPage(card),cell,out var slot))Produce(slot.Option);
        }

        public void Produce(ProductionOption option)
        {
            if(option.IsShip)BuyShip((ShipKind)option.Ship);
            else Recruit(option.Unit);
        }

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
