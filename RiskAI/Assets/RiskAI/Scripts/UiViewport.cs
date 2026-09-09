using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskAI
{
    /// <summary>Screen-space presentation only. All maps, cameras and pointer adapters share this viewport.</summary>
    public static class UiViewport
    {
        public readonly struct Layout
        {
            public readonly Rect Safe, World;
            public readonly float Scale;
            public Layout(Rect safe, Rect world, float scale) { Safe=safe; World=world; Scale=scale; }
        }

        static float headerOverride=-1,footerOverride=-1,cameraHeaderOverride=-1,cameraFooterOverride=-1;
        public static float Scale => PlatformPresentation.PixelDensity;
        public static Rect SafeRect => PlatformPresentation.SafeArea;
        public static float LogicalWidth => SafeRect.width/Scale;
        public static float LogicalHeight => SafeRect.height/Scale;
        public static bool IsCompact => LogicalWidth<1100;
        public static bool IsPortrait => LogicalHeight>LogicalWidth;
        public static bool IsTouchLayout => PlatformPresentation.TouchCapable || IsCompact;
        public static Rect WorldRect => Calculate(new Vector2(Screen.width,Screen.height),SafeRect,Scale,
            headerOverride>=0?headerOverride:IsPortrait?64:48,footerOverride>=0?footerOverride:IsPortrait?224:208).World;
        public static Rect CameraWorldRect => Calculate(new Vector2(Screen.width,Screen.height),SafeRect,Scale,
            cameraHeaderOverride>=0?cameraHeaderOverride:IsPortrait?64:48,cameraFooterOverride>=0?cameraFooterOverride:IsPortrait?224:208).World;
        public static float TopPixels => Screen.height-WorldRect.yMax;
        public static float BottomPixels => WorldRect.yMin;
        public static bool ContainsWorld(Vector2 screen) => WorldRect.Contains(screen);

        public static void SetHudHeights(float headerLogical,float footerLogical)
        {
            if(!float.IsNaN(headerLogical)&&!float.IsInfinity(headerLogical))headerOverride=Mathf.Max(0,headerLogical);
            if(!float.IsNaN(footerLogical)&&!float.IsInfinity(footerLogical))footerOverride=Mathf.Max(0,footerLogical);
        }
        public static void SetCameraHudHeights(float headerLogical,float footerLogical)
        {
            if(!float.IsNaN(headerLogical)&&!float.IsInfinity(headerLogical))cameraHeaderOverride=Mathf.Max(0,headerLogical);
            if(!float.IsNaN(footerLogical)&&!float.IsInfinity(footerLogical))cameraFooterOverride=Mathf.Max(0,footerLogical);
        }
        public static void ResetHudHeights() { headerOverride=footerOverride=cameraHeaderOverride=cameraFooterOverride=-1; }

        public static Layout Calculate(Vector2 screen,Rect safe,float scale,float header,float footer)
        {
            float width=Mathf.Max(1,screen.x),height=Mathf.Max(1,screen.y);
            scale=Mathf.Clamp(scale,.5f,4);
            if(safe.width<=0||safe.height<=0)safe=new Rect(0,0,width,height);
            float left=Mathf.Clamp(safe.xMin,0,width-1),bottom=Mathf.Clamp(safe.yMin,0,height-1);
            safe=Rect.MinMaxRect(left,bottom,Mathf.Clamp(safe.xMax,left+1,width),Mathf.Clamp(safe.yMax,bottom+1,height));
            float topPixels=Mathf.Clamp(header*scale,0,safe.height*.18f);
            float bottomPixels=Mathf.Clamp(footer*scale,0,safe.height*.44f);
            var world=Rect.MinMaxRect(safe.xMin,safe.yMin+bottomPixels,safe.xMax,safe.yMax-topPixels);
            return new Layout(safe,world,scale);
        }
    }
}
