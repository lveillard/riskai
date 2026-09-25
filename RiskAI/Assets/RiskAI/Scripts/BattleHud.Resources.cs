using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    public sealed partial class BattleHud
    {
        // Compact bars stay on one line: numbers only, the long form lives in the tooltip.
        string GoldText => DisplayedGold+" ORO"+(UiViewport.IsCompact?" +":" · +")+hud.Income;
        string PopulationText => UiViewport.IsCompact ? hud.RecruitmentReservations+"/"+BattleRules.PopulationLimit
            : hud.PlayerUnits[0]+" unidades · "+hud.RecruitmentReservations+"/"+BattleRules.PopulationLimit+" reclutadas";

        static Button ResourceButton(System.Action action,string name)
        {
            var button=new Button(action) { name=name, focusable=false };
            button.style.backgroundColor=Color.clear;button.style.borderTopWidth=button.style.borderBottomWidth=button.style.borderLeftWidth=button.style.borderRightWidth=0;
            button.style.marginLeft=button.style.marginRight=button.style.marginTop=button.style.marginBottom=0;
            button.style.paddingLeft=button.style.paddingRight=3;button.style.paddingTop=button.style.paddingBottom=0;
            button.style.height=UiViewport.IsTouchLayout?UiViewport.MinimumTouchTarget:34;button.style.minWidth=0;button.style.flexGrow=1;
            button.style.unityTextAlign=TextAnchor.MiddleLeft;
            return button;
        }

        void AddGoldDisplay(VisualElement parent)
        {
            var row=ResourceButton(ShowIncome,"HUD gold button");row.tooltip=GameText.Localize("Desglose del oro y del próximo ingreso");
            RtsUiStyle.Row(row);row.Add(new RtsGoldIcon());
            goldLabel=HeaderLabel(GoldText);goldLabel.name="HUD gold";goldLabel.style.color=RtsUiStyle.Gold;
            goldLabel.pickingMode=PickingMode.Ignore;row.Add(goldLabel);parent.Add(row);
            if(UiViewport.IsCompact)row.style.flexGrow=0;
        }

        /// <summary>Countdown to the next income with a dial, beside the gold. Round number stays compact ("R3") or in the tooltip.</summary>
        void AddIncomeDisplay(VisualElement parent)
        {
            var row=ResourceButton(ShowIncome,"HUD income countdown");RtsUiStyle.Row(row);
            row.tooltip=GameText.Localize(IncomeCountdown.Detail(session.Economy.Round,session.Economy.ElapsedInRound,hud.Income));
            if(UiViewport.IsCompact)row.style.flexGrow=0;
            incomeRing=new RtsIncomeRing();incomeRing.Progress=IncomeCountdown.Progress(session.Economy.ElapsedInRound);row.Add(incomeRing);
            roundLabel=HeaderLabel(IncomeCountdown.Label(session.Economy.Round,session.Economy.ElapsedInRound,UiViewport.IsCompact));
            roundLabel.name="HUD income countdown label";roundLabel.pickingMode=PickingMode.Ignore;row.Add(roundLabel);
            incomeButton=row;parent.Add(row);
        }

        void AddCitiesDisplay(VisualElement parent)
        {
            citiesLabel=AddMetric(parent,RtsGlyph.City,CitiesText,GameText.Localize("Ciudades controladas / total · abrir clasificación"),ShowPlayers,"HUD cities button");
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
                    label.text=GameText.Localize(MapLayout.Countries[country].Name+" · "+state.Owned+"/"+state.CityCount+" ciudades · +"+amount);
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

        void DrawBuildingName(Vector2 point,string name,int owner)
        {
            var style=RtsSkin.TownLabelFor(owner);
            // Measure the localized text (English names are longer) on a single line; very long
            // names are ellipsized instead of wrapping and being clipped by the plate.
            string shown=GameText.Localize(name);
            float width=style.CalcSize(new GUIContent(shown)).x;
            const float MaxLabel=210;
            if(width+14>MaxLabel)
            {
                while(shown.Length>4&&style.CalcSize(new GUIContent(shown+"…")).x+14>MaxLabel)shown=shown.Substring(0,shown.Length-1);
                shown=shown.TrimEnd()+"…";width=style.CalcSize(new GUIContent(shown)).x;
            }
            float size=Mathf.Clamp(width+14,48,MaxLabel);
            var rect=new Rect(point.x-size*.5f,point.y-2,size,19);
            // Subtle dark backing plate with a soft rim and an owner-coloured underline.
            RtsSkin.Fill(new Rect(rect.x-1,rect.y-1,rect.width+2,rect.height+2),new Color(0,0,0,.35f));
            RtsSkin.Fill(rect,new Color(.025f,.03f,.025f,.88f));
            var accent=PlayerRules.IsPlayer(owner)?VisualFactory.TeamColor(owner):new Color(.6f,.58f,.5f);accent.a=.75f;
            RtsSkin.Fill(new Rect(rect.x+3,rect.yMax-2,rect.width-6,1.5f),accent);
            GUI.Label(rect,shown,style);
        }

        void BuildingInfo(VisualElement root,System.Func<string> value)
        {
            var label=RtsUiStyle.Label(value(),"HUD building identity",UiViewport.IsCompact?11:14);
            label.style.color=RtsUiStyle.Gold;label.style.marginTop=0;label.style.marginBottom=4;
            // The compact info column beside the grid is narrow: wrap the name rather than cut it.
            if(UiViewport.IsCompact)label.style.whiteSpace=WhiteSpace.Normal;
            else {label.style.whiteSpace=WhiteSpace.NoWrap;label.style.overflow=Overflow.Hidden;label.style.textOverflow=TextOverflow.Ellipsis;}
            root.Add(label);liveContext.Add(()=>label.text=GameText.Localize(value()));
        }
    }
}
