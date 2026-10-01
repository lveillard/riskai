using UnityEngine;

namespace RiskAI
{
    /// <summary>Pure camera-input rules shared by the controller and policy tests.</summary>
    public static class RtsCameraPolicy
    {
        public const float EdgeBandPixels=20f;
        public const float WheelZoomExponent=.24f;
        public const float MaximumWheelStepsPerFrame=4f;
        public const float WebPixelUnitsPerStep=100f;
        public const float WebPinchZoomExponent=.01f;
        public const float WebLineUnitsPerStep=3f;

        /// <summary>
        /// Converts pinch and discrete wheel deltas to the shared zoom action.
        /// DOM positive Y means zoom out; ordinary touchpad scrolling only pans.
        /// </summary>
        public static float NormalizeWebWheelDeltas(float pinchPixels,float wheelPixels,float lines,float pages)
        {
            if(float.IsNaN(pinchPixels)||float.IsInfinity(pinchPixels))pinchPixels=0;
            if(float.IsNaN(wheelPixels)||float.IsInfinity(wheelPixels))wheelPixels=0;
            if(float.IsNaN(lines)||float.IsInfinity(lines))lines=0;
            if(float.IsNaN(pages)||float.IsInfinity(pages))pages=0;
            float steps=-(pinchPixels*WebPinchZoomExponent/WheelZoomExponent+wheelPixels/WebPixelUnitsPerStep+lines/WebLineUnitsPerStep+pages);
            return Mathf.Clamp(steps,-MaximumWheelStepsPerFrame,MaximumWheelStepsPerFrame);
        }

        public static CameraScrollInput BrowserCameraScroll(float[] sample,Vector2 viewport)
        {
            float steps=NormalizeWebWheelDeltas(sample[0],sample[1],sample[2],sample[3]);
            var drag=Vector2.Scale(new Vector2(sample[4],sample[5]),viewport);
            var position=Vector2.Scale(new Vector2(sample[6],sample[7]),viewport);
            return new CameraScrollInput(steps,drag,position);
        }

        public static float WheelZoomMultiplier(float steps) =>
            Mathf.Exp(-Mathf.Clamp(steps,-MaximumWheelStepsPerFrame,MaximumWheelStepsPerFrame)*WheelZoomExponent);

        /// <summary>Equal zoom ratios settle at the same rate at every camera height.</summary>
        public static float SmoothZoom(float current,float target,ref float logarithmicVelocity,float deltaTime)
        {
            if(deltaTime<=0)return current;
            return Mathf.Exp(Mathf.SmoothDamp(Mathf.Log(Mathf.Max(.001f,current)),
                Mathf.Log(Mathf.Max(.001f,target)),ref logarithmicVelocity,.10f,Mathf.Infinity,deltaTime));
        }

        /// <summary>
        /// Returns a unit screen-edge direction. Screen Y is mapped to camera Z:
        /// bottom is -Z and top is +Z. Points outside the viewport do not pan.
        /// </summary>
        public static Vector2 EdgePanDirection(Vector2 pointer,Vector2 viewport,float edgePixels=EdgeBandPixels)
        {
            if(viewport.x<=0||viewport.y<=0||pointer.x<0||pointer.y<0||pointer.x>viewport.x||pointer.y>viewport.y)return Vector2.zero;
            float band=Mathf.Max(1,edgePixels);
            Vector2 direction=Vector2.zero;
            if(pointer.x<=band)direction.x=-1;
            else if(pointer.x>=viewport.x-band)direction.x=1;
            if(pointer.y<=band)direction.y=-1;
            else if(pointer.y>=viewport.y-band)direction.y=1;
            return direction.sqrMagnitude>.001f?direction.normalized:Vector2.zero;
        }

        public static Vector2 EdgePanDirection(Vector2 pointer,float viewportWidth,float viewportHeight,float edgePixels=EdgeBandPixels)
        {
            return EdgePanDirection(pointer,new Vector2(viewportWidth,viewportHeight),edgePixels);
        }

        public static bool SupportsConfinedCursor(bool isEditor,RuntimePlatform platform)
        {
            return !isEditor&&platform==RuntimePlatform.WindowsPlayer;
        }

        public static bool ShouldCaptureCursor(bool isEditor,bool windowsStandalone,bool focused,bool gameplayActive,bool authorized)
        {
            return windowsStandalone&&!isEditor&&focused&&gameplayActive&&authorized;
        }

        public static bool ShouldCaptureCursor(bool isEditor,RuntimePlatform platform,bool focused,bool gameplayActive,bool authorized)
        {
            return ShouldCaptureCursor(isEditor,SupportsConfinedCursor(isEditor,platform),focused,gameplayActive,authorized);
        }
    }
}
