using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class MouseCameraInputTests
    {
        Scene previousScene, scene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers;
        RtsController controller;
        BattleSession battle;
        Mouse mouse, previousMouse;
        Keyboard keyboard, previousKeyboard;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditor;
        bool previousRunBackground;

        [UnitySetUp] public IEnumerator SetUp()
        {
            previousBackground=InputSystem.settings.backgroundBehavior;
            previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            previousRunBackground=Application.runInBackground;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground=true;
            previousScene=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;
            previousLayout=BattleSession.LayoutForNewMatch;previousPlayers=BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=2;
            scene=SceneManager.CreateScene("Mouse camera input");SceneManager.SetActiveScene(scene);
            new GameObject("Mouse camera bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            controller=Object.FindFirstObjectByType<RtsController>();controller.enabled=false;
            previousMouse=Mouse.current;previousKeyboard=Keyboard.current;
            mouse=InputSystem.AddDevice<Mouse>("Camera mouse fixture");
            keyboard=InputSystem.AddDevice<Keyboard>("Camera keyboard fixture");
            yield return null;
            controller.SendMessage("OnApplicationFocus",true);
            controller.CameraRig.SetHome(Vector3.zero);
            Pump(UiViewport.WorldRect.center);
        }

        [UnityTest] public IEnumerator PausedRightAndMiddleDragsMoveCameraWithoutIssuingOrders()
        {
            var point=UiViewport.WorldRect.center;
            controller.SelectOnly(battle.Units.First(unit=>unit.Team==0));
            battle.TogglePause();Pump(point);
            float time=battle.BattleTime;
            long applied=battle.Commands.AppliedCount;
            foreach(ushort button in new ushort[]{2,4})
            {
                var before=controller.CameraRig.FocusPoint;
                Pump(point,button);
                Pump(point+Vector2.right*40,button);
                Assert.That(controller.CameraDragging,Is.True,"Both mouse drag gestures must survive paused frames.");
                Assert.That(Vector3.Distance(before,controller.CameraRig.FocusPoint),Is.GreaterThan(.1f));
                Pump(point+Vector2.right*40);
                Assert.That(controller.CameraDragging,Is.False);
            }
            // A tap is a gameplay action; unlike a drag it must stay blocked.
            Pump(point,2);Pump(point);
            Assert.That(battle.Commands.PendingCount,Is.Zero);
            yield return null;
            Assert.That(battle.BattleTime,Is.EqualTo(time));
            Assert.That(battle.Commands.AppliedCount,Is.EqualTo(applied));
        }

        [UnityTest] public IEnumerator WheelZoomUsesMouseCoordinatesWhileRunningAndPausedWithHoveringPen()
        {
            var pen=InputSystem.AddDevice<Pen>("Hovering camera pen fixture");
            try
            {
                foreach(bool paused in new[]{false,true})
                {
                    if(battle.Paused!=paused)battle.TogglePause();
                    Pump(UiViewport.WorldRect.center);
                    float before=controller.CameraRig.TargetZoom;
                    InputSystem.QueueStateEvent(mouse,new MouseState { position=UiViewport.WorldRect.center,scroll=Vector2.up });
                    InputSystem.QueueStateEvent(pen,new PenState { position=Vector2.zero });
                    InputSystem.Update();pen.MakeCurrent();keyboard.MakeCurrent();
                    Assert.That(UnityEngine.InputSystem.Pointer.current,Is.SameAs(pen));
                    Assert.That(mouse.scroll.ReadValue().y,Is.GreaterThan(0));
                    controller.SendMessage("Update");
                    Assert.That(controller.CameraRig.TargetZoom,Is.LessThan(before),"Wheel input must use the mouse's world position, not the pen hovering over the HUD.");
                    before=controller.CameraRig.TargetZoom;
                    Pump(UiViewport.WorldRect.center,scroll:-1);
                    Assert.That(controller.CameraRig.TargetZoom,Is.GreaterThan(before));
                    before=controller.CameraRig.TargetZoom;
                    Pump(Vector2.zero,scroll:1);
                    Assert.That(controller.CameraRig.TargetZoom,Is.EqualTo(before),"Scrolling the HUD cannot zoom the battlefield.");
                }
            }
            finally { InputSystem.RemoveDevice(pen); }
            yield return null;
        }

        [UnityTest] public IEnumerator PauseTransitionCancelsHeldClickAndFreshDragStillWorksAfterResume()
        {
            var point=UiViewport.WorldRect.center;
            controller.SelectOnly(battle.Units.First(unit=>unit.Team==0));
            Pump(point,2);
            battle.TogglePause();Pump(point,2);
            battle.TogglePause();Pump(point);
            Assert.That(battle.Commands.PendingCount,Is.Zero,"Releasing a pre-pause click after resume must not submit a stale command.");
            var before=controller.CameraRig.FocusPoint;
            Pump(point,2);Pump(point+Vector2.left*40,2);
            Assert.That(Vector3.Distance(before,controller.CameraRig.FocusPoint),Is.GreaterThan(.1f));
            controller.HelpVisible=true;Pump(point+Vector2.left*80,2);
            Assert.That(controller.CameraDragging,Is.False,"Opening the menu still cancels camera gestures.");
            yield return null;
        }

        void Pump(Vector2 point,ushort buttons=0,float scroll=0)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point,buttons=buttons,scroll=Vector2.up*scroll });
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            mouse.MakeCurrent();keyboard.MakeCurrent();controller.SendMessage("Update");
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(previousMouse!=null)previousMouse.MakeCurrent();if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
            SceneManager.SetActiveScene(previousScene);yield return SceneManager.UnloadSceneAsync(scene);
            BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;BattleSession.PlayerCountForNewMatch=previousPlayers;
            InputSystem.settings.backgroundBehavior=previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor;
            Application.runInBackground=previousRunBackground;
        }
    }
}
