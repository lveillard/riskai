using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI
{
    /// <summary>Zoom thresholds for the shared strategic unit proxy.</summary>
    public static class UnitPresentationLodPolicy
    {
        public const float ViewportMargin = .08f;
        public const float DesktopEnterZoom = 96f;
        public const float DesktopExitZoom = 86f;
        public const float CompactEnterZoom = 88f;
        public const float CompactExitZoom = 78f;

        public static bool UseProxy(float zoom, bool compact, bool currentlyUsingProxy)
        {
            float enter = compact ? CompactEnterZoom : DesktopEnterZoom;
            float exit = compact ? CompactExitZoom : DesktopExitZoom;
            return currentlyUsingProxy ? zoom > exit : zoom >= enter;
        }

        public static bool IsInsideViewport(Vector3 center,float horizontalRadius,float verticalRadius,float margin=ViewportMargin)
        {
            if(center.z<=0)return false;
            return center.x+horizontalRadius>=-margin&&center.x-horizontalRadius<=1+margin&&
                center.y+verticalRadius>=-margin&&center.y-verticalRadius<=1+margin;
        }
    }

    /// <summary>
    /// Keeps the detailed model for tactical zoom and replaces it with one shared,
    /// team-coloured mesh at strategic zoom. Simulation, picking and HUD stay on Soldier.
    /// </summary>
    public sealed class UnitPresentationLodView : MonoBehaviour
    {
        // One shared proxy mesh per unit type, indexed by the catalog's dense type index.
        static Mesh[] proxyMeshes;
        static int proxyRevision = -1;
        readonly List<Renderer> detailRenderers = new List<Renderer>(8);
        readonly List<Animation> legacyAnimations = new List<Animation>(2);
        readonly List<Behaviour> animationControllers = new List<Behaviour>(2);
        MeshRenderer proxyRenderer;
        Bounds localPresentationBounds;
        bool initialized, globalProxy, selected, inCameraView=true, proxyApplied, controllersApplied, controllersActive;

        public bool UsingProxy { get; private set; }
        public int DetailRendererCount => detailRenderers.Count;
        public Renderer ProxyRenderer => proxyRenderer;
        public int AnimationControllerCount => animationControllers.Count;
        public bool ControllersActive => controllersActive;

        public void Initialize(UnitKind kind, int team)
        {
            if (initialized) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled && !(renderer is LineRenderer) && renderer.gameObject.name != "Soft ground shadow")
                    detailRenderers.Add(renderer);
            foreach (var animation in GetComponentsInChildren<Animation>(true))
            {
                if (!animation.enabled) continue;
                animation.cullingType = AnimationCullingType.BasedOnRenderers;
                legacyAnimations.Add(animation);
            }
            foreach (var animator in GetComponentsInChildren<SoldierAnimator>(true))
                if (animator.enabled) animationControllers.Add(animator);
            foreach (var knight in GetComponentsInChildren<MountedKnightView>(true))
                if (knight.enabled) animationControllers.Add(knight);
            foreach (var siege in GetComponentsInChildren<SiegeUnitView>(true))
                if (siege.enabled) animationControllers.Add(siege);

            CachePresentationBounds();

            var proxy = new GameObject("Shared strategic unit proxy");
            proxy.transform.SetParent(transform, false);
            proxy.transform.localScale = ProxyScale(kind);
            proxy.AddComponent<MeshFilter>().sharedMesh = ProxyMesh(kind);
            proxyRenderer = proxy.AddComponent<MeshRenderer>();
            proxyRenderer.sharedMaterial = VisualFactory.Mat(VisualFactory.TeamMaterialColor(team));
            proxyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            proxyRenderer.receiveShadows = false;
            proxyRenderer.enabled = false;
            initialized = true;
            Apply();
            if (Application.isPlaying) UnitPresentationLodManager.Register(this);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            Apply();
        }

        public void SetGlobalProxy(bool value)
        {
            globalProxy = value;
            Apply();
        }

        public void SetInCameraView(bool value)
        {
            inCameraView=value;
            Apply();
        }

        public bool IsInCameraView(Camera camera)
        {
            if(!camera)return true;
            Vector3 center=transform.TransformPoint(localPresentationBounds.center);
            float scale=Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z));
            float radius=localPresentationBounds.extents.magnitude*scale;
            Vector3 viewportCenter=camera.WorldToViewportPoint(center);
            Vector3 viewportRight=camera.WorldToViewportPoint(center+camera.transform.right*radius);
            Vector3 viewportUp=camera.WorldToViewportPoint(center+camera.transform.up*radius);
            return UnitPresentationLodPolicy.IsInsideViewport(viewportCenter,
                Mathf.Abs(viewportRight.x-viewportCenter.x),Mathf.Abs(viewportUp.y-viewportCenter.y));
        }

        void Apply()
        {
            if (!initialized) return;
            bool useProxy = globalProxy && !selected;
            bool enableControllers=!useProxy&&inCameraView;
            bool proxyChanged=!proxyApplied||UsingProxy!=useProxy;
            bool controllersChanged=!controllersApplied||controllersActive!=enableControllers;
            if(proxyChanged)
            {
                proxyApplied=true;UsingProxy=useProxy;
                foreach (var renderer in detailRenderers) if (renderer) renderer.enabled = !useProxy;
                foreach (var animation in legacyAnimations) if (animation) animation.enabled = !useProxy;
                if (proxyRenderer) proxyRenderer.enabled = useProxy;
            }
            if(controllersChanged)
            {
                controllersApplied=true;controllersActive=enableControllers;
                foreach (var controller in animationControllers) if (controller) controller.enabled = enableControllers;
            }
        }

        void CachePresentationBounds()
        {
            bool found=false;Vector3 minimum=default,maximum=default;
            foreach(var renderer in detailRenderers)
            {
                if(!renderer)continue;Bounds bounds=renderer.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    var world=new Vector3((corner&1)==0?bounds.min.x:bounds.max.x,
                        (corner&2)==0?bounds.min.y:bounds.max.y,(corner&4)==0?bounds.min.z:bounds.max.z);
                    Vector3 local=transform.InverseTransformPoint(world);
                    if(!found){minimum=maximum=local;found=true;}else{minimum=Vector3.Min(minimum,local);maximum=Vector3.Max(maximum,local);}
                }
            }
            localPresentationBounds=found?new Bounds((minimum+maximum)*.5f,maximum-minimum):new Bounds(Vector3.up,new Vector3(1,2,1));
        }

        void OnEnable()
        {
            inCameraView=true;
            controllersApplied=false;
            Apply();
            if (initialized && Application.isPlaying) UnitPresentationLodManager.Register(this);
        }

        void OnDisable()
        {
            if (initialized && Application.isPlaying) UnitPresentationLodManager.Unregister(this);
        }

        static Vector3 ProxyScale(UnitKind kind)
        {
            float height = Mathf.Max(.9f, UnitCatalog.Get(kind).VisualHeight);
            float width = Mathf.Max(.52f, UnitCatalog.Get(kind).VisualRadius * 1.75f);
            var silhouette = UnitCatalog.Get(kind).Silhouette;
            float depth = silhouette == UnitSilhouette.Mounted ? width * 1.45f : silhouette == UnitSilhouette.Siege ? width * 1.25f : width * .82f;
            return new Vector3(width, height, depth);
        }

        static Mesh ProxyMesh(UnitKind kind)
        {
            if (proxyMeshes == null || proxyRevision != UnitCatalog.Revision) { proxyMeshes = new Mesh[UnitCatalog.Count]; proxyRevision = UnitCatalog.Revision; }
            int index = UnitCatalog.Get(kind).Index;
            if (proxyMeshes[index]) return proxyMeshes[index];
            var silhouette = UnitCatalog.Get(kind).Silhouette;
            float[] heights = { 0f, .18f, .68f, .96f, 1.18f };
            float[] radii = silhouette == UnitSilhouette.Mounted
                ? new[] { .30f, .50f, .46f, .27f, .10f }
                : silhouette == UnitSilhouette.Marine
                    ? new[] { .25f, .40f, .42f, .34f, .20f }
                : silhouette == UnitSilhouette.Siege
                    ? new[] { .34f, .52f, .48f, .25f, .12f }
                    : silhouette == UnitSilhouette.Ranged
                        ? new[] { .24f, .38f, .42f, .28f, .04f }
                        : new[] { .27f, .43f, .47f, .25f, .10f };
            const int sides = 6;
            var vertices = new List<Vector3>(heights.Length * sides);
            var triangles = new List<int>((heights.Length - 1) * sides * 6 + sides * 3);
            for (int ring = 0; ring < heights.Length; ring++)
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    vertices.Add(new Vector3(Mathf.Sin(angle) * radii[ring], heights[ring], Mathf.Cos(angle) * radii[ring]));
                }
            for (int ring = 0; ring < heights.Length - 1; ring++)
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int lower = ring * sides + side, lowerNext = ring * sides + next;
                    int upper = (ring + 1) * sides + side, upperNext = (ring + 1) * sides + next;
                    triangles.Add(lower); triangles.Add(upper); triangles.Add(upperNext);
                    triangles.Add(lower); triangles.Add(upperNext); triangles.Add(lowerNext);
                }
            // RISKAI_SHARED_ASSET: one proxy mesh per unit kind, keyed to UnitCatalog.Revision.
            var mesh = new Mesh { name = "Shared strategic " + kind + " proxy" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return proxyMeshes[index] = mesh;
        }
    }

    public sealed class UnitPresentationLodManager : MonoBehaviour
    {
        static UnitPresentationLodManager instance;
        readonly List<UnitPresentationLodView> views = new List<UnitPresentationLodView>(512);
        float nextRefresh;
        bool proxyActive;
        bool? previousCullingDisabled;

        internal static void Register(UnitPresentationLodView view)
        {
            if (!view) return;
            if (!instance)
            {
                var root = new GameObject("Shared unit presentation LOD");
                root.hideFlags = HideFlags.DontSave;
                instance = root.AddComponent<UnitPresentationLodManager>();
            }
            if (!instance.views.Contains(view)) instance.views.Add(view);
            view.SetGlobalProxy(instance.proxyActive);
        }

        internal static void Unregister(UnitPresentationLodView view)
        {
            if (instance && view) instance.views.Remove(view);
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            var camera = Camera.main;
            if (!camera) return;
            bool disabled = LaunchArguments.HasFlag("--riskai-disable-unit-lod");
            bool cullingDisabled=LaunchArguments.HasFlag("--riskai-disable-unit-presentation-culling");
            bool next = !disabled && UnitPresentationLodPolicy.UseProxy(camera.orthographicSize, UiViewport.IsCompact, proxyActive);
            bool changed=next!=proxyActive||previousCullingDisabled!=cullingDisabled;
            proxyActive = next;
            previousCullingDisabled=cullingDisabled;
            for (int i = views.Count - 1; i >= 0; i--)
            {
                var view = views[i];
                if (!view) { views.RemoveAt(i); continue; }
                view.SetGlobalProxy(proxyActive);
                view.SetInCameraView(cullingDisabled||view.IsInCameraView(camera));
            }
            if(changed)Debug.Log($"RISKAI_UNIT_LOD proxy={proxyActive} views={views.Count} zoom={camera.orthographicSize:F2} compact={UiViewport.IsCompact} disabled={disabled} cullingDisabled={cullingDisabled}");
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            views.Clear();
        }
    }
}
