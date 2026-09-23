using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>
    /// Always-on desktop minimap console (bottom-right), the quick bar of discreet toggles
    /// (effects, music, ranking, map, chat) and the WC3-style multiboard ranking overlay.
    /// </summary>
    public sealed partial class BattleHud
    {
        const float DesktopConsoleHeight = 188;
        // The desktop minimap is visible unless the player explicitly hid it.
        public const string MinimapPreference = "riskai.hud.minimap";
        VisualElement minimapPanel, rankingBoard;
        bool rankingShown, lastRankingVisible;
        readonly List<QuickButton> quickButtons = new List<QuickButton>();
        readonly int[] countriesOwned = new int[PlayerRules.MaxPlayers];

        sealed class QuickButton
        {
            public Button Button; public RtsQuickIcon Icon;
            public System.Func<bool> Struck, Active;
        }

        /// <summary>Ranking multiboard: toggled from the quick bar/cities, or shown while Tab is held.</summary>
        bool RankingVisible => session && session.Winner < 0 && !controller.HelpVisible && (rankingShown || controller.ScoreboardVisible);
        static float QuickSize => UiViewport.IsTouchLayout ? UiViewport.MinimumTouchTarget - 4 : 28;
        // Desktop console geometry, logical pixels. The map keeps the source 4:3 aspect.
        static float DesktopMapHeight => DesktopConsoleHeight - 22 - QuickSize;
        static float DesktopMapWidth => DesktopMapHeight * (208f / 156f);
        static float DesktopPanelWidth => Mathf.Max(DesktopMapWidth, 5 * (QuickSize + 3)) + 16;
        float DesktopPanelHeight => MinimapVisible ? DesktopConsoleHeight : QuickSize + 14;

        void LoadHudPreferences()
        {
            try
            {
                if (!UiViewport.IsCompact) showMinimap = DesktopMinimapPreference();
                if (controller.CameraRig != null && PlayerPrefs.HasKey(CameraSpeedPreference)) controller.CameraRig.PanSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(CameraSpeedPreference), .5f, 3f);
                if (PlayerPrefs.HasKey(EdgePanPreference)) controller.EdgePan = PlayerPrefs.GetInt(EdgePanPreference) != 0;
            }
            catch (System.Exception) { }
        }

        /// <summary>The desktop minimap: visible unless the player explicitly hid it in this build line.</summary>
        public static bool DesktopMinimapPreference()
        {
            try { return !PlayerPrefs.HasKey(MinimapPreference) || PlayerPrefs.GetInt(MinimapPreference) != 0; }
            catch (System.Exception) { return true; }
        }
        static void SavePreference(string key, int value) { try { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); } catch (System.Exception) { } }
        static void SavePreference(string key, float value) { try { PlayerPrefs.SetFloat(key, value); PlayerPrefs.Save(); } catch (System.Exception) { } }

        /// <summary>Desktop minimap console at the bottom-right: quick bar on top, map below.</summary>
        void BuildDesktopMinimapPanel(VisualElement root)
        {
            minimapPanel = null;
            if (UiViewport.IsCompact) return;
            minimapPanel = RtsUiStyle.Panel("HUD minimap panel");
            minimapPanel.style.position = Position.Absolute; minimapPanel.style.right = 0; minimapPanel.style.bottom = 0;
            minimapPanel.style.width = DesktopPanelWidth; minimapPanel.style.height = DesktopPanelHeight;
            minimapPanel.style.paddingLeft = minimapPanel.style.paddingRight = 8; minimapPanel.style.paddingTop = 6; minimapPanel.style.paddingBottom = 6;
            var bar = BuildQuickBar(false); bar.style.justifyContent = Justify.Center; minimapPanel.Add(bar);
            root.Add(minimapPanel);
        }

        /// <summary>Logical (top-left origin) rectangle of the desktop map inside its console.</summary>
        Rect DesktopMinimapRect()
        {
            float scale = UiViewport.Scale;
            float right = UiViewport.SafeRect.xMax / scale, bottom = (Screen.height - UiViewport.SafeRect.yMin) / scale;
            float panelLeft = right - DesktopPanelWidth;
            return new Rect(panelLeft + (DesktopPanelWidth - DesktopMapWidth) * .5f, bottom - 8 - DesktopMapHeight, DesktopMapWidth, DesktopMapHeight);
        }

        VisualElement BuildQuickBar(bool compact)
        {
            quickButtons.RemoveAll(q => q.Button == null || q.Button.panel == null);
            var bar = new VisualElement { name = compact ? "HUD quick bar compact" : "HUD quick bar", pickingMode = PickingMode.Ignore };
            RtsUiStyle.Row(bar); bar.style.flexShrink = 0;
            AddQuick(bar, RtsQuickGlyph.Speaker, "Efectos de sonido (F7)", "HUD quick effects", () => ToggleEffects(false), () => Sfx.Muted, null);
            AddQuick(bar, RtsQuickGlyph.Note, "Música (F8)", "HUD quick music", () => Music.ToggleMusic(), () => !Music.MusicEnabled, null);
            AddQuick(bar, RtsQuickGlyph.Ranking, "Clasificación (Tab)", "HUD quick ranking", ToggleRanking, null, () => RankingVisible);
            AddQuick(bar, RtsQuickGlyph.Map, "Minimapa (F9)", "HUD quick map", ToggleMinimap, null, () => MinimapVisible);
            var chatQuick = AddQuick(bar, RtsQuickGlyph.Chat, "Chat (Intro)", "HUD quick chat", OpenChat, null, () => chatOpen);
            // Touch browsers need the DOM input armed inside the same finger gesture.
            chatQuick.RegisterCallback<PointerDownEvent>(_ => { if (WebChatInput.Supported) WebChatInput.Arm(ChatPlaceholder()); }, TrickleDown.TrickleDown);
            return bar;
        }

        Button AddQuick(VisualElement bar, RtsQuickGlyph glyph, string tooltip, string name, System.Action action, System.Func<bool> struck, System.Func<bool> active)
        {
            var button = RtsUiStyle.Button("", action, name);
            float size = QuickSize;
            button.style.width = button.style.minWidth = button.style.height = button.style.minHeight = size;
            button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 0;
            button.style.marginLeft = 0; button.style.marginRight = 3; button.style.marginTop = button.style.marginBottom = 0;
            button.style.alignItems = Align.Center; button.style.justifyContent = Justify.Center;
            button.style.backgroundColor = new Color(.06f, .065f, .055f, .88f);
            button.tooltip = GameText.Localize(tooltip);
            var icon = new RtsQuickIcon(glyph); icon.style.width = icon.style.height = Mathf.Round(size * .62f);
            button.Add(icon); bar.Add(button);
            var quick = new QuickButton { Button = button, Icon = icon, Struck = struck, Active = active };
            quickButtons.Add(quick); RefreshQuick(quick);
            return button;
        }

        void RefreshQuickBar()
        {
            for (int i = quickButtons.Count - 1; i >= 0; i--)
            {
                var quick = quickButtons[i];
                if (quick.Button == null || quick.Button.panel == null) { quickButtons.RemoveAt(i); continue; }
                RefreshQuick(quick);
            }
        }

        static void RefreshQuick(QuickButton quick)
        {
            if (quick.Struck != null) quick.Icon.Struck = quick.Struck();
            if (quick.Active != null)
            {
                bool on = quick.Active();
                quick.Icon.Active = on;
                quick.Button.style.borderTopColor = quick.Button.style.borderBottomColor = quick.Button.style.borderLeftColor = quick.Button.style.borderRightColor = on ? RtsUiStyle.Gold : RtsUiStyle.Bronze;
            }
        }

        void ToggleEffects(bool announce)
        {
            Sfx.SetMuted(!Sfx.Muted);
            if (announce) session.Message(Sfx.Muted ? "Efectos silenciados" : "Efectos activados", MessageKind.Info);
        }

        /// <summary>F7 effects, F9 minimap. F8 music lives in RtsController; Enter opens chat.</summary>
        void PollQuickKeys(Keyboard keyboard)
        {
            if (keyboard == null || ChatInput.IsTyping || controller.HelpVisible || session.Winner >= 0) return;
            if (keyboard.f7Key.wasPressedThisFrame) ToggleEffects(true);
            if (keyboard.f9Key.wasPressedThisFrame) ToggleMinimap();
        }

        public void ShowPlayers()
        {
            controller.CancelCursor();
            if (controller.HelpVisible) { controller.HelpVisible = false; menuTab = 0; }
            rankingShown = true;
            BuildRetainedUi(false);
        }

        public void HideRanking() { if (!rankingShown) return; rankingShown = false; RefreshRankingBoard(true); }
        void ToggleRanking() { rankingShown = !RankingVisible; RefreshRankingBoard(true); }

        void RefreshRankingBoard(bool force)
        {
            bool visible = RankingVisible;
            if (!force && visible == lastRankingVisible) return;
            lastRankingVisible = visible;
            rankingBoard?.RemoveFromHierarchy();
            rankingBoard = null; rankingRows.Clear();
            if (visible && retainedRoot != null) BuildRankingBoard(retainedRoot);
        }

        /// <summary>WC3 Risk multiboard: colour, player, cities, units, income, countries; sorted by cities.</summary>
        void BuildRankingBoard(VisualElement root)
        {
            lastRankingVisible = true;
            rankingRows.Clear();
            rankingBoard = new VisualElement { name = "HUD ranking board", pickingMode = UiViewport.IsCompact ? PickingMode.Position : PickingMode.Ignore };
            var board = rankingBoard;
            board.style.position = Position.Absolute;
            float width = Mathf.Min(360, UiViewport.LogicalWidth - 12);
            board.style.width = width;
            if (UiViewport.IsCompact) { board.style.right = 6; board.style.top = HeaderHeight + 6; }
            else { board.style.right = 0; board.style.bottom = DesktopPanelHeight + 6; }
            float available = UiViewport.LogicalHeight - HeaderHeight - (UiViewport.IsCompact ? FooterHeight : DesktopPanelHeight) - 20;
            board.style.maxHeight = Mathf.Max(120, available);
            board.style.backgroundColor = new Color(.03f, .035f, .03f, UiViewport.IsCompact ? .93f : .86f);
            board.style.borderTopWidth = board.style.borderBottomWidth = board.style.borderLeftWidth = board.style.borderRightWidth = 1;
            board.style.borderTopColor = board.style.borderBottomColor = board.style.borderLeftColor = board.style.borderRightColor = new Color(.72f, .56f, .30f, .7f);
            board.style.paddingLeft = board.style.paddingRight = 8; board.style.paddingTop = 5; board.style.paddingBottom = 6;

            var head = new VisualElement { name = "HUD ranking header", pickingMode = PickingMode.Ignore }; RtsUiStyle.Row(head);
            head.style.marginBottom = 3; head.style.borderBottomWidth = 1; head.style.borderBottomColor = new Color(.72f, .56f, .30f, .45f); head.style.paddingBottom = 2;
            var title = BoardLabel("CLASIFICACIÓN", 0, false); title.style.flexGrow = 1; title.style.color = RtsUiStyle.Gold; head.Add(title);
            head.Add(BoardHeader(RtsHudGlyph.City, "Ciudades"));
            head.Add(BoardHeader(RtsHudGlyph.Sword, "Unidades totales, incluidos defensores y barcos"));
            var coin = new VisualElement { pickingMode = PickingMode.Ignore }; coin.style.width = ColumnWidth; coin.style.alignItems = Align.FlexEnd;
            var gold = new RtsGoldIcon(); gold.style.width = gold.style.height = 15; gold.tooltip = GameText.Localize("Ingreso por ronda"); coin.Add(gold); head.Add(coin);
            head.Add(BoardHeader(RtsHudGlyph.Shield, "Países completos"));
            board.Add(head);

            var rows = new ScrollView(ScrollViewMode.Vertical) { name = "HUD ranking rows" }; rows.style.flexShrink = 1; rows.style.minHeight = 0;
            rows.verticalScrollerVisibility = ScrollerVisibility.Hidden; rows.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            if (!UiViewport.IsCompact) rows.pickingMode = PickingMode.Ignore;
            for (int rank = 0; rank < session.PlayerCount; rank++)
            {
                var row = new RankingRow { Root = new VisualElement { name = "HUD ranking row " + rank, pickingMode = PickingMode.Ignore } };
                RtsUiStyle.Row(row.Root); row.Root.style.height = UiViewport.IsTouchLayout ? 22 : 19; row.Root.style.flexShrink = 0;
                row.Chip = new VisualElement { pickingMode = PickingMode.Ignore }; row.Chip.style.width = row.Chip.style.height = 10; row.Chip.style.marginRight = 6; row.Chip.style.flexShrink = 0;
                row.Root.Add(row.Chip);
                row.Name = BoardLabel("", 0, false); row.Name.style.flexGrow = 1; row.Name.style.flexShrink = 1; row.Root.Add(row.Name);
                row.Cities = BoardLabel("", ColumnWidth, true); row.Root.Add(row.Cities);
                row.Units = BoardLabel("", ColumnWidth, true); row.Root.Add(row.Units);
                row.Income = BoardLabel("", ColumnWidth, true); row.Income.style.color = RtsUiStyle.Gold; row.Root.Add(row.Income);
                row.Countries = BoardLabel("", ColumnWidth, true); row.Root.Add(row.Countries);
                rows.Add(row.Root); rankingRows.Add(row);
            }
            board.Add(rows);
            root.Add(board);
            UpdateRankingLabels();
        }

        const float ColumnWidth = 36;

        static Label BoardLabel(string text, float width, bool number)
        {
            var label = RtsUiStyle.Label(text, null, UiViewport.IsTouchLayout ? 13 : 12); label.pickingMode = PickingMode.Ignore;
            label.style.marginLeft = label.style.marginRight = 0; label.style.paddingLeft = label.style.paddingRight = 0;
            label.style.whiteSpace = WhiteSpace.NoWrap; label.style.overflow = Overflow.Hidden; label.style.textOverflow = TextOverflow.Ellipsis; label.style.minWidth = 0;
            if (width > 0) { label.style.width = width; label.style.flexShrink = 0; }
            if (number) label.style.unityTextAlign = TextAnchor.MiddleRight;
            return label;
        }

        static VisualElement BoardHeader(RtsHudGlyph glyph, string tooltip)
        {
            var cell = new VisualElement(); cell.style.width = ColumnWidth; cell.style.alignItems = Align.FlexEnd; cell.style.flexShrink = 0;
            cell.tooltip = GameText.Localize(tooltip);
            var icon = new RtsHudIcon(glyph); icon.style.width = icon.style.height = 15; cell.Add(icon);
            return cell;
        }

        void UpdateRankingLabels()
        {
            if (rankingRows.Count == 0) return;
            System.Array.Clear(countriesOwned, 0, countriesOwned.Length);
            for (int i = 0; i < hud.Countries.Length; i++)
            {
                int owner = hud.Countries[i].Owner;
                if (PlayerRules.IsPlayer(owner)) countriesOwned[owner]++;
            }
            var order = RankedPlayers();
            for (int rank = 0; rank < rankingRows.Count && rank < order.Count; rank++)
            {
                int player = order[rank]; var row = rankingRows[rank];
                bool eliminated = session.IsPlayerEliminated(player);
                var colour = VisualFactory.TeamColor(player);
                row.Name.text = GameText.Localize((rank + 1) + ". " + VisualFactory.TeamName(player) + (eliminated ? " · ELIMINADO" : ""));
                row.Name.style.color = eliminated ? RtsUiStyle.Muted : player == 0 ? RtsUiStyle.Gold : Readable(colour);
                row.Name.style.unityFontStyleAndWeight = player == 0 ? FontStyle.Bold : FontStyle.Normal;
                if (row.Chip != null) row.Chip.style.backgroundColor = colour;
                row.Root.style.opacity = eliminated ? .45f : 1;
                row.Cities.text = hud.PlayerCities[player].ToString();
                row.Units.text = hud.PlayerUnits[player].ToString();
                if (row.Income != null) row.Income.text = eliminated ? "—" : "+" + session.Economy.Income(player);
                if (row.Countries != null) row.Countries.text = countriesOwned[player].ToString();
            }
        }

        /// <summary>World labels (IMGUI) never draw over the console or the ranking board.</summary>
        bool UnderHudPanel(float x, float y)
        {
            var point = new Vector2(x, y);
            if (minimapPanel != null && minimapPanel.panel != null && minimapPanel.worldBound.Contains(point)) return true;
            if (rankingBoard != null && rankingBoard.panel != null && rankingBoard.worldBound.Contains(point)) return true;
            return false;
        }
    }
}
