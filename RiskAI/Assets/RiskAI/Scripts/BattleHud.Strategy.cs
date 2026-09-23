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
            DrawCountryLabels();
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
        GUIStyle countryLabelStyle,countryLabelShadow;
        GUIContent[] countryLabelContent;
        int[] countryLabelOrder;
        readonly System.Collections.Generic.List<Rect> placedCountryLabels=new System.Collections.Generic.List<Rect>(128);

        /// <summary>
        /// Country names at each territory's widest interior point. A name appears only when its
        /// country is wide enough on screen to hold it (so small countries fade in as the view
        /// zooms in), larger countries claim space first and overlapping names are skipped.
        /// </summary>
        void DrawCountryLabels()
        {
            var atlas=StrategicMapView.Current?StrategicMapView.Current.Atlas:null;
            if(atlas==null||atlas.LabelAnchors==null)return;
            int count=atlas.LabelAnchors.Length;
            if(countryLabelContent==null||countryLabelContent.Length!=count)
            {
                countryLabelContent=new GUIContent[count];
                for(int c=0;c<count;c++)countryLabelContent[c]=new GUIContent(GameText.Localize(MapLayout.Countries[c].Name).ToUpperInvariant());
                countryLabelOrder=new int[count];for(int c=0;c<count;c++)countryLabelOrder[c]=c;
                var radii=atlas.LabelRadii;System.Array.Sort(countryLabelOrder,(a,b)=>radii[b].CompareTo(radii[a])!=0?radii[b].CompareTo(radii[a]):a.CompareTo(b));
            }
            if(countryLabelStyle==null)
            {
                countryLabelStyle=new GUIStyle(RtsSkin.Small){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,wordWrap=false,clipping=TextClipping.Overflow};
                countryLabelShadow=new GUIStyle(countryLabelStyle);
            }
            placedCountryLabels.Clear();
            var previous=GUI.color;
            foreach(int c in countryLabelOrder)
            {
                var anchor=atlas.LabelAnchors[c];if(float.IsNaN(anchor.x))continue;
                var world=StrategicMapView.SurfaceAnchor(MapLayout.Point(anchor.x,anchor.y));
                var p=cam.WorldToScreenPoint(world);if(p.z<=0)continue;
                var edge=cam.WorldToScreenPoint(world+cam.transform.right*atlas.LabelRadii[c]);
                float radius=Vector2.Distance(p,edge)/Scale;
                var point=new Vector2(p.x/Scale,height-p.y/Scale);
                var size=countryLabelStyle.CalcSize(countryLabelContent[c]);
                float alpha=Mathf.Clamp01((radius*2.2f-size.x*.7f)/(size.x*.5f));
                if(alpha<=.02f)continue;
                var rect=new Rect(point.x-size.x*.5f-3,point.y-size.y*.5f,size.x+6,size.y);
                if(rect.yMin<TopPixels/Scale+26||rect.yMax>bottom-6||rect.xMin<2||rect.xMax>width-2)continue;
                bool overlaps=false;
                for(int i=0;i<placedCountryLabels.Count&&!overlaps;i++)overlaps=placedCountryLabels[i].Overlaps(rect);
                if(overlaps)continue;
                placedCountryLabels.Add(rect);
                countryLabelShadow.normal.textColor=new Color(0,0,0,.85f*alpha);
                countryLabelStyle.normal.textColor=new Color(1,.97f,.86f,.95f*alpha);
                GUI.color=Color.white;
                GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),countryLabelContent[c],countryLabelShadow);
                GUI.Label(rect,countryLabelContent[c],countryLabelStyle);
            }
            GUI.color=previous;
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
