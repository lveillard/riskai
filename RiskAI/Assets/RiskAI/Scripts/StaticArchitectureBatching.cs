using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI
{
    /// <summary>
    /// Runtime batching for one immutable architecture group. Native mode leaves
    /// authored renderers intact; manual mode retains their GameObjects, meshes
    /// and colliders and disables only renderers replaced by a local batch.
    /// </summary>
    public static class StaticArchitectureBatching
    {
        public const string DisableFlag = "--riskai-disable-architecture-batching";
        public const string NativeFlag = "--riskai-native-architecture-batching";
        public const string ManualFlag = "--riskai-manual-architecture-batching";
        const int LastOpaqueRenderQueue = 2500;
        static Mode? configuredMode;

        public enum Mode { Disabled, Native, Manual }

        public readonly struct Result
        {
            public readonly int CandidateCount, CreatedMeshCount;
            public readonly bool Applied;
            public Result(int candidates,int meshes,bool applied)
            {CandidateCount=candidates;CreatedMeshCount=meshes;Applied=applied;}
        }

        public static Mode ActiveMode
        {
            get
            {
                if(!configuredMode.HasValue)configuredMode=ModeFor(LaunchArguments.Get());
                return configuredMode.Value;
            }
        }
        public static bool Enabled => ActiveMode!=Mode.Disabled;

        public static Mode ModeFor(IEnumerable<string> arguments)
        {
            bool native=false,manual=false;
            if(arguments==null)return Mode.Disabled;
            foreach(var argument in arguments)
            {
                if(string.Equals(argument,DisableFlag,StringComparison.OrdinalIgnoreCase))return Mode.Disabled;
                if(string.Equals(argument,NativeFlag,StringComparison.OrdinalIgnoreCase))native=true;
                if(string.Equals(argument,ManualFlag,StringComparison.OrdinalIgnoreCase))manual=true;
            }
            return manual?Mode.Manual:native?Mode.Native:Mode.Disabled;
        }
        public static bool EnabledFor(IEnumerable<string> arguments) => ModeFor(arguments)!=Mode.Disabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetConfiguration() => configuredMode=null;

        public static Result Combine(Transform root,params Renderer[] mutableRenderers) =>
            Combine(root,ActiveMode,mutableRenderers);

        public static Result Combine(Transform root,Mode mode,params Renderer[] mutableRenderers)
        {
            if(mode==Mode.Disabled||!root)return new Result(0,0,false);
            var mutable=new HashSet<Renderer>();
            if(mutableRenderers!=null)
                for(int i=0;i<mutableRenderers.Length;i++)if(mutableRenderers[i])mutable.Add(mutableRenderers[i]);

            var candidates=new List<MeshRenderer>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if(!Eligible(renderer,mutable))continue;
                if(mode==Mode.Manual&&(renderer.sharedMaterials.Length!=1||renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount!=1||
                    renderer.lightmapIndex>=0||renderer.HasPropertyBlock()))continue;
                candidates.Add(renderer);
            }
            if(candidates.Count<2)return new Result(candidates.Count,0,false);
            return mode==Mode.Manual?CombineManual(root,candidates):CombineNative(root,candidates);
        }

        static Result CombineNative(Transform root,List<MeshRenderer> candidates)
        {
            // Unity can replace each primitive/shared source mesh with one or
            // more runtime combined meshes. Remember every pre-existing mesh in
            // the group so Resources assets and Unity primitive meshes can never
            // be mistaken for allocations owned by this batch.
            var originalMeshes=new HashSet<Mesh>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh)originalMeshes.Add(filter.sharedMesh);

            var objects=new GameObject[candidates.Count];
            for(int i=0;i<candidates.Count;i++)objects[i]=candidates[i].gameObject;
            StaticBatchingUtility.Combine(objects,root.gameObject);

            var generated=new HashSet<Mesh>();
            for(int i=0;i<candidates.Count;i++)
            {
                var filter=candidates[i].GetComponent<MeshFilter>();
                if(filter&&filter.sharedMesh&&!originalMeshes.Contains(filter.sharedMesh))generated.Add(filter.sharedMesh);
            }
            if(generated.Count>0)
            {
                var owner=GeneratedResourceOwner.For(root);
                foreach(var mesh in generated)owner.Track(mesh);
            }
            return new Result(candidates.Count,generated.Count,true);
        }

        static Result CombineManual(Transform root,List<MeshRenderer> candidates)
        {
            var groups=new Dictionary<ManualKey,List<MeshRenderer>>();
            for(int i=0;i<candidates.Count;i++)
            {
                var key=new ManualKey(candidates[i]);
                if(!groups.TryGetValue(key,out var renderers))groups.Add(key,renderers=new List<MeshRenderer>());
                renderers.Add(candidates[i]);
            }

            var createdMeshes=new List<Mesh>();
            var createdObjects=new List<GameObject>();
            var createdRenderers=new List<MeshRenderer>();
            var consumed=new List<MeshRenderer>();
            try
            {
                foreach(var pair in groups)
                {
                    if(pair.Value.Count<2)continue;
                    var combines=new CombineInstance[pair.Value.Count];
                    int vertexCount=0;
                    for(int i=0;i<pair.Value.Count;i++)
                    {
                        var filter=pair.Value[i].GetComponent<MeshFilter>();vertexCount+=filter.sharedMesh.vertexCount;
                        combines[i]=new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=0,
                            transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix};
                    }
                    var mesh=new Mesh{name="Manual architecture batch"};createdMeshes.Add(mesh);
                    if(vertexCount>ushort.MaxValue)mesh.indexFormat=IndexFormat.UInt32;
                    mesh.CombineMeshes(combines,true,true,false);mesh.RecalculateBounds();
                    var go=new GameObject("Manual architecture batch · "+pair.Key.Material.name);createdObjects.Add(go);go.transform.SetParent(root,false);
                    go.layer=pair.Key.Layer;
                    var filterOut=go.AddComponent<MeshFilter>();filterOut.sharedMesh=mesh;
                    var rendererOut=go.AddComponent<MeshRenderer>();rendererOut.sharedMaterial=pair.Key.Material;
                    pair.Key.CopyTo(rendererOut);rendererOut.enabled=false;
                    createdRenderers.Add(rendererOut);consumed.AddRange(pair.Value);
                }
            }
            catch(Exception exception)
            {
                for(int i=0;i<createdObjects.Count;i++)DestroyOwned(createdObjects[i]);
                for(int i=0;i<createdMeshes.Count;i++)DestroyOwned(createdMeshes[i]);
                Debug.LogException(exception);return new Result(candidates.Count,0,false);
            }
            if(createdMeshes.Count==0)return new Result(candidates.Count,0,false);

            var owner=GeneratedResourceOwner.For(root);
            for(int i=0;i<createdMeshes.Count;i++)owner.Track(createdMeshes[i]);
            for(int i=0;i<consumed.Count;i++)consumed[i].enabled=false;
            for(int i=0;i<createdRenderers.Count;i++)createdRenderers[i].enabled=true;
            return new Result(candidates.Count,createdMeshes.Count,true);
        }

        static bool Eligible(MeshRenderer renderer,HashSet<Renderer> mutable)
        {
            if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy||renderer.isPartOfStaticBatch||mutable.Contains(renderer))return false;
            if(renderer.gameObject.name.StartsWith("Manual architecture batch · ",StringComparison.Ordinal))return false;
            var filter=renderer.GetComponent<MeshFilter>();
            if(!filter||!filter.sharedMesh||filter.sharedMesh.vertexCount==0)return false;
            if(renderer.name=="Soft ground shadow"||renderer.name=="Faction roof"||renderer.name=="Banner")return false;
            if(renderer.GetComponentInParent<MillSails>())return false;
            var materials=renderer.sharedMaterials;
            if(materials==null||materials.Length==0)return false;
            for(int i=0;i<materials.Length;i++)if(!materials[i]||materials[i].renderQueue>LastOpaqueRenderQueue)return false;
            return true;
        }

        readonly struct ManualKey:IEquatable<ManualKey>
        {
            public readonly Material Material;
            public readonly int Layer,SortingLayer,SortingOrder;
            public readonly uint RenderingLayerMask;
            public readonly ShadowCastingMode Shadows;
            public readonly bool ReceiveShadows,AllowOcclusion;
            public readonly LightProbeUsage LightProbes;
            public readonly ReflectionProbeUsage ReflectionProbes;
            public readonly MotionVectorGenerationMode MotionVectors;
            public readonly Transform ProbeAnchor;

            public ManualKey(MeshRenderer renderer)
            {
                Material=renderer.sharedMaterial;Layer=renderer.gameObject.layer;
                SortingLayer=renderer.sortingLayerID;SortingOrder=renderer.sortingOrder;
                RenderingLayerMask=renderer.renderingLayerMask;
                Shadows=renderer.shadowCastingMode;ReceiveShadows=renderer.receiveShadows;
                AllowOcclusion=renderer.allowOcclusionWhenDynamic;LightProbes=renderer.lightProbeUsage;
                ReflectionProbes=renderer.reflectionProbeUsage;MotionVectors=renderer.motionVectorGenerationMode;
                ProbeAnchor=renderer.probeAnchor;
            }
            public bool Equals(ManualKey other) => ReferenceEquals(Material,other.Material)&&Layer==other.Layer&&
                SortingLayer==other.SortingLayer&&SortingOrder==other.SortingOrder&&Shadows==other.Shadows&&
                RenderingLayerMask==other.RenderingLayerMask&&
                ReceiveShadows==other.ReceiveShadows&&AllowOcclusion==other.AllowOcclusion&&
                LightProbes==other.LightProbes&&ReflectionProbes==other.ReflectionProbes&&
                MotionVectors==other.MotionVectors&&ProbeAnchor==other.ProbeAnchor;
            public override bool Equals(object value) => value is ManualKey other&&Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash=Material?Material.GetInstanceID():0;hash=hash*397^Layer;hash=hash*397^SortingLayer;hash=hash*397^SortingOrder;
                    hash=hash*397^(int)RenderingLayerMask;
                    hash=hash*397^(int)Shadows;hash=hash*397^(ReceiveShadows?1:0);hash=hash*397^(AllowOcclusion?1:0);
                    hash=hash*397^(int)LightProbes;hash=hash*397^(int)ReflectionProbes;hash=hash*397^(int)MotionVectors;
                    return hash*397^(ProbeAnchor?ProbeAnchor.GetInstanceID():0);
                }
            }
            public void CopyTo(MeshRenderer renderer)
            {
                renderer.shadowCastingMode=Shadows;renderer.receiveShadows=ReceiveShadows;
                renderer.allowOcclusionWhenDynamic=AllowOcclusion;renderer.lightProbeUsage=LightProbes;
                renderer.reflectionProbeUsage=ReflectionProbes;renderer.motionVectorGenerationMode=MotionVectors;
                renderer.probeAnchor=ProbeAnchor;renderer.sortingLayerID=SortingLayer;renderer.sortingOrder=SortingOrder;
                renderer.renderingLayerMask=RenderingLayerMask;
            }
        }

        static void DestroyOwned(UnityEngine.Object value)
        {
            if(!value)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
