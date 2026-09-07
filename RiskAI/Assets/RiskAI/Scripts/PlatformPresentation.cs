using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskAI
{
    /// <summary>Device capabilities affect presentation and input, never map rules or the simulation clock.</summary>
    public static class PlatformPresentation
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern float RiskAI_CanvasDensity();
        [DllImport("__Internal")] static extern int RiskAI_TouchCapable();
        [DllImport("__Internal")] static extern float RiskAI_SafeInset(int edge);
#endif
        public static bool TouchCapable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return RiskAI_TouchCapable()!=0;
#else
                return Application.isMobilePlatform||Touchscreen.current!=null;
#endif
            }
        }
        public static float PixelDensity
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Mathf.Clamp(RiskAI_CanvasDensity(),.5f,4);
#else
                // Device discovery must not resize the desktop UI when a stylus/touchscreen connects.
                return Screen.dpi>0?Mathf.Clamp(Screen.dpi/(Application.isMobilePlatform?160f:96f),1,3):1;
#endif
            }
        }
        public static Rect SafeArea
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                float density=PixelDensity;
                float left=RiskAI_SafeInset(0)*density,top=RiskAI_SafeInset(1)*density;
                float right=RiskAI_SafeInset(2)*density,bottom=RiskAI_SafeInset(3)*density;
                return new Rect(left,bottom,Mathf.Max(1,Screen.width-left-right),Mathf.Max(1,Screen.height-top-bottom));
#else
                return Screen.safeArea;
#endif
            }
        }
    }
}
