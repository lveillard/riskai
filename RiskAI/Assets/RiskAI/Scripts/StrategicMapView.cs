using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI
{
    /// <summary>Presentation-only camera LOD. Never disables gameplay objects or colliders.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class StrategicMapView : MonoBehaviour
    {
        public const int StrategicLayer=30;
        public static StrategicMapView Current { get; private set; }
        public static bool Active => Current && Current.IsStrategic;
        public bool IsStrategic { get; private set; }
        public int SurfaceCount { get; private set; }
        public TerritoryAtlas Atlas { get; private set; }
        public int SelectedCountry { get; private set; }=-1;
        public float EnterZoom => 84;
        Camera cam; int tacticalMask; Color tacticalBackground;
        Material strategic,inspection;GameObject inspectionRoot;
        float nextRefresh;
        public void Initialize(BattleSession session,Camera camera,Transform terrain)
        {
            Current=this;cam=camera;tacticalMask=cam.cullingMask&~(1<<StrategicLayer);tacticalBackground=cam.backgroundColor;
            cam.cullingMask=tacticalMask;
            Atlas=new TerritoryAtlas(session);
            strategic=MakeMaterial(true);inspection=MakeMaterial(false);
            var root=new GameObject("Strategic terrain Â· render only");root.transform.SetParent(transform,false);
            inspectionRoot=new GameObject("Country inspection Â· terrain surface");inspectionRoot.transform.SetParent(transform,false);
            foreach(var filter in terrain.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                if(!mesh || !(mesh.name=="Imported land chunk" || mesh.name=="Irregular continental terrain" || mesh.name.StartsWith("Sculpted island")))continue;
                CopySurface(filter,root.transform,strategic,StrategicLayer);
                CopySurface(filter,inspectionRoot.transform,inspection,0);SurfaceCount++;
            }
            inspectionRoot.SetActive(false);
        }
        Material MakeMaterial(bool overview)
        {
            var mat=new Material(Resources.Load<Material>("StrategicTerritory"));
            mat.SetTexture("_Regions",Atlas.Regions);mat.SetTexture("_Palette",Atlas.Palette);mat.SetVector("_MapBounds",Atlas.Bounds);
            mat.SetFloat("_PaletteWidth",Atlas.Palette.width);mat.SetFloat("_Overview",overview?1:0);
            mat.SetFloat("_ZWrite",overview?1:0);mat.SetFloat("_SelectedCountry",-1);return mat;
        }
        static void CopySurface(MeshFilter source,Transform parent,Material material,int layer)
        {
            var go=new GameObject(source.name);go.layer=layer;
            go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);go.transform.localScale=source.transform.lossyScale;go.transform.SetParent(parent,true);
            go.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        public void SelectCountry(int country)
        {
            SelectedCountry=country;inspection.SetFloat("_SelectedCountry",country>=0?country+1:-1);strategic.SetFloat("_SelectedCountry",country>=0?country+1:-1);
            inspectionRoot.SetActive(country>=0);
        }
        public void SetStrategic(bool enabled)
        {
            if(IsStrategic==enabled)return;
            IsStrategic=enabled;cam.cullingMask=enabled?1<<StrategicLayer:tacticalMask;
            cam.backgroundColor=enabled?new Color(.055f,.15f,.20f):tacticalBackground;
        }
        void LateUpdate() => RefreshPresentation();

        public void RefreshPresentation()
        {
            if(!cam)return;
            if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.3f;Atlas.RefreshOwners();}
            SetStrategic(IsStrategic?cam.orthographicSize>EnterZoom*.88f:cam.orthographicSize>=EnterZoom);
        }
        void OnDisable()
        {
            IsStrategic=false;
            if(cam){cam.cullingMask=tacticalMask;cam.backgroundColor=tacticalBackground;}
        }
        void OnDestroy()
        {
            if(Current==this)Current=null;
            Atlas?.Dispose();if(strategic)Destroy(strategic);if(inspection)Destroy(inspection);
        }
    }
}
