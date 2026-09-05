using UnityEngine;

namespace RiskAI
{
    public sealed class RtsCameraRig : MonoBehaviour
    {
        public const float DefaultZoom=34;
        public float TargetZoom { get; private set; }=DefaultZoom;
        [Range(.1f,3f)] public float PanSpeed=1;
        public Vector3 FocusPoint => focus;
        Camera cam;Vector3 focus,targetFocus,panVelocity,zoomAnchor,homePoint=new Vector3(-26,0,-17);Vector2 anchorScreen;
        float zoomVelocity;bool anchorZoom;
        public void Initialize(Camera camera)
        {
            cam=camera;cam.orthographicSize=TargetZoom=DefaultZoom;
            focus=targetFocus=new Vector3(-26,0,-17);Apply();
        }
        public Vector3 Ground(Vector2 screen)
        {
            var ray=cam.ScreenPointToRay(screen);
            if(Physics.Raycast(ray,out var hit,500,1<<MapLayout.TerrainLayer,QueryTriggerInteraction.Ignore))return hit.point;
            new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance);return ray.GetPoint(distance);
        }
        public void Focus(Vector3 point) { targetFocus=Clamp(point);anchorZoom=false; }
        public void SetHome(Vector3 point) { homePoint=point;focus=targetFocus=Clamp(point);Apply(); }
        public void ResetView() { TargetZoom=DefaultZoom;targetFocus=Clamp(homePoint);anchorZoom=false; }
        public void Pan(Vector3 direction,float dt)
        {
            if(direction.sqrMagnitude<.001f)return;
            direction.Normalize();
            var right=Vector3.ProjectOnPlane(cam.transform.right,Vector3.up).normalized;
            var forward=Vector3.ProjectOnPlane(cam.transform.forward,Vector3.up).normalized;
            targetFocus=Clamp(targetFocus+(right*direction.x+forward*direction.z)*cam.orthographicSize*.9f*PanSpeed*dt);anchorZoom=false;
        }
        public void Drag(Vector2 previous,Vector2 current)
        {
            // Ground-space grabbing is independent of frame rate, resolution and zoom.
            Vector3 grabbed=Ground(previous),delta=grabbed-AtHeight(current,grabbed.y);focus=Clamp(focus+delta);targetFocus=focus;panVelocity=Vector3.zero;anchorZoom=false;Apply();
        }
        public void ZoomAt(float wheelSteps,Vector2 screen)
        {
            if(Mathf.Abs(wheelSteps)<.001f)return;
            zoomAnchor=Ground(screen);anchorScreen=screen;anchorZoom=true;
            TargetZoom=Mathf.Clamp(TargetZoom*Mathf.Exp(-Mathf.Clamp(wheelSteps,-4,4)*.24f),17,44);
        }
        public void CancelMotion() { targetFocus=focus;panVelocity=Vector3.zero;anchorZoom=false;zoomVelocity=0;if(cam)TargetZoom=cam.orthographicSize; }
        void LateUpdate()
        {
            if(!cam)return;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            cam.orthographicSize=Mathf.SmoothDamp(cam.orthographicSize,TargetZoom,ref zoomVelocity,.10f,200,dt);
            focus=Vector3.SmoothDamp(focus,Clamp(targetFocus),ref panVelocity,.1f,120,dt);Apply();
            if(anchorZoom)
            {
                // Keep the original surface point under the cursor, including on cliff edges.
                Vector3 delta=zoomAnchor-AtHeight(anchorScreen,zoomAnchor.y);focus=Clamp(focus+delta);targetFocus=Clamp(targetFocus+delta);Apply();
                if(Mathf.Abs(cam.orthographicSize-TargetZoom)<.01f)anchorZoom=false;
            }
        }
        Vector3 AtHeight(Vector2 screen,float height)
        {
            var ray=cam.ScreenPointToRay(screen);new Plane(Vector3.up,Vector3.up*height).Raycast(ray,out float distance);return ray.GetPoint(distance);
        }
        Vector3 Clamp(Vector3 point)
        {
            float xLimit=MapLayout.HalfWidth-cam.orthographicSize*.6f;
            float zLimit=MapLayout.HalfDepth-cam.orthographicSize*.7f;
            return new Vector3(Mathf.Clamp(point.x,-xLimit,xLimit),Mathf.Clamp(point.y,0,20),Mathf.Clamp(point.z,-zLimit,zLimit));
        }
        void Apply()
        {
            float playableOffset=cam.orthographicSize*(BattleHud.BottomPixels-BattleHud.TopPixels)/Screen.height;
            cam.transform.position=focus-cam.transform.forward*(cam.orthographicSize/Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad))-cam.transform.up*playableOffset;
        }
    }
}

