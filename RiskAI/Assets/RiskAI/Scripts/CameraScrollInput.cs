using UnityEngine;

namespace RiskAI
{
    public enum CameraScrollUnit { Pixels, Lines, Pages, Notches, Scale, Reset }

    /// <summary>Transport data only. Position is in render pixels; PixelScale converts logical pixels to render pixels.</summary>
    public readonly struct CameraScrollSample
    {
        public readonly Vector2 Delta,Position,PixelScale;
        public readonly CameraScrollUnit Unit;
        public readonly bool Control;
        public readonly double Time;
        public readonly float PagePixels;
        public CameraScrollSample(Vector2 delta,CameraScrollUnit unit,bool control,Vector2 position,Vector2 pixelScale,double time,float pagePixels)
        { Delta=delta;Unit=unit;Control=control;Position=position;PixelScale=pixelScale;Time=time;PagePixels=pagePixels; }

        public static CameraScrollSample FromUnity(Vector2 scroll,Vector2 position,bool control,float density,double time,float height) =>
            new CameraScrollSample(new Vector2(scroll.x,-scroll.y),CameraScrollUnit.Notches,control,position,Vector2.one*density,time,height/density);

        // Browser row: raw X/Y, unit, Ctrl, normalized pointer X/Y, inverse CSS width/height, age in ms.
        public static CameraScrollSample FromBrowser(float[] row,Vector2 viewport,double now) =>
            new CameraScrollSample(new Vector2(row[0],row[1]),(CameraScrollUnit)(int)row[2],row[3]!=0,
                Vector2.Scale(new Vector2(row[4],row[5]),viewport),Vector2.Scale(new Vector2(row[6],row[7]),viewport),
                now-row[8]/1000d,row[7]>0?1/row[7]:0);
    }

    public readonly struct CameraScrollInput
    {
        public readonly float ZoomSteps;
        public readonly Vector2 PanDelta,Position;
        public CameraScrollInput(float zoomSteps,Vector2 panDelta,Vector2 position)
        { ZoomSteps=zoomSteps;PanDelta=panDelta;Position=position; }
    }

    /// <summary>The only scroll/pinch interpretation, shared by WebGL and native Input System events.</summary>
    public sealed class CameraScrollInterpreter
    {
        public const float PixelsPerNotch=100,LinesPerNotch=3,PinchZoomExponent=.01f;
        public const double GestureGap=.25,MaximumSampleAge=.16;
        double lastScroll=double.NegativeInfinity,lastSample=double.NegativeInfinity;
        bool panning;
        float zoom;
        Vector2 pan,position;

        public void Reset() { ClearPending();lastScroll=double.NegativeInfinity;panning=false; }
        void ClearPending() { zoom=0;pan=Vector2.zero;position=Vector2.zero;lastSample=double.NegativeInfinity; }
        static bool Finite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector2 value) => Finite(value.x)&&Finite(value.y);
        static bool Multiple(float value,float step) => Mathf.Abs(value/step-Mathf.Round(value/step))<.00001f&&value>=step-.001f;
        static bool Discrete(Vector2 pixels) => pixels.x==0&&(Multiple(Mathf.Abs(pixels.y),100)||Multiple(Mathf.Abs(pixels.y),120));

        public void Push(CameraScrollSample sample,double now)
        {
            if(sample.Unit==CameraScrollUnit.Reset) { Reset();return; }
            if(!Finite(sample.Delta)||!Finite(sample.Position)||!Finite(sample.PixelScale)||!Finite(sample.PagePixels)||
                double.IsNaN(sample.Time)||double.IsInfinity(sample.Time)||now-sample.Time>MaximumSampleAge||sample.Time>now+.001)return;
            if(sample.PixelScale.x<=0||sample.PixelScale.y<=0||sample.PagePixels<=0)return;
            if(sample.Time-lastSample>MaximumSampleAge||sample.Position!=position)ClearPending();
            position=sample.Position;lastSample=sample.Time;
            if(sample.Unit==CameraScrollUnit.Scale)
            {
                if(sample.Delta.y>0)zoom+=Mathf.Log(sample.Delta.y)/RtsCameraPolicy.WheelZoomExponent;
                return;
            }
            Vector2 pixels=sample.Delta;
            switch(sample.Unit)
            {
                case CameraScrollUnit.Lines: pixels*=16;break;
                case CameraScrollUnit.Pages: pixels*=sample.PagePixels;break;
                case CameraScrollUnit.Notches: pixels*=PixelsPerNotch;break;
                case CameraScrollUnit.Pixels: break;
                default: return;
            }
            if(sample.Control) { zoom-=pixels.y*PinchZoomExponent/RtsCameraPolicy.WheelZoomExponent;return; }
            if(pixels==Vector2.zero)return;
            if(sample.Time-lastScroll>GestureGap)panning=false;
            bool discrete=sample.Unit==CameraScrollUnit.Lines||sample.Unit==CameraScrollUnit.Pages||Discrete(pixels);
            panning=panning||!discrete;lastScroll=sample.Time;
            if(panning)pan+=Vector2.Scale(new Vector2(-pixels.x,pixels.y),sample.PixelScale);
            else zoom-=sample.Unit==CameraScrollUnit.Lines?sample.Delta.y/LinesPerNotch:
                sample.Unit==CameraScrollUnit.Pages?sample.Delta.y:pixels.y/PixelsPerNotch;
        }

        public CameraScrollInput Consume(double now)
        {
            if(now-lastSample>MaximumSampleAge)ClearPending();
            var result=new CameraScrollInput(Mathf.Clamp(zoom,-RtsCameraPolicy.MaximumWheelStepsPerFrame,RtsCameraPolicy.MaximumWheelStepsPerFrame),pan,position);
            ClearPending();return result;
        }
    }
}
