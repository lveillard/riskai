using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    public static class CliffDetails
    {
        public static void Create(Transform parent)
        {
            var root=new GameObject("Fractured escarpment outcrops");root.transform.SetParent(parent,false);
            var resources=GeneratedResourceOwner.For(parent);
            var template=Resources.Load<Material>("PaintedSurface");var stone=resources.Track(new Material(template));
            stone.SetTexture("_Atlas",Resources.Load<Texture2D>("Painted/CliffAtlas-v07"));
            stone.SetVector("_Tile",new Vector4(0,.5f,0,0));stone.SetFloat("_Scale",.17f);
            stone.SetColor("_Tint",new Color(.94f,.99f,1.04f));
            var random=new System.Random(704);var mesh=resources.Track(Shard());
            for(int cliff=0;cliff<MapLayout.Cliffs.Length;cliff++)
            {
                var points=MapLayout.Cliffs[cliff];
                for(int i=0;i<points.Length;i++)
                {
                    Vector2 a=points[i],b=points[(i+1)%points.Length];int count=Mathf.CeilToInt(Vector2.Distance(a,b)/3.7f);
                    for(int j=0;j<count;j++)
                    {
                        var p=Vector2.Lerp(a,b,(j+.5f)/count);float x=p.x*MapLayout.Spacing,z=p.y*MapLayout.Spacing;
                        float y=MapLayout.Height(x,z),h=cliff==0?3.8f:6.2f;
                        if(!MapLayout.IsLand(x,z)||y<h*.20f||y>h*.78f)continue;
                        float slope=Mathf.Max(Mathf.Abs(MapLayout.Height(x+1,z)-MapLayout.Height(x-1,z)),Mathf.Abs(MapLayout.Height(x,z+1)-MapLayout.Height(x,z-1)));
                        if(slope<1.4f)continue;
                        // Keep rubble at the FOOT. Tall intersecting shards used to poke through the grass lip.
                        var downhill=new Vector2(MapLayout.Height(x-1,z)-MapLayout.Height(x+1,z),MapLayout.Height(x,z-1)-MapLayout.Height(x,z+1)).normalized;
                        x+=downhill.x*2.4f;z+=downhill.y*2.4f;
                        if(TerrainHydrology.DistanceToRiver(x,z)<4)continue;
                        var go=new GameObject("Embedded escarpment rubble");go.transform.SetParent(root.transform,false);
                        go.transform.position=new Vector3(x,MapLayout.Height(x,z)-.24f,z);
                        go.transform.localScale=new Vector3(1.25f+(float)random.NextDouble()*.55f,h*.16f,.85f+(float)random.NextDouble()*.45f);
                        go.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=stone;
                    }
                }
            }
            StaticBatchingUtility.Combine(root);
        }
        static Mesh Shard()
        {
            var ring=new Vector2[]{new Vector2(-.62f,-.30f),new Vector2(-.25f,-.60f),new Vector2(.43f,-.36f),new Vector2(.6f,.16f),new Vector2(.12f,.55f),new Vector2(-.55f,.37f)};
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<6;i++)
            {
                Vector2 a=ring[i],b=ring[(i+1)%6];int k=v.Count;
                v.Add(new Vector3(a.x,-.15f,a.y));v.Add(new Vector3(a.x*.56f+.15f,1-(i%3)*.11f,a.y*.56f));
                v.Add(new Vector3(b.x*.56f+.15f,1-((i+1)%3)*.11f,b.y*.56f));v.Add(new Vector3(b.x,-.15f,b.y));
                t.Add(k);t.Add(k+1);t.Add(k+2);t.Add(k);t.Add(k+2);t.Add(k+3);
                k=v.Count;v.Add(new Vector3(a.x*.56f+.15f,1-(i%3)*.11f,a.y*.56f));v.Add(new Vector3(.12f,1.08f,0));v.Add(new Vector3(b.x*.56f+.15f,1-((i+1)%3)*.11f,b.y*.56f));t.Add(k);t.Add(k+1);t.Add(k+2);
            }
            var mesh=new Mesh{name="Fractured slate shard"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
