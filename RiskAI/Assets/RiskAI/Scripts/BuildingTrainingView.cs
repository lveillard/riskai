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
    /// Update, so it naturally freezes with the match and does not add a render pass.
    /// </summary>
    public sealed class BuildingTrainingView : MonoBehaviour
    {
        Transform doorPivot;
        Transform warmSeams;
        Renderer[] warmRenderers;
        Material landMaterial;
        Material navalMaterial;
        Material displayedMaterial;
        Vector3 seamBaseScale;
        bool active;

        public bool Active => active;

        public static BuildingTrainingView Create(Transform building, BuildingEntranceAnchor entrance)
        {
            Vector3 position = entrance ? entrance.Position : building.position;
            Vector3 outward = entrance ? entrance.Outward : Vector3.back;
            return Create(building, position, outward);
        }

        // Retained for callers that build temporary presentation without authored art.
        public static BuildingTrainingView Create(Transform building, Vector3 worldDoor, Vector3 outward)
        {
            var root = new GameObject("Training activity");
            root.transform.SetParent(building, true);
            var view = root.AddComponent<BuildingTrainingView>();
            view.Reposition(worldDoor, outward);
            view.Build();
            root.SetActive(false);
            return view;
        }

        void Build()
        {
            landMaterial = VisualFactory.EmissiveMat(new Color(1f, .39f, .08f), .48f);
            navalMaterial = VisualFactory.EmissiveMat(new Color(.18f, .63f, 1f), .35f);
            var pivot = new GameObject("Training door pivot");
            pivot.transform.SetParent(transform, false);
            doorPivot = pivot.transform;
            warmSeams = new GameObject("Training gate glow").transform;
            warmSeams.SetParent(doorPivot, false);

            // These slits illuminate the art's existing door leaves; they do not add a second doorway.
            var left = VisualFactory.Shape(warmSeams, PrimitiveType.Cube, "Training gate seam left", new Vector3(-.24f, .63f, .016f), new Vector3(.035f, 1.03f, .018f), new Color(1f, .39f, .08f));
            var right = VisualFactory.Shape(warmSeams, PrimitiveType.Cube, "Training gate seam right", new Vector3(.24f, .63f, .016f), new Vector3(.035f, 1.03f, .018f), new Color(1f, .39f, .08f));
            var lintel = VisualFactory.Shape(warmSeams, PrimitiveType.Cube, "Training gate lintel glow", new Vector3(0, 1.12f, .016f), new Vector3(.55f, .035f, .018f), new Color(1f, .39f, .08f));
            warmRenderers = new[] { left.GetComponent<Renderer>(), right.GetComponent<Renderer>(), lintel.GetComponent<Renderer>() };
            seamBaseScale = warmSeams.localScale;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void Reposition(Vector3 worldDoor, Vector3 outward)
        {
            transform.position = worldDoor;
            outward.y = 0;
            if (outward.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(outward.normalized);
        }

        public void SetActivity(bool landTraining, bool navalTraining, float simulationTime)
        {
            bool shouldShow = landTraining || navalTraining;
            if (active != shouldShow)
            {
                active = shouldShow;
                gameObject.SetActive(shouldShow);
            }
            if (!shouldShow) return;

            float breath = .90f + Mathf.Sin(simulationTime * 4.2f) * .10f;
            doorPivot.localRotation = Quaternion.Euler(0, Mathf.Sin(simulationTime * 2.1f) * 1.4f, 0);
            warmSeams.localScale = seamBaseScale * breath;
            var material = navalTraining && !landTraining ? navalMaterial : landMaterial;
            if (displayedMaterial == material) return;
            displayedMaterial = material;
            foreach (var renderer in warmRenderers) if (renderer) renderer.sharedMaterial = material;
        }
    }
}
