using System;
using System.Collections;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>Standalone retained setup screen. It does not create a match until StartBattle.</summary>
    public sealed class FrontEndController : MonoBehaviour
    {
        public const string FrontEndSceneName = "FrontEnd";
        public const string BattlefieldSceneName = "LasMarcas";

        ScenarioMap selectedMap;
        BattleSession.StartLayout selectedLayout;
        BattleSession.AiDifficulty selectedDifficulty;
        int selectedPlayers;
        string seedText;
        bool sourceMountains;
        bool loading;
        string validation;
        RtsUiRuntime ui;
        VisualElement content;
        bool lastCompact;

        void Awake()
        {
            Application.targetFrameRate = 60;
            selectedMap = BattleSession.MapForNewMatch;
            selectedLayout = BattleSession.LayoutForNewMatch;
            selectedDifficulty = BattleSession.DifficultyForNewMatch;
            selectedPlayers = Mathf.Clamp(BattleSession.PlayerCountForNewMatch, 2, PlayerRules.MaxPlayers);
            seedText = BattleSession.SeedForNewMatch.ToString();
            sourceMountains = ImportedLandscapeAugment.Enabled;
        }

        void Start()
        {
            ui = RtsUiRuntime.Attach(gameObject, "Front end", 40);
            Rebuild();
            if (AutomatedLaunchRequested()) StartBattle();
        }

        void Update()
        {
            // A mobile keyboard changes available height, but must not recreate the focused seed field.
            if (lastCompact != UiViewport.IsCompact)
                Rebuild();
        }

        /// <summary>Returns from battlefield help to the standalone configuration scene.</summary>
        public static void Open()
        {
            if (SceneManager.GetActiveScene().name != FrontEndSceneName)
                SceneManager.LoadScene(FrontEndSceneName, LoadSceneMode.Single);
        }

        /// <summary>Applies the selected next-match configuration and loads the battlefield scene.</summary>
        public void StartBattle()
        {
            if (loading) return;
            if (!int.TryParse(seedText, out int seed))
            {
                validation = "Escribe una semilla numérica válida.";
                Rebuild();
                return;
            }
            BattleSession.MapForNewMatch = selectedMap;
            BattleSession.LayoutForNewMatch = selectedLayout;
            BattleSession.DifficultyForNewMatch = selectedDifficulty;
            BattleSession.PlayerCountForNewMatch = Mathf.Clamp(selectedPlayers, 2, PlayerRules.MaxPlayers);
            BattleSession.SeedForNewMatch = seed;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            ImportedLandscapeAugment.Enabled = sourceMountains;
            StartCoroutine(LoadBattlefield());
        }

        IEnumerator LoadBattlefield()
        {
            loading = true; Rebuild();
            yield return null; // Render feedback before a map or NavMesh is constructed.
            var operation = SceneManager.LoadSceneAsync(BattlefieldSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                loading = false; validation = "No se encontró la escena de batalla."; Rebuild();
                yield break;
            }
            while (!operation.isDone) yield return null;
        }

        static bool AutomatedLaunchRequested()
        {
            bool automated = false;
            foreach (var argument in LaunchArguments.Get())
            {
                if (string.Equals(argument, "--riskai-capture-menu", StringComparison.OrdinalIgnoreCase)) return false;
                if (string.Equals(argument, "--riskai-capture", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, "--riskai-probe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, "--riskai-play", StringComparison.OrdinalIgnoreCase)) automated = true;
            }
            return automated;
        }

        void Rebuild()
        {
            if (!ui) return;
            lastCompact = UiViewport.IsCompact;
            content = new VisualElement { name = "Front end content" };
            content.style.flexGrow = 1; content.style.backgroundColor = RtsUiStyle.Slate;
            content.style.paddingLeft = UiViewport.IsCompact ? 14 : 28;
            content.style.paddingRight = UiViewport.IsCompact ? 14 : 28;
            content.style.paddingTop = UiViewport.IsCompact ? 12 : 24;
            content.style.paddingBottom = UiViewport.IsCompact ? 12 : 24;
            if (loading) BuildLoading(content); else BuildSetup(content);
            ui.SetContent(content);
        }

        void BuildLoading(VisualElement root)
        {
            var panel = RtsUiStyle.Panel("Loading panel");
            if (UiViewport.IsCompact) panel.style.width = Length.Percent(100); else panel.style.width = 700;
            panel.style.alignSelf = Align.Center; panel.style.marginTop = Length.Percent(30);
            panel.Add(RtsUiStyle.Label("PREPARANDO LA CONQUISTA", null, 24));
            panel.Add(RtsUiStyle.Label(MapLayout.ScenarioDetail(selectedMap) + " · " + selectedPlayers + " jugadores", null, 16));
            panel.Add(RtsUiStyle.Label("Cargando terreno, ciudades y rutas…", null, 14));
            root.Add(panel);
        }

        void BuildSetup(VisualElement root)
        {
            var header = RtsUiStyle.Panel("Front end header");
            header.style.flexDirection = FlexDirection.Column;
            header.style.flexShrink = 0;
            header.style.marginBottom = 12;
            var titleRow = new VisualElement(); RtsUiStyle.Row(titleRow);
            var title = RtsUiStyle.Label("RISKAI · DOMINIOS", null, UiViewport.IsCompact ? 19 : 26);
            title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.flexGrow = 1; titleRow.Add(title);
            var version = RtsUiStyle.Label("v0.19 · CONQUISTA", null, UiViewport.IsCompact ? 11 : 13); version.style.marginLeft = 8; titleRow.Add(version); header.Add(titleRow);
            var description = RtsUiStyle.Label("Elige el mapa, prepara a tus rivales y comienza una conquista independiente.", null, 14); description.style.whiteSpace = WhiteSpace.Normal; header.Add(description);
            root.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "Front end scroll" };
            scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;
            scroll.contentContainer.style.minWidth=0;
            scroll.contentContainer.style.width=Length.Percent(100);
            scroll.style.flexGrow = 1; scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            BuildScenarioSection(scroll);
            BuildConfigurationSection(scroll);
            root.Add(scroll);

            var footer = RtsUiStyle.Panel("Front end footer");
            footer.style.flexDirection = UiViewport.IsCompact ? FlexDirection.Column : FlexDirection.Row;
            footer.style.flexShrink = 0;
            footer.style.marginTop = 12;
            var rules = RtsUiStyle.Label("4 de oro y un defensor por puesto. Conquista el 60 % de las ciudades.", null, 13);
            rules.style.flexGrow = 1; rules.style.whiteSpace = WhiteSpace.Normal; footer.Add(rules);
            if (!string.IsNullOrEmpty(validation))
            {
                var error = RtsUiStyle.Label(validation, null, 13); error.style.color = new Color(1f, .48f, .36f); footer.Add(error);
            }
            var start = RtsUiStyle.Button("INICIAR PARTIDA", StartBattle, "Start battle");
            if (UiViewport.IsCompact) { start.style.width = Length.Percent(100); start.style.marginRight = 0; start.style.marginBottom = 0; } else start.style.minWidth = 250;
            footer.Add(start); root.Add(footer);
        }

        void BuildScenarioSection(VisualElement root)
        {
            root.Add(SectionTitle("ESCENARIO"));
            var grid = new VisualElement { name = "Scenario cards" };
            RtsUiStyle.Row(grid, true); grid.style.marginBottom = 16;
            ScenarioCard(grid, ScenarioMap.Classic, "LAS MARCAS", "Costa, mesetas y un sur seco para campañas rápidas.");
            ScenarioCard(grid, ScenarioMap.Riverlands, "CUATRO RIBERAS", "Río central, puente y un archipiélago al norte.");
            ScenarioCard(grid, ScenarioMap.Europe, "EUROPE · REFORGED", "Territorio importado a escala con puertos y fronteras reales.");
            ScenarioCard(grid, ScenarioMap.NewWorld, "NEW WORLD · EUROPA Y AMÉRICA", "Europa y América para una conquista de gran escala.");
            root.Add(grid);
        }

        void ScenarioCard(VisualElement parent, ScenarioMap map, string title, string description)
        {
            bool chosen = selectedMap == map;
            var button = RtsUiStyle.Button("", () => { selectedMap = map; Rebuild(); }, "Map " + map);
            button.style.flexGrow = 1;
            if (UiViewport.IsCompact) { button.style.width = Length.Percent(100); button.style.marginRight = 0; }
            else button.style.minWidth = Length.Percent(47);
            button.style.minHeight = UiViewport.IsCompact ? 88 : 106;
            button.style.backgroundColor = chosen ? new Color(.20f, .18f, .10f, 1) : RtsUiStyle.Card;
            var titleLabel = RtsUiStyle.Label((chosen ? "●  " : "") + title, null, 16); titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold; titleLabel.style.whiteSpace = WhiteSpace.Normal;
            var detail = RtsUiStyle.Label(MapLayout.ScenarioDetail(map), null, 13); detail.style.color = RtsUiStyle.Bronze; detail.style.whiteSpace = WhiteSpace.Normal;
            var body = RtsUiStyle.Label(description, null, 12); body.style.color = RtsUiStyle.Muted; body.style.whiteSpace = WhiteSpace.Normal;
            button.Add(titleLabel); button.Add(detail); button.Add(body); parent.Add(button);
        }

        void BuildConfigurationSection(VisualElement root)
        {
            root.Add(SectionTitle("CONFIGURACIÓN"));
            var panel = RtsUiStyle.Panel("Match configuration"); panel.style.marginBottom = 16;
            AddPlayers(panel); AddSeed(panel); AddLayout(panel); AddDifficulty(panel); AddMountains(panel);
            root.Add(panel);
        }

        void AddPlayers(VisualElement parent)
        {
            var row = NewFieldRow(parent, "JUGADORES");
            row.Add(RtsUiStyle.Button("−", () => { selectedPlayers = Mathf.Max(2, selectedPlayers - 1); Rebuild(); }));
            var count = RtsUiStyle.Label(selectedPlayers + " · tú y " + (selectedPlayers - 1) + " IA", null, 15); count.style.minWidth = 154; row.Add(count);
            row.Add(RtsUiStyle.Button("+", () => { selectedPlayers = Mathf.Min(PlayerRules.MaxPlayers, selectedPlayers + 1); Rebuild(); }));
        }

        void AddSeed(VisualElement parent)
        {
            var row = NewFieldRow(parent, "SEMILLA");
            var field = new TextField { value = seedText, maxLength = 11, name = "Match seed" }; field.style.minHeight = 44; field.style.minWidth = 150;
            field.RegisterValueChangedCallback(change => seedText = change.newValue); row.Add(field);
            row.Add(RtsUiStyle.Button("NUEVA SEMILLA", () => { BattleSession.NewSeed(); seedText = BattleSession.SeedForNewMatch.ToString(); Rebuild(); }));
        }

        void AddLayout(VisualElement parent)
        {
            var row = NewFieldRow(parent, "REPARTO INICIAL");
            Choice(row, "Ciudades al azar", selectedLayout == BattleSession.StartLayout.RandomCities, () => selectedLayout = BattleSession.StartLayout.RandomCities);
            Choice(row, "Países iniciales", selectedLayout == BattleSession.StartLayout.RandomCountries, () => selectedLayout = BattleSession.StartLayout.RandomCountries);
            Choice(row, "Posiciones fijas", selectedLayout == BattleSession.StartLayout.Fixed, () => selectedLayout = BattleSession.StartLayout.Fixed);
        }

        void AddDifficulty(VisualElement parent)
        {
            var row = NewFieldRow(parent, "DIFICULTAD DE IA");
            Choice(row, "Relajada · ataque más tarde", selectedDifficulty == BattleSession.AiDifficulty.Relaxed, () => selectedDifficulty = BattleSession.AiDifficulty.Relaxed);
            Choice(row, "Estándar · presión temprana", selectedDifficulty == BattleSession.AiDifficulty.Standard, () => selectedDifficulty = BattleSession.AiDifficulty.Standard);
        }

        void AddMountains(VisualElement parent)
        {
            bool imported = selectedMap == ScenarioMap.Europe || selectedMap == ScenarioMap.NewWorld;
            var row = NewFieldRow(parent, "RELIEVE IMPORTADO");
            var toggle = new Toggle("Añadir cordilleras suaves a Europe y New World") { value = sourceMountains, name = "Source mountains" };
            toggle.SetEnabled(imported); toggle.style.minHeight = 44;
            toggle.style.flexShrink=1;toggle.style.whiteSpace=WhiteSpace.Normal;toggle.style.maxWidth=Length.Percent(100);
            toggle.RegisterValueChangedCallback(change => sourceMountains = change.newValue); row.Add(toggle);
            var note = RtsUiStyle.Label(imported ? "Respeta coordenadas y despeja anclajes." : "Disponible en escenarios importados.", null, 12);
            note.style.color = RtsUiStyle.Muted;note.style.whiteSpace=WhiteSpace.Normal;note.style.maxWidth=Length.Percent(100);row.Add(note);
        }

        VisualElement NewFieldRow(VisualElement parent, string heading)
        {
            var row = new VisualElement(); RtsUiStyle.Row(row, true); row.style.marginBottom = 10;
            var label = RtsUiStyle.Label(heading, null, 13); label.style.color = RtsUiStyle.Bronze;
            if (UiViewport.IsCompact) label.style.minWidth = Length.Percent(100); else label.style.minWidth = 180;
            row.Add(label); parent.Add(row); return row;
        }

        void Choice(VisualElement parent, string text, bool selected, Action select)
        {
            var choice = RtsUiStyle.Button((selected ? "●  " : "") + text, () => { select(); Rebuild(); });
            choice.style.backgroundColor = selected ? new Color(.20f, .18f, .10f, 1) : RtsUiStyle.Card; choice.style.whiteSpace = WhiteSpace.Normal;
            if (UiViewport.IsCompact) { choice.style.width = Length.Percent(100); choice.style.marginRight = 0; }
            parent.Add(choice);
        }

        static Label SectionTitle(string text)
        {
            var title = RtsUiStyle.Label(text, null, 17); title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.marginBottom = 8; return title;
        }
    }
}
