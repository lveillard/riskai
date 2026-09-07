using UnityEngine;

namespace RiskAI
{
    /// <summary>Shore boarding area; gameplay radius is independent of building selection visuals.</summary>
    public sealed class NavalEmbarkZone : MonoBehaviour
    {
        public Harbor Harbor { get; private set; }
        public Vector3 Center => Harbor ? Harbor.Landing : transform.position;
        public const float Radius = Ship.LoadRadius;
        public static NavalEmbarkZone Create(Transform parent, Harbor harbor)
        {
            if(!harbor)return null;
            var go=new GameObject("Playa de embarque · "+harbor.DisplayName);go.transform.SetParent(parent,false);go.transform.position=harbor.Landing;
            var zone=go.AddComponent<NavalEmbarkZone>();zone.Harbor=harbor;
            // Ports use the common building selection ring and the small garrison
            // circle. A second, much larger blue loading ring obscures both.
            return zone;
        }
        public bool Contains(Vector3 point)
        {
            var delta=point-Center;delta.y=0;return delta.sqrMagnitude<=Radius*Radius;
        }
    }
}
