using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI
{
    /// <summary>Art-owned world point and horizontal outward normal for a building entrance.</summary>
    public sealed class BuildingEntranceAnchor : MonoBehaviour
    {
        public Vector3 Position => transform.position;
        public Vector3 Outward
        {
            get
            {
                var outward = transform.forward; outward.y = 0;
                return outward.sqrMagnitude > .0001f ? outward.normalized : Vector3.back;
            }
        }
        public static BuildingEntranceAnchor Create(Transform parent, string name, Vector3 localPosition, Vector3 localOutward)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPosition;
            localOutward.y = 0;
            if (localOutward.sqrMagnitude > .0001f) go.transform.localRotation = Quaternion.LookRotation(localOutward.normalized);
            return go.AddComponent<BuildingEntranceAnchor>();
        }
        public static BuildingEntranceAnchor Find(Transform root) => root ? root.GetComponentInChildren<BuildingEntranceAnchor>() : null;
    }

    /// <summary>
    /// A small entrance cue advanced from the simulation tick. It has no per-building
    /// Update, so it naturally freezes with the match. The emissive opening and transparent
    /// spill need no realtime light or additional shadow pass.
    /// </summary>
    public sealed class BuildingTrainingView : MonoBehaviour
    {
        Transform doorPivot;
        Transform warmSeams;
        Renderer[] warmRenderers;
        Material landMaterial;
        Renderer spillRenderer;
        MaterialPropertyBlock spillProperties;
        Mesh spillMesh;
        Vector3[] spillShape;
        Material displayedMaterial;
        Vector3 seamBaseScale;
        bool active;

        public bool Active => active;

        public static BuildingTrainingView Create(Transform building, BuildingEntranceAnchor entrance)
        {
            Vector3 position = entrance ? entrance.Position : building.position;
            Vector3 outward = entrance ? entrance.Outward : Vector3.back;
            var view = Create(building, position, outward);
            if (entrance)
            {
                var scale = entrance.transform.lossyScale;
                var parentScale = building.lossyScale;
                view.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            }
            view.FitSpillToGround();
            return view;
        }

        // Retained for callers that build temporary presentation without authored art.
        public static BuildingTrainingView Create(Transform building, Vector3 worldDoor, Vector3 outward)
        {
            var root = new GameObject("Training activity");
            root.transform.SetParent(building, true);
            var view = root.AddComponent<BuildingTrainingView>();
            view.Reposition(worldDoor, outward);
            view.Build();
            view.FitSpillToGround();
            root.SetActive(false);
            return view;
        }

        void Build()
        {
            // Emissive materials are cached by VisualFactory, so every doorway shares
            // the same warm cue without adding realtime lights per building.
            landMaterial = VisualFactory.EmissiveMat(new Color(1f, .39f, .08f), .90f);

            var pivot = new GameObject("Training door pivot");
            pivot.transform.SetParent(transform, false);
            doorPivot = pivot.transform;
            warmSeams = new GameObject("Training gate glow").transform;
            warmSeams.SetParent(doorPivot, false);

            // The inset glow reads as light coming from the existing doorway and its
            // threshold; it does not add a second doorway or alter the authored anchor.
            var opening = VisualFactory.Shape(warmSeams, PrimitiveType.Cube, "Training doorway glow", new Vector3(0, .93f, .085f), new Vector3(.72f, 1.60f, .018f), new Color(1f, .39f, .08f));
            var left = VisualFactory.Shape(warmSeams, PrimitiveType.Cube, "Training gate seam left", new Vector3(-.43f, .93f, .09f), new Vector3(.045f, 1.66f, .025f), new Color(1f, .39f, .08f));
            var right = VisualFactory.Shape(warmSeams, PrimitiveType.Cube, "Training gate seam right", new Vector3(.43f, .93f, .09f), new Vector3(.045f, 1.66f, .025f), new Color(1f, .39f, .08f));
            var lintel = VisualFactory.Shape(warmSeams, PrimitiveType.Cube, "Training gate lintel glow", new Vector3(0, 1.76f, .09f), new Vector3(.90f, .045f, .025f), new Color(1f, .39f, .08f));
            var threshold = VisualFactory.Shape(warmSeams, PrimitiveType.Cube, "Training threshold glow", new Vector3(0, .50f, .22f), new Vector3(.92f, .035f, .38f), new Color(1f, .39f, .08f));
            warmRenderers = new[] { opening.GetComponent<Renderer>(), left.GetComponent<Renderer>(), right.GetComponent<Renderer>(), lintel.GetComponent<Renderer>(), threshold.GetComponent<Renderer>() };
            seamBaseScale = warmSeams.localScale;
            BuildSpill();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        void BuildSpill()
        {
            // A tapered transparent patch reads in daylight without bloom or one realtime light per queue.
            var owner = GeneratedResourceOwner.For(transform);
            var go = new GameObject("Training threshold light spill");
            go.transform.SetParent(transform, false);
            var mesh = owner.Track(new Mesh { name = "Warm entrance spill" });
            const int rows = 5;
            var vertices = new Vector3[rows * 3];
            var colors = new Color[vertices.Length];
            var triangles = new int[(rows - 1) * 12];
            for (int row = 0; row < rows; row++)
            {
                float t = row / (float)(rows - 1);
                float z = row == 0 ? .14f : row == 1 ? .40f : row == 2 ? .78f : row == 3 ? 1.4f : 2.74f;
                float width = Mathf.Lerp(.48f, 1.4f, t);
                // Keep two rows above the .46 m top stair before descending beyond its edge.
                // These are art-local heights, scaled with the entrance architecture.
                float y = Mathf.Lerp(.51f, .045f, Mathf.Clamp01((z - .40f) / .9f));
                for (int column = 0; column < 3; column++)
                {
                    int i = row * 3 + column;
                    vertices[i] = new Vector3((column - 1) * width, y, z);
                    colors[i] = new Color(1f, .62f, .16f, column == 1 ? .75f * (1f - t) : 0f);
                }
                if (row == rows - 1) continue;
                for (int column = 0; column < 2; column++)
                {
                    int i = row * 3 + column, k = row * 12 + column * 6;
                    triangles[k] = i; triangles[k + 1] = i + 3; triangles[k + 2] = i + 1;
                    triangles[k + 3] = i + 1; triangles[k + 4] = i + 3; triangles[k + 5] = i + 4;
                }
            }
            spillMesh = mesh; spillShape = vertices;
            mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = triangles; mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            spillRenderer = go.AddComponent<MeshRenderer>();
            spillRenderer.sharedMaterial = Resources.Load<Material>("TrainingGlow");
            spillProperties = new MaterialPropertyBlock();
        }

        void FitSpillToGround()
        {
            if (!spillMesh) return;
            var vertices = new Vector3[spillShape.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var world = transform.TransformPoint(spillShape[i]);
                world.y = Mathf.Max(world.y, MapLayout.Height(world.x, world.z) + .025f);
                vertices[i] = transform.InverseTransformPoint(world);
            }
            spillMesh.vertices = vertices;
            spillMesh.RecalculateBounds();
        }

        public void Reposition(Vector3 worldDoor, Vector3 outward)
        {
            transform.position = worldDoor;
            outward.y = 0;
            if (outward.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(outward.normalized);
            FitSpillToGround();
        }

        public void SetActivity(bool landTraining, bool navalTraining, float simulationTime)
        {
            bool shouldShow = landTraining || navalTraining;
            active = shouldShow;
            if (gameObject.activeSelf != shouldShow) gameObject.SetActive(shouldShow);
            if (!shouldShow) return;

            float breath = .90f + Mathf.Sin(simulationTime * 4.2f) * .10f;
            // Keep the luminous opening fixed against the authored gate; only light intensity breathes.
            doorPivot.localRotation = Quaternion.identity;
            warmSeams.localScale = seamBaseScale;
            spillProperties.SetColor("_BaseColor", new Color(1f, 1f, 1f, breath));
            spillRenderer.SetPropertyBlock(spillProperties);
            var material = landMaterial;
            if (displayedMaterial == material) return;
            displayedMaterial = material;
            foreach (var renderer in warmRenderers) if (renderer) renderer.sharedMaterial = material;
        }
    }
}
