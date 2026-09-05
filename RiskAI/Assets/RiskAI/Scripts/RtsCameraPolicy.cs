using UnityEngine;

namespace RiskAI
{
    /// <summary>Pure camera-input rules shared by the controller and policy tests.</summary>
    public static class RtsCameraPolicy
    {
        public const float EdgeBandPixels=20f;

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
