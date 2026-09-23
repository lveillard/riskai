using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>
    /// In-battle menu: a centred window with a close X, section tabs (Partida, Sonido, Cámara,
    /// Controles, Idioma) and real controls — sliders for volumes and camera speed, switches for
    /// on/off settings. Values persist through their owners (Sfx, Music, GameFeel, PlayerPrefs).
    /// </summary>
    public sealed partial class BattleHud
    {
        // Retained modal state. Income and population are detail pages opened from the top bar.
        const int MenuGame = 0, MenuControls = 1, MenuIncome = 3, MenuPopulation = 4, MenuSound = 5, MenuCamera = 6, MenuLanguage = 7;
        const string CameraSpeedPreference = "riskai.camera.speed", EdgePanPreference = "riskai.camera.edgepan";
        int menuTab;

        static readonly (int tab, string title)[] MenuSections =
        {
            (MenuGame, "Partida"), (MenuSound, "Sonido"), (MenuCamera, "Cámara"), (MenuControls, "Controles"), (MenuLanguage, "Idioma")
        };

        /// <summary>Opens the menu on a section ("game", "sound", "camera", "controls", "language"); used by UI captures.</summary>
        public void OpenMenuSection(string section)
        {
            controller.CancelCursor();
            menuTab = section == "sound" ? MenuSound : section == "camera" ? MenuCamera : section == "controls" ? MenuControls : section == "language" ? MenuLanguage : MenuGame;
            controller.HelpVisible = true;
            BuildRetainedUi(false);
        }

        void BuildModal(VisualElement root)
        {
            bool result = session.Winner >= 0;
            modal = new VisualElement { name = "HUD modal", pickingMode = PickingMode.Position };
            modal.style.position = Position.Absolute; modal.style.left = 0; modal.style.top = 0; modal.style.right = 0; modal.style.bottom = 0;
            modal.style.backgroundColor = new Color(.01f, .02f, .03f, .88f);
            modal.style.alignItems = Align.Center;
            modal.style.paddingLeft = modal.style.paddingRight = UiViewport.IsCompact ? 8 : 40;
            modal.style.paddingTop = modal.style.paddingBottom = UiViewport.IsCompact ? 10 : 36;
            var panel = RtsUiStyle.Panel("HUD modal panel"); panel.style.flexGrow = 1;
            panel.style.width = Length.Percent(100); panel.style.maxWidth = 820;
            panel.style.paddingLeft = panel.style.paddingRight = UiViewport.IsCompact ? 12 : 22;

            var head = new VisualElement { name = "HUD modal header" }; RtsUiStyle.Row(head); head.style.flexShrink = 0; head.style.marginBottom = 6;
            string heading = result ? (session.Winner == 0 ? "VICTORIA" : "DERROTA") : "RIESGUS · " + MapLayout.MapName;
            var title = RtsUiStyle.Title(heading, "HUD modal title", UiViewport.IsCompact ? 16 : 20); title.style.flexGrow = 1; title.style.flexShrink = 1; head.Add(title);
            if (!result) head.Add(CloseButton());
            panel.Add(head);

            if (!result && menuTab != MenuIncome && menuTab != MenuPopulation) panel.Add(MenuTabs());

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "HUD modal scroll" }; scroll.style.flexGrow = 1; scroll.style.minHeight = 0;
            RtsUiStyle.ConfigureScroll(scroll); scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; panel.Add(scroll);
            if (result) BuildResult(scroll);
            else if (menuTab == MenuIncome) BuildIncome(scroll);
            else if (menuTab == MenuPopulation) BuildPopulation(scroll);
            else if (menuTab == MenuSound) BuildSoundSection(scroll);
            else if (menuTab == MenuCamera) BuildCameraSection(scroll);
            else if (menuTab == MenuControls) BuildControlsSection(scroll);
            else if (menuTab == MenuLanguage) BuildLanguageSection(scroll);
            else BuildGameSection(scroll);

            var sticky = new VisualElement { name = "HUD modal actions" }; RtsUiStyle.Row(sticky, true); sticky.style.flexShrink = 0; sticky.style.marginTop = 8;
            if (!result)
            {
                sticky.Add(RtsUiStyle.Button("VOLVER", CloseModal, "HUD modal back"));
                modalPauseButton = RtsUiStyle.Button(session.Paused ? "CONTINUAR" : "PAUSA", () => { session.TogglePause(); UpdateRetainedLabels(); }, "HUD modal pause");
                sticky.Add(modalPauseButton);
            }
            panel.Add(sticky);
            root.Add(modal); modal.Add(panel);
        }

        Button CloseButton()
        {
            var close = RtsUiStyle.Button("", CloseModal, "HUD modal close");
            float size = UiViewport.IsTouchLayout ? UiViewport.MinimumTouchTarget : 36;
            close.style.width = close.style.minWidth = close.style.height = close.style.minHeight = size;
            close.style.paddingLeft = close.style.paddingRight = close.style.paddingTop = close.style.paddingBottom = 0;
            close.style.marginLeft = 8; close.style.marginRight = 0; close.style.marginBottom = 0; close.style.flexShrink = 0;
            close.style.alignItems = Align.Center; close.style.justifyContent = Justify.Center;
            close.tooltip = GameText.Localize("Cerrar (Esc)");
            var icon = new RtsQuickIcon(RtsQuickGlyph.Close); icon.style.width = icon.style.height = size * .5f; close.Add(icon);
            return close;
        }

        VisualElement MenuTabs()
        {
            var tabs = new VisualElement { name = "HUD menu tabs" }; RtsUiStyle.Row(tabs, true); tabs.style.flexShrink = 0; tabs.style.marginBottom = 8;
            tabs.style.borderBottomWidth = 1; tabs.style.borderBottomColor = new Color(.72f, .56f, .30f, .5f); tabs.style.paddingBottom = 4;
            foreach (var section in MenuSections)
            {
                int tab = section.tab;
                var button = RtsUiStyle.Button(section.title, () => { menuTab = tab; BuildRetainedUi(false); }, "HUD menu tab " + section.title);
                bool selected = menuTab == tab;
                button.style.minHeight = button.style.height = UiViewport.IsTouchLayout ? UiViewport.MinimumTouchTarget : 36;
                button.style.marginRight = 6; button.style.marginBottom = 4; button.style.paddingLeft = button.style.paddingRight = 14;
                button.style.color = selected ? RtsUiStyle.Gold : RtsUiStyle.Text;
                button.style.backgroundColor = selected ? new Color(.3f, .24f, .12f, 1) : RtsUiStyle.Card;
                button.style.borderBottomWidth = selected ? 3 : 1;
                button.style.borderBottomColor = selected ? RtsUiStyle.Gold : RtsUiStyle.Bronze;
                tabs.Add(button);
            }
            return tabs;
        }

        void BuildGameSection(VisualElement panel)
        {
            AddSection(panel, "PARTIDA");
            AddInfo(panel, "Conquista el 60 % de las ciudades. Completa países para recibir refuerzos de sus hogueras.");
            AddInfo(panel, "Semilla " + session.Seed + " · " + session.PlayerCount + " jugadores · " + session.DifficultyName);
            LiveInfo(panel, () => RuntimeDiagnostics.LatestReport == null ? "Recogiendo muestra de rendimiento…" : "Rendimiento: " + RuntimeDiagnostics.LatestAverageMs.ToString("F1") + " ms medio · " + RuntimeDiagnostics.LatestMaximumMs.ToString("F1") + " ms máximo · " + RuntimeDiagnostics.LatestUnits + " unidades · " + (RuntimeDiagnostics.LatestUnityAllocatedBytes / 1048576f).ToString("F0") + " MB Unity.");
            var row = SettingsButtons(panel);
            row.Add(RtsUiStyle.Button("CLASIFICACIÓN", ShowPlayers, "HUD menu ranking"));
            row.Add(RtsUiStyle.Button("DESGLOSE DEL ORO", ShowIncome, "HUD menu income"));
            row.Add(RtsUiStyle.Button("NUEVA PARTIDA · ELEGIR MAPA", FrontEndController.Open, "HUD menu new match"));
        }

        void BuildSoundSection(VisualElement panel)
        {
            AddSection(panel, "SONIDO");
            SettingSwitch(panel, "Sonido", () => !Sfx.Muted, on => Sfx.SetMuted(!on), "Audio mute");
            SettingSlider(panel, "Volumen general", Sfx.MasterVolume, Sfx.SetMasterVolume, "Audio master");
            SettingSlider(panel, "Efectos", Sfx.EffectsVolume, Sfx.SetEffectsVolume, "Audio effects");
            SettingSwitch(panel, "Música", () => Music.MusicEnabled, Music.SetMusicEnabled, "Music toggle");
            SettingSlider(panel, "Volumen de la música", Music.MusicVolume, Music.SetMusicVolume, "Music volume");
            AddInfo(panel, "Atajos: F7 efectos · F8 música.");
        }

        void BuildCameraSection(VisualElement panel)
        {
            AddSection(panel, "CÁMARA");
            SettingSlider(panel, "Velocidad de la cámara", Mathf.InverseLerp(.5f, 3f, controller.CameraRig.PanSpeed), value =>
            {
                controller.CameraRig.PanSpeed = Mathf.Lerp(.5f, 3f, value);
                SavePreference(CameraSpeedPreference, controller.CameraRig.PanSpeed);
            }, "Camera speed", value => Mathf.Lerp(.5f, 3f, value).ToString("0.0") + "×");
            SettingSwitch(panel, "Paneo en los bordes", () => controller.EdgePan, on => { controller.EdgePan = on; SavePreference(EdgePanPreference, on ? 1 : 0); }, "Camera edge pan");
            SettingSwitch(panel, "Temblor de cámara", () => GameFeel.ShakeEnabled, GameFeel.SetShakeEnabled, "Camera shake");
            SettingSwitch(panel, "Minimapa", () => showMinimap, on => { if (on != MinimapVisible) ToggleMinimap(); }, "Camera minimap");
            var row = SettingsButtons(panel);
            row.Add(RtsUiStyle.Button("CENTRAR MAPA", controller.CameraRig.FrameMap, "Camera frame map"));
            row.Add(RtsUiStyle.Button("RESTABLECER CÁMARA", controller.CameraRig.ResetView, "Camera reset"));
        }

        void BuildControlsSection(VisualElement panel)
        {
            AddSection(panel, "CONTROLES");
            AddInfo(panel, "Selección: clic o toque para seleccionar; arrastra un área para seleccionar tropas y edificios.");
            AddInfo(panel, "Órdenes: clic derecho en PC o una acción seguida de toque en tabletas. B/D embarca y desembarca.");
            AddInfo(panel, "Cámara: rueda para zoom, arrastre derecho para mover y botón central para girar. En pantalla táctil, dos dedos mueven y amplían; tres dedos giran.");
            BuildControlsTable(panel);
        }

        void BuildLanguageSection(VisualElement panel)
        {
            AddSection(panel, "IDIOMA");
            var row = SettingsButtons(panel);
            foreach (var option in new[] { (GameLanguage.Spanish, "ESPAÑOL"), (GameLanguage.English, "ENGLISH") })
            {
                var language = option.Item1;
                bool selected = GameText.Language == language;
                // Option names are shown in their own language and never translated.
                var button = RtsUiStyle.Button("", () => { if (GameText.Language != language) { GameText.Toggle(); BuildRetainedUi(false); BuildFeedbackUi(); } }, "Language " + language);
                button.text = option.Item2;
                button.style.minWidth = 150;
                button.style.color = selected ? RtsUiStyle.Gold : RtsUiStyle.Text;
                button.style.borderBottomWidth = selected ? 3 : 1; button.style.borderBottomColor = selected ? RtsUiStyle.Gold : RtsUiStyle.Bronze;
                row.Add(button);
            }
            AddInfo(panel, "La elección se guarda para las próximas partidas.");
        }

        static void AddSection(VisualElement root, string text)
        {
            var title = RtsUiStyle.Title(text, null, UiViewport.IsCompact ? 14 : 16); title.style.marginTop = 2; title.style.marginBottom = 8; root.Add(title);
        }

        static VisualElement SettingsButtons(VisualElement panel)
        {
            var row = new VisualElement { name = "HUD settings buttons" }; RtsUiStyle.Row(row, true); row.style.marginTop = 6; panel.Add(row);
            return row;
        }

        static VisualElement SettingRow(VisualElement panel, string label)
        {
            var row = new VisualElement { name = "HUD setting " + label }; RtsUiStyle.Row(row);
            row.style.minHeight = UiViewport.IsTouchLayout ? UiViewport.MinimumTouchTarget + 4 : 42; row.style.marginBottom = 4;
            row.style.borderBottomWidth = 1; row.style.borderBottomColor = new Color(.3f, .25f, .15f, .35f);
            var text = RtsUiStyle.Label(label, null, 14); text.style.width = UiViewport.IsCompact ? 120 : 220; text.style.flexShrink = 0;
            text.style.whiteSpace = WhiteSpace.Normal;
            row.Add(text); panel.Add(row);
            return row;
        }

        /// <summary>0–1 setting shown as a percentage unless <paramref name="format"/> says otherwise. Applies live while dragging.</summary>
        static void SettingSlider(VisualElement panel, string label, float value, System.Action<float> apply, string name, System.Func<float, string> format = null)
        {
            var row = SettingRow(panel, label);
            format ??= v => Mathf.RoundToInt(v * 100) + " %";
            var slider = new Slider(0, 1) { name = name, value = Mathf.Clamp01(value), focusable = false };
            slider.AddToClassList("riskai-slider");
            slider.style.flexGrow = 1; slider.style.minWidth = 80; slider.style.marginLeft = 4; slider.style.marginRight = 10;
            var shown = RtsUiStyle.Label(format(slider.value), name + " value", 14); shown.style.width = 56; shown.style.flexShrink = 0;
            shown.style.unityTextAlign = TextAnchor.MiddleRight; shown.style.color = RtsUiStyle.Gold;
            slider.RegisterValueChangedCallback(evt => { apply(evt.newValue); shown.text = format(evt.newValue); });
            row.Add(slider); row.Add(shown);
        }

        /// <summary>On/off pill switch with a textual state; toggles in place without rebuilding the menu.</summary>
        static void SettingSwitch(VisualElement panel, string label, System.Func<bool> get, System.Action<bool> set, string name)
        {
            var row = SettingRow(panel, label);
            var pill = new VisualElement { name = "Switch track", pickingMode = PickingMode.Ignore };
            pill.style.width = 52; pill.style.height = 28; pill.style.flexShrink = 0;
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius = pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 14;
            pill.style.borderTopWidth = pill.style.borderBottomWidth = pill.style.borderLeftWidth = pill.style.borderRightWidth = 1;
            pill.style.borderTopColor = pill.style.borderBottomColor = pill.style.borderLeftColor = pill.style.borderRightColor = RtsUiStyle.Bronze;
            var knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.style.position = Position.Absolute; knob.style.top = 3; knob.style.width = knob.style.height = 20;
            knob.style.borderTopLeftRadius = knob.style.borderTopRightRadius = knob.style.borderBottomLeftRadius = knob.style.borderBottomRightRadius = 10;
            pill.Add(knob);
            var state = RtsUiStyle.Label("", null, 13); state.pickingMode = PickingMode.Ignore; state.style.marginLeft = 10;
            System.Action sync = null;
            var button = RtsUiStyle.Button("", () => { set(!get()); sync(); }, name);
            button.style.backgroundColor = Color.clear; button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 0;
            button.style.flexDirection = FlexDirection.Row; button.style.alignItems = Align.Center; button.style.paddingLeft = 4; button.style.marginBottom = 0;
            button.style.minHeight = UiViewport.IsTouchLayout ? UiViewport.MinimumTouchTarget : 36;
            sync = () =>
            {
                bool on = get();
                pill.style.backgroundColor = on ? new Color(.62f, .47f, .2f, 1) : RtsUiStyle.Slate;
                knob.style.left = on ? 28 : 3;
                knob.style.backgroundColor = on ? RtsUiStyle.Gold : RtsUiStyle.Muted;
                state.text = GameText.Localize(on ? "Activado" : "Desactivado");
                state.style.color = on ? RtsUiStyle.Gold : RtsUiStyle.Muted;
            };
            button.Add(pill); button.Add(state); sync();
            row.Add(button);
        }

        void BuildResult(VisualElement panel)
        {
            AddInfo(panel, VisualFactory.TeamName(session.Winner) + " controla " + MapLayout.MapName);
            panel.Add(RtsUiStyle.Button("NUEVA PARTIDA", FrontEndController.Open));
        }
    }
}
