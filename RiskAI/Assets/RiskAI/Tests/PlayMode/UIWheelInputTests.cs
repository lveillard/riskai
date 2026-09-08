using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace RiskAI.Tests
{
    public sealed class UIWheelInputTests
    {
        Scene previous, scene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;
        float previousTimeScale;
        InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
#endif
        Mouse mouse;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene(); previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch; previousMode = BattleSession.ModeForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch; previousSeed = BattleSession.SeedForNewMatch;
            previousTimeScale = Time.timeScale; Time.timeScale = 1;
            previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            BattleSession.MapForNewMatch = ScenarioMap.Classic; BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest; BattleSession.PlayerCountForNewMatch = 2;
            BattleSession.SeedForNewMatch = 19032;
            scene = SceneManager.CreateScene("UI wheel input"); SceneManager.SetActiveScene(scene);
            new GameObject("UI wheel bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current; battle.AiEnabled = false;
            Object.FindFirstObjectByType<RtsController>().enabled = true;
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseWheelScrollsHudMenuBothDirectionsWithoutMapZoom()
        {
            var controller = Object.FindFirstObjectByType<RtsController>();
            var hud = Object.FindFirstObjectByType<BattleHud>();
            controller.HelpVisible = true;
            yield return null; yield return null;
            var document = hud.GetComponent<UIDocument>();
            var scroll = document.rootVisualElement.Q<ScrollView>("HUD modal scroll");
            Assert.That(scroll, Is.Not.Null);
            for (int i = 0; i < 32; i++) scroll.contentContainer.Add(new Label("Contenido desplazable " + i));
            yield return null;
            int actionTicks=0,uiWheels=0;
            var module=Object.FindFirstObjectByType<InputSystemUIInputModule>();
            module.scrollWheel.action.performed+=context=>{if(context.ReadValue<Vector2>().sqrMagnitude>.001f)actionTicks++;};
            scroll.RegisterCallback<WheelEvent>(evt=>uiWheels++,TrickleDown.TrickleDown);
            var bounds = scroll.contentViewport.worldBound;
            Assert.That(bounds.height, Is.GreaterThan(0));
            Assert.That(scroll.contentContainer.layout.height, Is.GreaterThan(bounds.height));
            float zoom = controller.CameraRig.TargetZoom;
            float scale=document.panelSettings.scale;
            Vector2 pointer = new Vector2(bounds.center.x*scale, Screen.height - bounds.center.y*scale);
            Pump(pointer,0);yield return null;yield return null;
            Pump(pointer, -1);
            yield return null;yield return null;
            float down = scroll.scrollOffset.y;
            Pump(pointer, 1);
            yield return null;yield return null;
            float up = scroll.scrollOffset.y;
            Assert.That(actionTicks,Is.GreaterThan(0),"The engine frame must deliver the synthetic physical wheel through Input Actions.");
            Assert.That(uiWheels,Is.GreaterThan(0),"The EventSystem must forward the wheel to the retained panel. focus="+EventSystem.current.isFocused);
            Assert.That(down, Is.GreaterThan(0), "A wheel tick over the modal must scroll its native ScrollView.");
            Assert.That(up, Is.LessThan(down), "The opposite wheel direction must scroll the same panel back.");
            Assert.That(controller.CameraRig.TargetZoom, Is.EqualTo(zoom), "HUD wheel input must not zoom the tactical map.");

            controller.HelpVisible = false;
            controller.SelectBuildings(BattleSession.Current.Towns.Where(t => t.State.Owner == 0), NavalWorld.Current.Harbors.Where(h => h.Owner == 0));
            yield return null; yield return null;
            var hudScroll = document.rootVisualElement.Q<ScrollView>("HUD selection column") ?? document.rootVisualElement.Q<ScrollView>("HUD context");
            Assert.That(hudScroll, Is.Not.Null, "The non-modal HUD must expose a retained scrollable context.");
            for (int i = 0; i < 32; i++) hudScroll.contentContainer.Add(new Label("HUD contenido desplazable " + i));
            yield return null; yield return null;
            Assert.That(hudScroll.contentContainer.layout.height, Is.GreaterThan(hudScroll.contentViewport.worldBound.height));
            var hudBounds = hudScroll.contentViewport.worldBound;
            pointer = new Vector2(hudBounds.center.x * scale, Screen.height - hudBounds.center.y * scale);
            Pump(pointer,0);yield return null;yield return null;
            Pump(pointer, -1);yield return null;yield return null;
            float hudDown = hudScroll.scrollOffset.y;
            Pump(pointer, 1);yield return null;yield return null;
            float hudUp = hudScroll.scrollOffset.y;
            Assert.That(hudDown, Is.GreaterThan(0), "A real wheel tick must scroll the non-modal HUD context.");
            Assert.That(hudUp, Is.LessThan(hudDown));
            Assert.That(controller.CameraRig.TargetZoom, Is.EqualTo(zoom), "Non-modal HUD wheel input must not zoom the tactical map.");
        }

        [UnityTest]
        public IEnumerator RuntimeTooltipShowsOnHoverAndHidesWhenItsSourceIsRemoved()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();controller.HelpVisible=true;
            yield return null;yield return null;
            var document=Object.FindFirstObjectByType<BattleHud>().GetComponent<UIDocument>();
            var scroll=document.rootVisualElement.Q<ScrollView>("HUD modal scroll");
            var button=RtsUiStyle.Button("Hover fixture",()=>{});button.tooltip="Compra una vez en la cola compatible.";
            scroll.contentContainer.Insert(0,button);
            yield return null;yield return null;
            var point=button.worldBound.center;float scale=document.panelSettings.scale;
            Pump(new Vector2(point.x*scale,Screen.height-point.y*scale),0);
            yield return new WaitForSecondsRealtime(.6f);
            var tip=document.rootVisualElement.Q<Label>("Runtime tooltip");
            Assert.That(tip,Is.Not.Null);Assert.That(tip.resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
            Assert.That(tip.text,Is.EqualTo(button.tooltip));Assert.That(tip.pickingMode,Is.EqualTo(PickingMode.Ignore));
            Assert.That(tip.worldBound.xMin,Is.GreaterThanOrEqualTo(0));
            Assert.That(tip.worldBound.xMax,Is.LessThanOrEqualTo(document.rootVisualElement.worldBound.xMax+.1f));
            button.RemoveFromHierarchy();yield return null;yield return null;
            Assert.That(tip.resolvedStyle.display,Is.EqualTo(DisplayStyle.None),"A rebuilt context must never retain a stale tooltip.");
        }

        void Pump(Vector2 position, float wheel)
        {
            // Let the engine's next dynamic update consume the event immediately
            // before EventSystem.Process. A manual extra InputSystem.Update here
            // can reset DeltaControl again before the UI module sees the frame.
            InputSystem.QueueStateEvent(mouse,new MouseState {position=position,scroll=Vector2.up*wheel});
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior=previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
#endif
            Time.timeScale = previousTimeScale; BattleSession.MapForNewMatch = previousMap;
            BattleSession.LayoutForNewMatch = previousLayout; BattleSession.ModeForNewMatch = previousMode;
            BattleSession.PlayerCountForNewMatch = previousPlayers; BattleSession.SeedForNewMatch = previousSeed;
            MapLayout.Configure(previousMap); SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
