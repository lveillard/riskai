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
        Button pauseButton;
        readonly List<QueueSlot> queueSlots = new List<QueueSlot>(20);
        int retainedTab;
        bool showMinimap = true;
        int retainedContextKey;
        bool lastCompact, lastPortrait;
        int lastModalKind;
        int ModalKind => session.Winner >= 0 ? 3 : controller.ScoreboardVisible ? 2 : controller.HelpVisible ? 1 : 0;
        Vector2 lastScreen;
        Rect lastSafe;
        float nextRetainedLabelRefresh;
        readonly List<Label> rankingLabels = new List<Label>();
        readonly List<System.Action> liveContext = new List<System.Action>();
        Button modalPauseButton;
        float lastDensity;
        bool wideFooter;
        bool MinimapVisible => showMinimap && (!UiViewport.IsPortrait || retainedTab == 3);

        float RequestedHeaderHeight => !UiViewport.IsCompact ? 48 : UiViewport.IsPortrait ? 96 : 44;
        float RequestedFooterHeight => UiViewport.IsPortrait ? 260 : UiViewport.IsCompact ? 232 : 208;
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
            int modalKind=ModalKind;
            int contextKey = ContextKey();
            bool orientationChanged = lastPortrait != UiViewport.IsPortrait;
            bool resized = lastScreen != new Vector2(Screen.width, Screen.height) || lastSafe != UiViewport.SafeRect || lastDensity != UiViewport.Scale;
            if (lastCompact != UiViewport.IsCompact || orientationChanged || lastModalKind != modalKind || resized)
            {
                if (orientationChanged) { showMinimap = !UiViewport.IsPortrait; if(retainedTab==3)retainedTab=0; }
                BuildRetainedUi(false);
                return;
            }
            if (retainedContextKey != contextKey)
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
            if (goldLabel != null) goldLabel.text = hud.Gold + " ORO · +" + hud.Income;
            if (citiesLabel != null) citiesLabel.text = CitiesText;
            if (populationLabel != null) populationLabel.text = hud.MobilePopulation0 + " MÓVILES · " + hud.GarrisonPopulation0 + " GUARDIAS";
            if (roundLabel != null) roundLabel.text = "RONDA " + hud.Round + " · " + Mathf.CeilToInt(BattleRules.RoundSeconds - hud.RoundElapsed) + " s";
            if (pauseButton != null) pauseButton.text = session.Paused ? "Continuar" : "Pausa";
            if (modalPauseButton != null) modalPauseButton.text = session.Paused ? "CONTINUAR" : "PAUSA";
            for(int i=0;i<liveContext.Count;i++)liveContext[i]();
            UpdateQueueSlots();
            UpdateRankingLabels();
        }

        void RebuildContext()
        {
            if (context == null) return;
            context.Clear(); queueSlots.Clear(); liveContext.Clear();
            if (wideFooter) { wideContext.Clear(); BuildWideContext(context, wideContext); }
            else BuildContext(context);
        }

        void UpdateQueueSlots()
        {
            for (int i = 0; i < queueSlots.Count; i++) queueSlots[i].Refresh();
        }

        int ContextKey()
        {
            unchecked
            {
                int key = retainedTab;
                key = key * 29 + (controller.InspectedTarget ? controller.InspectedTarget.GetInstanceID() : 0);
                for (int i = 0; i < controller.Selection.Count; i++) key = key * 31 + controller.Selection[i].GetInstanceID();
                for (int i = 0; i < controller.Fleet.Count; i++) key = key * 37 + controller.Fleet[i].GetInstanceID();
                key = key * 41 + (controller.SelectedTown ? controller.SelectedTown.GetInstanceID() : 0);
                key = key * 43 + (controller.SelectedHarbor ? controller.SelectedHarbor.GetInstanceID() : 0);
                key = key * 47 + (controller.SelectedCamp ? controller.SelectedCamp.GetInstanceID() : 0);
                for (int i = 0; i < controller.SelectedTowns.Count; i++) key = key * 53 + controller.SelectedTowns[i].GetInstanceID();
                for (int i = 0; i < controller.SelectedHarbors.Count; i++) key = key * 59 + controller.SelectedHarbors[i].GetInstanceID();
                return key;
            }
        }

        void UpdateRankingLabels()
        {
            if (rankingLabels.Count == 0) return;
            var order = RankedPlayers();
            for (int rank = 0; rank < rankingLabels.Count && rank < order.Count; rank++)
            { int player = order[rank]; rankingLabels[rank].text = (rank + 1) + ". " + VisualFactory.TeamName(player) + " · " + hud.PlayerCities[player] + " ciudades · " + hud.PlayerMobile[player] + " móviles / " + hud.PlayerGuards[player] + " guardias"; }
        }

        void BuildRetainedUi(bool initial)
        {
            ConfigureViewport();
            liveContext.Clear(); rankingLabels.Clear(); modalPauseButton=null;
            lastCompact = UiViewport.IsCompact; lastPortrait = UiViewport.IsPortrait;
            lastModalKind=ModalKind;
            retainedContextKey = ContextKey(); lastScreen = new Vector2(Screen.width, Screen.height); lastSafe = UiViewport.SafeRect; lastDensity=UiViewport.Scale; nextRetainedLabelRefresh = Time.unscaledTime;
            retainedRoot = new VisualElement { name = "Battle retained UI", pickingMode = PickingMode.Ignore };
            retainedRoot.style.flexGrow = 1;
            BuildHeader(retainedRoot);
            BuildFooter(retainedRoot);
            if (MinimapVisible) BuildMinimapHitOverlay(retainedRoot);
            if (lastModalKind!=0) BuildModal(retainedRoot);
            retainedUi.SetContent(retainedRoot);
        }

        void BuildHeader(VisualElement root)
        {
            header = RtsUiStyle.Panel("HUD header"); header.style.position = Position.Absolute;
            header.style.left = 0; header.style.top = 0; header.style.right = 0; header.style.height = HeaderHeight;
            header.style.paddingLeft = 6; header.style.paddingRight = 6; header.style.paddingTop = 2; header.style.paddingBottom = 2;
            if (!UiViewport.IsCompact) BuildWideHeader(); else BuildCompactHeader();
            root.Add(header);
        }

        void BuildWideHeader()
        {
            RtsUiStyle.Row(header);
            var title = RtsUiStyle.Title("DOMINIOS", null, 17); header.Add(title);
            goldLabel = HeaderLabel(hud.Gold + " ORO · +" + hud.Income); goldLabel.name="HUD gold";goldLabel.style.color=RtsUiStyle.Gold;goldLabel.style.flexGrow=1; header.Add(goldLabel);
            citiesLabel = HeaderLabel(hud.OwnedTowns + " / " + MapLayout.Towns.Length + " CIUDADES");citiesLabel.style.flexGrow=1; header.Add(citiesLabel);
            populationLabel = HeaderLabel(hud.MobilePopulation0 + " MÓVILES · " + hud.GarrisonPopulation0 + " GUARDIAS");populationLabel.style.flexGrow=1; header.Add(populationLabel);
            roundLabel = HeaderLabel("RONDA " + hud.Round); roundLabel.style.flexGrow = 1; header.Add(roundLabel);
            header.Add(HeaderButton("Ranking", ShowPlayers));
            header.Add(HeaderButton(showMinimap ? "Ocultar mapa" : "Mapa", () => { showMinimap = !showMinimap; BuildRetainedUi(false); }));
            pauseButton = HeaderButton(session.Paused ? "Continuar" : "Pausa", () => { session.TogglePause(); UpdateRetainedLabels(); }); header.Add(pauseButton);
            header.Add(HeaderButton("Menú", OpenMenu));
        }

        void BuildCompactHeader()
        {
            var top = new VisualElement(); RtsUiStyle.Row(top);
            goldLabel = HeaderLabel(hud.Gold + " ORO · +" + hud.Income); goldLabel.name="HUD gold"; goldLabel.style.flexGrow = 1; top.Add(goldLabel);
            citiesLabel = HeaderLabel(CitiesText); citiesLabel.style.flexGrow = 1; top.Add(citiesLabel);
            if(UiViewport.IsPortrait)top.Add(HeaderButton("Mapa", ToggleMinimap));
            top.Add(HeaderButton("Menú", OpenMenu)); header.Add(top);
            if (HeaderHeight >= 76)
            {
                var lower = new VisualElement(); RtsUiStyle.Row(lower);
                roundLabel = HeaderLabel("RONDA " + hud.Round); roundLabel.style.flexGrow = 1; lower.Add(roundLabel);
                populationLabel = HeaderLabel(hud.MobilePopulation0 + " MÓVILES · " + hud.GarrisonPopulation0 + " GUARDIAS"); populationLabel.style.flexGrow = 1; lower.Add(populationLabel);
                header.Add(lower);
            }
            else { roundLabel = null; populationLabel = null; }
        }

        Label HeaderLabel(string text)
        {
            var label = RtsUiStyle.Label(text, null, 12); label.style.marginLeft = 8; label.style.marginRight = 8; return label;
        }
        string CitiesText => hud.OwnedTowns + " / " + MapLayout.Towns.Length + (UiViewport.IsPortrait?"":" CIUDADES");

        static Button HeaderButton(string text, System.Action action)
        {
            var button = RtsUiStyle.Button(text, action);
            button.style.minHeight = 40; button.style.marginBottom = 0; button.style.marginRight = 4;
            return button;
        }

        void OpenMenu() { controller.CancelCursor(); menuTab = 0; controller.HelpVisible = true; BuildRetainedUi(false); }
        void CloseModal() { controller.CancelCursor(); controller.HelpVisible = false; menuTab = 0; BuildRetainedUi(false); }
        void ToggleMinimap()
        {
            if(UiViewport.IsPortrait) { retainedTab=retainedTab==3?0:3;showMinimap=retainedTab==3; }
            else showMinimap=!showMinimap;
            BuildRetainedUi(false);
        }

        void BuildFooter(VisualElement root)
        {
            footer = RtsUiStyle.Panel("HUD footer"); footer.style.position = Position.Absolute;
            footer.style.left = MinimapVisible && !UiViewport.IsPortrait ? MinimapRect().xMax + 8 - UiViewport.SafeRect.xMin/UiViewport.Scale : 0; footer.style.right = 0; footer.style.bottom = 0;
            footer.style.height = FooterHeight;
            footer.style.paddingTop=16;footer.style.paddingBottom=12;
            wideFooter = !UiViewport.IsCompact;
            if (!wideFooter)
            {
                var tabs = new VisualElement { name = "HUD tabs" }; RtsUiStyle.Row(tabs); tabs.style.marginBottom = 4;tabs.style.flexShrink=0;
                Tab(tabs, 0, "Selección"); Tab(tabs, 1, "Órdenes"); Tab(tabs, 2, "Crear"); footer.Add(tabs);
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
            queueSlots.Clear();
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
            else
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
            Soldier unit = controller.Selection.Count == 1 ? controller.Selection[0] : controller.InspectedTarget as Soldier;
            if (!unit) { BuildSelection(root);return; }
            var row=new VisualElement();RtsUiStyle.Row(row);row.style.alignItems=Align.FlexStart;
            var frame=RtsUiStyle.Panel("HUD portrait frame");frame.style.paddingLeft=5;frame.style.paddingRight=5;frame.style.paddingTop=10;frame.style.paddingBottom=10;frame.style.marginRight=12;frame.style.flexShrink=0;
            var portrait = new Image { name = "HUD unit portrait", image = Resources.Load<Texture2D>(PortraitResource(unit.Kind)), scaleMode = ScaleMode.ScaleToFit };
            portrait.style.width = UiViewport.IsCompact?64:96; portrait.style.height = UiViewport.IsCompact?76:112;
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

        void Tab(VisualElement parent, int index, string text)
        {
            var button = RtsUiStyle.Button(text, () => { retainedTab = index; BuildRetainedUi(false); }, "HUD tab " + index);
            button.style.flexGrow=1;button.style.flexBasis=0;button.style.minWidth=0;button.style.paddingLeft=4;button.style.paddingRight=4;button.style.marginRight=4;button.style.marginBottom=0;
            if(retainedTab==index)button.style.backgroundColor=new Color(.22f,.20f,.12f);
            parent.Add(button);
        }

        void BuildContext(VisualElement root)
        {
            if (retainedTab == 1) { BuildOrders(root); return; }
            if (retainedTab == 2) { BuildProduction(root); return; }
            BuildSelectionWithPortrait(root);
        }

        void BuildSelection(VisualElement root)
        {
            if (controller.SelectedCamp)
            {
                var camp = controller.SelectedCamp; AddTitle(root, camp.DisplayName.ToUpperInvariant());
                LiveInfo(root, () => camp.HasRally ? "Salida fijada. Órdenes permite cambiar el punto de reunión." : "Los refuerzos esperan en la hoguera hasta fijar una salida.");
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
                for (int i = 0; i < controller.SelectedTowns.Count; i++) QueueLabels(root, controller.SelectedTowns[i]);
                for (int i = 0; i < controller.SelectedHarbors.Count; i++) QueueLabels(root, controller.SelectedHarbors[i]);
                return;
            }
            if (controller.SelectedTown && !controller.SelectedHarbor)
            {
                var town = controller.SelectedTown; AddTitle(root, town.DisplayName);
                LiveInfo(root,()=>town?VisualFactory.TeamName(town.State.Owner)+" · "+town.QueueCount+" / 5 · "+(town.QueueCount>0?BattleRules.Name(town.TrainingKind):"cola vacía"):"Ciudad retirada");
                QueueLabels(root, town, false); return;
            }
            if (controller.SelectedHarbor)
            {
                var harbor = controller.SelectedHarbor; AddTitle(root, harbor.DisplayName);
                LiveInfo(root,()=>harbor?"PUERTO · "+VisualFactory.TeamName(harbor.Owner)+" · tierra "+harbor.LandQueueCount+" / 5 · barcos "+harbor.QueueCount+" / 5":"Puerto retirado");
                QueueLabels(root, harbor, false); return;
            }
            if (controller.Fleet.Count > 0)
            {
                AddTitle(root, controller.Fleet.Count == 1 ? controller.Fleet[0].DisplayName : "FLOTA · " + controller.Fleet.Count + " barcos");
                if(!wideFooter)AddInfo(root, "Selecciona ÓRDENES para navegar, atacar, detener, embarcar o desembarcar."); return;
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
                if(!wideFooter)AddInfo(root, "Selecciona ÓRDENES para mover, atacar, patrullar, detener o mantener."); return;
            }
            AddTitle(root, StrategicMapView.Active ? "TU IMPERIO, DE UN VISTAZO" : "SELECCIONA TROPAS O UN EDIFICIO");
            AddInfo(root, "Arrastra un área para seleccionar. Las acciones táctiles se activan desde ÓRDENES.");
            var row = new VisualElement(); RtsUiStyle.Row(row, true);
            row.Add(RtsUiStyle.Button("Mi ciudad", controller.FocusHome)); row.Add(RtsUiStyle.Button("Mi ejército", controller.SelectAll)); root.Add(row);
        }

        void BuildOrders(VisualElement root)
        {
            AddTitle(root, "ÓRDENES");
            var grid = new VisualElement(); RtsUiStyle.Row(grid, true);
            grid.Add(GridButton("Mover", controller.ArmMove)); grid.Add(GridButton("Atacar", controller.ArmAttack));
            grid.Add(GridButton("Patrullar", controller.ArmPatrol)); grid.Add(GridButton("Detener", controller.Stop));
            grid.Add(GridButton("Mantener", controller.Hold));
            if (controller.Fleet.Count > 0)
            {
                grid.Add(GridButton("Embarcar", controller.BoardNearby)); grid.Add(GridButton("Desembarcar", controller.UnloadFleet));
                grid.Add(GridButton("Puerto", controller.FocusHarbor));
            }
            root.Add(grid); AddInfo(root, "Tras activar una orden, toca o haz clic en el mundo para elegir el objetivo.");
        }

        void BuildProduction(VisualElement root)
        {
            if (controller.SelectedTowns.Count + controller.SelectedHarbors.Count > 1)
                root.tooltip = "Cada compra se añade una vez a la cola compatible más corta.";
            if (controller.SelectedTowns.Count > 0)
            {
                AddTitle(root, "EJÉRCITO");
                UnitButtons(root, ProductionCatalog.SettlementUnits);
            }
            if (controller.SelectedHarbors.Count > 0)
            {
                AddTitle(root, "MARINA"); UnitButtons(root, ProductionCatalog.HarborUnits);
                var ships = new VisualElement(); RtsUiStyle.Row(ships, true);
                foreach (var ship in ProductionCatalog.HarborShips)
                {
                    var profile = Harbor.Profile((ShipKind)ship);
                    ships.Add(ShipButton((ShipKind)ship, profile.Name + " · " + profile.Cost + " oro", () => controller.BuyShip((ShipKind)ship)));
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

        Button RecruitButton(UnitKind kind)
        {
            var button = RtsUiStyle.Button("", () => controller.Recruit(kind), "Recruit " + kind);
            bool compactLandscape=UiViewport.IsCompact&&!UiViewport.IsPortrait;
            button.style.width=Length.Percent(UiViewport.IsPortrait?46:30);button.style.minWidth=0;button.style.paddingLeft=4;button.style.paddingRight=4;
            float height=compactLandscape?50:62;
            button.style.height=height;button.style.minHeight=height;button.style.maxHeight=height;button.style.marginBottom=5;button.style.marginRight=6;
            button.style.flexDirection=FlexDirection.Row;button.style.alignItems=Align.Center;
            button.style.paddingTop=3;button.style.paddingBottom=3;
            var portrait = new Image { image = Resources.Load<Texture2D>(PortraitResource(kind)), scaleMode = ScaleMode.ScaleToFit };
            portrait.style.width = 36; portrait.style.height = 36; portrait.style.alignSelf = Align.Center;portrait.style.flexShrink=0;portrait.style.marginRight=6;
            var copy = RtsUiStyle.Label(BattleRules.Name(kind) + "\n" + BattleRules.Cost(kind) + " oro · " + BattleRules.Hotkey(kind), null, 12);
            copy.style.whiteSpace = WhiteSpace.Normal;copy.style.flexShrink=1;copy.style.minWidth=0;
            button.Add(portrait); button.Add(copy); return button;
        }

        static Button GridButton(string text, System.Action action)
        {
            var button = RtsUiStyle.Button(text, action);
            if (UiViewport.IsCompact) button.style.width = Length.Percent(46);
            else button.style.minWidth = 126;
            return button;
        }

        static Button ShipButton(ShipKind kind, string text, System.Action action)
        {
            var button = RtsUiStyle.Button("", action, "Build ship " + kind);
            if (UiViewport.IsCompact) button.style.width = Length.Percent(46); else button.style.minWidth = 126;
            var icon = new NavalQueueIcon(); icon.style.width = 30; icon.style.height = 22; icon.style.alignSelf = Align.Center;
            var label = RtsUiStyle.Label(text, null, 11); label.style.whiteSpace = WhiteSpace.Normal; label.style.unityTextAlign = TextAnchor.MiddleCenter;
            icon.SetKind(kind); button.Add(icon); button.Add(label); button.tooltip = text;
            return button;
        }

        void QueueLabels(VisualElement root, Settlement town, bool includeStatus = true)
        {
            if (!town) return;
            if (includeStatus) LiveInfo(root,()=>town?town.DisplayName+" · "+town.QueueCount+" / 5":"Ciudad retirada");
            var strip = new VisualElement(); RtsUiStyle.Row(strip, true); root.Add(strip);
            for (int i = 0; i < 5; i++) AddQueueSlot(strip, town, null, false, i, wideFooter);
            UpdateQueueSlots();
        }

        void QueueLabels(VisualElement root, Harbor harbor, bool includeStatus = true)
        {
            if (!harbor) return;
            if(includeStatus) LiveInfo(root,()=>harbor?harbor.DisplayName+" · tierra "+harbor.LandQueueCount+" / 5 · barcos "+harbor.QueueCount+" / 5":"Puerto retirado");
            var queues = new VisualElement(); RtsUiStyle.Row(queues, true); queues.style.alignItems=Align.FlexStart; root.Add(queues);
            var land = new VisualElement(); RtsUiStyle.Row(land, true); queues.Add(land);
            for (int i = 0; i < Harbor.QueueCapacity; i++) AddQueueSlot(land, null, harbor, false, i, wideFooter);
            var naval = new VisualElement(); RtsUiStyle.Row(naval, true); queues.Add(naval);
            for (int i = 0; i < Harbor.QueueCapacity; i++) AddQueueSlot(naval, null, harbor, true, i, wideFooter);
            UpdateQueueSlots();
        }

        void AddQueueSlot(VisualElement parent, Settlement town, Harbor harbor, bool naval, int index, bool showEmpty)
        {
            var button = RtsUiStyle.Button("", () => { if (town) controller.CancelTraining(town, index); else controller.CancelTraining(harbor, index, naval); });
            button.style.width = 66;button.style.marginRight=4; button.style.height=68;button.style.minHeight = 68;button.style.paddingTop=3;button.style.paddingBottom=3; button.style.paddingLeft = 4; button.style.paddingRight = 4;
            var portrait = new Image { scaleMode = ScaleMode.ScaleToFit }; portrait.style.width = 30; portrait.style.height = 30; portrait.style.alignSelf = Align.Center;
            var navalIcon = new NavalQueueIcon(); navalIcon.style.width = 28; navalIcon.style.height = 20; navalIcon.style.alignSelf = Align.Center; navalIcon.style.display = DisplayStyle.None;
            var label = RtsUiStyle.Label("", null, 10); label.style.unityTextAlign = TextAnchor.MiddleCenter; label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow=Overflow.Hidden;label.style.textOverflow=TextOverflow.Ellipsis;
            var progress = new VisualElement(); progress.style.height = 4; progress.style.backgroundColor = RtsUiStyle.Bronze;
            button.Add(portrait); button.Add(navalIcon); button.Add(label); button.Add(progress); parent.Add(button);
            queueSlots.Add(new QueueSlot(town, harbor, naval, index, showEmpty, button, portrait, navalIcon, label, progress));
        }

        sealed class QueueSlot
        {
            static readonly Dictionary<string, Texture2D> portraitCache = new Dictionary<string, Texture2D>();
            readonly Settlement town; readonly Harbor harbor; readonly bool naval, showEmpty; readonly int index;
            readonly Button button; readonly Image portrait; readonly NavalQueueIcon navalIcon; readonly Label label; readonly VisualElement progress;
            public QueueSlot(Settlement town, Harbor harbor, bool naval, int index, bool showEmpty, Button button, Image portrait, NavalQueueIcon navalIcon, Label label, VisualElement progress)
            { this.town = town; this.harbor = harbor; this.naval = naval; this.index = index; this.showEmpty = showEmpty; this.button = button; this.portrait = portrait; this.navalIcon = navalIcon; this.label = label; this.progress = progress; }
            public void Refresh()
            {
                bool visible; string name; float amount; string resource;
                if (town)
                {
                    visible = index < town.QueueCount; name = visible ? BattleRules.Name(town.QueuedKind(index)) : ""; amount = index == 0 ? town.TrainingProgress : 0;
                    resource = visible ? PortraitResource(town.QueuedKind(index)) : null;
                }
                else if (naval)
                {
                    visible = index < harbor.QueueCount; name = visible ? Harbor.Profile(harbor.QueuedKind(index)).Name : ""; amount = index == 0 ? harbor.TrainingProgress : 0;
                    resource = null;
                }
                else
                {
                    visible = index < harbor.LandQueueCount; name = visible ? BattleRules.Name(harbor.QueuedLandKind(index)) : ""; amount = index == 0 ? harbor.LandTrainingProgress : 0;
                    resource = visible ? PortraitResource(harbor.QueuedLandKind(index)) : null;
                }
                button.style.display = visible || showEmpty ? DisplayStyle.Flex : DisplayStyle.None;
                button.SetEnabled(visible);
                if (!visible)
                {
                    portrait.style.display = DisplayStyle.None; navalIcon.style.display = DisplayStyle.None;
                    label.text = "—"; progress.style.width = Length.Percent(0); return;
                }
                portrait.style.display = naval ? DisplayStyle.None : DisplayStyle.Flex;
                navalIcon.style.display = naval ? DisplayStyle.Flex : DisplayStyle.None;
                if (naval) navalIcon.SetKind(harbor.QueuedKind(index));
                label.text = name;
                button.tooltip=name+" · cancelar encargo";
                Texture2D texture = resource == null ? null : CachedPortrait(resource);
                if (portrait.image != texture) portrait.image = texture;
                progress.style.width = Length.Percent(Mathf.Clamp01(amount) * 100);
            }

            static string PortraitResource(UnitKind kind) => "Portraits/" + (kind == UnitKind.Guard ? "MountedKnight" : BattleRules.Model(kind));

            static Texture2D CachedPortrait(string resource)
            {
                if (!portraitCache.TryGetValue(resource, out var texture)) { texture = Resources.Load<Texture2D>(resource); portraitCache.Add(resource, texture); }
                return texture;
            }
        }

        sealed class NavalQueueIcon : VisualElement
        {
            ShipKind kind;
            bool hasKind;

            public NavalQueueIcon() { generateVisualContent += Paint; }

            public void SetKind(ShipKind value)
            {
                if (hasKind && kind == value) return;
                kind = value; hasKind = true; MarkDirtyRepaint();
            }

            void Paint(MeshGenerationContext context)
            {
                Rect r = contentRect; if (r.width < 2 || r.height < 2) return;
                var painter = context.painter2D;
                float left = r.xMin, right = r.xMax, top = r.yMin, bottom = r.yMax;
                painter.fillColor = new Color(.28f, .15f, .07f); Hull(painter, left + 2, right - 2, bottom - 3, kind == ShipKind.Transport ? 6 : 4);
                painter.strokeColor = new Color(.16f, .09f, .04f); painter.lineWidth = 1.2f;
                if (kind == ShipKind.Galley)
                {
                    Mast(painter, left + r.width * .36f, top + 2, bottom - 6); Mast(painter, left + r.width * .66f, top + 4, bottom - 6);
                    Sail(painter, left + r.width * .38f, top + 4, 7, 9, new Color(.88f, .78f, .55f));
                    Sail(painter, left + r.width * .68f, top + 6, 6, 7, new Color(.78f, .67f, .45f));
                }
                else
                {
                    Mast(painter, left + r.width * .5f, top + 2, bottom - 7);
                    Sail(painter, left + r.width * .52f, top + 4, 9, 10, new Color(.86f, .78f, .58f));
                    painter.fillColor = new Color(.64f, .40f, .16f); painter.BeginPath(); painter.MoveTo(new Vector2(left + 4, bottom - 8)); painter.LineTo(new Vector2(left + 9, bottom - 8)); painter.LineTo(new Vector2(left + 9, bottom - 4)); painter.LineTo(new Vector2(left + 4, bottom - 4)); painter.ClosePath(); painter.Fill();
                }
            }

            static void Hull(Painter2D painter, float left, float right, float bottom, float depth)
            { painter.BeginPath(); painter.MoveTo(new Vector2(left, bottom - depth)); painter.LineTo(new Vector2(right, bottom - depth)); painter.LineTo(new Vector2(right - 3, bottom)); painter.LineTo(new Vector2(left + 3, bottom)); painter.ClosePath(); painter.Fill(); }
            static void Mast(Painter2D painter, float x, float top, float bottom)
            { painter.BeginPath(); painter.MoveTo(new Vector2(x, top)); painter.LineTo(new Vector2(x, bottom)); painter.Stroke(); }
            static void Sail(Painter2D painter, float mast, float top, float width, float height, Color color)
            { painter.fillColor = color; painter.BeginPath(); painter.MoveTo(new Vector2(mast + 1, top)); painter.LineTo(new Vector2(mast + width, top + height * .45f)); painter.LineTo(new Vector2(mast + 1, top + height)); painter.ClosePath(); painter.Fill(); }
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
                AddInfo(panel, "Cámara: rueda, arrastre y teclado en PC; pellizco y gesto directo en pantallas táctiles.");
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
            AddTitle(panel, "CLASIFICACIÓN · CIUDADES");
            rankingLabels.Clear();
            var order = RankedPlayers();
            for (int rank = 0; rank < order.Count; rank++)
            {
                int player = order[rank]; var label = RtsUiStyle.Label((rank + 1) + ". " + VisualFactory.TeamName(player) + " · " + hud.PlayerCities[player] + " ciudades · " + hud.PlayerMobile[player] + " móviles / " + hud.PlayerGuards[player] + " guardias", null, 14); label.style.color = RtsUiStyle.Muted;label.style.whiteSpace=WhiteSpace.Normal; label.style.marginBottom = 7; panel.Add(label); rankingLabels.Add(label);
            }
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
            var title = RtsUiStyle.Title(text, null, 17);title.style.whiteSpace=WhiteSpace.Normal; title.style.marginBottom = 6; root.Add(title);
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
