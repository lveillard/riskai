using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// A small, pooled-by-lifetime training cue. Its owner advances it from the
    /// simulation tick, so it has no per-building Update and naturally freezes on pause.
    /// </summary>
    public sealed class BuildingTrainingView : MonoBehaviour
    {
        Transform hammerPivot;
        Transform pennant;
        Renderer pennantRenderer;
        Material landMaterial;
        Material navalMaterial;
        Material displayedMaterial;
        Vector3 pennantBaseScale;
        bool active;

        public bool Active => active;

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
            landMaterial = VisualFactory.Mat(new Color(.96f, .67f, .25f));
            navalMaterial = VisualFactory.Mat(new Color(.28f, .76f, 1f));
            var pivot = new GameObject("Training hammer pivot");pivot.transform.SetParent(transform,false);pivot.transform.localPosition=new Vector3(0,.12f,0);
            hammerPivot=pivot.transform;
            VisualFactory.Shape(hammerPivot, PrimitiveType.Cube, "Training hammer handle", new Vector3(0, .31f, 0), new Vector3(.07f, .62f, .07f), new Color(.28f, .15f, .07f));
            VisualFactory.Shape(hammerPivot, PrimitiveType.Cube, "Training hammer head", new Vector3(0, .62f, 0), new Vector3(.34f, .11f, .13f), new Color(.42f, .43f, .39f));
            var flag = VisualFactory.Shape(transform, PrimitiveType.Cube, "Training pennant", new Vector3(.32f, .67f, 0), new Vector3(.25f, .42f, .035f), new Color(.96f, .67f, .25f));
            pennant = flag.transform;
            pennantBaseScale = pennant.localScale;
            pennantRenderer = flag.GetComponent<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void Reposition(Vector3 worldDoor, Vector3 outward)
        {
            transform.position = worldDoor + Vector3.up * .16f;
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

            // These transforms are deliberately changed only by an already-existing
            // rules tick. Paused sessions do not call us, so the cue is motionless.
            float swing = Mathf.Sin(simulationTime * 8f) * 28f;
            hammerPivot.localRotation = Quaternion.Euler(0, 0, swing);
            pennant.localScale = new Vector3(pennantBaseScale.x, pennantBaseScale.y * (.82f + Mathf.Sin(simulationTime * 5f) * .18f), pennantBaseScale.z);
            var material = navalTraining && !landTraining ? navalMaterial : landMaterial;
            if (pennantRenderer && displayedMaterial != material) { pennantRenderer.sharedMaterial = material; displayedMaterial = material; }
        }
    }
}
