using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>
    /// Shared retained-UI hit exclusion. Screen coordinates use the Input System's
    /// bottom-left origin, exactly like UiViewport and RtsController.
    /// </summary>
    public static class RtsUiInput
    {
        static readonly List<Rect> blocks = new List<Rect>(8);

        public static void BeginFrame() => blocks.Clear();
        public static void Block(Rect screenRect)
        {
            if (screenRect.width > 0 && screenRect.height > 0) blocks.Add(screenRect);
        }
        public static bool BlocksWorld(Vector2 screen)
        {
            if (!UiViewport.WorldRect.Contains(screen)) return true;
            for (int i = 0; i < blocks.Count; i++) if (blocks[i].Contains(screen)) return true;
            return false;
        }
    }

    /// <summary>Creates a runtime UIToolkit panel with the project's Input System event module.</summary>
    public sealed class RtsUiRuntime : MonoBehaviour
    {
        PanelSettings panelSettings;
        EventSystem ownedEventSystem;
        VisualElement safeRoot;
        float lastScale;
        Rect lastSafe;

        public VisualElement Root { get; private set; }
        public ThemeStyleSheet Theme => panelSettings != null ? panelSettings.themeStyleSheet : null;

        public static RtsUiRuntime Attach(GameObject host, string panelName, int sortingOrder)
        {
            var runtime = host.GetComponent<RtsUiRuntime>();
            if (runtime) return runtime;
            runtime = host.AddComponent<RtsUiRuntime>();
            runtime.Create(panelName, sortingOrder);
            return runtime;
        }

        void Create(string panelName, int sortingOrder)
        {
            EnsureEventSystem();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = panelName + " panel settings";
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/RiskAITheme");
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = Mathf.Max(.25f, UiViewport.Scale);
            panelSettings.sortingOrder = sortingOrder;
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            Root = document.rootVisualElement;
            var chrome=Resources.Load<StyleSheet>("UI/RiskAIChrome");
            if(chrome)Root.styleSheets.Add(chrome);
            Root.pickingMode = PickingMode.Ignore;
            safeRoot = new VisualElement { name = panelName + " safe root", pickingMode = PickingMode.Ignore };
            safeRoot.style.position = Position.Absolute;
            Root.Add(safeRoot);
            RefreshViewport(true);
        }

        void EnsureEventSystem()
        {
            var existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing)
            {
                // A scene-owned EventSystem is preserved, while its UI Toolkit module
                // is supplied if the scene only created the legacy component.
                if (!existing.GetComponent<InputSystemUIInputModule>())
                {
                    var existingModule = existing.gameObject.AddComponent<InputSystemUIInputModule>();
                    existingModule.AssignDefaultActions();
                }
                foreach (var legacy in existing.GetComponents<StandaloneInputModule>()) legacy.enabled = false;
                return;
            }
            var go = new GameObject("RiskAI UI EventSystem");
            ownedEventSystem = go.AddComponent<EventSystem>();
            var createdModule = go.AddComponent<InputSystemUIInputModule>();
            createdModule.AssignDefaultActions();
        }

        public void SetContent(VisualElement content)
        {
            if (safeRoot == null) return;
            safeRoot.Clear();
            if (content != null) safeRoot.Add(content);
        }

        void Update() => RefreshViewport(false);
        void RefreshViewport(bool force)
        {
            if (safeRoot == null) return;
            float scale = Mathf.Max(.25f, UiViewport.Scale);
            Rect safe = UiViewport.SafeRect;
            if (!force && Mathf.Approximately(lastScale, scale) && safe.Equals(lastSafe)) return;
            lastScale = scale; lastSafe = safe;
            panelSettings.scale = scale;
            safeRoot.style.left = safe.x / scale;
            safeRoot.style.top = (Screen.height - safe.yMax) / scale;
            safeRoot.style.width = UiViewport.LogicalWidth;
            safeRoot.style.height = UiViewport.LogicalHeight;
        }

        void OnDestroy()
        {
            if (panelSettings) Destroy(panelSettings);
            if (ownedEventSystem) Destroy(ownedEventSystem.gameObject);
        }
    }

    public static class RtsUiStyle
    {
        public static readonly Color Slate = new Color(.035f, .043f, .041f, 1);
        public static readonly Color PanelColor = new Color(.085f, .083f, .068f, .99f);
        public static readonly Color Card = new Color(.16f, .15f, .115f, 1);
        public static readonly Color Bronze = new Color(.72f, .56f, .30f, 1);
        public static readonly Color Gold = new Color(.94f, .79f, .43f, 1);
        public static readonly Color Text = new Color(.93f, .92f, .84f, 1);
        public static readonly Color Muted = new Color(.68f, .72f, .70f, 1);
        static Font titleFont;

        public static Label Title(string text, string name = null, int size = 20)
        {
            var label=Label(text,name,size);
            if(!titleFont)titleFont=Resources.Load<Font>("UI/CinzelDecorative-Regular");
            if(titleFont)label.style.unityFontDefinition=FontDefinition.FromFont(titleFont);
            label.style.color=Gold;label.style.whiteSpace=WhiteSpace.Normal;
            return label;
        }

        public static Label Label(string text, string name = null, int size = 14)
        {
            var label = new Label(text) { name = name };
            label.style.color = Text; label.style.fontSize = size; label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }
        public static Button Button(string text, System.Action action, string name = null)
        {
            var button = new RtsOrnamentButton(action) { text = text, name = name };
            button.style.minHeight = 44; button.style.paddingLeft = 12; button.style.paddingRight = 12;
            button.style.marginRight = 8; button.style.marginBottom = 8;
            button.style.backgroundColor = Card; button.style.borderTopColor = Bronze; button.style.borderBottomColor = Bronze;
            button.style.borderLeftColor = Bronze; button.style.borderRightColor = Bronze;
            button.style.borderTopWidth = 1; button.style.borderBottomWidth = 1; button.style.borderLeftWidth = 1; button.style.borderRightWidth = 1;
            button.style.color = Text; button.style.unityTextAlign = TextAnchor.MiddleCenter;
            return button;
        }
        public static VisualElement Panel(string name = null)
        {
            var panel = new RtsOrnamentPanel { name = name };
            panel.style.backgroundColor = PanelColor; panel.style.paddingLeft = 16; panel.style.paddingRight = 16;
            panel.style.paddingTop = 14; panel.style.paddingBottom = 14;
            panel.style.borderTopColor = Bronze; panel.style.borderBottomColor = Bronze;
            panel.style.borderLeftColor = Bronze; panel.style.borderRightColor = Bronze;
            panel.style.borderTopWidth = 1; panel.style.borderBottomWidth = 1; panel.style.borderLeftWidth = 1; panel.style.borderRightWidth = 1;
            return panel;
        }
        public static void Row(VisualElement element, bool wrap = false)
        {
            element.style.flexDirection = FlexDirection.Row; element.style.flexWrap = wrap ? Wrap.Wrap : Wrap.NoWrap;
            element.style.alignItems = Align.Center;
        }
    }
}
