using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    /// <summary>Verifies the browser adapter only supplies Input System pen events; it never orders the game itself.</summary>
    public sealed class WebPenBridgeTests
    {
        Scene scene, previous;
        RtsController controller;
        WebPenBridge bridge;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;
        float previousTimeScale;
        InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
#endif
        float[] nextSample;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            nextSample=null;
            previousBackground=InputSystem.settings.backgroundBehavior;
            // Batchmode has no focused Game view. Keep synthetic devices enabled
            // across rendered frames; production retains normal focus cancellation.
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;
            previousMode=BattleSession.ModeForNewMatch;previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;previousTimeScale=Time.timeScale;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=31019;
            scene=SceneManager.CreateScene("Web pen bridge");SceneManager.SetActiveScene(scene);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<WebPenBridge>(),Is.Null,
                "Desktop and Editor players must not auto-create the WebGL bridge.");
            new GameObject("Web pen bootstrap").AddComponent<RiskBootstrap>();
            BattleSession.Current.AiEnabled=false;controller=UnityEngine.Object.FindFirstObjectByType<RtsController>();controller.enabled=false;
            bridge=new GameObject("Test web pen bridge").AddComponent<WebPenBridge>();
            bridge.SetSampleSourceForTests(ReadNextSample);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StateEdgesSurviveConsecutiveDynamicFrames()
        {
            var rows=new Queue<float[]>();
            rows.Enqueue(new[] { 0f,.5f,.5f,1f,.4f,0f,0f,10f });
            rows.Enqueue(new[] { 0f,.5f,.5f,0f,0f,0f,0f,11f });
            bridge.SetSampleSourceForTests(destination=>
            {
                if(rows.Count==0)return false;
                Array.Copy(rows.Dequeue(),destination,8);return true;
            });
            yield return null;
            var pen=bridge.VirtualPenForTests;Assert.That(pen,Is.Not.Null);Assert.That(pen.tip.isPressed,Is.True);
            InputSystem.Update();InputSystem.Update();
            Assert.That(rows.Count,Is.EqualTo(1),"Repeated Dynamic updates in one rendered frame cannot consume the release before the controller sees the press.");
            yield return null;
            Assert.That(pen.tip.isPressed,Is.False,"A later browser row must release the same virtual pen rather than losing its edge.");
            Assert.That(pen.added,Is.True);
        }

        [UnityTest]
        public IEnumerator MapsTopOriginBrowserCoordinatesAndDomButtons()
        {
            SetState(.25f,.75f,3,.6f,45,-30);
            Assert.That(bridge.ProcessSampleForTests(),Is.True);InputSystem.Update();yield return null;
            var pen=bridge.VirtualPenForTests;
            Assert.That(pen.position.ReadValue().x,Is.EqualTo(Screen.width*.25f).Within(.01f));
            Assert.That(pen.position.ReadValue().y,Is.EqualTo(Screen.height*.25f).Within(.01f),"Browser y=0 is top, while Input System screen coordinates are bottom-origin.");
            Assert.That(controller.Pointer,Is.EqualTo(pen.position.ReadValue()),"Picking and presentation must use the current pen position, not a stale mouse coordinate.");
            Assert.That(pen.tip.isPressed,Is.True);Assert.That(pen.firstBarrelButton.isPressed,Is.True);
            Assert.That(pen.pressure.ReadValue(),Is.EqualTo(.6f).Within(.001f));
            Assert.That(pen.tilt.ReadValue().x,Is.EqualTo(.5f).Within(.001f));
            Assert.That(pen.tilt.ReadValue().y,Is.EqualTo(-1f/3f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator CancelDiscardsPenMarqueeBeforeRemovingItsVirtualDevice()
        {
            var selected=BattleSession.Current.Towns[0];controller.SelectTown(selected);
            var start=UiViewport.WorldRect.center;var end=start+Vector2.right*42f;
            SetScreenState(start,1);Assert.That(bridge.ProcessSampleForTests(),Is.True);InputSystem.Update();controller.SendMessage("Update");yield return null;
            SetScreenState(end,1);Assert.That(bridge.ProcessSampleForTests(),Is.True);InputSystem.Update();controller.SendMessage("Update");yield return null;
            Assert.That(controller.Dragging,Is.True,"The virtual pen must enter the same marquee path as a native pen before cancellation.");

            long pendingBefore=BattleSession.Current.Commands.PendingCount;
            nextSample=new[] { 1f,.5f,.5f,0f,0f,0f,0f,10f };
            Assert.That(bridge.ProcessSampleForTests(),Is.True);InputSystem.Update();controller.SendMessage("Update");yield return null;
            Assert.That(bridge.VirtualPenForTests,Is.Null,"Cancellation removes only the bridge-owned device.");
            Assert.That(controller.Dragging,Is.False,"Cancellation must happen before a synthetic release can become a tap.");
            Assert.That(BattleSession.Current.Commands.PendingCount,Is.EqualTo(pendingBefore),"A cancelled pen gesture must not issue a primary or context command.");
            nextSample=null;yield return new WaitForSecondsRealtime(.3f);controller.SendMessage("Update");
            Assert.That(controller.SelectedTown,Is.SameAs(selected),"No delayed primary tap or area selection may escape after cancellation.");
        }

        [UnityTest]
        public IEnumerator TipHeldAcrossHelpClosureWaitsForReleaseAndFreshPress()
        {
            var selected=BattleSession.Current.Towns[0];controller.SelectTown(selected);
            var start=UiViewport.WorldRect.center;var end=start+Vector2.right*42f;
            controller.HelpVisible=true;
            SetScreenState(start,1);bridge.ProcessSampleForTests();InputSystem.Update();controller.SendMessage("Update");
            yield return null;
            controller.HelpVisible=false;controller.SendMessage("Update");
            SetScreenState(end,1);bridge.ProcessSampleForTests();InputSystem.Update();controller.SendMessage("Update");
            Assert.That(controller.Dragging,Is.False,"A held tip from a rejected frame must not begin a world gesture.");
            SetScreenState(end,0);bridge.ProcessSampleForTests();InputSystem.Update();controller.SendMessage("Update");
            yield return new WaitForSecondsRealtime(.3f);controller.SendMessage("Update");
            Assert.That(controller.SelectedTown,Is.SameAs(selected),"Releasing the rejected stroke must not select the world.");
            SetScreenState(start,1);bridge.ProcessSampleForTests();InputSystem.Update();controller.SendMessage("Update");
            SetScreenState(end,1);bridge.ProcessSampleForTests();InputSystem.Update();controller.SendMessage("Update");
            Assert.That(controller.Dragging,Is.True,"A fresh accepted press must still start normal area selection.");
        }

        [UnityTest]
        public IEnumerator BarrelHeldAcrossHelpClosureDoesNotBecomeAContextCommand()
        {
            controller.SelectOnly(BattleSession.Current.Units.Find(unit=>unit&&unit.Team==0));
            controller.HelpVisible=true;
            SetState(.5f,.5f,2,0,0,0);bridge.ProcessSampleForTests();InputSystem.Update();controller.SendMessage("Update");
            yield return null;
            controller.HelpVisible=false;controller.ArmAttack();controller.SendMessage("Update");
            Assert.That(controller.AttackCursor,Is.True,"An old held barrel must not generate ContextAction on resuming input.");
            yield return null; // Let the retained modal detach before testing a world press.
            SetState(.5f,.5f,0,0,0,0);bridge.ProcessSampleForTests();InputSystem.Update();controller.SendMessage("Update");
            SetState(.5f,.5f,2,0,0,0);bridge.ProcessSampleForTests();InputSystem.Update();controller.SendMessage("Update");
            Assert.That(controller.AttackCursor,Is.False,"A fresh barrel press still invokes the shared context path and cancels the armed cursor.");
        }

        [UnityTest]
        public IEnumerator HoveringPenDoesNotScrollFromTheLastMouseEdge()
        {
            var mouse=InputSystem.AddDevice<Mouse>("Stale mouse position fixture");
            try
            {
                controller.CameraRig.SetHome(Vector3.zero);
                InputSystem.QueueStateEvent(mouse,new MouseState { position=new Vector2(0,Screen.height*.5f) });
                InputSystem.Update();
                SetState(0,.5f,0,0,0,0);bridge.ProcessSampleForTests();InputSystem.Update();
                var before=controller.CameraRig.FocusPoint;
                controller.SendMessage("Update");yield return new WaitForSecondsRealtime(.15f);
                Assert.That(Vector3.Distance(before,controller.CameraRig.FocusPoint),Is.LessThan(.001f),
                    "Direct pointer devices pan explicitly; a hovering pen cannot reactivate the mouse edge scroll.");
            }
            finally { if(mouse.added)InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator MalformedRowReleasesOnlyTheBridgeDevice()
        {
            var other=InputSystem.AddDevice<Pen>("Unrelated test pen");
            try
            {
                SetState(.5f,.5f,1,.4f,0,0);bridge.ProcessSampleForTests();InputSystem.Update();
                var owned=bridge.VirtualPenForTests;Assert.That(owned.tip.isPressed,Is.True);
                nextSample=new[] { 0f,float.NaN,.5f,1f,.4f,0f,0f,10f };
                Assert.That(bridge.ProcessSampleForTests(),Is.False);
                Assert.That(owned.added,Is.False);Assert.That(other.added,Is.True);
                Assert.That(bridge.VirtualPenForTests,Is.Null);
            }
            finally { if(other.added)InputSystem.RemoveDevice(other); }
            yield return null;
        }

        bool ReadNextSample(float[] destination)
        {
            if(nextSample==null)return false;
            Array.Copy(nextSample,destination,8);return true;
        }

        void SetState(float x,float y,int buttons,float pressure,float tiltX,float tiltY)
        {
            nextSample=new[] { 0f,x,y,buttons,pressure,tiltX,tiltY,10f };
        }

        void SetScreenState(Vector2 point,int buttons)
        {
            SetState(point.x/Screen.width,1f-point.y/Screen.height,buttons,.5f,0,0);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=previousTimeScale;BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.ModeForNewMatch=previousMode;BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;
            SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.settings.backgroundBehavior=previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
#endif
        }
    }
}
