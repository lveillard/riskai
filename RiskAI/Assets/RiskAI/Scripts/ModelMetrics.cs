using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Calibrates original art to numerical source heights, without stretching its proportions.</summary>
    public static class ModelMetrics
    {
        static readonly Dictionary<UnitKind,Bounds> StandingBounds = new Dictionary<UnitKind,Bounds>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => StandingBounds.Clear();

        public static void MatchStandingHeight(GameObject model,UnitKind kind)
        {
            float target=SourceGeometry.StandingHeight(kind);
            if(target<=0)return;
            // Put every fresh instance into the same pose, including cache hits;
            // imported animation curves can also set the rig-root transform.
            var animation=model.GetComponentInChildren<Animation>();
            if(animation&&animation["Idle"]){animation.Stop();animation["Idle"].clip.SampleAnimation(animation.gameObject,0);}
            if(!StandingBounds.TryGetValue(kind,out var bounds))
            {
                bounds=Measure(model.transform);
                if(bounds.size.y<.001f)return;
                StandingBounds.Add(kind,bounds);
            }
            float scale=target/bounds.size.y;
            model.transform.localScale=Vector3.one*scale;
            model.transform.localPosition=Vector3.up*(-bounds.min.y*scale);
        }

        // Root-local posed bounds. Baked skinned vertices avoid the conservative
        // all-animation culling box, which can be much larger than a standing unit.
        public static Bounds Measure(Transform root)
        {
            var result=new Bounds();bool found=false;
            void Include(Vector3 point)
            {
                if(!found){result=new Bounds(point,Vector3.zero);found=true;}
                else result.Encapsulate(point);
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                if(renderer is SkinnedMeshRenderer skinned)
                {
                    // Unity 6.3: true compensates transform scale; apply the renderer transform once below.
                    var baked=new Mesh();skinned.BakeMesh(baked,true);
                    foreach(var point in baked.vertices)Include(root.InverseTransformPoint(skinned.transform.TransformPoint(point)));
                    if(Application.isPlaying)Object.Destroy(baked);else Object.DestroyImmediate(baked);
                }
                else
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    if(!filter||!filter.sharedMesh)continue;
                    var bounds=filter.sharedMesh.bounds;
                    for(int corner=0;corner<8;corner++)
                    {
                        var point=new Vector3((corner&1)==0?bounds.min.x:bounds.max.x,
                            (corner&2)==0?bounds.min.y:bounds.max.y,(corner&4)==0?bounds.min.z:bounds.max.z);
                        Include(root.InverseTransformPoint(renderer.transform.TransformPoint(point)));
                    }
                }
            }
            return result;
        }
    }
}
