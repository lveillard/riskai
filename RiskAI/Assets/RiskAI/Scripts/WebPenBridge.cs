using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RiskAI
{
    /// <summary>
    /// WebGL-only browser pen adapter. It feeds the existing Input System Pen path, never gameplay directly.
    /// Browser build and physical-pen verification remain external QA; PlayMode coverage uses its injected row source.
    /// </summary>
    public sealed class WebPenBridge : MonoBehaviour
    {
        public delegate bool PenSampleSource(float[] destination);
        const int SampleLength=8;
        const int StateKind=0,CancelKind=1;
        const int TipButton=1,BarrelButton=2;
        readonly float[] sample=new float[SampleLength];
        PenSampleSource testSource;
        Pen pen;
        Vector2 previousPosition;
        bool hasPreviousPosition;
        int processedFrame=-1;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int RiskAI_ReadPenSample([Out, MarshalAs(UnmanagedType.LPArray, SizeConst=SampleLength)] float[] destination);
#endif

        public Pen VirtualPenForTests => pen;
        public void SetSampleSourceForTests(PenSampleSource source) => testSource=source;
        public bool ProcessSampleForTests() => ProcessSample();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateForWebPlayer()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var existing=FindFirstObjectByType<WebPenBridge>();if(existing)return;
            var host=new GameObject("Web pen bridge");DontDestroyOnLoad(host);host.AddComponent<WebPenBridge>();
#endif
        }

        void OnEnable() => InputSystem.onBeforeUpdate+=BeforeInputUpdate;
        void OnDisable()
        {
            InputSystem.onBeforeUpdate-=BeforeInputUpdate;
            CancelPen();
        }
        void OnDestroy() => CancelPen();

        void BeforeInputUpdate()
        {
            if(InputState.currentUpdateType!=InputUpdateType.Dynamic||processedFrame==Time.frameCount)return;
            processedFrame=Time.frameCount;ProcessSample();
        }

        bool ProcessSample()
        {
            if(!ReadSample(sample))return false;
            // A malformed browser row while a tip is held must not leave the existing virtual device pressed.
            if(!Valid(sample)){if(pen!=null)CancelPen();return false;}
            int kind=Mathf.RoundToInt(sample[0]);
            if(kind==CancelKind){CancelPen();return true;}
            EnsurePen();
            float x=sample[1]*Screen.width,y=(1f-sample[2])*Screen.height;
            int buttons=Mathf.RoundToInt(sample[3]);
            var state=new PenState
            {
                position=new Vector2(x,y),delta=hasPreviousPosition?new Vector2(x,y)-previousPosition:Vector2.zero,pressure=Mathf.Clamp01(sample[4]),
                // DOM reports degrees; Input System Pen tilt is normalized to [-1, 1].
                tilt=new Vector2(Mathf.Clamp(sample[5]/90f,-1,1),Mathf.Clamp(sample[6]/90f,-1,1))
            };
            state=state.WithButton(PenButton.Tip,(buttons&TipButton)!=0);
            state=state.WithButton(PenButton.BarrelFirst,(buttons&BarrelButton)!=0);
            // DOM time origins do not necessarily equal Unity's input runtime origin. Validate it, but let Input System timestamp this event.
            InputSystem.QueueStateEvent(pen,state);
            previousPosition=state.position;hasPreviousPosition=true;
            return true;
        }

        bool ReadSample(float[] destination)
        {
            if(testSource!=null)return testSource(destination);
#if UNITY_WEBGL && !UNITY_EDITOR
            return RiskAI_ReadPenSample(destination)!=0;
#else
            return false;
#endif
        }

        static bool Valid(float[] data)
        {
            if(data==null||data.Length<SampleLength)return false;
            for(int i=0;i<SampleLength;i++)if(float.IsNaN(data[i])||float.IsInfinity(data[i]))return false;
            int kind=Mathf.RoundToInt(data[0]),buttons=Mathf.RoundToInt(data[3]);
            return Mathf.Abs(data[0]-kind)<.001f&&(kind==StateKind||kind==CancelKind)&&
                   data[1]>=0&&data[1]<=1&&data[2]>=0&&data[2]<=1&&
                   Mathf.Abs(data[3]-buttons)<.001f&&buttons>=0&&buttons<=63&&data[4]>=0&&data[4]<=1;
        }

        void EnsurePen()
        {
            if(pen!=null)return;
            pen=InputSystem.AddDevice<Pen>("RiskAI Web Pen");pen.MakeCurrent();
        }

        void CancelPen()
        {
            var controller=FindFirstObjectByType<RtsController>();
            if(controller)controller.CancelDirectPointerInput();
            if(pen!=null&&pen.added)InputSystem.RemoveDevice(pen);
            pen=null;hasPreviousPosition=false;processedFrame=Time.frameCount;
        }
    }
}
