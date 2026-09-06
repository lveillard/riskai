using UnityEngine;

namespace RiskAI
{
    /// <summary>Visible own adapter for reliable shore boarding; source A00V itself is radius based.</summary>
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
            var ring=VisualFactory.Ring(go.transform,Radius,.045f,new Color(.32f,.78f,1f,.55f));ring.transform.position=harbor.Landing;ring.startColor=ring.endColor=new Color(.32f,.78f,1f,.55f);
            return zone;
        }
        public bool Contains(Vector3 point)
        {
            var delta=point-Center;delta.y=0;return delta.sqrMagnitude<=Radius*Radius;
        }
    }
}
