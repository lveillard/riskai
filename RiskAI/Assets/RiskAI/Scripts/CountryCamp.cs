using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace RiskAI
{
    /// <summary>Authored group rally and a territory overlay generated only when inspected.</summary>
    public sealed class CountryCamp : MonoBehaviour
    {
        public int Country { get; private set; }
        public Vector3 SpawnPoint => transform.position;
        public string DisplayName => MapLayout.Countries[Country].Name;
        public int Reinforcements => MapLayout.Countries[Country].PerTurn;
        public bool Selected { get; private set; }
        public bool HasRally => hasRally;
        public Vector3 RallyPoint => hasRally ? rallyPoint : SpawnPoint;

        BattleSession session;
        Renderer banner;
        LineRenderer rallyRing;
        long lastRefresh=-1;
        bool hasRally;
        int rallyOwner=-1;
        Vector3 rallyPoint;

        public static CountryCamp Create(BattleSession battle, int country, Transform parent)
        {
            Settlement anchor=null;
            foreach(var town in battle.Towns)if(town.State.Country==country){anchor=town;break;}
            if(!anchor)return null;
            var probe=MapLayout.IsImported?MapLayout.Countries[country].CampPoint:anchor.Rally+Vector3.left*6;
            if(!NavMesh.SamplePosition(probe,out var hit,8,NavMesh.AllAreas))return null;
            var go=new GameObject("Hoguera · "+MapLayout.Countries[country].Name);
            go.transform.SetParent(parent,false);go.transform.position=hit.position;
            var camp=go.AddComponent<CountryCamp>();camp.session=battle;camp.Country=country;
            WorldLife.AddCampfire(parent,hit.position+new Vector3(2.5f,0,3.3f));
            VisualFactory.Shape(go.transform,PrimitiveType.Cylinder,"Asta",new Vector3(1.1f,1.3f,0),new Vector3(.08f,1.3f,.08f),new Color(.3f,.19f,.09f));
            camp.banner=VisualFactory.Shape(go.transform,PrimitiveType.Cube,"Estandarte del grupo",new Vector3(1.55f,2.1f,0),new Vector3(.85f,.55f,.06f),Color.white).GetComponent<Renderer>();
            return camp;
        }

        /// <summary>Stores a walkable rally point for future country reinforcements.</summary>
        public bool SetRally(Vector3 point)
        {
            if(!NavMesh.SamplePosition(point,out var hit,8,NavMesh.AllAreas))return false;
            rallyPoint=hit.position;hasRally=true;rallyOwner=session.Economy.CountryOwner(Country);RefreshRallyView();return true;
        }
        public void ClearRally() { hasRally=false;RefreshRallyView(); }
        internal void ReconcileOwner(int owner){if(hasRally&&rallyOwner!=owner)ClearRally();}

        /// <summary>Applies this camp's explicit rally. Without one, reinforcements hold at the camp.</summary>
        public void ApplyRally(Soldier unit)
        {
            if(!unit||!unit.IsAlive)return;
            ReconcileOwner(session.Economy.CountryOwner(Country));
            if(hasRally)unit.MoveTo(rallyPoint,true,false);
            else unit.HoldPosition();
        }

        void Update()
        {
            if(!session||lastRefresh==session.Clock.TickCount/20)return;
            lastRefresh=session.Clock.TickCount/20;
            banner.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamColor(session.Economy.CountryOwner(Country)));
        }
        void RefreshRallyView()
        {
            if(!rallyRing && hasRally)
                rallyRing=VisualFactory.Ring(transform,.8f,.065f,VisualFactory.TeamColor(rallyOwner));
            if(!rallyRing)return;
            rallyRing.enabled=Selected&&hasRally&&rallyOwner==0;
            rallyRing.transform.position=rallyPoint;
        }
        public void Select(bool selected)
        {
            Selected=selected;
            RefreshRallyView();
            if(StrategicMapView.Current)
                StrategicMapView.Current.SelectCountry(selected?Country:-1);
        }
    }
}
