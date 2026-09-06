using UnityEngine;

namespace RiskAI
{
    public sealed class RtsCameraRig : MonoBehaviour
    {
        public const float DefaultZoom=34;
        // Use the same oblique tactical view on every map; imported coordinates
        // should not turn the camera into a nearly overhead map view.
        public static Quaternion DefaultRotation => Quaternion.Euler(55, 0, 0);
        float InitialZoom => MapLayout.IsImported ? 80 * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) : DefaultZoom;
        float MinimumZoom => MapLayout.IsImported ? 18 * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) : 17;
        public float TargetZoom { get; private set; }=DefaultZoom;
        [Range(.1f,3f)] public float PanSpeed=1.35f;
        public Vector3 FocusPoint => focus;
        public float MaximumZoom => MapLayout.IsImported ? MapFrameZoom() : MapLayout.IsExpanded ? 60 : 44;
        float FocusSpeedCap => MapLayout.IsImported?Mathf.Max(120,MapLayout.HalfDepth*1.25f):120;
        Camera cam;Vector3 focus,targetFocus,panVelocity,zoomAnchor,homePoint=new Vector3(-26,0,-17);Vector2 anchorScreen;
        float zoomVelocity;bool anchorZoom;
        public void Initialize(Camera camera)
        {
            cam=camera;cam.orthographicSize=TargetZoom=InitialZoom;
            focus=targetFocus=new Vector3(-26,0,-17);Apply();
        }
        public Vector3 Ground(Vector2 screen)
        {
            var ray=cam.ScreenPointToRay(screen);
            if(Physics.Raycast(ray,out var hit,MapLayout.IsImported?2300:500,1<<MapLayout.TerrainLayer,QueryTriggerInteraction.Ignore))return hit.point;
            new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance);return ray.GetPoint(distance);
        }
        public void Focus(Vector3 point) { targetFocus=Clamp(point,TargetZoom);anchorZoom=false; }
        public void SetHome(Vector3 point) { homePoint=point;focus=targetFocus=Clamp(point);Apply(); }
        public void ResetView() { TargetZoom=InitialZoom;targetFocus=Clamp(homePoint,TargetZoom);anchorZoom=false; }
        public void FrameMap(){TargetZoom=MaximumZoom;targetFocus=MapLayout.PlayableCenter;anchorZoom=false;}
        public void Pan(Vector3 direction,float dt)
        {
            if(direction.sqrMagnitude<.001f)return;
            direction.Normalize();
            var right=Vector3.ProjectOnPlane(cam.transform.right,Vector3.up).normalized;
            var forward=Vector3.ProjectOnPlane(cam.transform.forward,Vector3.up).normalized;
            // Normalize after projecting the camera basis so diagonals do not move faster.
            Vector3 planar=right*direction.x+forward*direction.z;
            if(planar.sqrMagnitude>.001f)planar.Normalize();
            targetFocus=Clamp(targetFocus+planar*TargetZoom*.9f*PanSpeed*dt);anchorZoom=false;
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
            TargetZoom=Mathf.Clamp(TargetZoom*Mathf.Exp(-Mathf.Clamp(wheelSteps,-4,4)*.24f),MinimumZoom,MaximumZoom);
        }
        public void CancelMotion() { targetFocus=focus;panVelocity=Vector3.zero;anchorZoom=false;zoomVelocity=0;if(cam)TargetZoom=cam.orthographicSize; }
        void LateUpdate()
        {
            if(!cam)return;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            cam.orthographicSize=Mathf.SmoothDamp(cam.orthographicSize,TargetZoom,ref zoomVelocity,.10f,200,dt);
            focus=Vector3.SmoothDamp(focus,Clamp(targetFocus),ref panVelocity,.1f,FocusSpeedCap,dt);Apply();
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
        Vector3 Clamp(Vector3 point,float requestedZoom=-1)
        {
            float zoom=requestedZoom>=0?requestedZoom:cam.orthographicSize;
            Vector2 min = MapLayout.PlayableMin, max = MapLayout.PlayableMax;
            float marginX = Mathf.Min((max.x - min.x) * .5f, zoom * .6f);
            float marginZ = Mathf.Min((max.y - min.y) * .5f, zoom * .7f);
            return new Vector3(Mathf.Clamp(point.x, min.x + marginX, max.x - marginX),
                Mathf.Clamp(point.y, 0, 20), Mathf.Clamp(point.z, min.y + marginZ, max.y - marginZ));
        }
        float MapFrameZoom()
        {
            if (!cam) return Mathf.Max(180, MapLayout.HalfDepth);
            // Solve the perspective frustum for the W3I rectangle and the space
            // left by the HUD. This also handles New World's wider, off-centre map.
            float tangent = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            float top = 1 - 2 * BattleHud.TopPixels / Screen.height;
            float bottom = -1 + 2 * BattleHud.BottomPixels / Screen.height;
            float offset = (BattleHud.BottomPixels - BattleHud.TopPixels) / Screen.height;
            Vector2 min = MapLayout.PlayableMin, max = MapLayout.PlayableMax;
            float zoom = 180;
            for (int corner = 0; corner < 4; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? min.x : max.x, 0, (corner & 2) == 0 ? min.y : max.y) - MapLayout.PlayableCenter;
                float x = Vector3.Dot(point, cam.transform.right);
                float y = Vector3.Dot(point, cam.transform.up);
                float depth = Vector3.Dot(point, cam.transform.forward) * tangent;
                zoom = Mathf.Max(zoom, Mathf.Abs(x) / cam.aspect - depth);
                zoom = Mathf.Max(zoom, (y - top * depth) / Mathf.Max(.1f, top - offset));
                zoom = Mathf.Max(zoom, (-y + bottom * depth) / Mathf.Max(.1f, offset - bottom));
            }
            return zoom * 1.025f;
        }

        void Apply()
        {
            float playableOffset=cam.orthographicSize*(BattleHud.BottomPixels-BattleHud.TopPixels)/Screen.height;
            cam.transform.position=focus-cam.transform.forward*(cam.orthographicSize/Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad))-cam.transform.up*playableOffset;
        }
    }
}

