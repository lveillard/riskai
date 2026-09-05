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
