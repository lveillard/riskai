using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public sealed partial class BattleHud
    {
        Vector2 queueScroll;
        int productionTab;
        void DrawStrategicSymbols()
        {
            Label(22,66,650,"VISTA ESTRATÉGICA · rombos: hogueras · acerca la rueda para combatir",RtsSkin.Small);
            foreach(var camp in session.Camps)
            {
                if(!camp)continue;
                var p=cam.WorldToScreenPoint(camp.SpawnPoint)/Scale;float y=height-p.y;
                if(p.z<=0||y<58||y>bottom-12)continue;
                var point=new Vector2(p.x,y);var previous=GUI.matrix;
                GUIUtility.RotateAroundPivot(45,point);
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
            var p=cam.WorldToScreenPoint(world)/Scale;float y=height-p.y;
            if(p.z<=0||p.x<5||p.x>width-5||y<TopPixels/Scale+4||y>bottom-10)return;
            var rect=new Rect(p.x-5,y-5,10,10);
            RtsSkin.Fill(new Rect(rect.x-2,rect.y-2,14,14),new Color(.025f,.04f,.04f));
            RtsSkin.Fill(rect,VisualFactory.TeamColor(owner));
            if(icon!=null){RtsSkin.Fill(new Rect(rect.x+3,rect.y-3,4,16),Color.white);RtsSkin.Fill(new Rect(rect.x-3,rect.y+7,16,3),Color.white);}
            if(selected)Outline(new Rect(p.x-10,y-10,20,20),RtsSkin.Gold);
            if(selected||new Rect(p.x-12,y-12,24,24).Contains(MousePoint))
            {var label=new Rect(p.x-95,y+14,190,24);RtsSkin.Fill(label,new Color(.025f,.035f,.035f,.92f));Text(label,name,RtsSkin.Center);}
        }
        void BuildingGroupDetails(float actions)
        {
            int count=controller.SelectedTowns.Count+controller.SelectedHarbors.Count;
            Label(253,bottom+12,660,count+" EDIFICIOS · COLAS DE PRODUCCIÓN",RtsSkin.Title);
            Label(253,bottom+40,650,"Cada compra añade una unidad a la cola compatible más corta.",RtsSkin.Tiny);
            var viewport=new Rect(253,bottom+69,actions-274,129);
            queueScroll=GUI.BeginScrollView(viewport,queueScroll,new Rect(0,0,viewport.width-20,count*42));
            int row=0;
            foreach(var town in controller.SelectedTowns)
            {
                if(!town)continue;float y=row++*42;
                if(Button(new Rect(0,y,174,34),town.DisplayName,"Centrar ciudad"))controller.Focus(town.transform.position);
                for(int i=0;i<5;i++)
                {
                    var r=new Rect(184+i*47,y,42,34);RtsSkin.Frame(r);
                    if(i>=town.QueueCount)continue;
                    Portrait(r,town.QueuedKind(i),i==0?RtsSkin.Gold:RtsSkin.Muted);
                    bool allowed=GUI.enabled;GUI.enabled=allowed&&town.State.Owner==0&&!session.Paused;
                    if(GUI.Button(r,GUIContent.none,GUIStyle.none))controller.Feedback(town.CancelTraining(i));GUI.enabled=allowed;
                    if(i==0)RtsSkin.Bar(new Rect(r.x,r.yMax-5,r.width,5),town.TrainingProgress,RtsSkin.Gold);
                }
            }
            foreach(var port in controller.SelectedHarbors)
            {
                if(!port)continue;float y=row++*42;
                if(Button(new Rect(0,y,174,34),port.DisplayName,"Centrar puerto"))controller.Focus(port.Landing);
                bool naval=productionTab==2;
                for(int i=0;i<5;i++)
                {
                    var r=new Rect(184+i*47,y,42,34);RtsSkin.Frame(r);
                    if(i>=(naval?port.QueueCount:port.LandQueueCount))continue;
                    if(naval)ShipQueueIcon(r,port.QueuedKind(i),RtsSkin.Gold);else Portrait(r,port.QueuedLandKind(i),RtsSkin.Gold);
                    bool allowed=GUI.enabled;GUI.enabled=allowed&&port.Owner==0&&!session.Paused;
                    if(GUI.Button(r,GUIContent.none,GUIStyle.none))controller.Feedback(naval?port.CancelTraining(i):port.CancelLandTraining(i));GUI.enabled=allowed;
                    if(i==0)RtsSkin.Bar(new Rect(r.x,r.yMax-5,r.width,5),naval?port.TrainingProgress:port.LandTrainingProgress,RtsSkin.Gold);
                }
            }
            GUI.EndScrollView();
            productionTab=GUI.SelectionGrid(new Rect(actions,bottom+12,610,34),productionTab,new[]{"EJÉRCITO","MARINA","BARCOS"},3,RtsSkin.Button);
            bool active=GUI.enabled;GUI.enabled=active&&!session.Paused;
            if(productionTab==2)
            {
                for(int i=0;i<2;i++)
                {var kind=(ShipKind)i;if(Button(new Rect(actions+i*303,bottom+62,295,66),Harbor.Profile(kind).Name+" · "+Harbor.Cost(kind)+" oro"))controller.Feedback(controller.TryBuySelected(kind));}
            }
            else
            {
                int amount=productionTab==0?6:3;
                for(int i=0;i<amount;i++)
                {
                    var kind=(UnitKind)(productionTab==0?i:(int)UnitKind.MarinePrivate+i);
                    var rect=new Rect(actions+(i%3)*205,bottom+58+i/3*64,197,57);
                    if(Button(rect,BattleRules.Name(kind)+"\n"+BattleRules.Cost(kind)+" oro"))controller.Feedback(controller.TryRecruitSelected(kind));
                }
            }
            GUI.enabled=active;
            Label(actions,bottom+186,615,"Clic derecho: salida común · pulsa un encargo para cancelarlo",RtsSkin.Tiny);
        }
    }
    static class SelectedPortLookup
    {
        public static bool ContainsPort(this System.Collections.Generic.IReadOnlyList<Harbor> ports,Harbor port)
        {for(int i=0;i<ports.Count;i++)if(ports[i]==port)return true;return false;}
    }
}
