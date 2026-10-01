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

        [UnityTest] public IEnumerator PausedRightDragPansAndMiddleDragOrbitsWithoutIssuingOrders()
        {
            var point=UiViewport.WorldRect.center;
            controller.SelectOnly(battle.Units.First(unit=>unit.Team==0));
            battle.TogglePause();Pump(point);
            float time=battle.BattleTime;
            long applied=battle.Commands.AppliedCount;
            foreach(ushort button in new ushort[]{2,4})
            {
                var before=controller.CameraRig.FocusPoint;var rotation=Camera.main.transform.rotation;
                Pump(point,button);
                Pump(point+Vector2.right*40,button);
                Assert.That(controller.CameraDragging,Is.True,"Both mouse drag gestures must survive paused frames.");
                if(button==2)Assert.That(Vector3.Distance(before,controller.CameraRig.FocusPoint),Is.GreaterThan(.1f));
                else
                {
                    Assert.That(controller.CameraRig.FocusPoint,Is.EqualTo(before));
                    Assert.That(Quaternion.Angle(rotation,Camera.main.transform.rotation),Is.GreaterThan(1));
                }
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
                    Pump(UiViewport.WorldRect.center);
                    Assert.That(controller.CameraRig.TargetZoom,Is.EqualTo(before),"A discarded HUD wheel must not jump after the pointer returns to the map.");
                }
            }
            finally { InputSystem.RemoveDevice(pen); }
            yield return null;
        }

        [UnityTest] public IEnumerator NativeTouchpadBurstPansWithoutZoomBeforeUnityAccumulatesIt()
        {
            var point=UiViewport.WorldRect.center;
            controller.CameraRig.ResetView();
            float initial=controller.CameraRig.TargetZoom;
            var before=controller.CameraRig.FocusPoint;
            for(int eventIndex=0;eventIndex<10;eventIndex++)
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point,scroll=Vector2.one*.1f});
            InputSystem.Update();controller.SendMessage("Update");
            Assert.That(controller.CameraRig.TargetZoom,Is.EqualTo(initial),"Ten fine events must not become a wheel-notch zoom.");
            Assert.That(Vector3.Distance(before,controller.CameraRig.FocusPoint),Is.GreaterThan(.1f));
            before=controller.CameraRig.FocusPoint;
            // Ctrl is delivered before the wheel within the same Input System update.
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl));
            InputSystem.QueueStateEvent(mouse,new MouseState {position=point,scroll=Vector2.up*.1f});
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            InputSystem.Update();controller.SendMessage("Update");
            Assert.That(controller.CameraRig.TargetZoom,Is.LessThan(initial),"Native ctrl+wheel pinch must zoom even if Ctrl is released in the same frame.");
            Assert.That(controller.CameraRig.FocusPoint,Is.EqualTo(before));
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

        [UnityTest] public IEnumerator BrowserPanSharesTouchDragAndHudOwnsItsScrollWhilePaused()
        {
            var point=UiViewport.WorldRect.center;
            var rig=controller.CameraRig;
            battle.TogglePause();Pump(point);
            long commands=battle.Commands.AppliedCount;
            float zoom=rig.TargetZoom;
            var start=rig.FocusPoint;
            var delta=new Vector2(-25,18);
            rig.Drag(point,point+delta);
            var expected=rig.FocusPoint;
            rig.SetHome(start);
            controller.SendMessage("ApplyCameraScroll",new CameraScrollInput(0,delta,point));
            Assert.That(Vector3.Distance(rig.FocusPoint,expected),Is.LessThan(.001f),"Touchpad and direct touch share ground-space dragging.");
            Assert.That(Vector3.Distance(start,rig.FocusPoint),Is.GreaterThan(.1f));
            Assert.That(rig.TargetZoom,Is.EqualTo(zoom),"Two-finger scrolling cannot change scale.");
            var before=rig.FocusPoint;
            controller.SendMessage("ApplyCameraScroll",new CameraScrollInput(1,delta,Vector2.zero));
            Assert.That(rig.FocusPoint,Is.EqualTo(before),"HUD scroll cannot pan even when the mouse is now over the world.");
            Assert.That(rig.TargetZoom,Is.EqualTo(zoom));
            controller.SendMessage("ApplyCameraScroll",new CameraScrollInput(1,Vector2.zero,point));
            Assert.That(rig.TargetZoom,Is.LessThan(zoom),"Pinch zoom remains available while paused.");
            Assert.That(battle.Commands.PendingCount,Is.Zero);
            Assert.That(battle.Commands.AppliedCount,Is.EqualTo(commands));
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
