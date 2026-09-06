using System;
using System.Collections;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskAI
{
    /// <summary>
    /// Lightweight match setup scene. It deliberately creates neither a BattleSession
    /// nor map geometry; the battlefield scene owns those only after StartBattle.
    /// </summary>
    public sealed class FrontEndController : MonoBehaviour
    {
        public const string FrontEndSceneName = "FrontEnd";
        public const string BattlefieldSceneName = "LasMarcas";
        const float DesignWidth = 1120f;
        const float DesignHeight = 760f;

        static GUIStyle heading, subheading, cardTitle, cardDetail, startButton, choiceButton, errorStyle, versionLabel, loadingLabel;
        ScenarioMap selectedMap;
        BattleSession.StartLayout selectedLayout;
        BattleSession.AiDifficulty selectedDifficulty;
        int selectedPlayers;
        string seedText;
        bool sourceMountains;
        bool loading;
        string validation;

        void Awake()
        {
            Application.targetFrameRate=60;
            selectedMap = BattleSession.MapForNewMatch;
            selectedLayout = BattleSession.LayoutForNewMatch;
            selectedDifficulty = BattleSession.DifficultyForNewMatch;
            selectedPlayers = Mathf.Clamp(BattleSession.PlayerCountForNewMatch, 2, PlayerRules.MaxPlayers);
            seedText = BattleSession.SeedForNewMatch.ToString();
            sourceMountains = ImportedLandscapeAugment.Enabled;
            if (AutomatedLaunchRequested()) StartBattle();
        }

        /// <summary>Returns from a battlefield UI to the standalone setup scene.</summary>
        public static void Open()
        {
            if (SceneManager.GetActiveScene().name == FrontEndSceneName) return;
            SceneManager.LoadScene(FrontEndSceneName, LoadSceneMode.Single);
        }

        /// <summary>Applies the selected next-match configuration, then loads the battlefield.</summary>
        public void StartBattle()
        {
            if (loading) return;
            if (!int.TryParse(seedText, out int seed))
            {
                validation = "Escribe una semilla numérica válida.";
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
            loading = true;
            // Present the loading state for one rendered frame before the terrain build starts.
            yield return null;
            var operation = SceneManager.LoadSceneAsync(BattlefieldSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                loading = false;
                validation = "No se encontró la escena de batalla.";
                yield break;
            }
            while (!operation.isDone) yield return null;
        }

        static bool AutomatedLaunchRequested()
        {
            bool automated = false;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--riskai-capture-menu", StringComparison.OrdinalIgnoreCase)) return false;
                if (string.Equals(argument, "--riskai-capture", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, "--riskai-probe", StringComparison.OrdinalIgnoreCase)) automated = true;
            }
            return automated;
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / (DesignWidth+36), Screen.height / (DesignHeight+36));
            scale = Mathf.Max(.5f, scale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            Draw(new Rect(0, 0, Screen.width / scale, Screen.height / scale));
            GUI.matrix = previousMatrix;
        }

        void Draw(Rect screen)
        {
            RtsSkin.Fill(screen, new Color(.018f, .028f, .042f));
            if(loading)
            {
                var box=new Rect(screen.center.x-350,screen.center.y-95,700,190);
                RtsSkin.Frame(box,RtsSkin.Gold);
                GUI.Label(new Rect(box.x+30,box.y+31,640,38),"PREPARANDO LA CONQUISTA",heading);
                GUI.Label(new Rect(box.x+30,box.y+90,640,28),MapLayout.ScenarioDetail(selectedMap)+"   ·   "+selectedPlayers+" jugadores",subheading);
                GUI.Label(new Rect(box.x+30,box.y+132,640,28),"Cargando terreno, ciudades y rutas…",subheading);
                return;
            }
            var panel = new Rect(screen.x + (screen.width - DesignWidth) * .5f, screen.y + Mathf.Max(18, (screen.height - DesignHeight) * .5f), DesignWidth, DesignHeight);
            RtsSkin.Frame(panel, new Color(.67f, .52f, .25f));
            RtsSkin.Fill(new Rect(panel.x + 2, panel.y + 2, panel.width - 4, 86), new Color(.075f, .105f, .15f));
            GUI.Label(new Rect(panel.x + 34, panel.y + 18, 700, 32), "RISKAI · DOMINIOS", heading);
            GUI.Label(new Rect(panel.x + 34, panel.y + 53, 860, 24), "Elige el mapa, prepara a tus rivales y comienza una conquista independiente.", subheading);
            GUI.Label(new Rect(panel.xMax - 220, panel.y + 28, 180, 24), "v0.18 · CONQUISTA", versionLabel);

            DrawMaps(new Rect(panel.x + 28, panel.y + 120, panel.width - 56, 228));
            DrawConfiguration(new Rect(panel.x + 28, panel.y + 356, panel.width - 56, 270));
            GUI.Label(new Rect(panel.x + 34, panel.yMax - 94, 690, 18), "4 de oro y un defensor por puesto. Completa países para ganar sus refuerzos.", RtsSkin.Small);
            GUI.Label(new Rect(panel.x + 34, panel.yMax - 69, 700, 18), "Conquista: controla el 60 % de las ciudades. Todas las configuraciones usan las mismas reglas.", RtsSkin.Tiny);
            if (!string.IsNullOrEmpty(validation)) GUI.Label(new Rect(panel.x + 34, panel.yMax - 42, 500, 24), validation, errorStyle);
            if (GUI.Button(new Rect(panel.xMax - 292, panel.yMax - 78, 252, 50), loading ? "CARGANDO…" : "INICIAR PARTIDA", startButton) && !loading) StartBattle();
            if (loading) GUI.Label(new Rect(panel.xMax - 292, panel.yMax - 25, 252, 18), "Preparando el campo de batalla…", loadingLabel);
        }

        void DrawMaps(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y - 30, 540, 30), "ESCENARIO", heading);
            DrawMapCard(new Rect(area.x, area.y, area.width * .5f - 8, 104), ScenarioMap.Classic, "LAS MARCAS", MapLayout.ScenarioDetail(ScenarioMap.Classic), "Costa, mesetas y un sur seco para campañas rápidas.");
            DrawMapCard(new Rect(area.x + area.width * .5f + 8, area.y, area.width * .5f - 8, 104), ScenarioMap.Riverlands, "CUATRO RIBERAS", MapLayout.ScenarioDetail(ScenarioMap.Riverlands), "Río central, puente y un archipiélago al norte.");
            DrawMapCard(new Rect(area.x, area.y + 116, area.width * .5f - 8, 104), ScenarioMap.Europe, "EUROPE · REFORGED", MapLayout.ScenarioDetail(ScenarioMap.Europe), "Territorio importado a escala con puertos y fronteras reales.");
            DrawMapCard(new Rect(area.x + area.width * .5f + 8, area.y + 116, area.width * .5f - 8, 104), ScenarioMap.NewWorld, "NEW WORLD · EUROPA Y AMÉRICA", MapLayout.ScenarioDetail(ScenarioMap.NewWorld), "Europa y América para una conquista de gran escala.");
        }

        void DrawMapCard(Rect rect, ScenarioMap map, string title, string detail, string description)
        {
            bool selected = selectedMap == map;
            RtsSkin.Frame(rect, selected ? RtsSkin.Gold : new Color(.24f, .31f, .33f));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) selectedMap = map;
            RtsSkin.Fill(new Rect(rect.x + 13, rect.y + 15, 7, rect.height - 30), selected ? RtsSkin.Gold : new Color(.24f, .35f, .40f));
            GUI.Label(new Rect(rect.x + 34, rect.y + 13, rect.width - 48, 25), (selected ? "●  " : "") + title, cardTitle);
            GUI.Label(new Rect(rect.x + 34, rect.y + 42, rect.width - 48, 19), detail, cardDetail);
            GUI.Label(new Rect(rect.x + 34, rect.y + 66, rect.width - 48, 30), description, RtsSkin.Tiny);
        }

        void DrawConfiguration(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, 420, 25), "CONFIGURACIÓN", heading);
            GUI.Label(new Rect(area.x, area.y + 39, 152, 22), "JUGADORES", subheading);
            if (GUI.Button(new Rect(area.x + 156, area.y + 33, 46, 38), "−", choiceButton)) selectedPlayers = Mathf.Max(2, selectedPlayers - 1);
            GUI.Label(new Rect(area.x + 210, area.y + 40, 210, 22), selectedPlayers + " · tú y " + (selectedPlayers - 1) + " IA", RtsSkin.Small);
            if (GUI.Button(new Rect(area.x + 426, area.y + 33, 46, 38), "+", choiceButton)) selectedPlayers = Mathf.Min(PlayerRules.MaxPlayers, selectedPlayers + 1);

            GUI.Label(new Rect(area.x + 570, area.y + 39, 90, 22), "SEMILLA", subheading);
            seedText = GUI.TextField(new Rect(area.x + 657, area.y + 33, 176, 38), seedText, 11, choiceButton);
            if (GUI.Button(new Rect(area.x + 842, area.y + 33, 202, 38), "NUEVA SEMILLA", choiceButton)) { BattleSession.NewSeed(); seedText = BattleSession.SeedForNewMatch.ToString(); }

            GUI.Label(new Rect(area.x, area.y + 94, 240, 22), "REPARTO INICIAL", subheading);
            selectedLayout = (BattleSession.StartLayout)GUI.SelectionGrid(new Rect(area.x, area.y + 122, 500, 42), (int)selectedLayout, new[] { "Ciudades al azar", "Grupos iniciales", "Posiciones fijas" }, 3, choiceButton);
            GUI.Label(new Rect(area.x + 540, area.y + 94, 280, 22), "DIFICULTAD DE IA", subheading);
            selectedDifficulty = (BattleSession.AiDifficulty)GUI.SelectionGrid(new Rect(area.x + 540, area.y + 122, 504, 42), (int)selectedDifficulty, new[] { "Relajada · ataque más tarde", "Estándar · presión temprana" }, 2, choiceButton);

            bool imported = selectedMap == ScenarioMap.Europe || selectedMap == ScenarioMap.NewWorld;
            GUI.Label(new Rect(area.x, area.y + 191, 330, 20), "RELIEVE IMPORTADO", subheading);
            GUI.enabled = imported;
            sourceMountains = GUI.Toggle(new Rect(area.x, area.y + 218, 650, 34), sourceMountains, "Añadir cordilleras suaves a las fuentes Europe y New World", RtsSkin.Button);
            GUI.enabled = true;
            GUI.Label(new Rect(area.x + 680, area.y + 222, 360, 22), imported ? "Respeta coordenadas y despeja anclajes." : "Disponible en escenarios importados.", RtsSkin.Tiny);
        }

        static void EnsureStyles()
        {
            RtsSkin.Initialize();
            if (heading != null) return;
            heading = new GUIStyle(RtsSkin.Title) { fontSize = 22 };
            subheading = new GUIStyle(RtsSkin.Small) { fontSize = 13 };
            cardTitle = new GUIStyle(RtsSkin.Text) { fontSize = 16, fontStyle = FontStyle.Bold };
            cardDetail = new GUIStyle(RtsSkin.Small) { fontSize = 12 };
            startButton = new GUIStyle(RtsSkin.Button) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            choiceButton = new GUIStyle(RtsSkin.Button) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            errorStyle = new GUIStyle(RtsSkin.Small) { normal = { textColor = new Color(1f, .47f, .35f) } };
            versionLabel = new GUIStyle(subheading) { alignment = TextAnchor.MiddleRight };
            loadingLabel = new GUIStyle(RtsSkin.Tiny) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
