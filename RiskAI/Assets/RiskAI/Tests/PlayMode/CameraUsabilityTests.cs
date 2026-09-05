using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class CameraUsabilityTests
    {
        Scene previous, scene;
        BattleSession battle;

        [Test] public void EdgePolicyUsesEveryOuterEdgeAndNormalizesCorners()
        {
            var viewport=new Vector2(1600,900);
            Assert.That(RtsCameraPolicy.EdgePanDirection(new Vector2(0,450),viewport),Is.EqualTo(new Vector2(-1,0)));
            Assert.That(RtsCameraPolicy.EdgePanDirection(new Vector2(1600,450),viewport),Is.EqualTo(new Vector2(1,0)));
            Assert.That(RtsCameraPolicy.EdgePanDirection(new Vector2(800,0),viewport),Is.EqualTo(new Vector2(0,-1)));
            Assert.That(RtsCameraPolicy.EdgePanDirection(new Vector2(800,900),viewport),Is.EqualTo(new Vector2(0,1)));
            var corner=RtsCameraPolicy.EdgePanDirection(new Vector2(0,0),viewport);
            Assert.That(corner.x,Is.EqualTo(-Mathf.Sqrt(.5f)).Within(.0001f));
            Assert.That(corner.y,Is.EqualTo(-Mathf.Sqrt(.5f)).Within(.0001f));
            Assert.That(corner.magnitude,Is.EqualTo(1).Within(.0001f));
            Assert.That(RtsCameraPolicy.EdgePanDirection(new Vector2(-1,450),viewport),Is.EqualTo(Vector2.zero));
            Assert.That(RtsCameraPolicy.EdgePanDirection(new Vector2(1601,450),viewport),Is.EqualTo(Vector2.zero));
            Assert.That(RtsCameraPolicy.EdgePanDirection(new Vector2(800,901),viewport),Is.EqualTo(Vector2.zero));
        }

        [Test] public void CursorPolicyRequiresWindowsFocusGameplayAndAuthorization()
        {
            Assert.That(RtsCameraPolicy.SupportsConfinedCursor(true,RuntimePlatform.WindowsEditor),Is.False);
            Assert.That(RtsCameraPolicy.SupportsConfinedCursor(false,RuntimePlatform.WindowsPlayer),Is.True);
            Assert.That(RtsCameraPolicy.ShouldCaptureCursor(false,true,true,true,true),Is.True);
            Assert.That(RtsCameraPolicy.ShouldCaptureCursor(false,true,false,true,true),Is.False);
            Assert.That(RtsCameraPolicy.ShouldCaptureCursor(false,true,true,false,true),Is.False);
            Assert.That(RtsCameraPolicy.ShouldCaptureCursor(false,true,true,true,false),Is.False);
            Assert.That(RtsCameraPolicy.ShouldCaptureCursor(true,true,true,true,true),Is.False);
        }

        [UnitySetUp] public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene();
            scene=SceneManager.CreateScene("Camera usability");
            SceneManager.SetActiveScene(scene);
            new GameObject("Camera test bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;
            battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;
            yield return null;
        }

        [UnityTest] public IEnumerator FocusPointProjectsToPlayableAreaCenter()
        {
            var rig=Object.FindFirstObjectByType<RtsCameraRig>();
            rig.Focus(battle.Towns[1].transform.position);
            yield return new WaitForSecondsRealtime(.35f);
            var projected=Camera.main.WorldToScreenPoint(rig.FocusPoint);
            float playableCenterY=(BattleHud.BottomPixels+Screen.height-BattleHud.TopPixels)*.5f;
            Assert.That(projected.x,Is.EqualTo(Screen.width*.5f).Within(2f));
            Assert.That(projected.y,Is.EqualTo(playableCenterY).Within(2f));
        }

        [UnityTest] public IEnumerator ZoomAtKeepsGroundAnchorUnderCursor()
        {
            var rig=Object.FindFirstObjectByType<RtsCameraRig>();
            var cursor=new Vector2(Screen.width*.61f,(BattleHud.BottomPixels+Screen.height-BattleHud.TopPixels)*.53f);
            var anchor=rig.Ground(cursor);
            rig.ZoomAt(1,cursor);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(Vector3.Distance(anchor,rig.Ground(cursor)),Is.LessThan(.15f));
        }

        [UnityTest] public IEnumerator CancelMotionStopsZoomWhenFocusIsLostAndHelpIsOpen()
        {
            var rig=Object.FindFirstObjectByType<RtsCameraRig>();
            var controller=Object.FindFirstObjectByType<RtsController>();
            var previousMouse=Mouse.current;
            var previousKeyboard=Keyboard.current;
            var mouse=InputSystem.AddDevice<Mouse>("Camera usability mouse");
            var keyboard=InputSystem.AddDevice<Keyboard>("Camera usability keyboard");
            try
            {
                controller.SendMessage("OnApplicationFocus",true);
                rig.ZoomAt(2,new Vector2(Screen.width*.5f,Screen.height*.5f));
                controller.SendMessage("OnApplicationFocus",false);
                Assert.That(rig.TargetZoom,Is.EqualTo(Camera.main.orthographicSize).Within(.001f));
                rig.ZoomAt(2,new Vector2(Screen.width*.5f,Screen.height*.5f));
                controller.HelpVisible=true;
                InputSystem.QueueStateEvent(mouse,new MouseState { position=new Vector2(Screen.width*.5f,Screen.height*.5f) });
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                InputSystem.Update();
                mouse.MakeCurrent();keyboard.MakeCurrent();
                controller.SendMessage("Update");
                Assert.That(rig.TargetZoom,Is.EqualTo(Camera.main.orthographicSize).Within(.001f));
            }
            finally
            {
                controller.HelpVisible=false;
                InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
                if(previousMouse!=null)previousMouse.MakeCurrent();
                if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EscapeClosesHelpAndStopsPendingCameraMotion()
        {
            var rig=Object.FindFirstObjectByType<RtsCameraRig>();var controller=Object.FindFirstObjectByType<RtsController>();
            var previousMouse=Mouse.current;var previousKeyboard=Keyboard.current;
            var previousBackground=InputSystem.settings.backgroundBehavior;
            var previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            bool previousRunBackground=Application.runInBackground;
            Mouse mouse=null;Keyboard keyboard=null;
            try
            {
                Application.runInBackground=true;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                mouse=InputSystem.AddDevice<Mouse>("Camera escape test mouse");keyboard=InputSystem.AddDevice<Keyboard>("Camera escape test keyboard");
                controller.SendMessage("OnApplicationFocus",true);
                controller.HelpVisible=true;rig.Pan(Vector3.right,1);
                var before=rig.FocusPoint;
                InputSystem.QueueStateEvent(mouse,new MouseState { position=new Vector2(Screen.width*.5f,Screen.height*.5f) });
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                mouse.MakeCurrent();keyboard.MakeCurrent();
                Assert.That(keyboard.escapeKey.wasPressedThisFrame,Is.False);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));InputSystem.Update();
                mouse.MakeCurrent();keyboard.MakeCurrent();
                Assert.That(keyboard.escapeKey.wasPressedThisFrame,Is.True,"The synthetic Escape must reach the player input buffer.");
                controller.SendMessage("Update");
                Assert.That(controller.HelpVisible,Is.False);
                rig.SendMessage("LateUpdate");
                Assert.That(Vector3.Distance(before,rig.FocusPoint),Is.LessThan(.01f));
            }
            finally
            {
                controller.HelpVisible=false;if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=previousBackground;InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor;
                Application.runInBackground=previousRunBackground;
                if(previousMouse!=null)previousMouse.MakeCurrent();if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
            }
            yield return null;
        }

        [UnityTest] public IEnumerator FocusLossCancelsHeldCameraInput()
        {
            var rig=Object.FindFirstObjectByType<RtsCameraRig>();var controller=Object.FindFirstObjectByType<RtsController>();
            var previousMouse=Mouse.current;var previousKeyboard=Keyboard.current;
            var previousBackground=InputSystem.settings.backgroundBehavior;
            var previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            bool previousRunBackground=Application.runInBackground;
            Mouse mouse=null;Keyboard keyboard=null;
            try
            {
                Application.runInBackground=true;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                mouse=InputSystem.AddDevice<Mouse>("Camera focus test mouse");keyboard=InputSystem.AddDevice<Keyboard>("Camera focus test keyboard");
                controller.SendMessage("OnApplicationFocus",true);
                var inside=new Vector2(Screen.width*.5f,Screen.height*.5f);
                InputSystem.QueueStateEvent(mouse,new MouseState { position=inside });
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));InputSystem.Update();
                mouse.MakeCurrent();keyboard.MakeCurrent();
                Assert.That(keyboard.rightArrowKey.isPressed,Is.True,"Held camera input must exist before focus is lost.");
                controller.SendMessage("OnApplicationFocus",false);
                var before=rig.FocusPoint;controller.SendMessage("Update");rig.SendMessage("LateUpdate");
                Assert.That(Vector3.Distance(before,rig.FocusPoint),Is.LessThan(.01f));
            }
            finally
            {
                if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=previousBackground;InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor;
                Application.runInBackground=previousRunBackground;
                if(previousMouse!=null)previousMouse.MakeCurrent();if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
            }
            yield return null;
        }

        [UnityTest] public IEnumerator MiddleDragLeavingScreenCancelsWithoutReentryJump()
        {
            var rig=Object.FindFirstObjectByType<RtsCameraRig>();var controller=Object.FindFirstObjectByType<RtsController>();
            var previousMouse=Mouse.current;var previousKeyboard=Keyboard.current;
            var previousBackground=InputSystem.settings.backgroundBehavior;
            var previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            bool previousRunBackground=Application.runInBackground;
            Mouse mouse=null;Keyboard keyboard=null;
            try
            {
                // The batch Editor has no focused Game view. Route the synthetic devices
                // to the actual player buffer, including frame-based button transitions.
                Application.runInBackground=true;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                controller.SendMessage("OnApplicationFocus",true);
                mouse=InputSystem.AddDevice<Mouse>("Camera drag test mouse");keyboard=InputSystem.AddDevice<Keyboard>("Camera drag test keyboard");
                var inside=new Vector2(Screen.width*.5f,(BattleHud.BottomPixels+Screen.height-BattleHud.TopPixels)*.5f);
                InputSystem.QueueStateEvent(mouse,new MouseState { position=inside });InputSystem.Update();
                mouse.MakeCurrent();keyboard.MakeCurrent();
                Assert.That(mouse.middleButton.wasPressedThisFrame,Is.False);
                InputSystem.QueueStateEvent(mouse,new MouseState { position=inside, buttons=4 });
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();mouse.MakeCurrent();keyboard.MakeCurrent();
                Assert.That(mouse.middleButton.wasPressedThisFrame,Is.True,"The simulated press must enter the player input buffer.");
                controller.SendMessage("Update");Assert.That(controller.CameraDragging,Is.True);
                var before=rig.FocusPoint;
                InputSystem.QueueStateEvent(mouse,new MouseState { position=new Vector2(-100,inside.y), buttons=4 });
                InputSystem.Update();mouse.MakeCurrent();keyboard.MakeCurrent();controller.SendMessage("Update");
                Assert.That(controller.CameraDragging,Is.False);
                InputSystem.QueueStateEvent(mouse,new MouseState { position=new Vector2(Screen.width*.8f,inside.y), buttons=4 });
                InputSystem.Update();mouse.MakeCurrent();keyboard.MakeCurrent();controller.SendMessage("Update");
                Assert.That(Vector3.Distance(before,rig.FocusPoint),Is.LessThan(.01f));
            }
            finally
            {
                if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=previousBackground;InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor;
                Application.runInBackground=previousRunBackground;
                if(previousMouse!=null)previousMouse.MakeCurrent();if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
            }
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale=1;SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
