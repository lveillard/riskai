using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Replaces the KayKit two-handed crossbow of the Ballestero with one shared
    /// procedural crossbow: shaped stock, curved recurve prod, cocked string,
    /// stirrup, steel fittings and a loaded bolt. It keeps the source weapon's
    /// hand slot transform, so idle/aim/shoot clips hold it exactly as before, and
    /// samples the unit's own palette texture so it adds no material or draw state.
    /// </summary>
    public static class CrossbowView
    {
        const string SourceName="2H_Crossbow",ViewName="Procedural crossbow";
        static Mesh mesh;

        // Palette cells of rogue_texture.png (KayKit atlas used by the unit material).
        static readonly Vector2 Wood=new Vector2(.176f,.912f),
            Steel=new Vector2(.42f,.893f),Iron=new Vector2(.547f,.863f),Cord=new Vector2(.049f,.414f),
            Leather=new Vector2(.80f,.883f),Black=new Vector2(.293f,.853f);

        public static void Apply(GameObject model)
        {
            if(!model)return;
            Transform source=null;
            foreach(var child in model.GetComponentsInChildren<Transform>(true))if(child.name==SourceName){source=child;break;}
            if(!source||!source.parent||source.parent.Find(ViewName))return;
            var sourceRenderer=source.GetComponent<Renderer>();
            var material=sourceRenderer?sourceRenderer.sharedMaterial:null;
            if(!material)return;
            source.gameObject.SetActive(false);
            var view=new GameObject(ViewName);view.transform.SetParent(source.parent,false);
            view.transform.SetLocalPositionAndRotation(source.localPosition,source.localRotation);view.transform.localScale=source.localScale;
            view.AddComponent<MeshFilter>().sharedMesh=SharedMesh;
            var renderer=view.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            if(sourceRenderer)renderer.shadowCastingMode=sourceRenderer.shadowCastingMode;
        }

        /// <summary>Weapon space of the source slot: -X shoots forward, +Y up, Z across the prod.</summary>
        public static Mesh SharedMesh => mesh?mesh:mesh=Build();

        sealed class Builder
        {
            public readonly List<Vector3> V=new List<Vector3>();
            public readonly List<Vector3> N=new List<Vector3>();
            public readonly List<Vector2> U=new List<Vector2>();
            public readonly List<int> T=new List<int>();
            Vector3 center;
            void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2 uv)
            {
                var n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude<1e-12f)n=Vector3.Cross(c-a,d-a);n.Normalize();
                // Unity front faces wind clockwise; always face away from the solid's centre.
                if(Vector3.Dot(n,(a+b+c+d)*.25f-center)<0){var swap=b;b=d;d=swap;n=-n;}
                int k=V.Count;V.Add(a);V.Add(b);V.Add(c);V.Add(d);for(int i=0;i<4;i++){N.Add(n);U.Add(uv);}
                T.Add(k);T.Add(k+1);T.Add(k+2);T.Add(k);T.Add(k+2);T.Add(k+3);
            }
            /// <summary>Hexahedron from 8 corners: p[0..3] bottom (ccw seen from above), p[4..7] top.</summary>
            public void Hex(Vector3[] p,Vector2 uv)
            {
                center=Vector3.zero;for(int i=0;i<8;i++)center+=p[i]*.125f;
                Face(p[0],p[1],p[2],p[3],uv);           // bottom
                Face(p[4],p[7],p[6],p[5],uv);           // top
                Face(p[0],p[4],p[5],p[1],uv);
                Face(p[1],p[5],p[6],p[2],uv);
                Face(p[2],p[6],p[7],p[3],uv);
                Face(p[3],p[7],p[4],p[0],uv);
            }
            public void Box(Vector3 center,Vector3 size,Vector2 uv) => Box(center,size,Quaternion.identity,uv);
            public void Box(Vector3 center,Vector3 size,Quaternion rotation,Vector2 uv)
            {
                Vector3 h=size*.5f;var p=new Vector3[8];
                Vector3[] corners={new(-1,-1,-1),new(-1,-1,1),new(1,-1,1),new(1,-1,-1),new(-1,1,-1),new(-1,1,1),new(1,1,1),new(1,1,-1)};
                for(int i=0;i<8;i++)p[i]=center+rotation*Vector3.Scale(corners[i],h);
                Hex(p,uv);
            }
            /// <summary>Square-section bar between two points.</summary>
            public void Bar(Vector3 from,Vector3 to,float width,float height,Vector2 uv)
            {
                var axis=to-from;var rotation=Quaternion.LookRotation(axis.normalized,Mathf.Abs(Vector3.Dot(axis.normalized,Vector3.up))>.95f?Vector3.right:Vector3.up);
                Box((from+to)*.5f,new Vector3(width,height,axis.magnitude),rotation,uv);
            }
            /// <summary>Tapered swept limb through a polyline.</summary>
            public void Sweep(IList<Vector3> path,float thick0,float thick1,float tall0,float tall1,Vector2 uv)
            {
                for(int i=0;i<path.Count-1;i++)
                {
                    float t0=i/(float)(path.Count-1),t1=(i+1)/(float)(path.Count-1);
                    Vector3 a=path[i],b=path[i+1];
                    Vector3 ta=Tangent(path,i),tb=Tangent(path,i+1);
                    Vector3 sa=Vector3.Cross(Vector3.up,ta).normalized,sb=Vector3.Cross(Vector3.up,tb).normalized;
                    float wa=Mathf.Lerp(thick0,thick1,t0)*.5f,wb=Mathf.Lerp(thick0,thick1,t1)*.5f;
                    float ha=Mathf.Lerp(tall0,tall1,t0)*.5f,hb=Mathf.Lerp(tall0,tall1,t1)*.5f;
                    var p=new[]{a-sa*wa-Vector3.up*ha,b-sb*wb-Vector3.up*hb,b+sb*wb-Vector3.up*hb,a+sa*wa-Vector3.up*ha,
                                a-sa*wa+Vector3.up*ha,b-sb*wb+Vector3.up*hb,b+sb*wb+Vector3.up*hb,a+sa*wa+Vector3.up*ha};
                    Hex(p,uv);
                }
            }
            static Vector3 Tangent(IList<Vector3> path,int i)
            {
                var t=path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(i-1,0)];return t.normalized;
            }
        }

        static Mesh Build()
        {
            var b=new Builder();
            // Chunky proportions (KayKit style) so the silhouette survives the RTS camera.
            // Stock: a butt that drops below the tiller line, a wrist and the long
            // tiller running forward to the prod.
            b.Hex(new[]{new Vector3(.36f,-.17f,-.085f),new Vector3(.36f,-.17f,.085f),new Vector3(.02f,-.07f,.075f),new Vector3(.02f,-.07f,-.075f),
                        new Vector3(.36f,.08f,-.085f),new Vector3(.36f,.08f,.085f),new Vector3(.02f,.1f,.075f),new Vector3(.02f,.1f,-.075f)},Wood);
            b.Box(new Vector3(.37f,-.045f,0),new Vector3(.035f,.27f,.18f),Leather);                    // butt plate
            b.Hex(new[]{new Vector3(.02f,-.07f,-.075f),new Vector3(.02f,-.07f,.075f),new Vector3(-.86f,-.02f,.065f),new Vector3(-.86f,-.02f,-.065f),
                        new Vector3(.02f,.1f,-.075f),new Vector3(.02f,.1f,.075f),new Vector3(-.86f,.1f,.065f),new Vector3(-.86f,.1f,-.065f)},Wood);
            // Iron trigger lever under the tiller and the revolving nut that holds the string.
            b.Bar(new Vector3(-.08f,-.05f,0),new Vector3(.2f,-.17f,0),.035f,.04f,Iron);
            b.Box(new Vector3(-.3f,.125f,0),new Vector3(.09f,.06f,.11f),Iron);
            // Steel side plates and the bridle binding the prod to the tiller.
            b.Box(new Vector3(-.3f,.045f,0),new Vector3(.18f,.1f,.135f),Steel);
            b.Box(new Vector3(-.84f,.05f,0),new Vector3(.17f,.17f,.2f),Leather);
            b.Box(new Vector3(-.84f,.05f,0),new Vector3(.05f,.19f,.22f),Iron);
            // Steel recurve prod: swept limbs curving back towards the shooter, iron nocks.
            const float prodX=-.86f,prodY=.05f;
            var tips=new Vector3[2];
            for(int side=-1;side<=1;side+=2)
            {
                var path=new List<Vector3>();
                for(int i=0;i<=8;i++)
                {
                    float t=i/8f,z=side*(.06f+t*.56f);
                    // Deep curvature near the tip; a slight recurve flick at the end.
                    float x=prodX+.22f*t*t+(t>.85f?-.14f*(t-.85f):0);
                    path.Add(new Vector3(x,prodY,z));
                }
                b.Sweep(path,.15f,.06f,.12f,.07f,Steel);
                var tip=path[path.Count-1];tips[(side+1)/2]=tip;
                b.Box(tip,new Vector3(.09f,.1f,.06f),Iron);
            }
            // Cocked string running from both nocks to the nut.
            var nut=new Vector3(-.3f,.12f,0);
            for(int i=0;i<2;i++)b.Bar(tips[i]+Vector3.up*.01f,nut+new Vector3(0,0,(i==0?-1:1)*.015f),.02f,.02f,Cord);
            // Loaded bolt in the groove: dark shaft, steel head, light fletching.
            b.Bar(new Vector3(-.3f,.135f,0),new Vector3(-.98f,.135f,0),.03f,.03f,Black);
            b.Hex(new[]{new Vector3(-.98f,.115f,-.03f),new Vector3(-.98f,.115f,.03f),new Vector3(-1.1f,.135f,.003f),new Vector3(-1.1f,.135f,-.003f),
                        new Vector3(-.98f,.155f,-.03f),new Vector3(-.98f,.155f,.03f),new Vector3(-1.1f,.138f,.003f),new Vector3(-1.1f,.138f,-.003f)},Steel);
            b.Box(new Vector3(-.37f,.16f,0),new Vector3(.1f,.05f,.008f),Cord);
            // Iron stirrup in front of the prod, used to span the bow with a foot.
            float sx0=-.9f,sx1=-1.1f,sy=-.01f,sz=.085f;
            b.Bar(new Vector3(sx0,sy,-sz),new Vector3(sx1,sy-.035f,-sz),.035f,.035f,Iron);
            b.Bar(new Vector3(sx0,sy,sz),new Vector3(sx1,sy-.035f,sz),.035f,.035f,Iron);
            b.Bar(new Vector3(sx1,sy-.035f,-sz-.017f),new Vector3(sx1,sy-.035f,sz+.017f),.038f,.038f,Iron);

            var result=new Mesh{name="Shared procedural crossbow"};
            result.SetVertices(b.V);result.SetNormals(b.N);result.SetUVs(0,b.U);result.SetTriangles(b.T,0);result.RecalculateBounds();
            return result;
        }
    }
}
