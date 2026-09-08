using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    /// <summary>Exercises the real Input System devices through the same controller update path used at runtime.</summary>
    public sealed class DirectPointerInputTests
    {
        Scene scene, previous;
        RtsController controller;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
#endif

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Batchmode has no focused Game view. Exercise real frame edges rather
            // than the Editor's independently reset pointer buffers.
            previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            scene=SceneManager.CreateScene("Direct pointer input");SceneManager.SetActiveScene(scene);
            new GameObject("Direct pointer bootstrap").AddComponent<RiskBootstrap>();
            BattleSession.Current.AiEnabled=false;controller=Object.FindFirstObjectByType<RtsController>();controller.enabled=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnhancedTouchDragUsesSharedAreaSelectionPath()
        {
            Touchscreen touch=null;
            try
            {
                touch=InputSystem.AddDevice<Touchscreen>("Direct pointer touch");
                controller.SendMessage("OnApplicationFocus",true);
                var start=UiViewport.WorldRect.center;var end=start+new Vector2(30,0);
                Pump(touch,new TouchState { touchId=17,position=start,phase=UnityEngine.InputSystem.TouchPhase.Began });
                Pump(touch,new TouchState { touchId=17,position=end,delta=end-start,phase=UnityEngine.InputSystem.TouchPhase.Moved });
                Assert.That(controller.Dragging,Is.True,"EnhancedTouch drag must enter the controller's shared area-selection path.");
                Pump(touch,new TouchState { touchId=17,position=end,phase=UnityEngine.InputSystem.TouchPhase.Ended });
                Assert.That(controller.Dragging,Is.False);
            }
            finally { if(touch!=null)InputSystem.RemoveDevice(touch); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PausedTwoFingerTouchPansAndPinchesWithoutCommands()
        {
            Touchscreen touch=null;
            try
            {
                touch=InputSystem.AddDevice<Touchscreen>("Paused two finger touch");
                controller.SendMessage("OnApplicationFocus",true);
                controller.SelectOnly(BattleSession.Current.Units.Find(unit=>unit&&unit.Team==0));
                var commandsApplied=BattleSession.Current.Commands.AppliedCount;
                BattleSession.Current.TogglePause();
                var pausedTime=BattleSession.Current.BattleTime;
                controller.SendMessage("Update");
                var center=UiViewport.WorldRect.center;
                var beforeFocus=controller.CameraRig.FocusPoint;var beforeZoom=controller.CameraRig.TargetZoom;
                Pump(touch,
                    new TouchState { touchId=101,position=center+new Vector2(-50,0),phase=UnityEngine.InputSystem.TouchPhase.Began },
                    new TouchState { touchId=102,position=center+new Vector2(50,0),phase=UnityEngine.InputSystem.TouchPhase.Began });
                Pump(touch,
                    new TouchState { touchId=101,position=center+new Vector2(-70,20),delta=new Vector2(-20,20),phase=UnityEngine.InputSystem.TouchPhase.Moved },
                    new TouchState { touchId=102,position=center+new Vector2(70,20),delta=new Vector2(20,20),phase=UnityEngine.InputSystem.TouchPhase.Moved });
                Assert.That(Vector3.Distance(beforeFocus,controller.CameraRig.FocusPoint),Is.GreaterThan(.01f),"A paused two-finger drag must pan the camera.");
                Assert.That(controller.CameraRig.TargetZoom,Is.Not.EqualTo(beforeZoom),"A paused two-finger spread must pinch-zoom the camera.");
                Pump(touch,
                    new TouchState { touchId=101,position=center+new Vector2(-70,20),phase=UnityEngine.InputSystem.TouchPhase.Ended },
                    new TouchState { touchId=102,position=center+new Vector2(70,20),phase=UnityEngine.InputSystem.TouchPhase.Ended });
                Assert.That(controller.Dragging,Is.False);
                Assert.That(BattleSession.Current.Commands.PendingCount,Is.Zero,"Touch gestures in pause must not queue commands.");
                Assert.That(BattleSession.Current.Commands.AppliedCount,Is.EqualTo(commandsApplied));
                Assert.That(BattleSession.Current.BattleTime,Is.EqualTo(pausedTime),"A paused gesture must not advance the battle clock.");
                yield return null;
                Assert.That(BattleSession.Current.BattleTime,Is.EqualTo(pausedTime));
            }
            finally { if(touch!=null)InputSystem.RemoveDevice(touch); }
        }

        [UnityTest]
        public IEnumerator PenTipDragUsesSharedAreaSelectionPath()
        {
            Pen pen=null;
            try
            {
                pen=InputSystem.AddDevice<Pen>("Direct pointer pen");pen.MakeCurrent();controller.SendMessage("OnApplicationFocus",true);
                var start=UiViewport.WorldRect.center;var end=start+new Vector2(30,0);
                Pump(pen,new PenState { position=start }.WithButton(PenButton.Tip));
                Pump(pen,new PenState { position=end,delta=end-start }.WithButton(PenButton.Tip));
                Assert.That(controller.Dragging,Is.True,"Pen tip input must use the same area-selection path as a touch.");
                Pump(pen,new PenState { position=end });
                Assert.That(controller.Dragging,Is.False);
            }
            finally { if(pen!=null)InputSystem.RemoveDevice(pen); }
            yield return null;
        }

        void Pump(InputDevice device,params TouchState[] states)
        {
            foreach(var state in states)InputSystem.QueueStateEvent(device,state);
            InputSystem.Update();controller.SendMessage("Update");
        }

        [UnityTest]
        public IEnumerator BarrelPressCancelsPenMarqueeBeforeContext()
        {
            Pen pen=null;
            try
            {
                pen=InputSystem.AddDevice<Pen>("Direct pointer barrel");pen.MakeCurrent();controller.SendMessage("OnApplicationFocus",true);
                var start=UiViewport.WorldRect.center;var end=start+new Vector2(30,0);
                Pump(pen,new PenState { position=start }.WithButton(PenButton.Tip));yield return null;
                Pump(pen,new PenState { position=end,delta=end-start }.WithButton(PenButton.Tip));yield return null;
                Assert.That(controller.Dragging,Is.True);
                Pump(pen,new PenState { position=end }.WithButton(PenButton.Tip).WithButton(PenButton.BarrelFirst));yield return null;
                Assert.That(controller.Dragging,Is.False,"A barrel context action must clear the pending pen marquee.");
                Pump(pen,new PenState { position=end });
            }
            finally { if(pen!=null)InputSystem.RemoveDevice(pen); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiOriginTouchAndFocusCancelledTouchNeverResumeWorldGesture()
        {
            Touchscreen touch=null;
            try
            {
                touch=InputSystem.AddDevice<Touchscreen>("Direct pointer quarantine");controller.SendMessage("OnApplicationFocus",true);
                var uiPoint=Vector2.zero;
                Pump(touch,new TouchState { touchId=61,position=uiPoint,phase=UnityEngine.InputSystem.TouchPhase.Began });
                Pump(touch,new TouchState { touchId=61,position=uiPoint+Vector2.right*60,phase=UnityEngine.InputSystem.TouchPhase.Moved });
                Assert.That(controller.Dragging,Is.False,"A contact beginning outside the world viewport must never own a marquee.");
                Pump(touch,new TouchState { touchId=61,position=uiPoint,phase=UnityEngine.InputSystem.TouchPhase.Ended });

                var start=UiViewport.WorldRect.center;var end=start+new Vector2(30,0);
                Pump(touch,new TouchState { touchId=62,position=start,phase=UnityEngine.InputSystem.TouchPhase.Began });
                Pump(touch,new TouchState { touchId=62,position=end,delta=end-start,phase=UnityEngine.InputSystem.TouchPhase.Moved });
                Assert.That(controller.Dragging,Is.True);
                controller.SendMessage("OnApplicationFocus",false);controller.SendMessage("OnApplicationFocus",true);
                Pump(touch,new TouchState { touchId=62,position=end+Vector2.right*30,phase=UnityEngine.InputSystem.TouchPhase.Moved });
                Assert.That(controller.Dragging,Is.False,"A focus-cancelled held touch must remain quarantined until release.");
                Pump(touch,new TouchState { touchId=62,position=end,phase=UnityEngine.InputSystem.TouchPhase.Ended });
            }
            finally { if(touch!=null)InputSystem.RemoveDevice(touch); }
            yield return null;
        }

        void Pump(InputDevice device,PenState state)
        {
            InputSystem.QueueStateEvent(device,state);InputSystem.Update();controller.SendMessage("Update");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=1;BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
            SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.settings.backgroundBehavior=previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
#endif
        }
    }
}
