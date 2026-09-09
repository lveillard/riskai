using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI
{
    /// <summary>Zoom thresholds for the shared strategic unit proxy.</summary>
    public static class UnitPresentationLodPolicy
    {
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
    }

    /// <summary>
    /// Keeps the detailed model for tactical zoom and replaces it with one shared,
    /// team-coloured mesh at strategic zoom. Simulation, picking and HUD stay on Soldier.
    /// </summary>
    public sealed class UnitPresentationLodView : MonoBehaviour
    {
        static readonly Mesh[] ProxyMeshes = new Mesh[9];
        readonly List<Renderer> detailRenderers = new List<Renderer>(8);
        readonly List<Animation> legacyAnimations = new List<Animation>(2);
        readonly List<Behaviour> animationControllers = new List<Behaviour>(2);
        MeshRenderer proxyRenderer;
        bool initialized, globalProxy, selected;

        public bool UsingProxy { get; private set; }
        public int DetailRendererCount => detailRenderers.Count;
        public Renderer ProxyRenderer => proxyRenderer;

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

        void Apply()
        {
            if (!initialized) return;
            bool useProxy = globalProxy && !selected;
            if (UsingProxy == useProxy) return;
            UsingProxy = useProxy;
            foreach (var renderer in detailRenderers) if (renderer) renderer.enabled = !useProxy;
            foreach (var animation in legacyAnimations) if (animation) animation.enabled = !useProxy;
            foreach (var controller in animationControllers) if (controller) controller.enabled = !useProxy;
            if (proxyRenderer) proxyRenderer.enabled = useProxy;
        }

        void OnEnable()
        {
            if (initialized && Application.isPlaying) UnitPresentationLodManager.Register(this);
        }

        void OnDisable()
        {
            if (initialized && Application.isPlaying) UnitPresentationLodManager.Unregister(this);
        }

        static Vector3 ProxyScale(UnitKind kind)
        {
            float height = Mathf.Max(.9f, VisualMetrics.HeightFor(kind));
            float width = Mathf.Max(.52f, VisualMetrics.RadiusFor(kind) * 1.75f);
            float depth = kind == UnitKind.Guard || kind == UnitKind.MarineMajor || kind == UnitKind.MarineGeneral
                ? width * 1.45f : kind == UnitKind.Mortar ? width * 1.25f : width * .82f;
            return new Vector3(width, height, depth);
        }

        static Mesh ProxyMesh(UnitKind kind)
        {
            int index = (int)kind;
            if (ProxyMeshes[index]) return ProxyMeshes[index];
            bool mounted = kind == UnitKind.Guard || kind == UnitKind.MarineMajor || kind == UnitKind.MarineGeneral;
            bool ranged = kind == UnitKind.Archer || kind == UnitKind.Mage || kind == UnitKind.Medic || kind == UnitKind.MarinePrivate;
            float[] heights = { 0f, .18f, .68f, .96f, 1.18f };
            float[] radii = mounted
                ? new[] { .30f, .50f, .46f, .27f, .10f }
                : kind == UnitKind.Mortar
                    ? new[] { .34f, .52f, .48f, .25f, .12f }
                    : ranged
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
            var mesh = new Mesh { name = "Shared strategic " + kind + " proxy" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return ProxyMeshes[index] = mesh;
        }
    }

    public sealed class UnitPresentationLodManager : MonoBehaviour
    {
        static UnitPresentationLodManager instance;
        readonly List<UnitPresentationLodView> views = new List<UnitPresentationLodView>(512);
        float nextRefresh;
        bool proxyActive;

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
            bool next = !disabled && UnitPresentationLodPolicy.UseProxy(camera.orthographicSize, UiViewport.IsCompact, proxyActive);
            if (next == proxyActive) return;
            proxyActive = next;
            for (int i = views.Count - 1; i >= 0; i--)
            {
                var view = views[i];
                if (!view) { views.RemoveAt(i); continue; }
                view.SetGlobalProxy(proxyActive);
            }
            Debug.Log($"RISKAI_UNIT_LOD proxy={proxyActive} views={views.Count} zoom={camera.orthographicSize:F2} compact={UiViewport.IsCompact} disabled={disabled}");
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            views.Clear();
        }
    }
}
