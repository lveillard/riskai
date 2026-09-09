using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public sealed partial class BattleHud
    {
        /// <summary>Rotate a logical-space symbol before applying the screen-density transform.</summary>
        public static Matrix4x4 StrategicSymbolMatrix(Matrix4x4 previous,Vector2 point)
        {
            var pivot=new Vector3(point.x,point.y,0);
            // GUIUtility.RotateAroundPivot left-multiplies a screen-space rotation:
            // its unscaled pivot drifts when the HUD is scaled on high-DPI displays.
            return previous*Matrix4x4.Translate(pivot)*Matrix4x4.Rotate(Quaternion.Euler(0,0,45))*Matrix4x4.Translate(-pivot);
        }

        void DrawStrategicSymbols()
        {
            float titleLeft=UiViewport.SafeRect.xMin/Scale+12;
            Label(titleLeft,TopPixels/Scale+8,UiViewport.LogicalWidth-24,
                UiViewport.IsCompact?"VISTA ESTRATÉGICA · ◇ hogueras":"VISTA ESTRATÉGICA · rombos: hogueras · acerca el mapa para combatir",RtsSkin.Small);
            foreach(var camp in session.Camps)
            {
                if(!camp)continue;
                var p=cam.WorldToScreenPoint(StrategicMapView.SurfaceAnchor(camp.SpawnPoint))/Scale;float y=height-p.y;
                if(p.z<=0||y<TopPixels/Scale+4||y>bottom-12)continue;
                var point=new Vector2(p.x,y);var previous=GUI.matrix;
                GUI.matrix=StrategicSymbolMatrix(previous,point);
                RtsSkin.Fill(new Rect(point.x-5,point.y-5,10,10),new Color(.09f,.075f,.035f));
                RtsSkin.Fill(new Rect(point.x-3,point.y-3,6,6),camp.Selected?Color.white:RtsSkin.Gold);
                GUI.matrix=previous;
                if(camp.Selected)Text(new Rect(point.x-100,point.y-30,200,24),camp.DisplayName,RtsSkin.Center);
            }
            foreach(var town in session.Towns)
                MapSymbol(town.transform.position,town.State.Owner,town.Port?"⚓":null,town.Selected,town.DisplayName);
            if(session.Naval)foreach(var port in session.Naval.Harbors)
                if(!port.IsImportedPort)MapSymbol(port.Landing,port.Owner,"⚓",controller.SelectedHarbors.ContainsPort(port),port.DisplayName);
            if(controller.SelectedCamp)DrawCountryPorts(controller.SelectedCamp.Country);
        }
        void DrawCountryPorts(int country)
        {
            if(!session.Naval)return;
            foreach(var port in session.Naval.Harbors)
            {
                int member=port.LinkedTown?port.LinkedTown.State.Country:port.State.Country;
                if(member!=country)continue;
                MapSymbol(port.Landing,port.Owner,"⚓",true,port.DisplayName);
            }
        }
        void MapSymbol(Vector3 world,int owner,string icon,bool selected,string name)
        {
            var p=cam.WorldToScreenPoint(StrategicMapView.SurfaceAnchor(world))/Scale;float y=height-p.y;
            if(p.z<=0||p.x<5||p.x>width-5||y<TopPixels/Scale+4||y>bottom-10)return;
            var rect=new Rect(p.x-5,y-5,10,10);
            RtsSkin.Fill(new Rect(rect.x-2,rect.y-2,14,14),new Color(.025f,.04f,.04f));
            RtsSkin.Fill(rect,VisualFactory.TeamColor(owner));
            if(icon!=null){RtsSkin.Fill(new Rect(rect.x+3,rect.y-3,4,16),Color.white);RtsSkin.Fill(new Rect(rect.x-3,rect.y+7,16,3),Color.white);}
            if(selected)Outline(new Rect(p.x-10,y-10,20,20),RtsSkin.Gold);
            if(selected||new Rect(p.x-12,y-12,24,24).Contains(MousePoint))
            {var label=new Rect(p.x-95,y+14,190,24);RtsSkin.Fill(label,new Color(.025f,.035f,.035f,.92f));Text(label,name,RtsSkin.Center);}
        }
    }
    static class SelectedPortLookup
    {
        public static bool ContainsPort(this System.Collections.Generic.IReadOnlyList<Harbor> ports,Harbor port)
        {for(int i=0;i<ports.Count;i++)if(ports[i]==port)return true;return false;}
    }
}
