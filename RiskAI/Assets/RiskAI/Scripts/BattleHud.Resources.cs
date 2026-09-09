using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    public sealed partial class BattleHud
    {
        string GoldText => hud.Gold+" ORO"+(UiViewport.IsPortrait?"\n+":" · +")+hud.Income;
        string PopulationText => hud.PlayerUnits[0]+" unidades"+(UiViewport.IsPortrait?"\n":" · ")+hud.RecruitmentReservations+"/"+BattleRules.PopulationLimit+" reclutadas";

        static Button ResourceButton(System.Action action,string name)
        {
            var button=new Button(action) { name=name };
            button.style.backgroundColor=Color.clear;button.style.borderTopWidth=button.style.borderBottomWidth=button.style.borderLeftWidth=button.style.borderRightWidth=0;
            button.style.marginLeft=button.style.marginRight=button.style.marginTop=button.style.marginBottom=0;
            button.style.paddingLeft=button.style.paddingRight=3;button.style.paddingTop=button.style.paddingBottom=0;
            button.style.height=34;button.style.minWidth=0;button.style.flexGrow=1;
            button.style.unityTextAlign=TextAnchor.MiddleLeft;
            return button;
        }

        void AddGoldDisplay(VisualElement parent)
        {
            var row=ResourceButton(ShowIncome,"HUD gold button");row.tooltip="Desglose del oro y del próximo ingreso";
            RtsUiStyle.Row(row);row.Add(new RtsGoldIcon());
            goldLabel=HeaderLabel(GoldText);goldLabel.name="HUD gold";goldLabel.style.color=RtsUiStyle.Gold;
            goldLabel.pickingMode=PickingMode.Ignore;row.Add(goldLabel);parent.Add(row);
        }

        void AddCitiesDisplay(VisualElement parent)
        {
            citiesLabel=AddMetric(parent,RtsHudGlyph.City,CitiesText,"Ciudades controladas / total · abrir clasificación",ShowPlayers,"HUD cities button");
        }

        public void ShowIncome() { controller.CancelCursor();menuTab=3;controller.HelpVisible=true;BuildRetainedUi(false); }
        void ShowPopulation() { controller.CancelCursor();menuTab=4;controller.HelpVisible=true;BuildRetainedUi(false); }

        void BuildIncome(VisualElement root)
        {
            AddTitle(root,"DESGLOSE DEL ORO");
            var breakdown=new Dictionary<int,int>();
            int basic=0,total=0;
            System.Action read=()=>total=session.Economy.IncomeBreakdown(0,breakdown,out basic);
            read();liveContext.Add(read);
            LiveInfo(root,()=>"Disponible: "+session.Economy.Gold[0]+" oro");
            LiveInfo(root,()=>"Próxima ronda: +"+total+" oro en "+Mathf.CeilToInt(BattleRules.RoundSeconds-session.Economy.ElapsedInRound)+" s");
            LiveInfo(root,()=>"Ingreso básico: +"+basic);
            AddInfo(root,"Cada ciudad de un país completo aporta oro. Las ciudades de países incompletos no añaden ingresos.");
            for(int i=0;i<MapLayout.Countries.Length;i++)
            {
                int country=i;
                var label=RtsUiStyle.Label("",null,12);label.style.whiteSpace=WhiteSpace.Normal;label.style.marginBottom=7;root.Add(label);
                void Refresh()
                {
                    var state=hud.Countries[country];
                    breakdown.TryGetValue(country,out int amount);
                    label.text=MapLayout.Countries[country].Name+" · "+state.Owned+"/"+state.CityCount+" ciudades · +"+amount;
                    label.style.display=state.Owned>0?DisplayStyle.Flex:DisplayStyle.None;
                    label.style.color=amount>0?RtsUiStyle.Gold:RtsUiStyle.Muted;
                }
                Refresh();liveContext.Add(Refresh);
            }
            AddInfo(root,"Las bajas enemigas conceden aparte ¼ de su valor de recompensa; las fracciones se acumulan hasta completar una moneda. No forman parte del ingreso por ronda.");
        }

        void BuildPopulation(VisualElement root)
        {
            AddTitle(root,"UNIDADES");
            LiveInfo(root,()=>"Total: "+hud.PlayerUnits[0]+" unidades");
            LiveInfo(root,()=>"Reclutamiento: "+hud.RecruitmentReservations+" / "+BattleRules.PopulationLimit+" plazas, incluidos los encargos pendientes.");
            AddInfo(root,"El total incluye soldados, defensores de ciudades y barcos. Los defensores y barcos no consumen plazas de reclutamiento.");
        }

        static VisualElement PortraitFrame(string resource,float size)
        {
            var frame=new VisualElement { name="Portrait frame",pickingMode=PickingMode.Ignore };
            frame.style.width=frame.style.height=size;frame.style.flexShrink=0;frame.style.alignSelf=Align.Center;
            frame.style.backgroundColor=RtsUiStyle.Slate;
            frame.style.borderTopWidth=frame.style.borderBottomWidth=frame.style.borderLeftWidth=frame.style.borderRightWidth=1;
            frame.style.borderTopColor=frame.style.borderLeftColor=RtsUiStyle.Bronze;
            frame.style.borderBottomColor=frame.style.borderRightColor=new Color(.3f,.23f,.12f);
            var portrait=new Image { image=CachedPortrait(resource),scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore };
            portrait.style.flexGrow=1;portrait.style.width=Length.Percent(100);portrait.style.height=Length.Percent(100);
            frame.Add(portrait);return frame;
        }

        static Button PurchaseButton(string name,string resource,string title,string cost,System.Action action,bool enabled=true)
        {
            var button=RtsUiStyle.Button("",action,name);
            button.SetEnabled(enabled);
            button.AddToClassList("riskai-purchase-card");button.tooltip=title+" · "+cost;
            bool landscape=UiViewport.IsCompact&&!UiViewport.IsPortrait;
            button.style.width=Length.Percent(UiViewport.IsPortrait?48:31);
            button.style.minWidth=0;button.style.height=button.style.minHeight=button.style.maxHeight=UiViewport.IsPortrait?54:landscape?50:60;
            button.style.marginLeft=button.style.marginTop=0;button.style.marginRight=5;button.style.marginBottom=5;
            button.style.paddingLeft=button.style.paddingRight=4;button.style.paddingTop=button.style.paddingBottom=3;
            RtsUiStyle.Row(button);
            var frame=PortraitFrame(resource,landscape?36:40);frame.style.marginRight=6;button.Add(frame);
            var text=new VisualElement { pickingMode=PickingMode.Ignore };text.style.flexGrow=1;text.style.minWidth=0;
            var heading=RtsUiStyle.Label(title,null,11);heading.style.whiteSpace=WhiteSpace.Normal;heading.pickingMode=PickingMode.Ignore;
            var price=RtsUiStyle.Label(cost,null,10);price.style.whiteSpace=WhiteSpace.Normal;price.style.color=RtsUiStyle.Gold;price.pickingMode=PickingMode.Ignore;
            text.Add(heading);text.Add(price);button.Add(text);return button;
        }

        void DrawBuildingName(Vector2 point,string name,int owner)
        {
            var style=RtsSkin.TownLabelFor(owner);
            float size=Mathf.Clamp(style.CalcSize(new GUIContent(name)).x+14,48,148);
            var rect=new Rect(point.x-size*.5f,point.y-2,size,19);
            RtsSkin.Fill(rect,new Color(.025f,.035f,.025f,.86f));Text(rect,name,style);
        }

        void BuildingInfo(VisualElement root,System.Func<string> value)
        {
            var label=RtsUiStyle.Label(value(),"HUD building identity",UiViewport.IsCompact?11:14);
            label.style.color=RtsUiStyle.Gold;label.style.marginTop=0;label.style.marginBottom=4;
            label.style.whiteSpace=WhiteSpace.NoWrap;label.style.overflow=Overflow.Hidden;label.style.textOverflow=TextOverflow.Ellipsis;
            root.Add(label);liveContext.Add(()=>label.text=value());
        }
    }
}
