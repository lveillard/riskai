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
        RtsIncomeRing incomeRing;
        VisualElement incomeButton;
        VisualElement startCountdown, pauseNotice;
        Label startCountdownNumber;
        VisualElement startCountdownProgress;
        readonly Label[] startCountdownMilestones = new Label[3];
        Button pauseButton;
        int retainedTab;
        bool showMinimap = true;
        int retainedContextKey;
        bool lastCompact, lastPortrait, lastFooterVisible;
        int lastModalKind;
        // Holding Tab shows the non-modal ranking board (BattleHud.Overlay), not a modal sheet.
        int ModalKind => session.Winner >= 0 ? 3 : controller.HelpVisible ? 1 : 0;
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
        bool BuildingSelection => controller.SelectedTowns.Count+controller.SelectedHarbors.Count>0;
        bool HasSelection => controller && (controller.SelectedTown || controller.SelectedHarbor || controller.SelectedCamp || controller.InspectedTarget || controller.Selection.Count + controller.Fleet.Count > 0);
        // Compact layouts show the map inside the footer (tab 3); desktop keeps it in its own
        // always-on console at the bottom-right, independent of the selection footer.
        bool FooterVisible => HasSelection || (UiViewport.IsCompact && showMinimap && retainedTab == 3);
        bool MinimapVisible => !UiViewport.IsCompact ? showMinimap : showMinimap && FooterVisible && !FooterCollapsed && (!UiViewport.IsPortrait || retainedTab == 3);

        // Compact bars are a single row of icon+number items in both orientations.
        float RequestedHeaderHeight => !UiViewport.IsCompact ? 48 : UiViewport.MinimumTouchTarget + 6;
        // Phone portrait keeps the footer within ~30% of the screen; it can collapse further to one row.
        float RequestedFooterHeight => !FooterVisible ? 0 : FooterCollapsed ? CollapsedFooterHeight :
            UiViewport.IsPortrait ? Mathf.Min(210, UiViewport.LogicalHeight * .3f) : UiViewport.IsCompact ? 164 : 188;
        float HeaderHeight => Mathf.Max(0, (UiViewport.SafeRect.yMax - UiViewport.WorldRect.yMax) / UiViewport.Scale);
        float FooterHeight => Mathf.Max(0, (UiViewport.BottomPixels - UiViewport.SafeRect.yMin) / UiViewport.Scale);

        void ConfigureViewport()
        {
            UiViewport.SetHudHeights(RequestedHeaderHeight, RequestedFooterHeight);
            // Keep one stable camera framing while the contextual footer opens and
            // closes. Hiding it reveals more map below instead of moving the world.
            UiViewport.SetCameraHudHeights(RequestedHeaderHeight,UiViewport.IsPortrait?210:188);
        }

        void InitializeRetainedUi()
        {
            retainedUi = RtsUiRuntime.Attach(gameObject, "Battle HUD", 20);
            showMinimap = !UiViewport.IsPortrait;
            LoadHudPreferences();
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
                // Entering the desktop layout (e.g. a web canvas that starts small) restores the
                // desktop default instead of keeping a compact-layout state.
                if (!UiViewport.IsCompact && (lastCompact || orientationChanged)) showMinimap = DesktopMinimapPreference();
                BuildRetainedUi(false);
                return;
            }
            RefreshRankingBoard(false);
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
            if (goldLabel != null) goldLabel.text = GameText.Localize(GoldText);
            if (citiesLabel != null) citiesLabel.text = GameText.Localize(CitiesText);
            if (populationLabel != null) populationLabel.text = GameText.Localize(PopulationText);
            if (roundLabel != null)
            {
                // Live economy values keep the dial smooth between HUD snapshots.
                int round = session.Economy.Round; float elapsed = session.Economy.ElapsedInRound;
                roundLabel.text = GameText.Localize(IncomeCountdown.Label(round, elapsed, UiViewport.IsCompact));
                if (incomeRing != null) incomeRing.Progress = IncomeCountdown.Progress(elapsed);
                if (incomeButton != null) incomeButton.tooltip = GameText.Localize(IncomeCountdown.Detail(round, elapsed, hud.Income));
            }
            if (pauseButton != null) { pauseButton.text = GameText.Localize(session.Paused ? "Continuar" : "Pausa");pauseButton.SetEnabled(!session.IsStarting); }
            if (modalPauseButton != null) { modalPauseButton.text = GameText.Localize(session.Paused ? "CONTINUAR" : "PAUSA");modalPauseButton.SetEnabled(!session.IsStarting); }
            if(pauseNotice!=null)pauseNotice.style.display=session.Paused&&!session.IsStarting&&session.Winner<0&&!controller.HelpVisible&&!controller.ScoreboardVisible?DisplayStyle.Flex:DisplayStyle.None;
            for(int i=0;i<liveContext.Count;i++)liveContext[i]();
            UpdateRankingLabels();
        }

        void RebuildContext()
        {
            if (FooterCollapsed && footer != null)
            {
                footer.Clear(); liveContext.Clear(); RememberRoster();
                BuildCollapsedFooter(footer);
                return;
            }
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
                key = key * 23 + session.Economy.Gold[0];
                key = key * 29 + (controller.InspectedTarget ? controller.InspectedTarget.EntityId : 0);
                key = key * 41 + (controller.SelectedTown ? controller.SelectedTown.GetInstanceID() * 17 + controller.SelectedTown.State.Owner : 0);
                key = key * 43 + (controller.SelectedHarbor ? controller.SelectedHarbor.GetInstanceID() * 17 + controller.SelectedHarbor.Owner : 0);
                key = key * 47 + (controller.SelectedCamp ? controller.SelectedCamp.GetInstanceID() : 0);
                for (int i = 0; i < controller.SelectedTowns.Count; i++) key = key * 53 + controller.SelectedTowns[i].GetInstanceID() * 17 + controller.SelectedTowns[i].State.Owner + controller.SelectedTowns[i].QueueCount * 97 + controller.SelectedTowns[i].State.Level * 101;
                for (int i = 0; i < controller.SelectedHarbors.Count; i++) key = key * 59 + controller.SelectedHarbors[i].GetInstanceID() * 17 + controller.SelectedHarbors[i].Owner + controller.SelectedHarbors[i].QueueCount * 103 + controller.SelectedHarbors[i].LandQueueCount * 107;
                for(int i=0;i<controller.Fleet.Count;i++)if(controller.Fleet[i])key=key*61+controller.Fleet[i].EntityId*17+controller.Fleet[i].CargoCount;
                key=key*67+controller.ProductionPage(ProductionBuilding.City)*3+controller.ProductionPage(ProductionBuilding.Harbor);
                return key;
            }
        }

        void BuildRetainedUi(bool initial)
        {
            ConfigureViewport();
            liveContext.Clear(); rankingRows.Clear(); modalPauseButton=null;
            context=null;wideContext=null;footer=null;incomeRing=null;incomeButton=null;
            lastCompact = UiViewport.IsCompact; lastPortrait = UiViewport.IsPortrait;
            lastModalKind=ModalKind;lastFooterVisible=FooterVisible;
            retainedContextKey = ContextKey(); RememberRoster(); lastScreen = new Vector2(Screen.width, Screen.height); lastSafe = UiViewport.SafeRect; lastDensity=UiViewport.Scale; nextRetainedLabelRefresh = Time.unscaledTime;
            retainedRoot = new VisualElement { name = "Battle retained UI", pickingMode = PickingMode.Ignore };
            retainedRoot.style.flexGrow = 1;
            BuildHeader(retainedRoot);
            if(FooterVisible) { BuildFooter(retainedRoot); BuildFooterHandle(retainedRoot); }
            BuildDesktopMinimapPanel(retainedRoot);
            BuildWorldQueues(retainedRoot);
            if (MinimapVisible) BuildMinimapHitOverlay(retainedRoot);
            if (lastModalKind!=0) BuildModal(retainedRoot);
            BuildStartCountdown(retainedRoot);
            BuildPauseNotice(retainedRoot);
            // Last, so the pause plaque never covers the board (the board hides while a modal is open).
            rankingBoard=null;lastRankingVisible=false;
            if (RankingVisible) BuildRankingBoard(retainedRoot);
            // An open menu/result must draw above the feedback overlay (log, toasts) and take its pointer events.
            retainedUi.SortingOrder = lastModalKind!=0 ? FeedbackSortingOrder + 1 : 20;
            retainedUi.SetContent(retainedRoot);
        }

        void BuildPauseNotice(VisualElement root)
        {
            pauseNotice=RtsUiStyle.Panel("HUD paused notice");pauseNotice.pickingMode=PickingMode.Ignore;
            pauseNotice.style.position=Position.Absolute;pauseNotice.style.left=Length.Percent(50);pauseNotice.style.top=Length.Percent(34);
            pauseNotice.style.width=UiViewport.IsCompact?210:280;pauseNotice.style.marginLeft=UiViewport.IsCompact?-105:-140;
            pauseNotice.style.alignItems=Align.Center;pauseNotice.style.paddingTop=16;pauseNotice.style.paddingBottom=16;
            var title=RtsUiStyle.Title("PAUSADO","Paused title",UiViewport.IsCompact?28:36);title.pickingMode=PickingMode.Ignore;pauseNotice.Add(title);
            root.Add(pauseNotice);
            pauseNotice.style.display=session.Paused&&!session.IsStarting&&session.Winner<0&&!controller.HelpVisible&&!controller.ScoreboardVisible?DisplayStyle.Flex:DisplayStyle.None;
        }

        void BuildStartCountdown(VisualElement root)
        {
            startCountdown=RtsUiStyle.Panel("Match start countdown");startCountdown.pickingMode=PickingMode.Ignore;
            startCountdown.style.position=Position.Absolute;startCountdown.style.left=Length.Percent(50);
            startCountdown.style.top=Length.Percent(UiViewport.IsPortrait?23:27);
            float countdownWidth=Mathf.Min(UiViewport.IsPortrait?360:540,UiViewport.LogicalWidth-24);
            startCountdown.style.width=countdownWidth;startCountdown.style.marginLeft=-countdownWidth*.5f;
            startCountdown.style.paddingLeft=UiViewport.IsCompact?16:22;startCountdown.style.paddingRight=UiViewport.IsCompact?16:22;
            startCountdown.style.paddingTop=14;startCountdown.style.paddingBottom=16;

            var heading=new VisualElement();RtsUiStyle.Row(heading);heading.pickingMode=PickingMode.Ignore;
            var title=RtsUiStyle.Title("RIESGUS · DESPLIEGUE",null,UiViewport.IsCompact?15:18);title.pickingMode=PickingMode.Ignore;title.style.flexGrow=1;
            heading.Add(title);
            startCountdownNumber=RtsUiStyle.Title("5","Countdown number",UiViewport.IsCompact?38:44);startCountdownNumber.pickingMode=PickingMode.Ignore;
            heading.Add(startCountdownNumber);startCountdown.Add(heading);

            var progressTrack=new VisualElement { name="Countdown progress",pickingMode=PickingMode.Ignore };
            progressTrack.style.height=4;progressTrack.style.marginTop=4;progressTrack.style.marginBottom=10;progressTrack.style.backgroundColor=RtsUiStyle.Slate;
            startCountdownProgress=new VisualElement { name="Countdown progress fill",pickingMode=PickingMode.Ignore };
            startCountdownProgress.style.height=4;startCountdownProgress.style.backgroundColor=RtsUiStyle.Gold;progressTrack.Add(startCountdownProgress);startCountdown.Add(progressTrack);

            var milestones=new VisualElement { name="Countdown milestones",pickingMode=PickingMode.Ignore };
            string[] names={
                "1 · Selecciona una ciudad. Crea unidades e invade.",
                "2 · La guarnición no sale sin un relevo aliado.",
                "3 · Completa países para obtener oro y refuerzos."
            };
            for(int i=0;i<startCountdownMilestones.Length;i++)
            {
                var label=RtsUiStyle.Label(names[i],"Countdown milestone "+(i+1),UiViewport.IsCompact?13:14);label.pickingMode=PickingMode.Ignore;
                label.style.whiteSpace=WhiteSpace.Normal;label.style.marginBottom=i<2?5:0;
                startCountdownMilestones[i]=label;milestones.Add(label);
            }
            startCountdown.Add(milestones);root.Add(startCountdown);RefreshStartCountdown();
        }

        void RefreshStartCountdown()
        {
            if(startCountdown==null)return;
            startCountdown.style.display=session.IsStarting?DisplayStyle.Flex:DisplayStyle.None;
            if(!session.IsStarting)return;
            float remaining=session.StartCountdownRemaining;
            startCountdownNumber.text=Mathf.CeilToInt(remaining).ToString();
            startCountdownProgress.style.width=Length.Percent(Mathf.Clamp01((5f-remaining)/5f)*100);
            int stage=remaining>3f?0:remaining>1f?1:2;
            for(int i=0;i<startCountdownMilestones.Length;i++)
            {
                startCountdownMilestones[i].style.color=i==stage?RtsUiStyle.Gold:RtsUiStyle.Muted;
                startCountdownMilestones[i].style.opacity=i==stage?1:.82f;
            }
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
            var title = RtsUiStyle.Title("RIESGUS", null, 17); header.Add(title);
            AddGoldDisplay(header);
            AddIncomeDisplay(header);
            AddCitiesDisplay(header);
            AddPopulationDisplay(header);
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore }; spacer.style.flexGrow = 1; header.Add(spacer);
            // Ranking and Map live in the quick bar beside the minimap.
            pauseButton = HeaderButton(session.Paused ? "Continuar" : "Pausa", () => { session.TogglePause(); UpdateRetainedLabels(); }); header.Add(pauseButton);
            header.Add(HeaderButton("Menú", OpenMenu));
        }

        // One row on every phone/tablet orientation: icon+number items, details on hover/long-press.
        void BuildCompactHeader()
        {
            var top = new VisualElement { name="HUD resource row" }; RtsUiStyle.Row(top);top.style.height=UiViewport.MinimumTouchTarget;top.style.flexShrink=0;
            AddGoldDisplay(top);
            AddIncomeDisplay(top);
            AddCitiesDisplay(top);
            AddPopulationDisplay(top);
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore }; spacer.style.flexGrow = 1; spacer.style.flexShrink = 1; top.Add(spacer);
            top.Add(HeaderButton("Menú", OpenMenu));
            header.Add(top);
        }

        Label HeaderLabel(string text)
        {
            var label = RtsUiStyle.Label(text, null, UiViewport.IsTouchLayout?12:11); label.style.marginLeft = UiViewport.IsCompact?2:4; label.style.marginRight = UiViewport.IsCompact?2:4; label.style.marginTop=0;label.style.marginBottom=0;
            // Never wrap a bar item onto a second line; the tooltip carries the long form.
            label.style.whiteSpace=WhiteSpace.NoWrap;label.style.overflow=Overflow.Hidden;label.style.textOverflow=TextOverflow.Ellipsis;label.style.flexShrink=1;label.style.minWidth=0;
            return label;
        }
        string CitiesText => UiViewport.IsCompact ? hud.OwnedTowns + "/" + MapLayout.Towns.Length : hud.OwnedTowns + " / " + MapLayout.Towns.Length;

        static Button HeaderButton(string text, System.Action action)
        {
            var button = RtsUiStyle.Button(text, action);
            float height=UiViewport.IsTouchLayout?UiViewport.MinimumTouchTarget:36;
            button.style.height=button.style.minHeight=height;
            button.style.paddingTop=2;button.style.paddingBottom=2;button.style.marginBottom=0;button.style.marginTop=0;button.style.marginLeft=0;button.style.marginRight=4;
            button.style.fontSize=11;button.style.flexShrink=0;
            if(UiViewport.IsCompact){button.style.paddingLeft=button.style.paddingRight=8;button.style.minWidth=UiViewport.MinimumTouchTarget;}
            return button;
        }

        void OpenMenu() { controller.CancelCursor(); menuTab = 0; controller.HelpVisible = true; BuildRetainedUi(false); }
        void CloseModal() { controller.CancelCursor(); controller.HelpVisible = false; menuTab = 0; BuildRetainedUi(false); }
        void ToggleMinimap()
        {
            bool visible=MinimapVisible;showMinimap=!visible;
            if(!UiViewport.IsCompact)SavePreference(MinimapPreference,showMinimap?1:0);
            else { retainedTab=visible?0:3; if(!visible)footerCollapsed=false; }
            BuildRetainedUi(false);
        }

        void BuildFooter(VisualElement root)
        {
            footer = RtsUiStyle.Panel("HUD footer"); footer.style.position = Position.Absolute;
            footer.style.left = UiViewport.IsCompact && MinimapVisible && !UiViewport.IsPortrait ? MinimapRect().xMax + 8 - UiViewport.SafeRect.xMin/UiViewport.Scale : 0;
            // Desktop: selection and command grid take the left/centre; the minimap console owns the right.
            footer.style.right = UiViewport.IsCompact ? 0 : DesktopPanelWidth + 6; footer.style.bottom = 0;
            footer.style.height = FooterHeight;
            footer.style.paddingTop=6;footer.style.paddingBottom=6;
            wideFooter = !UiViewport.IsCompact;
            if (FooterCollapsed)
            {
                footer.style.paddingLeft=8;footer.style.paddingRight=8;
                context=null;wideContext=null;
                BuildCollapsedFooter(footer);root.Add(footer);
                return;
            }
            if (UiViewport.IsCompact) { footer.style.paddingLeft=10; footer.style.paddingRight=10; }
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
            selection.style.width=Length.Percent(BuildingSelection?100:43);
            contextual.style.display=BuildingSelection?DisplayStyle.None:DisplayStyle.Flex;
            if(BuildingSelection){BuildContext(selection);return;}
            BuildSelectionWithPortrait(selection);
            if (controller.SelectedCamp)
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

        static string PortraitResource(UnitKind kind) => UnitVariantViews.PortraitResource(kind);

        Rect MinimapRect()
        {
            if (!UiViewport.IsCompact) return DesktopMinimapRect();
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
            if(BuildingSelection)
            {
                BuildCommandCard(root);
                return;
            }
            if(controller.Selection.Count+controller.Fleet.Count>0)BuildOrders(root);
            BuildSelectionWithPortrait(root);
        }

        void BuildSelection(VisualElement root)
        {
            if (controller.SelectedCamp)
            {
                var camp = controller.SelectedCamp; AddTitle(root, GameText.Localize(camp.DisplayName).ToUpperInvariant());
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
                        System.Action refreshTown = () => button.text = GameText.Localize(town ? town.DisplayName + " · " + VisualFactory.TeamName(town.State.Owner) : "Ciudad retirada");
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
                foreach(var ship in controller.Fleet)if(ship&&ship.Type.CanTransport&&ship.CargoCount>0)BuildCargoRoster(root,ship);
                return;
            }
            if (controller.InspectedTarget is Soldier inspected)
            {
                var profile = UnitCatalog.Get(inspected.Kind);
                AddTitle(root, UnitCatalog.Get(inspected.Kind).Name.ToUpperInvariant());
                LiveInfo(root,()=>SoldierStats(inspected));
                return;
            }
            if (controller.Selection.Count > 0)
            {
                AddTitle(root, controller.Selection.Count==1?UnitCatalog.Get(controller.Selection[0].Kind).Name.ToUpperInvariant():controller.Selection.Count + " TROPAS SELECCIONADAS");
                if (controller.Selection.Count == 1)
                {
                    var unit = controller.Selection[0]; var profile = UnitCatalog.Get(unit.Kind);
                    LiveInfo(root,()=>SoldierStats(unit));
                }
                else BuildSelectionRoster(root);
                return;
            }

        }

        // Unit command card. Keys are the unit hotkeys that stay live while no building is selected.
        (string title,RtsHudGlyph glyph,System.Action action,string key)[] PrimaryOrders() => new (string,RtsHudGlyph,System.Action,string)[]
        {
            ("Mover",RtsHudGlyph.Move,controller.ArmMove,"M"),("Atacar",RtsHudGlyph.Sword,controller.ArmAttack,"A"),
            ("Patrullar",RtsHudGlyph.Patrol,controller.ArmPatrol,"P"),("Encolar",RtsHudGlyph.Patrol,controller.ToggleQueueOrders,null),
            ("Detener",RtsHudGlyph.Stop,controller.Stop,"S"),
            ("Mantener",RtsHudGlyph.Shield,controller.Hold,"H"),("Centrar",RtsHudGlyph.Focus,controller.FocusSelection,null)
        };
        (string title,RtsHudGlyph glyph,System.Action action,string key)[] NavalOrders() => new (string,RtsHudGlyph,System.Action,string)[]
        {
            ("Embarcar",RtsHudGlyph.Board,controller.BoardNearby,"B"),("Desembarcar",RtsHudGlyph.Unload,controller.UnloadFleet,"D"),
            ("Puerto",RtsHudGlyph.City,controller.FocusHarbor,"F3")
        };

        void BuildOrders(VisualElement root)
        {
            var grid=new VisualElement { name="HUD direct actions" };
            var row=new VisualElement { name="HUD primary action row" };RtsUiStyle.Row(row);
            row.style.flexShrink=0;grid.Add(row);
            foreach(var order in PrimaryOrders())row.Add(ActionButton(order.title,order.glyph,order.action,order.key));
            if(controller.Fleet.Count>0)
            {
                row=new VisualElement { name="HUD naval action row" };RtsUiStyle.Row(row);
                row.style.flexShrink=0;grid.Add(row);
                foreach(var order in NavalOrders())row.Add(ActionButton(order.title,order.glyph,order.action,order.key));
            }
            root.Add(grid);
        }

        void BuildOrderCells(VisualElement strip,float size)
        {
            foreach(var order in PrimaryOrders()){var button=ActionButton(order.title,order.glyph,order.action,null);SquareCell(button,size);strip.Add(button);}
            if(controller.Fleet.Count>0)
                foreach(var order in NavalOrders()){var button=ActionButton(order.title,order.glyph,order.action,null);SquareCell(button,size);strip.Add(button);}
        }

        static string UnitTooltip(UnitKind kind)
        {
            var profile=UnitCatalog.Get(kind);var mana=UnitCatalog.Get(kind).Mana;
            return UnitCatalog.Get(kind).Role+" · "+profile.MaxHealth+" vida · "+UnitCatalog.Get(kind).Weapon.DamageText+" "+profile.AttackType+" · alcance "+profile.Weapon.Range+" · armadura "+profile.Armor+" "+profile.ArmorType+
                (mana.Enabled?" · maná "+mana.Maximum:"");
        }

        static Button GridButton(string text, System.Action action)
        {
            var button = RtsUiStyle.Button(text, action);
            if (UiViewport.IsCompact) button.style.width = Length.Percent(46);
            else button.style.minWidth = 126;
            return button;
        }

        static string PurchaseTitle(string product,ProductionBatchPreview preview) =>
            preview.IsGrouped?product+" ×"+preview.CandidateCount:product;

        static string PurchaseCost(ProductionBatchPreview preview,int unitCost,string hotkey)
        {
            string cost=preview.IsGrouped
                ? (preview.PlannedCount==preview.CandidateCount?preview.PlannedCost+" oro":"Hasta ×"+preview.PlannedCount+" · "+preview.PlannedCost+" oro")
                : unitCost+" oro";
            if(preview.UnfundedCount>0)cost+=" · falta oro";
            return string.IsNullOrEmpty(hotkey)?cost:cost+" · "+hotkey;
        }

        static readonly Dictionary<string, Texture2D> portraitCache = new Dictionary<string, Texture2D>();
        static Texture2D CachedPortrait(string resource)
        {
            if (!portraitCache.TryGetValue(resource, out var texture)) { texture = Resources.Load<Texture2D>(resource); portraitCache.Add(resource, texture); }
            return texture;
        }

        List<int> RankedPlayers()
        {
            var order = new List<int>(); for (int i = 0; i < session.PlayerCount; i++) order.Add(i);
            order.Sort((a, b) => { int compare = hud.PlayerCities[b].CompareTo(hud.PlayerCities[a]); return compare != 0 ? compare : a.CompareTo(b); });
            return order;
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
            root.Add(label);liveContext.Add(()=>label.text=GameText.Localize(value()));
        }
        static string SoldierStats(Soldier unit)
        {
            if(!unit||!unit.IsAlive)return "Unidad eliminada";
            var profile=UnitCatalog.Get(unit.Kind);
            var mana=unit.Mana;
            return UnitCatalog.Get(unit.Kind).Name+" · "+Mathf.CeilToInt(unit.Health)+" / "+profile.MaxHealth+" vida"+(mana!=null&&mana.Enabled?" · "+Mathf.FloorToInt(mana.Current)+" / "+mana.Maximum+" maná":"")+" · "+UnitCatalog.Get(unit.Kind).Weapon.DamageText+" "+profile.AttackType+" · alcance "+profile.Weapon.Range+" · armadura "+profile.Armor+" "+profile.ArmorType+(unit.IsRoaring?" · rugido +25%":"");
        }
    }
}
