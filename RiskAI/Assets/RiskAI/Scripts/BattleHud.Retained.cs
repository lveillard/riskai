using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>Retained interactive battle chrome. World-space presentation remains in BattleHud.OnGUI.</summary>
    public sealed partial class BattleHud
    {
        RtsUiRuntime retainedUi;
        VisualElement retainedRoot, header, footer, context, wideContext, modal;
        Label goldLabel, citiesLabel, populationLabel, roundLabel;
        VisualElement startCountdown;
        Label startCountdownNumber;
        Button pauseButton;
        int retainedTab;
        bool showMinimap = true;
        int retainedContextKey;
        bool lastCompact, lastPortrait, lastFooterVisible;
        int lastModalKind;
        int ModalKind => session.Winner >= 0 ? 3 : controller.ScoreboardVisible ? 2 : controller.HelpVisible ? 1 : 0;
        Vector2 lastScreen;
        Rect lastSafe;
        float nextRetainedLabelRefresh;
        readonly List<RankingRow> rankingRows = new List<RankingRow>();
        readonly List<System.Action> liveContext = new List<System.Action>();
        readonly List<int> retainedRosterIds = new List<int>();
        int retainedSoldierCount;
        Button modalPauseButton;
        float lastDensity;
        bool wideFooter;
        bool HasSelection => controller && (controller.SelectedTown || controller.SelectedHarbor || controller.SelectedCamp || controller.InspectedTarget || controller.Selection.Count + controller.Fleet.Count > 0);
        bool FooterVisible => HasSelection || (showMinimap && retainedTab == 3);
        bool MinimapVisible => showMinimap && FooterVisible && (!UiViewport.IsPortrait || retainedTab == 3);

        float RequestedHeaderHeight => !UiViewport.IsCompact ? 48 : UiViewport.IsPortrait ? 76 : 44;
        float RequestedFooterHeight => FooterVisible ? UiViewport.IsPortrait ? 210 : 188 : 0;
        float HeaderHeight => Mathf.Max(0, (UiViewport.SafeRect.yMax - UiViewport.WorldRect.yMax) / UiViewport.Scale);
        float FooterHeight => Mathf.Max(0, (UiViewport.BottomPixels - UiViewport.SafeRect.yMin) / UiViewport.Scale);

        void ConfigureViewport()
        {
            UiViewport.SetHudHeights(RequestedHeaderHeight, RequestedFooterHeight);
        }

        void InitializeRetainedUi()
        {
            retainedUi = RtsUiRuntime.Attach(gameObject, "Battle HUD", 20);
            showMinimap = !UiViewport.IsPortrait;
            BuildRetainedUi(true);
        }

        void RefreshRetainedUi()
        {
            if (!retainedUi || !session || !controller) return;
            RefreshStartCountdown();
            int modalKind=ModalKind;
            int contextKey = ContextKey();
            bool orientationChanged = lastPortrait != UiViewport.IsPortrait;
            bool resized = lastScreen != new Vector2(Screen.width, Screen.height) || lastSafe != UiViewport.SafeRect || lastDensity != UiViewport.Scale;
            if (lastCompact != UiViewport.IsCompact || orientationChanged || lastModalKind != modalKind || lastFooterVisible != FooterVisible || resized)
            {
                if (orientationChanged) { showMinimap = !UiViewport.IsPortrait; if(retainedTab==3)retainedTab=0; }
                BuildRetainedUi(false);
                return;
            }
            if (retainedContextKey != contextKey || RosterChanged())
            {
                retainedContextKey = contextKey;
                RebuildContext();
            }
            if (Time.unscaledTime < nextRetainedLabelRefresh) return;
            nextRetainedLabelRefresh = Time.unscaledTime + .1f;
            UpdateRetainedLabels();
        }

        void UpdateRetainedLabels()
        {
            if (goldLabel != null) goldLabel.text = GoldText;
            if (citiesLabel != null) citiesLabel.text = CitiesText;
            if (populationLabel != null) populationLabel.text = PopulationText;
            if (roundLabel != null) roundLabel.text = "RONDA " + hud.Round + " · " + Mathf.CeilToInt(BattleRules.RoundSeconds - hud.RoundElapsed) + " s";
            if (pauseButton != null) { pauseButton.text = session.Paused ? "Continuar" : "Pausa";pauseButton.SetEnabled(!session.IsStarting); }
            if (modalPauseButton != null) { modalPauseButton.text = session.Paused ? "CONTINUAR" : "PAUSA";modalPauseButton.SetEnabled(!session.IsStarting); }
            for(int i=0;i<liveContext.Count;i++)liveContext[i]();
            UpdateRankingLabels();
        }

        void RebuildContext()
        {
            if (context == null) return;
            context.Clear(); liveContext.Clear();
            RememberRoster();
            if (wideFooter) { wideContext.Clear(); BuildWideContext(context, wideContext); }
            else BuildContext(context);
        }

        int ContextKey()
        {
            unchecked
            {
                int key = retainedTab;
                key = key * 29 + (controller.InspectedTarget ? controller.InspectedTarget.EntityId : 0);
                key = key * 41 + (controller.SelectedTown ? controller.SelectedTown.GetInstanceID() * 17 + controller.SelectedTown.State.Owner : 0);
                key = key * 43 + (controller.SelectedHarbor ? controller.SelectedHarbor.GetInstanceID() * 17 + controller.SelectedHarbor.Owner : 0);
                key = key * 47 + (controller.SelectedCamp ? controller.SelectedCamp.GetInstanceID() : 0);
                for (int i = 0; i < controller.SelectedTowns.Count; i++) key = key * 53 + controller.SelectedTowns[i].GetInstanceID() * 17 + controller.SelectedTowns[i].State.Owner;
                for (int i = 0; i < controller.SelectedHarbors.Count; i++) key = key * 59 + controller.SelectedHarbors[i].GetInstanceID() * 17 + controller.SelectedHarbors[i].Owner;
                return key;
            }
        }

        void UpdateRankingLabels()
        {
            if(rankingRows.Count==0)return;
            var order = RankedPlayers();
            for (int rank=0;rank<rankingRows.Count && rank<order.Count;rank++)
            {
                int player=order[rank];var row=rankingRows[rank];
                bool eliminated=session.IsPlayerEliminated(player);
                row.Name.text=(rank+1)+". "+VisualFactory.TeamName(player)+(eliminated?" · ELIMINADO":"");
                row.Root.style.borderLeftColor=VisualFactory.TeamColor(player);
                row.Root.style.opacity=eliminated?.55f:1;
                row.Cities.text=hud.PlayerCities[player].ToString();
                row.Units.text=hud.PlayerUnits[player].ToString();
            }
        }

        void BuildRetainedUi(bool initial)
        {
            ConfigureViewport();
            liveContext.Clear(); rankingRows.Clear(); modalPauseButton=null;
            context=null;wideContext=null;footer=null;
            lastCompact = UiViewport.IsCompact; lastPortrait = UiViewport.IsPortrait;
            lastModalKind=ModalKind;lastFooterVisible=FooterVisible;
            retainedContextKey = ContextKey(); RememberRoster(); lastScreen = new Vector2(Screen.width, Screen.height); lastSafe = UiViewport.SafeRect; lastDensity=UiViewport.Scale; nextRetainedLabelRefresh = Time.unscaledTime;
            retainedRoot = new VisualElement { name = "Battle retained UI", pickingMode = PickingMode.Ignore };
            retainedRoot.style.flexGrow = 1;
            BuildHeader(retainedRoot);
            if(FooterVisible) BuildFooter(retainedRoot);
            BuildWorldQueues(retainedRoot);
            if (MinimapVisible) BuildMinimapHitOverlay(retainedRoot);
            if (lastModalKind!=0) BuildModal(retainedRoot);
            BuildStartCountdown(retainedRoot);
            retainedUi.SetContent(retainedRoot);
        }

        void BuildStartCountdown(VisualElement root)
        {
            startCountdown=RtsUiStyle.Panel("Match start countdown");startCountdown.pickingMode=PickingMode.Ignore;
            startCountdown.style.position=Position.Absolute;startCountdown.style.left=Length.Percent(50);
            startCountdown.style.top=Length.Percent(34);startCountdown.style.width=236;startCountdown.style.marginLeft=-118;
            startCountdown.style.alignItems=Align.Center;startCountdown.style.paddingTop=14;startCountdown.style.paddingBottom=14;
            var title=RtsUiStyle.Label("LA CONQUISTA EMPIEZA EN",null,12);title.pickingMode=PickingMode.Ignore;
            startCountdown.Add(title);
            startCountdownNumber=RtsUiStyle.Title("3","Countdown number",48);startCountdownNumber.pickingMode=PickingMode.Ignore;
            startCountdown.Add(startCountdownNumber);root.Add(startCountdown);RefreshStartCountdown();
        }

        void RefreshStartCountdown()
        {
            if(startCountdown==null)return;
            startCountdown.style.display=session.IsStarting?DisplayStyle.Flex:DisplayStyle.None;
            if(session.IsStarting)startCountdownNumber.text=Mathf.CeilToInt(session.StartCountdownRemaining).ToString();
        }

        void BuildHeader(VisualElement root)
        {
            header = RtsUiStyle.Panel("HUD header"); header.style.position = Position.Absolute;
            header.style.left = 0; header.style.top = 0; header.style.right = 0; header.style.height = HeaderHeight;
            header.style.paddingLeft = 6; header.style.paddingRight = 6; header.style.paddingTop = UiViewport.IsPortrait?1:2; header.style.paddingBottom = UiViewport.IsPortrait?1:2;
            if (!UiViewport.IsCompact) BuildWideHeader(); else BuildCompactHeader();
            root.Add(header);
        }

        void BuildWideHeader()
        {
            RtsUiStyle.Row(header);
            var title = RtsUiStyle.Title("DOMINIOS", null, 17); header.Add(title);
            AddGoldDisplay(header);
            AddCitiesDisplay(header);
            AddPopulationDisplay(header);
            roundLabel = HeaderLabel("RONDA " + hud.Round); roundLabel.style.flexGrow = 1; header.Add(roundLabel);
            header.Add(HeaderButton("Ranking", ShowPlayers));
            header.Add(HeaderButton(MinimapVisible ? "Ocultar mapa" : "Mapa", ToggleMinimap));
            pauseButton = HeaderButton(session.Paused ? "Continuar" : "Pausa", () => { session.TogglePause(); UpdateRetainedLabels(); }); header.Add(pauseButton);
            header.Add(HeaderButton("Menú", OpenMenu));
        }

        void BuildCompactHeader()
        {
            var top = new VisualElement { name="HUD resource row" }; RtsUiStyle.Row(top);top.style.height=UiViewport.IsPortrait?36:38;top.style.flexShrink=0;
            AddGoldDisplay(top);
            AddCitiesDisplay(top);
            AddPopulationDisplay(top);
            if(!UiViewport.IsPortrait)
            {
                roundLabel=HeaderLabel("RONDA "+hud.Round);top.Add(roundLabel);
                top.Add(HeaderButton("Mapa", ToggleMinimap));
                top.Add(HeaderButton("Menú", OpenMenu));
            }
            header.Add(top);
            if (UiViewport.IsPortrait)
            {
                var lower = new VisualElement { name="HUD navigation row" }; RtsUiStyle.Row(lower);lower.style.height=34;lower.style.flexShrink=0;
                roundLabel = HeaderLabel("RONDA " + hud.Round); roundLabel.style.flexGrow = 1; lower.Add(roundLabel);
                lower.Add(HeaderButton("Mapa", ToggleMinimap));
                lower.Add(HeaderButton("Menú", OpenMenu));
                header.Add(lower);
            }
        }

        Label HeaderLabel(string text)
        {
            var label = RtsUiStyle.Label(text, null, 11); label.style.marginLeft = 4; label.style.marginRight = 4; label.style.marginTop=0;label.style.marginBottom=0;return label;
        }
        string CitiesText => hud.OwnedTowns + " / " + MapLayout.Towns.Length;

        static Button HeaderButton(string text, System.Action action)
        {
            var button = RtsUiStyle.Button(text, action);
            button.style.height=button.style.minHeight=UiViewport.IsPortrait?32:36;
            button.style.paddingTop=2;button.style.paddingBottom=2;button.style.marginBottom=0;button.style.marginTop=0;button.style.marginLeft=0;button.style.marginRight=4;
            button.style.fontSize=11;button.style.flexShrink=0;
            return button;
        }

        void OpenMenu() { controller.CancelCursor(); menuTab = 0; controller.HelpVisible = true; BuildRetainedUi(false); }
        void CloseModal() { controller.CancelCursor(); controller.HelpVisible = false; menuTab = 0; BuildRetainedUi(false); }
        void ToggleMinimap()
        {
            bool visible=MinimapVisible;showMinimap=!visible;retainedTab=visible?0:3;
            BuildRetainedUi(false);
        }

        void BuildFooter(VisualElement root)
        {
            footer = RtsUiStyle.Panel("HUD footer"); footer.style.position = Position.Absolute;
            footer.style.left = MinimapVisible && !UiViewport.IsPortrait ? MinimapRect().xMax + 8 - UiViewport.SafeRect.xMin/UiViewport.Scale : 0; footer.style.right = 0; footer.style.bottom = 0;
            footer.style.height = FooterHeight;
            footer.style.paddingTop=6;footer.style.paddingBottom=6;
            wideFooter = !UiViewport.IsCompact;
            if (!wideFooter)
            {
                context = new ScrollView(ScrollViewMode.Vertical) { name = "HUD context" }; context.style.flexGrow = 1;context.style.minHeight=0; RtsUiStyle.ConfigureScroll((ScrollView)context);
                wideContext = null;
            }
            else
            {
                var columns = new VisualElement { name = "HUD wide columns" }; RtsUiStyle.Row(columns); columns.style.flexGrow = 1;columns.style.minHeight=0;columns.style.alignItems=Align.Stretch;
                context = new ScrollView(ScrollViewMode.Vertical) { name = "HUD selection column" }; context.style.width = Length.Percent(43); context.style.flexShrink = 0; RtsUiStyle.ConfigureScroll((ScrollView)context);
                wideContext = new ScrollView(ScrollViewMode.Vertical) { name = "HUD contextual column" }; wideContext.style.flexGrow = 1;wideContext.style.minWidth=0; wideContext.style.marginLeft = 16; RtsUiStyle.ConfigureScroll((ScrollView)wideContext);
                wideContext.style.paddingLeft=14;wideContext.style.borderLeftWidth=1;wideContext.style.borderLeftColor=RtsUiStyle.Bronze;
                ((ScrollView)context).horizontalScrollerVisibility=ScrollerVisibility.Hidden;
                ((ScrollView)wideContext).horizontalScrollerVisibility=ScrollerVisibility.Hidden;
                context.style.minHeight=0;wideContext.style.minHeight=0;
                columns.Add(context); columns.Add(wideContext); footer.Add(columns);
            }

            if (wideFooter) BuildWideContext(context, wideContext); else BuildContext(context);
            if (UiViewport.IsPortrait && MinimapVisible) context.style.visibility=Visibility.Hidden;
            if (!wideFooter) footer.Add(context); root.Add(footer);
        }

        void BuildWideContext(VisualElement selection, VisualElement contextual)
        {
            BuildSelectionWithPortrait(selection);
            if (controller.SelectedTown || controller.SelectedHarbor || controller.SelectedTowns.Count + controller.SelectedHarbors.Count > 0)
                BuildProduction(contextual);
            else if (controller.SelectedCamp)
                BuildCampOrders(contextual);
            else if(controller.Selection.Count+controller.Fleet.Count>0)
                BuildOrders(contextual);
        }

        void BuildCampOrders(VisualElement root)
        {
            AddTitle(root, "ÓRDENES DE HOGUERA");
            AddInfo(root, "Clic derecho en terreno fija la salida de los refuerzos del país.");
            root.Add(GridButton("Borrar salida", controller.ClearCampRally));
        }

        void BuildSelectionWithPortrait(VisualElement root)
        {
            Soldier unit = controller.Selection.Count == 1 && controller.Fleet.Count == 0 ? controller.Selection[0] : controller.InspectedTarget as Soldier;
            if (!unit) { BuildSelection(root);return; }
            var row=new VisualElement();RtsUiStyle.Row(row);row.style.alignItems=Align.FlexStart;
            var frame=RtsUiStyle.Panel("HUD portrait frame");frame.style.paddingLeft=5;frame.style.paddingRight=5;frame.style.paddingTop=4;frame.style.paddingBottom=4;frame.style.marginRight=8;frame.style.flexShrink=0;
            var portrait = new Image { name = "HUD unit portrait", image = CachedPortrait(PortraitResource(unit.Kind)), scaleMode = ScaleMode.ScaleToFit };
            portrait.style.width = 64; portrait.style.height = 72;
            frame.Add(portrait);row.Add(frame);
            var details=new VisualElement();details.style.flexGrow=1;details.style.minWidth=0;BuildSelection(details);row.Add(details);root.Add(row);
        }

        static string PortraitResource(UnitKind kind) => "Portraits/" + (kind == UnitKind.Guard ? "MountedKnight" : BattleRules.Model(kind));

        Rect MinimapRect()
        {
            float mapHeight = Mathf.Min(156, Mathf.Max(80, FooterHeight - 52));
            float mapWidth = Mathf.Min(208, mapHeight * (208f / 156f));
            float safeTop = (Screen.height - UiViewport.SafeRect.yMax) / UiViewport.Scale;
            float footerTop = safeTop + UiViewport.LogicalHeight - FooterHeight;
            float x = UiViewport.IsPortrait ? UiViewport.SafeRect.center.x/UiViewport.Scale-mapWidth*.5f : UiViewport.SafeRect.xMin/UiViewport.Scale+16;
            float y = UiViewport.IsPortrait ? footerTop + FooterHeight - mapHeight - 8 : footerTop + 23;
            return new Rect(x, y, mapWidth, mapHeight);
        }

        void BuildMinimapHitOverlay(VisualElement root)
        {
            Rect map = MinimapRect();
            float scale = UiViewport.Scale;
            var hit = new VisualElement { name = "HUD minimap input", pickingMode = PickingMode.Position };
            hit.style.position = Position.Absolute;
            hit.style.left = map.x - UiViewport.SafeRect.xMin / scale;
            hit.style.top = map.y - (Screen.height - UiViewport.SafeRect.yMax) / scale;
            hit.style.width = map.width; hit.style.height = map.height;
            hit.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (controller == null || controller.HelpVisible) return;
                float x = Mathf.Clamp01(evt.localPosition.x / Mathf.Max(1, map.width));
                float y = Mathf.Clamp01(evt.localPosition.y / Mathf.Max(1, map.height));
                Vector3 point = MapLayout.Point(Mathf.Lerp(MapLayout.PlayableMin.x, MapLayout.PlayableMax.x, x), Mathf.Lerp(MapLayout.PlayableMax.y, MapLayout.PlayableMin.y, y));
                if (evt.button == 0 && controller.OrderCursor) controller.OrderAt(point, controller.AttackCursor);
                else if (evt.button == 0) controller.Focus(point);
                else if (evt.button == 1) controller.OrderAt(point, controller.AttackCursor);
                evt.StopPropagation();
            });
            root.Add(hit);
        }

        void BuildContext(VisualElement root)
        {
            // Commands and purchases are available immediately after selecting their owner.
            bool building=controller.SelectedTowns.Count+controller.SelectedHarbors.Count>0;
            if(building)
            {
                BuildSelection(root);
                BuildProduction(root);
                return;
            }
            if(controller.Selection.Count+controller.Fleet.Count>0)BuildOrders(root);
            BuildSelectionWithPortrait(root);
        }

        void BuildSelection(VisualElement root)
        {
            if (controller.SelectedCamp)
            {
                var camp = controller.SelectedCamp; AddTitle(root, camp.DisplayName.ToUpperInvariant());
                LiveInfo(root, () => camp.HasRally ? "Salida fijada. Clic derecho cambia el punto de reunión." : "Los refuerzos esperan en la hoguera hasta fijar una salida.");
                var clearRally = RtsUiStyle.Button("BORRAR SALIDA", controller.ClearCampRally);
                root.Add(clearRally);
                System.Action refreshRally = () => clearRally.style.display = camp.HasRally ? DisplayStyle.Flex : DisplayStyle.None;
                liveContext.Add(refreshRally); refreshRally();
                var group = hud.Countries[camp.Country]; LiveInfo(root, () => "HOGUERA · " + group.Owned + " / " + group.CityCount + " ciudades");
                foreach (var town in group.Cities)
                    if (town)
                    {
                        var button = RtsUiStyle.Button("", () => { controller.SelectTown(town); controller.Focus(town.transform.position); });
                        System.Action refreshTown = () => button.text = town ? town.DisplayName + " · " + VisualFactory.TeamName(town.State.Owner) : "Ciudad retirada";
                        root.Add(button); liveContext.Add(refreshTown); refreshTown();
                    }
                return;
            }
            if (controller.SelectedTowns.Count + controller.SelectedHarbors.Count > 1)
            {
                AddTitle(root, "EDIFICIOS SELECCIONADOS · " + (controller.SelectedTowns.Count + controller.SelectedHarbors.Count));
                for (int i = 0; i < controller.SelectedTowns.Count; i++) AddInfo(root, controller.SelectedTowns[i].DisplayName);
                for (int i = 0; i < controller.SelectedHarbors.Count; i++) AddInfo(root, controller.SelectedHarbors[i].DisplayName);
                return;
            }
            if (controller.SelectedTown && !controller.SelectedHarbor)
            {
                var town = controller.SelectedTown;
                BuildingInfo(root,()=>town?town.DisplayName+" · "+VisualFactory.TeamName(town.State.Owner):"Ciudad retirada"); return;
            }
            if (controller.SelectedHarbor)
            {
                var harbor = controller.SelectedHarbor;
                BuildingInfo(root,()=>harbor?harbor.DisplayName+" · "+VisualFactory.TeamName(harbor.Owner):"Puerto retirado"); return;
            }
            if (controller.Fleet.Count > 0)
            {
                int count = controller.Selection.Count + controller.Fleet.Count;
                AddTitle(root, count == 1 ? controller.Fleet[0].DisplayName : count + " UNIDADES SELECCIONADAS");
                BuildSelectionRoster(root);
                return;
            }
            if (controller.InspectedTarget is Soldier inspected)
            {
                var profile = BattleRules.Profile(inspected.Kind);
                AddTitle(root, BattleRules.Name(inspected.Kind).ToUpperInvariant());
                LiveInfo(root,()=>SoldierStats(inspected));
                return;
            }
            if (controller.Selection.Count > 0)
            {
                AddTitle(root, controller.Selection.Count==1?BattleRules.Name(controller.Selection[0].Kind).ToUpperInvariant():controller.Selection.Count + " TROPAS SELECCIONADAS");
                if (controller.Selection.Count == 1)
                {
                    var unit = controller.Selection[0]; var profile = BattleRules.Profile(unit.Kind);
                    LiveInfo(root,()=>SoldierStats(unit));
                }
                else BuildSelectionRoster(root);
                return;
            }

        }

        void BuildOrders(VisualElement root)
        {
            var grid=new VisualElement { name="HUD direct actions" };
            var row=new VisualElement { name="HUD primary action row" };RtsUiStyle.Row(row);
            row.style.flexShrink=0;grid.Add(row);
            row.Add(ActionButton("Mover",RtsHudGlyph.Move,controller.ArmMove));
            row.Add(ActionButton("Atacar",RtsHudGlyph.Sword,controller.ArmAttack));
            row.Add(ActionButton("Patrullar",RtsHudGlyph.Patrol,controller.ArmPatrol));
            row.Add(ActionButton("Detener",RtsHudGlyph.Stop,controller.Stop));
            row.Add(ActionButton("Mantener",RtsHudGlyph.Shield,controller.Hold));
            row.Add(ActionButton("Centrar",RtsHudGlyph.Focus,controller.FocusSelection));
            if(controller.Fleet.Count>0)
            {
                row=new VisualElement { name="HUD naval action row" };RtsUiStyle.Row(row);
                row.style.flexShrink=0;grid.Add(row);
                row.Add(ActionButton("Embarcar",RtsHudGlyph.Board,controller.BoardNearby));
                row.Add(ActionButton("Desembarcar",RtsHudGlyph.Unload,controller.UnloadFleet));
                row.Add(ActionButton("Puerto",RtsHudGlyph.City,controller.FocusHarbor));
            }
            root.Add(grid);
        }

        void BuildProduction(VisualElement root)
        {
            bool ownTown=false, ownHarbor=false;
            foreach(var town in controller.SelectedTowns) if(town && town.State.Owner==0) { ownTown=true; break; }
            foreach(var harbor in controller.SelectedHarbors) if(harbor && harbor.Owner==0) { ownHarbor=true; break; }
            if (controller.SelectedTowns.Count + controller.SelectedHarbors.Count > 1)
                root.tooltip = "Cada compra se añade una vez a la cola compatible más corta.";
            if (ownTown)
            {

                UnitButtons(root, ProductionCatalog.SettlementUnits);
            }
            if (ownHarbor)
            {
                UnitButtons(root, ProductionCatalog.HarborUnits);
                var ships = new VisualElement(); RtsUiStyle.Row(ships, true);
                foreach (var ship in ProductionCatalog.HarborShips)
                {
                    var profile = Harbor.Profile((ShipKind)ship);
                    ships.Add(ShipButton((ShipKind)ship, () => controller.BuyShip((ShipKind)ship)));
                }
                root.Add(ships);
            }
            if (!controller.SelectedTown && !controller.SelectedHarbor && controller.SelectedTowns.Count + controller.SelectedHarbors.Count == 0)
                AddInfo(root, "Selecciona una ciudad, un puerto o varios edificios propios para producir.");
        }

        void UnitButtons(VisualElement root, IReadOnlyList<UnitKind> kinds)
        {
            var grid = new VisualElement(); RtsUiStyle.Row(grid, true);
            for (int i = 0; i < kinds.Count; i++)
            {
                var kind = kinds[i];
                grid.Add(RecruitButton(kind));
            }
            root.Add(grid);
        }

        Button RecruitButton(UnitKind kind) => PurchaseButton("Recruit "+kind,PortraitResource(kind),
            BattleRules.Name(kind),BattleRules.Cost(kind)+" oro · "+BattleRules.Hotkey(kind),()=>controller.Recruit(kind));

        static Button GridButton(string text, System.Action action)
        {
            var button = RtsUiStyle.Button(text, action);
            if (UiViewport.IsCompact) button.style.width = Length.Percent(46);
            else button.style.minWidth = 126;
            return button;
        }

        static Button ShipButton(ShipKind kind, System.Action action) => PurchaseButton("Build ship "+kind,
            ShipPortrait.Resource(kind),Harbor.Profile(kind).Name,Harbor.Profile(kind).Cost+" oro",action);

        static readonly Dictionary<string, Texture2D> portraitCache = new Dictionary<string, Texture2D>();
        static Texture2D CachedPortrait(string resource)
        {
            if (!portraitCache.TryGetValue(resource, out var texture)) { texture = Resources.Load<Texture2D>(resource); portraitCache.Add(resource, texture); }
            return texture;
        }

        void BuildModal(VisualElement root)
        {
            modal = new VisualElement { name = "HUD modal", pickingMode = PickingMode.Position };
            modal.style.position = Position.Absolute; modal.style.left = 0; modal.style.top = 0; modal.style.right = 0; modal.style.bottom = 0;
            modal.style.backgroundColor = new Color(.01f, .02f, .03f, .88f); modal.style.paddingLeft = UiViewport.IsCompact ? 12 : 80; modal.style.paddingRight = UiViewport.IsCompact ? 12 : 80;
            modal.style.paddingTop = UiViewport.IsCompact ? 14 : 48; modal.style.paddingBottom = UiViewport.IsCompact ? 14 : 48;
            var panel = RtsUiStyle.Panel("HUD modal panel"); panel.style.flexGrow = 1;
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "HUD modal scroll" }; scroll.style.flexGrow = 1;scroll.style.minHeight=0; RtsUiStyle.ConfigureScroll(scroll); panel.Add(scroll);
            if (session.Winner >= 0) BuildResult(scroll);
            else if (controller.ScoreboardVisible || menuTab == 2) BuildRanking(scroll);
            else if(menuTab==3)BuildIncome(scroll);
            else if(menuTab==4)BuildPopulation(scroll);
            else BuildHelp(scroll);
            var sticky = new VisualElement { name = "HUD modal actions" }; RtsUiStyle.Row(sticky, true);
            sticky.Add(RtsUiStyle.Button("VOLVER", CloseModal));
            sticky.style.flexShrink=0;
            modalPauseButton=RtsUiStyle.Button(session.Paused ? "CONTINUAR" : "PAUSA", () => { session.TogglePause(); UpdateRetainedLabels(); });
            sticky.Add(modalPauseButton);
            panel.Add(sticky);
            root.Add(modal); modal.Add(panel);
        }

        void BuildHelp(VisualElement panel)
        {
            AddTitle(panel, "DOMINIOS · " + MapLayout.MapName);
            var tabs = new VisualElement(); RtsUiStyle.Row(tabs, true);
            tabs.Add(RtsUiStyle.Button("Partida", () => { menuTab = 0; BuildRetainedUi(false); }));
            tabs.Add(RtsUiStyle.Button("Controles", () => { menuTab = 1; BuildRetainedUi(false); }));
            tabs.Add(RtsUiStyle.Button("Ranking", () => { menuTab = 2; BuildRetainedUi(false); })); panel.Add(tabs);
            if (menuTab == 1)
            {
                AddInfo(panel, "Selección: clic o toque para seleccionar; arrastra un área para seleccionar tropas y edificios.");
                AddInfo(panel, "Órdenes: clic derecho en PC o una acción seguida de toque en tabletas. B/D embarca y desembarca.");
                AddInfo(panel, "Cámara: rueda para zoom, arrastre derecho para mover y botón central para girar. En pantalla táctil, dos dedos mueven y amplían; tres dedos giran.");
            }
            else
            {
                AddInfo(panel, "Conquista el 60 % de las ciudades. Completa países para recibir refuerzos de sus hogueras.");
                AddInfo(panel, "Semilla " + session.Seed + " · " + session.PlayerCount + " jugadores · " + session.DifficultyName);
                panel.Add(RtsUiStyle.Button(MinimapVisible ? "OCULTAR MAPA TÁCTICO" : "MOSTRAR MAPA TÁCTICO", ToggleMinimap));
                panel.Add(RtsUiStyle.Button("CENTRAR MAPA", controller.CameraRig.FrameMap));
                panel.Add(RtsUiStyle.Button("RESTABLECER CÁMARA", controller.CameraRig.ResetView));
                panel.Add(RtsUiStyle.Button(controller.EdgePan ? "PANEO EN BORDES: ACTIVO" : "PANEO EN BORDES: INACTIVO", () => { controller.EdgePan = !controller.EdgePan; BuildRetainedUi(false); }));
                panel.Add(RtsUiStyle.Button("VELOCIDAD CÁMARA −", () => { controller.CameraRig.PanSpeed = Mathf.Max(.5f, controller.CameraRig.PanSpeed - .2f); BuildRetainedUi(false); }));
                panel.Add(RtsUiStyle.Button("VELOCIDAD CÁMARA +", () => { controller.CameraRig.PanSpeed = Mathf.Min(3f, controller.CameraRig.PanSpeed + .2f); BuildRetainedUi(false); }));
                AddInfo(panel, RuntimeDiagnostics.LatestReport == null ? "Recogiendo muestra de rendimiento…" : "Rendimiento: " + RuntimeDiagnostics.LatestAverageMs.ToString("F1") + " ms medio · " + RuntimeDiagnostics.LatestMaximumMs.ToString("F1") + " ms máximo · " + RuntimeDiagnostics.LatestUnits + " unidades · " + (RuntimeDiagnostics.LatestUnityAllocatedBytes/1048576f).ToString("F0") + " MB Unity.");
                panel.Add(RtsUiStyle.Button("NUEVA PARTIDA · ELEGIR MAPA", FrontEndController.Open));
            }
        }

        void BuildRanking(VisualElement panel)
        {
            AddTitle(panel,"CLASIFICACIÓN · CIUDADES");rankingRows.Clear();
            for(int rank=0;rank<session.PlayerCount;rank++)
            {
                var row=new RankingRow();row.Root=new VisualElement { name="HUD ranking row "+rank };
                row.Root.style.backgroundColor=new Color(.075f,.085f,.075f,.9f);
                row.Root.style.borderLeftWidth=4;row.Root.style.paddingLeft=10;row.Root.style.paddingRight=8;
                row.Root.style.paddingTop=7;row.Root.style.paddingBottom=7;row.Root.style.marginBottom=5;
                row.Name=RtsUiStyle.Label("",null,14);row.Name.style.whiteSpace=WhiteSpace.Normal;
                row.Root.Add(row.Name);
                var metrics=new VisualElement();RtsUiStyle.Row(metrics);metrics.style.marginTop=3;
                row.Cities=AddMetric(metrics,RtsHudGlyph.City,"","Ciudades controladas");
                row.Units=AddMetric(metrics,RtsHudGlyph.Sword,"","Unidades totales, incluidos defensores y barcos");
                row.Root.Add(metrics);panel.Add(row.Root);rankingRows.Add(row);
            }
            UpdateRankingLabels();
        }

        List<int> RankedPlayers()
        {
            var order = new List<int>(); for (int i = 0; i < session.PlayerCount; i++) order.Add(i);
            order.Sort((a, b) => { int compare = hud.PlayerCities[b].CompareTo(hud.PlayerCities[a]); return compare != 0 ? compare : a.CompareTo(b); });
            return order;
        }

        void BuildResult(VisualElement panel)
        {
            AddTitle(panel, session.Winner == 0 ? "VICTORIA" : "DERROTA");
            AddInfo(panel, VisualFactory.TeamName(session.Winner) + " controla " + MapLayout.MapName);
            panel.Add(RtsUiStyle.Button("NUEVA PARTIDA", FrontEndController.Open));
        }

        static void AddTitle(VisualElement root, string text)
        {
            var title = RtsUiStyle.Title(text, null, UiViewport.IsCompact?14:17);title.style.whiteSpace=WhiteSpace.Normal; title.style.marginBottom = 4; root.Add(title);
        }
        static void AddInfo(VisualElement root, string text)
        {
            var label = RtsUiStyle.Label(text, null, 14); label.style.color = RtsUiStyle.Muted; label.style.whiteSpace = WhiteSpace.Normal; label.style.marginBottom = 7; root.Add(label);
        }
        void LiveInfo(VisualElement root,System.Func<string> value)
        {
            var label=RtsUiStyle.Label(value(),null,13);label.style.color=RtsUiStyle.Muted;label.style.whiteSpace=WhiteSpace.Normal;label.style.marginBottom=4;
            root.Add(label);liveContext.Add(()=>label.text=value());
        }
        static string SoldierStats(Soldier unit)
        {
            if(!unit||!unit.IsAlive)return "Unidad eliminada";
            var profile=BattleRules.Profile(unit.Kind);
            return BattleRules.Name(unit.Kind)+" · "+Mathf.CeilToInt(unit.Health)+" / "+profile.Health+" vida · "+BattleRules.DamageRange(unit.Kind)+" "+profile.Attack+" · alcance "+profile.Range+" · armadura "+profile.Armor+" "+profile.Defense;
        }
    }
}
