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
        bool playersAdjusted;
        string seedText;
        bool sourceMountains;
        bool loading;
        string validation;
        RtsUiRuntime ui;
        VisualElement content;
        bool lastCompact;
        ScenarioMap lastPressedMap;
        double lastScenarioPressAt=double.NegativeInfinity;

        void Awake()
        {
            Application.targetFrameRate = 60;
            selectedMap = BattleSession.MapForNewMatch;
            selectedLayout = BattleSession.LayoutForNewMatch;
            selectedDifficulty = BattleSession.DifficultyForNewMatch;
            selectedPlayers = MapLayout.MaximumPlayersForScenario(selectedMap);
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
            selectedPlayers = Mathf.Clamp(selectedPlayers, 2, MapLayout.MaximumPlayersForScenario(selectedMap));
            BattleSession.PlayerCountForNewMatch = selectedPlayers;
            BattleSession.SeedForNewMatch = seed;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            ImportedLandscapeAugment.Enabled = sourceMountains;
            BattleSession.CountdownForNewMatch = true;
            foreach(var argument in LaunchArguments.Get())
                if(argument.StartsWith("--riskai-",StringComparison.OrdinalIgnoreCase) &&
                    (argument.IndexOf("capture",StringComparison.OrdinalIgnoreCase)>=0 ||
                     argument.IndexOf("probe",StringComparison.OrdinalIgnoreCase)>=0))
                    BattleSession.CountdownForNewMatch=false;
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
            root.style.justifyContent = Justify.Center;
            var panel = RtsUiStyle.Panel("Loading panel");
            panel.style.width = Length.Percent(100); panel.style.maxWidth = 700;
            panel.style.minWidth = 0; panel.style.flexShrink = 1;
            panel.style.alignSelf = Align.Center;
            var title = RtsUiStyle.Label("PREPARANDO LA CONQUISTA", null, UiViewport.IsCompact ? 20 : 24);
            var detail = RtsUiStyle.Label(MapLayout.ScenarioDetail(selectedMap) + " · " + selectedPlayers + " jugadores", null, UiViewport.IsCompact ? 14 : 16);
            var status = RtsUiStyle.Label("Cargando terreno, ciudades y rutas…", null, 14);
            foreach (var label in new[] { title, detail, status })
            {
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.minWidth = 0; label.style.maxWidth = Length.Percent(100);
                label.style.flexShrink = 1; panel.Add(label);
            }
            root.Add(panel);
        }

        void BuildSetup(VisualElement root)
        {
            var header = RtsUiStyle.Panel("Front end header");
            header.style.flexDirection = FlexDirection.Column;
            header.style.flexShrink = 0;
            header.style.marginBottom = 12;
            var titleRow = new VisualElement(); RtsUiStyle.Row(titleRow);
            if(!UiViewport.IsCompact){var seal=new RtsHeraldicSeal(2,RtsUiStyle.Gold);seal.style.width=62;seal.style.height=62;seal.style.marginRight=18;titleRow.Add(seal);}
            var title = RtsUiStyle.Title("DOMINIOS", null, UiViewport.IsCompact ? 24 : 34);
            title.style.flexGrow = 1; titleRow.Add(title);
            var version = RtsUiStyle.Label("v"+Application.version+" · CONQUISTA", null, UiViewport.IsCompact ? 11 : 13); version.style.marginLeft = 8; titleRow.Add(version); header.Add(titleRow);
            var description = RtsUiStyle.Label("RISKAI  ·  Traza tu conquista. Reúne tus ejércitos. Defiende cada frontera.", null, 14); description.style.whiteSpace = WhiteSpace.Normal;description.style.color=RtsUiStyle.Muted; header.Add(description);
            root.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "Front end scroll" };
            RtsUiStyle.ConfigureScroll(scroll);
            scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;
            scroll.contentContainer.style.minWidth=0;
            // Percentage width can include the vertical scroller itself. Bind to
            // the actual viewport so cards keep their right border on narrow screens.
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if(evt.newRect.width>0)scroll.contentContainer.style.width=evt.newRect.width;
            });
            scroll.style.flexGrow = 1; scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            var body=new VisualElement { name="War table setup" };
            body.style.flexDirection=UiViewport.IsCompact?FlexDirection.Column:FlexDirection.Row;
            var scenarios=new VisualElement();scenarios.style.minWidth=0;
            var configuration=new VisualElement();configuration.style.minWidth=0;
            if(!UiViewport.IsCompact)
            {
                scenarios.style.width=Length.Percent(53);scenarios.style.paddingRight=18;
                configuration.style.width=Length.Percent(47);
            }
            BuildScenarioSection(scenarios);BuildConfigurationSection(configuration);
            body.Add(scenarios);body.Add(configuration);scroll.Add(body);
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
            var start = RtsUiStyle.Button("COMENZAR LA CONQUISTA", StartBattle, "Start battle");
            start.style.backgroundColor=new Color(.31f,.23f,.105f);start.style.color=RtsUiStyle.Gold;start.style.minHeight=50;
            if (UiViewport.IsCompact) { start.style.width = Length.Percent(100); start.style.marginRight = 0; start.style.marginBottom = 0; } else start.style.minWidth = 250;
            footer.Add(start); root.Add(footer);
        }

        void BuildScenarioSection(VisualElement root)
        {
            root.Add(SectionTitle("Elige tu campo de batalla"));
            var grid = new VisualElement { name = "Scenario cards" };
            RtsUiStyle.Row(grid, true);grid.style.alignItems=Align.Stretch; grid.style.marginBottom = 16;
            ScenarioCard(grid, ScenarioMap.Europe, "EUROPE", "Territorio importado a escala con puertos y fronteras reales.");
            ScenarioCard(grid, ScenarioMap.Classic, "LAS MARCAS", "Costa, mesetas y un sur seco para campañas rápidas.");
            ScenarioCard(grid, ScenarioMap.Riverlands, "CUATRO RIBERAS", "Río central, puente y un archipiélago al norte.");
            ScenarioCard(grid, ScenarioMap.NewWorld, "NEW WORLD · EUROPA Y AMÉRICA", "Europa y América para una conquista de gran escala.");
            root.Add(grid);
        }

        void ScenarioCard(VisualElement parent, ScenarioMap map, string title, string description)
        {
            bool chosen = selectedMap == map;
            var button = RtsUiStyle.Button("", () => SelectMap(map), "Map " + map);
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                // WebGL does not report a reliable clickCount for emulated touch.
                // Keep the gesture at controller level so rebuilding the selected
                // card after the first press cannot lose the second one.
                if(evt.button!=0||loading)return;
                double now=Time.unscaledTimeAsDouble;
                bool repeated=lastPressedMap==map&&now-lastScenarioPressAt<=.55;
                lastPressedMap=map;lastScenarioPressAt=now;
                if(!repeated)return;
                lastScenarioPressAt=double.NegativeInfinity;
                ApplyMapSelection(map);
                StartBattle();
                evt.StopImmediatePropagation();
            },TrickleDown.TrickleDown);
            button.style.flexGrow = 1;
            if (UiViewport.IsCompact) { button.style.width = Length.Percent(100); button.style.marginRight = 0; }
            else { button.style.width=Length.Percent(47);button.style.minWidth=0; }
            button.style.minHeight = UiViewport.IsCompact ? 108 : 186;
            button.style.backgroundColor = chosen ? new Color(.20f, .18f, .10f, 1) : RtsUiStyle.Card;
            button.style.paddingTop=12;button.style.paddingBottom=12;
            var composition=new VisualElement();composition.pickingMode=PickingMode.Ignore;
            composition.style.flexDirection=UiViewport.IsCompact?FlexDirection.Row:FlexDirection.Column;
            composition.style.alignItems=UiViewport.IsCompact?Align.Center:Align.FlexStart;
            var accent=map==ScenarioMap.Classic?new Color(.60f,.72f,.43f):map==ScenarioMap.Riverlands?new Color(.43f,.68f,.69f):map==ScenarioMap.Europe?new Color(.88f,.67f,.36f):new Color(.70f,.60f,.83f);
            var seal=new RtsHeraldicSeal((int)map,accent);seal.style.width=UiViewport.IsCompact?54:50;seal.style.height=UiViewport.IsCompact?66:58;seal.style.marginRight=12;
            var words=new VisualElement();words.style.minWidth=0;words.style.flexShrink=1;
            var titleLabel = RtsUiStyle.Title(title, null, 15);
            var detail = RtsUiStyle.Label(MapLayout.ScenarioDetail(map) + " · máx. " + MapLayout.MaximumPlayersForScenario(map) + " jugadores", null, 13); detail.style.color = RtsUiStyle.Bronze; detail.style.whiteSpace = WhiteSpace.Normal;
            var body = RtsUiStyle.Label(description, null, 12); body.style.color = RtsUiStyle.Muted; body.style.whiteSpace = WhiteSpace.Normal;
            words.Add(titleLabel);words.Add(detail);words.Add(body);
            if(chosen){var selected=RtsUiStyle.Label("ELEGIDO",null,10);selected.style.color=RtsUiStyle.Gold;selected.style.marginTop=5;words.Add(selected);}
            composition.Add(seal);composition.Add(words);button.Add(composition);parent.Add(button);
        }

        void BuildConfigurationSection(VisualElement root)
        {
            root.Add(SectionTitle("Prepara la expedición"));
            var panel = RtsUiStyle.Panel("Match configuration"); panel.style.marginBottom = 16;
            AddPlayers(panel); AddSeed(panel); AddLayout(panel); AddDifficulty(panel); AddMountains(panel);
            root.Add(panel);
        }

        void SelectMap(ScenarioMap map)
        {
            ApplyMapSelection(map);
            Rebuild();
        }

        void ApplyMapSelection(ScenarioMap map)
        {
            selectedMap = map;
            int maximum = MapLayout.MaximumPlayersForScenario(map);
            selectedPlayers = playersAdjusted ? Mathf.Clamp(selectedPlayers, 2, maximum) : maximum;
        }

        void AdjustPlayers(int delta)
        {
            playersAdjusted = true;
            selectedPlayers = Mathf.Clamp(selectedPlayers + delta, 2, MapLayout.MaximumPlayersForScenario(selectedMap));
            Rebuild();
        }

        void AddPlayers(VisualElement parent)
        {
            var row = NewFieldRow(parent, "JUGADORES · MÁXIMO " + MapLayout.MaximumPlayersForScenario(selectedMap));
            row.Add(RtsUiStyle.Button("−", () => AdjustPlayers(-1)));
            var count = RtsUiStyle.Label(selectedPlayers + " · tú y " + (selectedPlayers - 1) + " IA", null, 15); count.style.minWidth = 154; row.Add(count);
            row.Add(RtsUiStyle.Button("+", () => AdjustPlayers(1)));
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
            Choice(row, "Relajada · tácticas sencillas", selectedDifficulty == BattleSession.AiDifficulty.Relaxed, () => selectedDifficulty = BattleSession.AiDifficulty.Relaxed);
            Choice(row, "Estándar · mayor coordinación", selectedDifficulty == BattleSession.AiDifficulty.Standard, () => selectedDifficulty = BattleSession.AiDifficulty.Standard);
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
            label.style.minWidth = Length.Percent(100);label.style.marginBottom=4;
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
            var title = RtsUiStyle.Title(text, null, 17); title.style.marginBottom = 10; return title;
        }
    }
}
