using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using PenButton = UnityEngine.InputSystem.PenButton;

namespace RiskAI.Tests
{
    public sealed class RtsRuntimeTooltipTests
    {
        Scene previousScene, scene;
        EventSystem previousEventSystem;
        bool previousEventSystemEnabled;
        InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
#endif
        PanelSettings settings;
        VisualElement root, content;
        Button button;
        Label label;
        RtsRuntimeTooltip tooltip;
        Touchscreen touchscreen;
        Mouse mouse;
        Pen pen;
        int actions, clicks, downs, cancellations, downPointer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            actions = clicks = downs = cancellations = 0;
            mouse = null; pen = null; touchscreen = null;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            previousScene = SceneManager.GetActiveScene();
            previousEventSystem = EventSystem.current;
            if(previousEventSystem)
            {
                previousEventSystemEnabled = previousEventSystem.enabled;
                previousEventSystem.enabled = false;
            }
            scene = SceneManager.CreateScene("Runtime tooltip input");
            SceneManager.SetActiveScene(scene);
            var events = new GameObject("Tooltip EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/RiskAITheme");
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1;
            settings.sortingOrder = 1000;
            var document = new GameObject("Tooltip document").AddComponent<UIDocument>();
            document.panelSettings = settings;
            root = document.rootVisualElement;
            content = new VisualElement();
            root.Add(content);
            button = RtsUiStyle.Button("Acción", () => actions++, "Tooltip action");
            button.tooltip = "Explicación de la acción.";
            button.style.width = 200;
            button.style.height = 60;
            button.style.marginLeft = 32;
            button.style.marginTop = 32;
            button.RegisterCallback<ClickEvent>(_ => clicks++);
            content.Add(button);
            tooltip = new RtsRuntimeTooltip(root);
            label = root.Q<Label>("Runtime tooltip");
            root.RegisterCallback<PointerDownEvent>(evt => { downs++; downPointer = evt.pointerId; }, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerCancelEvent>(_ => cancellations++, TrickleDown.TrickleDown);
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return null; yield return null;
            Assert.That(root.panel, Is.Not.Null);
            Assert.That(button.worldBound.width, Is.GreaterThan(0));
        }

        Vector2 Position => new Vector2(button.worldBound.center.x, Screen.height-button.worldBound.center.y);

        void Touch(int id, TouchPhase phase, Vector2 position)
        {
            // Consume in the engine's dynamic update, immediately before EventSystem.Process.
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            { touchId = id, phase = phase, position = position, pressure = phase == TouchPhase.Ended ? 0 : 1 });
        }

        IEnumerator Frame() { yield return null; yield return null; }
        IEnumerator Hold() { yield return new WaitForSecondsRealtime(.75f); }
        void AssertHidden() => Assert.That(label.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        void AssertShown()
        {
            Assert.That(label.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(label.text, Is.EqualTo(button.tooltip));
            Assert.That(label.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(actions, Is.Zero);
            Assert.That(clicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ShortTouchTapInvokesButtonAndClickOnce()
        {
            Touch(1, TouchPhase.Began, Position); yield return Frame();
            Assert.That(downs, Is.EqualTo(1), "Input Actions must deliver the contact to the attached panel.");
            AssertHidden();
            Touch(1, TouchPhase.Ended, Position); yield return Frame();
            yield return Hold();
            Assert.That(actions, Is.EqualTo(1));
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(cancellations, Is.Zero);
            AssertHidden();
        }

        [UnityTest]
        public IEnumerator HeldTouchShowsHelpWithoutActionAndNextTapStillWorks()
        {
            Touch(1, TouchPhase.Began, Position); yield return Frame(); yield return Hold();
            AssertShown();
            Assert.That(cancellations, Is.EqualTo(1), "Recognition must cancel native Clickable and ClickDetector state.");
            yield return Hold(); AssertShown();
            Touch(1, TouchPhase.Ended, Position); yield return Frame();
            AssertHidden(); Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
            Touch(2, TouchPhase.Began, Position); yield return Frame();
            Touch(2, TouchPhase.Ended, Position); yield return Frame();
            Assert.That(actions, Is.EqualTo(1)); Assert.That(clicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DragBeforeThresholdCancelsHelpAndAllowsNativeScroll()
        {
            var scroll = new ScrollView();
            scroll.style.width = 300; scroll.style.height = 220;
            content.Add(scroll); scroll.Add(button);
            for(int i = 0; i < 30; i++) scroll.Add(new Label("Contenido " + i));
            yield return Frame();
            var start = Position;
            Touch(1, TouchPhase.Began, start); yield return Frame();
            Touch(1, TouchPhase.Moved, start+Vector2.up*45); yield return Frame();
            Touch(1, TouchPhase.Moved, start+Vector2.up*80); yield return Frame();
            yield return Hold();
            AssertHidden();
            Assert.That(scroll.scrollOffset.y, Is.GreaterThan(0), "Tooltip tracking must not take native scroll ownership.");
            Touch(1, TouchPhase.Ended, start+Vector2.up*80); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CapturedButtonMovementBeyondSlopCancelsHelpWithoutTakingCapture()
        {
            var start = Position;
            Touch(1, TouchPhase.Began, start); yield return Frame();
            Assert.That(root.panel.GetCapturingElement(downPointer), Is.SameAs(button));
            Touch(1, TouchPhase.Moved, start+Vector2.right*20); yield return Frame();
            Assert.That(root.panel.GetCapturingElement(downPointer), Is.SameAs(button), "Tracking must preserve the native capture owner.");
            yield return Hold(); AssertHidden();
            Assert.That(cancellations, Is.Zero, "Movement should leave the native gesture intact.");
            Touch(1, TouchPhase.Ended, start+Vector2.right*20); yield return Frame();
            Assert.That(actions, Is.EqualTo(1)); Assert.That(clicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CapturedButtonSmallMovementStillAllowsDeliberateHelp()
        {
            var start = Position;
            Touch(1, TouchPhase.Began, start); yield return Frame();
            Assert.That(root.panel.GetCapturingElement(downPointer), Is.SameAs(button));
            Touch(1, TouchPhase.Moved, start+Vector2.right*6); yield return Frame();
            yield return Hold(); AssertShown();
            Assert.That(cancellations, Is.EqualTo(1));
            Assert.That(root.panel.GetCapturingElement(downPointer), Is.Null);
            Touch(1, TouchPhase.Ended, start+Vector2.right*6); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SecondContactOutsidePanelContentCancelsPendingHelp()
        {
            Touch(1, TouchPhase.Began, Position); yield return Frame();
            Touch(2, TouchPhase.Began, new Vector2(Screen.width-4, 4)); yield return Frame();
            yield return Hold(); AssertHidden();
            Touch(2, TouchPhase.Ended, new Vector2(Screen.width-4, 4));
            Touch(1, TouchPhase.Ended, Position); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
            Assert.That(cancellations, Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator CanceledInputContactCannotShowHelpOrActivate()
        {
            Touch(1, TouchPhase.Began, Position); yield return Frame();
            Touch(1, TouchPhase.Canceled, Position); yield return Frame();
            yield return Hold(); AssertHidden();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ContentRebuildCancelsPendingPressAndReusedButtonDoesNotClickLate()
        {
            var position = Position;
            Touch(1, TouchPhase.Began, position); yield return Frame();
            tooltip.SetContentChanged(); content.Clear(); content.Add(button);
            yield return Frame(); yield return Hold(); AssertHidden();
            Touch(1, TouchPhase.Ended, position); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DetachAfterHelpAndReattachCannotLeakReleaseClick()
        {
            var position = Position;
            Touch(1, TouchPhase.Began, position); yield return Frame(); yield return Hold();
            AssertShown();
            button.RemoveFromHierarchy(); yield return Frame(); AssertHidden();
            content.Add(button); yield return Frame();
            Touch(1, TouchPhase.Ended, position); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisposeAfterHelpClearsNativeClickStateBeforeRelease()
        {
            Touch(1, TouchPhase.Began, Position); yield return Frame(); yield return Hold();
            AssertShown();
            tooltip.Dispose();
            Assert.That(root.Q<Label>("Runtime tooltip"), Is.Null);
            Touch(1, TouchPhase.Ended, Position); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
            Touch(2, TouchPhase.Began, Position); yield return Frame();
            Touch(2, TouchPhase.Ended, Position); yield return Frame();
            Assert.That(actions, Is.EqualTo(1)); Assert.That(clicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisabledButtonCanExplainWithoutBecomingActionable()
        {
            button.SetEnabled(false);
            Touch(1, TouchPhase.Began, Position); yield return Frame(); yield return Hold();
            AssertShown(); Assert.That(button.enabledSelf, Is.False);
            Touch(1, TouchPhase.Ended, Position); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
            Assert.That(button.enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator PenTipHoldSuppressesReleaseButBarrelDoesNotStartHelp()
        {
            pen = InputSystem.AddDevice<Pen>();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}.WithButton(PenButton.Tip));
            yield return Frame(); yield return Hold(); AssertShown();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position});
            yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero); AssertHidden();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}.WithButton(PenButton.BarrelFirst));
            yield return Frame(); yield return Hold(); AssertHidden();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PenTipAndBarrelTogetherCannotOpenHelpOrActivate()
        {
            pen = InputSystem.AddDevice<Pen>();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}
                .WithButton(PenButton.Tip).WithButton(PenButton.BarrelFirst));
            yield return Frame(); yield return Hold(); AssertHidden();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}.WithButton(PenButton.Tip));
            yield return Frame();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}); yield return Frame();
            Assert.That(actions, Is.EqualTo(1)); Assert.That(clicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AddingBarrelDuringCapturedTipHoldCancelsHelpAndLateAction()
        {
            pen = InputSystem.AddDevice<Pen>();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}.WithButton(PenButton.Tip));
            yield return Frame();
            Assert.That(root.panel.GetCapturingElement(downPointer), Is.SameAs(button));
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}
                .WithButton(PenButton.Tip).WithButton(PenButton.BarrelFourth));
            yield return Frame(); yield return Hold(); AssertHidden();
            Assert.That(cancellations, Is.EqualTo(1));
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}.WithButton(PenButton.Tip));
            yield return Frame(); yield return Hold(); AssertHidden();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position}); yield return Frame();
            Assert.That(actions, Is.Zero); Assert.That(clicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MouseAndPenHoverStillShowHelpAndSourceRemovalHidesIt()
        {
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState {position = Position});
            yield return Frame(); yield return Hold(); AssertShown();
            InputSystem.QueueStateEvent(mouse, new MouseState {position = new Vector2(Screen.width-4, 4)});
            yield return Frame(); AssertHidden();
            pen = InputSystem.AddDevice<Pen>();
            InputSystem.QueueStateEvent(pen, new PenState {position = Position});
            yield return Frame(); yield return Hold(); AssertShown();
            button.RemoveFromHierarchy(); yield return Frame(); AssertHidden();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            tooltip?.Dispose();
            if(touchscreen != null) InputSystem.RemoveDevice(touchscreen);
            if(mouse != null) InputSystem.RemoveDevice(mouse);
            if(pen != null) InputSystem.RemoveDevice(pen);
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(scene);
            if(settings) Object.Destroy(settings);
            if(previousEventSystem) previousEventSystem.enabled = previousEventSystemEnabled;
            InputSystem.settings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
#endif
        }
    }
}
