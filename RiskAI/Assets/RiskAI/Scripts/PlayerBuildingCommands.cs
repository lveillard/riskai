using System;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Local command boundary for building production and rally changes. This is not a network authority layer.</summary>
    public sealed class PlayerBuildingCommands
    {
        readonly BattleSession session;
        public PlayerBuildingCommands(BattleSession session) { this.session=session; }

        public string Execute(int team,PlayerBuildingIntent intent)
        {
            string error=ValidateSession(team,intent.BuildingId);if(error!=null)return error;
            switch(intent.BuildingId.Kind)
            {
                case BuildingKind.Settlement:
                    var town=FindTown(intent.BuildingId);return town?ExecuteTown(team,town,intent):"El edificio ya no existe.";
                case BuildingKind.Harbor:
                    var harbor=FindHarbor(intent.BuildingId);return harbor?ExecuteHarbor(team,harbor,intent):"El edificio ya no existe.";
                case BuildingKind.CountryCamp:
                    var camp=FindCamp(intent.BuildingId);return camp?ExecuteCamp(team,camp,intent):"El edificio ya no existe.";
                default:return "Identificador de edificio inválido.";
            }
        }

        string ValidateSession(int team,BuildingId buildingId)
        {
            if(!session)return "No hay una batalla activa.";
            if(!buildingId.IsValid)return "Identificador de edificio inválido.";
            if(!PlayerRules.IsPlayer(team)||team>=session.PlayerCount)return "Bando inválido.";
            if(session.Winner>=0)return "La batalla ha terminado.";
            if(session.Paused)return "Reanuda la partida para dar esta orden.";
            return null;
        }
        Settlement FindTown(BuildingId id)
        {
            foreach(var town in session.Towns)if(town&&town.BuildingId==id)return town;
            return null;
        }
        Harbor FindHarbor(BuildingId id)
        {
            if(!session.Naval)return null;
            foreach(var harbor in session.Naval.Harbors)if(harbor&&harbor.BuildingId==id)return harbor;
            return null;
        }
        CountryCamp FindCamp(BuildingId id)
        {
            foreach(var camp in session.Camps)if(camp&&camp.BuildingId==id)return camp;
            return null;
        }
        static bool Finite(PlayerBuildingIntent intent) => !float.IsNaN(intent.RallyX)&&!float.IsNaN(intent.RallyY)&&!float.IsNaN(intent.RallyZ)&&!float.IsInfinity(intent.RallyX)&&!float.IsInfinity(intent.RallyY)&&!float.IsInfinity(intent.RallyZ);
        static Vector3 Rally(PlayerBuildingIntent intent) => new Vector3(intent.RallyX,intent.RallyY,intent.RallyZ);
        static string InvalidKind() => "Esta orden no corresponde a este edificio.";
        static string InvalidCancelIndex(int index) => index<0 ? "Índice de encargo inválido." : null;

        string ExecuteTown(int team,Settlement town,PlayerBuildingIntent intent)
        {
            if(town.State.Owner!=team)return "Selecciona una ciudad de tu bando.";
            switch(intent.Kind)
            {
                case PlayerBuildingIntentKind.RecruitUnit:
                    return ProductionCatalog.AllowsSettlementUnit(intent.Unit) ? town.Recruit(intent.Unit,team) : "Esta ciudad sólo recluta tropas regulares.";
                case PlayerBuildingIntentKind.CancelTraining:
                    return intent.QueueChannel!=ProductionQueueChannel.Land?InvalidKind():InvalidCancelIndex(intent.CancelIndex)??town.CancelTraining(intent.CancelIndex,team);
                case PlayerBuildingIntentKind.SetRally:
                    return intent.RallyDestination!=RallyDestination.Land?InvalidKind():!Finite(intent)?"Punto de reunión inválido.":town.SetRally(Rally(intent))?null:"El punto de reunión no es transitable.";
                case PlayerBuildingIntentKind.BuildTower:return town.BuildTower(team);
                default:return InvalidKind();
            }
        }
        string ExecuteHarbor(int team,Harbor harbor,PlayerBuildingIntent intent)
        {
            if(harbor.Owner!=team)return "Este puerto no pertenece a tu bando.";
            switch(intent.Kind)
            {
                case PlayerBuildingIntentKind.RecruitUnit:
                    return ProductionCatalog.AllowsHarborUnit(intent.Unit) ? harbor.RecruitLand(intent.Unit,team) : "Este puerto sólo recluta Marines.";
                case PlayerBuildingIntentKind.BuyShip:
                    return ProductionCatalog.AllowsHarborShip(intent.Ship) ? harbor.Buy((ShipKind)intent.Ship,team) : "Tipo de barco inválido.";
                case PlayerBuildingIntentKind.CancelTraining:
                    if(InvalidCancelIndex(intent.CancelIndex)!=null)return InvalidCancelIndex(intent.CancelIndex);
                    return intent.QueueChannel==ProductionQueueChannel.Land?harbor.CancelLandTraining(intent.CancelIndex,team):intent.QueueChannel==ProductionQueueChannel.Naval?harbor.CancelTraining(intent.CancelIndex,team):InvalidKind();
                case PlayerBuildingIntentKind.SetRally:
                    return intent.RallyDestination!=RallyDestination.Land?InvalidKind():!Finite(intent)?"Punto de reunión inválido.":harbor.SetRally(Rally(intent))?null:"El punto de reunión no es transitable.";
                case PlayerBuildingIntentKind.BuildTower:return harbor.BuildTower(team);
                default:return InvalidKind();
            }
        }
        string ExecuteCamp(int team,CountryCamp camp,PlayerBuildingIntent intent)
        {
            if(session.Economy.CountryOwner(camp.Country)!=team)return "Controla todo el país para gestionar esta hoguera.";
            switch(intent.Kind)
            {
                case PlayerBuildingIntentKind.SetRally:
                    return intent.RallyDestination!=RallyDestination.Land?InvalidKind():!Finite(intent)?"Punto de reunión inválido.":camp.SetRally(Rally(intent))?null:"El punto de reunión no es transitable.";
                case PlayerBuildingIntentKind.ClearRally:camp.ClearRally();return null;
                default:return InvalidKind();
            }
        }
    }
}
