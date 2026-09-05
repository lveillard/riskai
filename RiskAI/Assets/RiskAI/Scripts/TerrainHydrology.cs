using System.Collections.Generic;
using UnityEngine;
namespace RiskAI
{
    // A single smooth course drives erosion, water, vegetation and the bank material.
    public static class TerrainHydrology
    {
        public static readonly Vector3[] Course={new(54,14.2f,29),new(50,9.2f,33),new(46,6,36),new(43,3.5f,40),new(38,1.4f,43),new(35,.25f,47),new(33,-.24f,50),new(31,-.24f,54)};
        public static readonly Vector3[] Samples=BuildSamples();
        public static Vector3 Point(int i)=>new(Course[i].x*MapLayout.Spacing,Course[i].y,Course[i].z*MapLayout.Spacing);
        static float Tangent(float a,float b)=>a*b<=0?0:2*a*b/(a+b);
        static Vector3[] BuildSamples()
        {
            var result=new Vector3[57];
            for(int s=0;s<7;s++)for(int j=0;j<8;j++)
            {
                float t=j/8f,t2=t*t,t3=t2*t;
                var a=Point(Mathf.Max(0,s-1));var b=Point(s);var c=Point(s+1);var d=Point(Mathf.Min(7,s+2));
                var p=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3);
                // Monotone elevation prevents uphill reaches and artificial steps at control points.
                float delta=c.y-b.y,m0=s==0?delta:Tangent(b.y-a.y,delta),m1=s==6?delta:Tangent(delta,d.y-c.y);
                p.y=(2*t3-3*t2+1)*b.y+(t3-2*t2+t)*m0+(-2*t3+3*t2)*c.y+(t3-t2)*m1;
                result[s*8+j]=p;
            }
            result[56]=Point(7);return result;
        }
        public static float Width(float t)=>Mathf.Lerp(.65f,2.15f,Mathf.SmoothStep(0,1,t))*(1+.07f*Mathf.Sin(t*21));
        public static void Sample(float x,float z,out float distance,out float level,out float width)
        {
            distance=float.MaxValue;level=-.24f;width=0;
            if(x<27*MapLayout.Spacing||x>58*MapLayout.Spacing||z<25*MapLayout.Spacing||z>58*MapLayout.Spacing)return;
            var p=new Vector2(x,z);
            for(int i=0;i<Samples.Length-1;i++)
            {
                var a=Samples[i];var b=Samples[i+1];var ab=new Vector2(b.x-a.x,b.z-a.z);
                float t=Mathf.Clamp01(Vector2.Dot(p-new Vector2(a.x,a.z),ab)/ab.sqrMagnitude);
                float d=(p-new Vector2(a.x,a.z)-ab*t).sqrMagnitude;
                if(d>=distance)continue;distance=d;level=Mathf.Lerp(a.y,b.y,t);width=Width((i+t)/56);
            }
            distance=Mathf.Sqrt(distance);
        }
        public static float DistanceToRiver(float x,float z){Sample(x,z,out float d,out _,out _);return d;}
        public static float Carve(float x,float z,float height)
        {
            Sample(x,z,out float d,out float level,out float width);if(d>width+4)return height;
            float shoulder=Mathf.SmoothStep(0,1,Mathf.InverseLerp(width*.62f,width+3.6f,d));
            float bed=level-.65f+.23f*Mathf.Clamp01(d/Mathf.Max(width,.1f));
            float bank=Mathf.Lerp(Mathf.Max(height,level+.45f),height,Mathf.SmoothStep(0,1,Mathf.InverseLerp(width+1,width+4,d)));
            return Mathf.Lerp(bed,bank,shoulder);
        }
        public static void Create(Transform root)
        {
            var points=new Vector4[57];for(int i=0;i<57;i++){var p=Samples[i];points[i]=new(p.x,p.y,p.z,Width(i/56f));}Shader.SetGlobalVectorArray("_RiskRiver",points);
            var v=new List<Vector3>();var uv=new List<Vector2>();var flow=new List<Vector2>();var triangles=new List<int>();float length=0;const int across=8;
            for(int i=0;i<57;i++)
            {
                var p=Samples[i];var before=Samples[Mathf.Max(0,i-1)];var after=Samples[Mathf.Min(56,i+1)];var dir=after-before;
                float slope=Mathf.Abs(dir.y)/Mathf.Max(.1f,new Vector2(dir.x,dir.z).magnitude);dir.y=0;var side=Vector3.Cross(Vector3.up,dir.normalized);
                float progress=i/56f,width=Width(progress);if(i>0)length+=Vector3.Distance(p,before);
                for(int j=0;j<=across;j++)
                {
                    float u=j/(float)across;v.Add(p+side*((u*2-1)*width)+Vector3.up*.025f);uv.Add(new(u,length));flow.Add(new(slope,progress));
                    if(i==56||j==across)continue;int k=i*(across+1)+j,n=k+across+1;
                    triangles.Add(k);triangles.Add(n);triangles.Add(k+1);triangles.Add(k+1);triangles.Add(n);triangles.Add(n+1);
                }
            }
            var mesh=new Mesh{name="Continuous spring, rapids and estuary"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetUVs(1,flow);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Río de la Sierra · cauce erosionado");go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Cascade");
            for(int i=0;i<24;i++)
            {
                int s=2+i*2;var p=Samples[s];var dir=Samples[s+1]-Samples[s];dir.y=0;p+=Vector3.Cross(Vector3.up,dir.normalized)*(i%2==0?1:-1)*(Width(s/56f)+.5f);
                if(!MapLayout.IsLand(p.x,p.z))continue;p.y=MapLayout.Height(p.x,p.z)-.13f;WorldArt.Rock(root,p,.42f+(i%4)*.13f,210+i);
            }
        }
    }
}
