using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Shared procedural meshes for the tower that rises from integrated city and
    /// harbor buildings. Every mesh is built once per process and reused by all
    /// towers, so a city adds a handful of renderers and no per-instance geometry.
    /// Dimensions are model units, scaled by <see cref="VisualMetrics.TowerScale"/>.
    /// </summary>
    public static class TowerArt
    {
        /// <summary>Outer radius of the corbelled fighting platform and its parapet: the keep's widest point.</summary>
        public const float CrownRadius=1.2f;
        public const float BaseTop=1.5f,ShaftRadius=.95f,ShaftTop=5.8f,WalkHeight=6.25f,ParapetTop=6.55f,CrownTop=6.95f;
        public const float RoofBase=6.3f,RoofRadius=1.0f,RoofHeight=1.9f,MastTop=9.15f;
        const int Sides=12;

        static Mesh stone,darkStone,slits,roof,pennant;
        public static Mesh Stone => stone?stone:stone=Build("Integrated tower stone",BuildStone);
        public static Mesh DarkStone => darkStone?darkStone:darkStone=Build("Integrated tower dark footing",BuildDarkStone);
        public static Mesh ArrowSlits => slits?slits:slits=Build("Integrated tower arrow slits",BuildSlits);
        public static Mesh Roof => roof?roof:roof=Build("Integrated tower conical roof",BuildRoof);
        public static Mesh Pennant => pennant?pennant:pennant=Build("Integrated tower pennant",BuildPennant);

        sealed class Builder
        {
            public readonly List<Vector3> V=new List<Vector3>();
            public readonly List<Vector3> N=new List<Vector3>();
            public readonly List<int> T=new List<int>();
            public Vector3 Center;
            /// <summary>Adds a quad whose front face (Unity: clockwise) looks along its normals.</summary>
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 na,Vector3 nb,Vector3 nc,Vector3 nd)
            {
                bool flip=Vector3.Dot(Vector3.Cross(b-a,c-a),na+nb+nc+nd)<0;
                int k=V.Count;V.Add(a);V.Add(b);V.Add(c);V.Add(d);N.Add(na);N.Add(nb);N.Add(nc);N.Add(nd);
                if(flip){T.Add(k);T.Add(k+2);T.Add(k+1);T.Add(k);T.Add(k+3);T.Add(k+2);}
                else{T.Add(k);T.Add(k+1);T.Add(k+2);T.Add(k);T.Add(k+2);T.Add(k+3);}
            }
            /// <summary>Flat quad facing away from <see cref="Center"/>.</summary>
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                var n=Vector3.Cross(b-a,c-a).normalized;
                if(Vector3.Dot(n,(a+b+c+d)*.25f-Center)<0)n=-n;
                Quad(a,b,c,d,n,n,n,n);
            }
            static Vector3 Ring(float angle,float radius,float y)=>new Vector3(Mathf.Sin(angle)*radius,y,Mathf.Cos(angle)*radius);
            /// <summary>Smooth outward side of a (possibly tapered) round wall.</summary>
            public void Frustum(float r0,float y0,float r1,float y1,bool inward=false)
            {
                float slope=(r0-r1)/Mathf.Max(.0001f,y1-y0);
                for(int i=0;i<Sides;i++)
                {
                    float a=i*Mathf.PI*2/Sides,b=(i+1)*Mathf.PI*2/Sides;
                    Vector3 na=new Vector3(Mathf.Sin(a),slope,Mathf.Cos(a)).normalized,nb=new Vector3(Mathf.Sin(b),slope,Mathf.Cos(b)).normalized;
                    if(inward)Quad(Ring(b,r0,y0),Ring(b,r1,y1),Ring(a,r1,y1),Ring(a,r0,y0),-nb,-nb,-na,-na);
                    else Quad(Ring(a,r0,y0),Ring(a,r1,y1),Ring(b,r1,y1),Ring(b,r0,y0),na,na,nb,nb);
                }
            }
            /// <summary>Horizontal ring between two radii; up=true faces the sky.</summary>
            public void Annulus(float inner,float outer,float y,bool up)
            {
                var n=up?Vector3.up:Vector3.down;
                for(int i=0;i<Sides;i++)
                {
                    float a=i*Mathf.PI*2/Sides,b=(i+1)*Mathf.PI*2/Sides;
                    if(up)Quad(Ring(a,inner,y),Ring(a,outer,y),Ring(b,outer,y),Ring(b,inner,y),n,n,n,n);
                    else Quad(Ring(b,inner,y),Ring(b,outer,y),Ring(a,outer,y),Ring(a,inner,y),n,n,n,n);
                }
            }
            /// <summary>Oriented box facing outward from the tower axis at the given angle.</summary>
            public void Box(float angle,float radius,float y0,float y1,float width,float depth)
            {
                var outward=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));var side=new Vector3(outward.z,0,-outward.x);
                Vector3 c=outward*radius;float w=width*.5f,d=depth*.5f;
                Vector3 P(float s,float o,float y)=>c+side*s+outward*o+Vector3.up*y;
                Center=c+Vector3.up*((y0+y1)*.5f);
                Quad(P(-w,d,y0),P(-w,d,y1),P(w,d,y1),P(w,d,y0));      // outer face
                Quad(P(w,-d,y0),P(w,-d,y1),P(-w,-d,y1),P(-w,-d,y0));  // inner face
                Quad(P(w,d,y0),P(w,d,y1),P(w,-d,y1),P(w,-d,y0));      // side
                Quad(P(-w,-d,y0),P(-w,-d,y1),P(-w,d,y1),P(-w,d,y0));  // side
                Quad(P(-w,-d,y1),P(w,-d,y1),P(w,d,y1),P(-w,d,y1));    // top
            }
        }

        static Mesh Build(string name,System.Action<Builder> fill)
        {
            var builder=new Builder();fill(builder);
            // RISKAI_SHARED_ASSET: five shared tower meshes, each built once (static lazy fields).
            var mesh=new Mesh{name=name};
            mesh.SetVertices(builder.V);mesh.SetNormals(builder.N);mesh.SetTriangles(builder.T,0);mesh.RecalculateBounds();
            return mesh;
        }

        static void BuildStone(Builder b)
        {
            b.Frustum(ShaftRadius,BaseTop,ShaftRadius,ShaftTop);
            // Corbelled machicolation flare carrying the wider fighting platform.
            b.Frustum(ShaftRadius,ShaftTop,CrownRadius,6.05f);
            b.Annulus(0,CrownRadius,6.05f,false);
            b.Frustum(CrownRadius,6.05f,CrownRadius,ParapetTop);
            b.Annulus(1.02f,CrownRadius,ParapetTop,true);
            b.Frustum(1.02f,WalkHeight,1.02f,ParapetTop,true);
            b.Annulus(0,1.02f,WalkHeight,true);
            // Eight merlons with open crenels between them.
            for(int i=0;i<8;i++)b.Box((i+.5f)*Mathf.PI/4,1.11f,ParapetTop-.01f,CrownTop,.44f,.2f);
        }

        static void BuildDarkStone(Builder b)
        {
            // Battered footing: wider, darker masonry that anchors the shaft.
            b.Frustum(1.22f,0,1.04f,BaseTop);
            b.Annulus(ShaftRadius,1.04f,BaseTop,true);
            // A string course marks the storey that rises above the city roof.
            b.Frustum(1.02f,3.95f,1.02f,4.13f);
            b.Annulus(ShaftRadius,1.02f,4.13f,true);
            b.Annulus(ShaftRadius,1.02f,3.95f,false);
        }

        static void BuildSlits(Builder b)
        {
            // Tall narrow loopholes: diagonals on the upper storey, flanks lower down.
            for(int i=0;i<4;i++)b.Box((i+.5f)*Mathf.PI/2,ShaftRadius+.005f,4.55f,5.3f,.13f,.07f);
            for(int i=0;i<3;i++)b.Box(i*Mathf.PI/2,ShaftRadius+.005f,2.6f,3.25f,.12f,.07f);
        }

        static void BuildRoof(Builder b)
        {
            const int sides=10;
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,c=(i+1)*Mathf.PI*2/sides;
                Vector3 pa=new Vector3(Mathf.Sin(a)*RoofRadius,RoofBase,Mathf.Cos(a)*RoofRadius),pc=new Vector3(Mathf.Sin(c)*RoofRadius,RoofBase,Mathf.Cos(c)*RoofRadius);
                var tip=Vector3.up*(RoofBase+RoofHeight);
                float slope=RoofRadius/RoofHeight;
                Vector3 na=new Vector3(Mathf.Sin(a),slope,Mathf.Cos(a)).normalized,nc=new Vector3(Mathf.Sin(c),slope,Mathf.Cos(c)).normalized;
                int k=b.V.Count;b.V.Add(pa);b.V.Add(tip);b.V.Add(pc);b.N.Add(na);b.N.Add(((na+nc)*.5f).normalized);b.N.Add(nc);
                // Clockwise seen from outside.
                if(Vector3.Dot(Vector3.Cross(tip-pa,pc-pa),na+nc)<0){b.T.Add(k);b.T.Add(k+2);b.T.Add(k+1);}else{b.T.Add(k);b.T.Add(k+1);b.T.Add(k+2);}
            }
        }

        static void BuildPennant(Builder b)
        {
            // Swallow-tailed pennant on the mast, double-sided for any camera yaw.
            float y0=MastTop-.55f,y1=MastTop-.08f;
            Vector3[] p={new Vector3(0,y1,0),new Vector3(1.05f,y1-.05f,0),new Vector3(.78f,(y0+y1)*.5f,0),new Vector3(1.05f,y0+.05f,0),new Vector3(0,y0,0)};
            foreach(var n in new[]{Vector3.back,Vector3.forward})
            {
                int k=b.V.Count;for(int i=0;i<p.Length;i++){b.V.Add(p[i]+n*.006f);b.N.Add(n);}
                int[] tris={0,1,2,0,2,4,4,2,3};
                for(int i=0;i<tris.Length;i+=3)
                {
                    Vector3 a=p[tris[i]],c=p[tris[i+1]],d=p[tris[i+2]];
                    bool front=Vector3.Dot(Vector3.Cross(c-a,d-a),n)>0;
                    b.T.Add(k+tris[i]);b.T.Add(k+(front?tris[i+1]:tris[i+2]));b.T.Add(k+(front?tris[i+2]:tris[i+1]));
                }
            }
        }
    }
}
