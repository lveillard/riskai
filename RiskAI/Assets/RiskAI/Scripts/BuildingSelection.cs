using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RiskAI
{
    // Static building geometry only. Picking covers roofs as well as collision walls,
    // and treats the house and its permanent tower as one selectable post.
    public static class BuildingSelection
    {
        sealed class Footprint { public Bounds[] Parts; public Bounds Combined; }
        static readonly ConditionalWeakTable<Component, Footprint> Cache = new();

        static Footprint Geometry(Component building)
        {
            if(Cache.TryGetValue(building,out var cached))return cached;
            var groups=new Dictionary<Transform,Bounds>();
            foreach(var renderer in building.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled || renderer.name.Contains("shadow") || renderer.name.Contains("Shadow"))continue;
                var bounds=renderer.bounds;if(bounds.size.y<.08f)continue;
                var tower=renderer.GetComponentInParent<DefenseTower>();
                Transform key=tower?tower.transform:building.transform;
                if(groups.TryGetValue(key,out var group)){group.Encapsulate(bounds);groups[key]=group;}
                else groups.Add(key,bounds);
            }
            var parts=new List<Bounds>(groups.Values);
            if(parts.Count==0)parts.Add(new Bounds(building.transform.position+Vector3.up*1.5f,new Vector3(4,3,4)));
            var combined=parts[0];for(int i=1;i<parts.Count;i++)combined.Encapsulate(parts[i]);
            var result=new Footprint{Parts=parts.ToArray(),Combined=combined};Cache.Add(building,result);return result;
        }
        public static float HitDistance(Component building,Ray ray)
        {
            float nearest=float.PositiveInfinity;
            foreach(var part in Geometry(building).Parts)
                if(part.IntersectRay(ray,out float distance))nearest=Mathf.Min(nearest,distance);
            return nearest;
        }
        public static Bounds Bounds(Component building)=>Geometry(building).Combined;
        public static LineRenderer CreateRing(Component building)
        {
            var bounds=Bounds(building);
            float radius=new Vector2(bounds.extents.x,bounds.extents.z).magnitude+.35f;
            var ring=VisualFactory.Ring(building.transform,radius,.085f,new Color(.6f,1,.55f));
            ring.name="Whole building selection";ring.useWorldSpace=true;
            for(int i=0;i<ring.positionCount;i++)
            {
                float angle=i*Mathf.PI*2/ring.positionCount;
                float x=bounds.center.x+Mathf.Cos(angle)*radius,z=bounds.center.z+Mathf.Sin(angle)*radius;
                ring.SetPosition(i,new Vector3(x,Mathf.Max(building.transform.position.y,MapLayout.Height(x,z))+.13f,z));
            }
            ring.enabled=false;return ring;
        }
    }
}
