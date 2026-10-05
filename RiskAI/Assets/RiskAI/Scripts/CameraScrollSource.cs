using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RiskAI
{
    /// <summary>Platform transports feed the same interpreter before Input System can sum scroll events.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraScrollSource : MonoBehaviour
    {
        readonly CameraScrollInterpreter interpreter=new CameraScrollInterpreter();
#if UNITY_WEBGL && !UNITY_EDITOR
        const int SampleLength=9;
        readonly float[] row=new float[SampleLength];
        [DllImport("__Internal")]
        static extern int RiskAI_ReadWheelDeltas([Out,MarshalAs(UnmanagedType.LPArray,SizeConst=SampleLength)] float[] destination);
#else
        static int nativeReaders;
        static bool previousMerging;
        void OnEnable()
        {
            // Otherwise FastMouse can merge many fine scroll events into one
            // apparent wheel notch before onEvent runs. Restore on the last detach.
            if(nativeReaders++==0)
            {
                previousMerging=InputSystem.settings.disableRedundantEventsMerging;
                InputSystem.settings.disableRedundantEventsMerging=true;
            }
            InputSystem.onEvent+=OnInputEvent;
        }
        void OnDisable()
        {
            InputSystem.onEvent-=OnInputEvent;
            if(--nativeReaders==0)InputSystem.settings.disableRedundantEventsMerging=previousMerging;
            Reset();
        }
        void OnInputEvent(InputEventPtr evt,InputDevice device)
        {
            if(!(device is Mouse mouse)||(!evt.IsA<StateEvent>()&&!evt.IsA<DeltaStateEvent>()))return;
            if(!mouse.scroll.ReadValueFromEvent(evt,out Vector2 delta)||delta==Vector2.zero)return;
            Vector2 position=mouse.position.ReadValueFromEvent(evt,out Vector2 value)?value:mouse.position.ReadValue();
            var keyboard=Keyboard.current;
            bool control=keyboard!=null&&(keyboard.leftCtrlKey.isPressed||keyboard.rightCtrlKey.isPressed);
            double now=InputState.currentTime;
            interpreter.Push(CameraScrollSample.FromUnity(delta,position,control,PlatformPresentation.PixelDensity,evt.time,Screen.height),now);
            // Leave the original event untouched for UI Toolkit scrolling.
        }
#endif
        public CameraScrollInput Consume()
        {
            double now=InputState.currentTime;
#if UNITY_WEBGL && !UNITY_EDITOR
            while(RiskAI_ReadWheelDeltas(row)!=0)
                interpreter.Push(CameraScrollSample.FromBrowser(row,new Vector2(Screen.width,Screen.height),now),now);
#endif
            return interpreter.Consume(now);
        }
        public void Reset() => interpreter.Reset();
        void OnApplicationFocus(bool focused) { if(!focused)Reset(); }
        void OnApplicationPause(bool paused) { if(paused)Reset(); }
    }
}
