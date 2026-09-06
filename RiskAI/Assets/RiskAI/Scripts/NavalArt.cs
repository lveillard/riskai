using System.Collections.Generic;
using UnityEngine;
namespace RiskAI
{
    public static class NavalArt
    {
        static GameObject Block(Transform root,string name,Vector3 p,Vector3 size,int tile=2,Color? tint=null)
        {
            var go=VisualFactory.Shape(root,PrimitiveType.Cube,name,p,size,Color.white);go.GetComponent<Renderer>().sharedMaterial=WorldArt.Painted(tile,tint);return go;
        }
        static void Beam(Transform root,Vector3 a,Vector3 b,float radius,Color? tint=null)
        {
            var go=Block(root,"Rigging and oak spars",(a+b)*.5f,new(radius,(b-a).magnitude,radius),2,tint);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        public static void CreateShip(Ship ship)
        {
            var root=new GameObject("Ship model").transform;root.SetParent(ship.transform,false);bool war=ship.Kind==ShipKind.Galley;float length=war?7.2f:5.15f,width=war?1.65f:2.65f;
            var v=new List<Vector3>();var t=new List<int>();const int sections=12;
            for(int level=0;level<3;level++)for(int s=0;s<=sections;s++)
            {
                float angle=s*Mathf.PI*2/sections,beam=level==0?.45f:level==1?1:1.02f;
                v.Add(new(Mathf.Sin(angle)*width*.5f*beam,level==0?-.22f:level==1?.32f:.7f,Mathf.Cos(angle)*length*.5f*(level==0?.72f:1)));
                if(level==2||s==sections)continue;int i=level*(sections+1)+s,b=i+sections+1;t.Add(i);t.Add(b);t.Add(b+1);t.Add(i);t.Add(b+1);t.Add(i+1);
            }
            var mesh=new Mesh{name="Carvel planked ship hull"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var hull=new GameObject("Oak hull");hull.transform.SetParent(root,false);hull.AddComponent<MeshFilter>().sharedMesh=mesh;hull.AddComponent<MeshRenderer>().sharedMaterial=WorldArt.Painted(2,new Color(.9f,.67f,.36f),.8f);
            Block(root,"Planked deck",new(0,.48f,0),new(width*.86f,.13f,length*.68f),2,new Color(1.35f,1.12f,.75f));
            for(int side=-1;side<=1;side+=2)
            {
                Beam(root,new(side*width*.43f,.74f,-length*.32f),new(side*width*.43f,.74f,length*.32f),.13f,new Color(1.7f,1.2f,.55f));
                if(war)for(int i=0;i<5;i++)Beam(root,new(side*.62f,.38f,-1.55f+i*.7f),new(side*1.75f,.08f,-1.9f+i*.7f),.075f,new Color(1.35f,1.1f,.72f));
                Beam(root,new(0,4.3f,0),new(side*.7f,.7f,-1.5f),.025f,new Color(1.7f,1.5f,1));
            }
            Beam(root,new(0,.5f,-.15f),new(0,4.8f,-.15f),.14f,new Color(1.1f,.8f,.48f));
            Beam(root,new(-1.42f,4.35f,-.15f),new(1.42f,4.35f,-.15f),.11f);
            Sail(root,ship.Team,war);
            Block(root,"Raised stern",new(0,.8f,-length*.33f),new(width*.78f,.3f,.85f));
            if(war)
            {
                var cannon=VisualFactory.Shape(root,PrimitiveType.Cylinder,"Deck ballista",new(0,.94f,length*.23f),new(.3f,.6f,.3f),new Color(.21f,.26f,.28f));cannon.transform.localRotation=Quaternion.Euler(90,0,0);
                Beam(root,new(-.7f,.95f,length*.22f),new(.7f,.95f,length*.22f),.12f);
            }
            else for(int i=0;i<6;i++)Block(root,"Transport cargo hold",new(-.85f+i%3*.85f,.88f,-1.15f+i/3*.8f),new(.68f,.55f,.62f));
            Beam(root,new(0,.65f,length*.35f),new(0,1.05f,length*.58f),.13f,new Color(1.6f,1.15f,.4f));
            var collider=ship.gameObject.AddComponent<BoxCollider>();collider.center=new(0,1.3f,0);collider.size=new(width,3,length*.82f);collider.isTrigger=true;
            var visual=ship.gameObject.AddComponent<ShipAppearance>();visual.Initialize(ship,root,VisualFactory.Ring(ship.transform,1.5f,.07f,new Color(.5f,1,.55f)));
        }
        static void Sail(Transform root,int team,bool war)
        {
            var v=new List<Vector3>();var t=new List<int>();const int nx=8,ny=6;
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
            {
                float u=x/(float)nx,f=y/(float)ny,w=Mathf.Lerp(1.08f,1.36f,f);
                v.Add(new((u*2-1)*w,Mathf.Lerp(1.55f,4.3f,f),-.15f+.55f*Mathf.Sin(u*Mathf.PI)*Mathf.Sin(f*Mathf.PI)));
                if(x==nx||y==ny)continue;int k=y*(nx+1)+x,b=k+nx+1;t.Add(k);t.Add(b);t.Add(b+1);t.Add(k);t.Add(b+1);t.Add(k+1);
            }
            var mesh=new Mesh{name="Wind filled team sail"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();
            var go=new GameObject("Team sail");go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var cloth=new Material(VisualFactory.Mat(Color.Lerp(VisualFactory.TeamColor(team),Color.white,war?.08f:.35f)));cloth.SetFloat("_Cull",0);go.AddComponent<MeshRenderer>().sharedMaterial=cloth;
            for(int side=-1;side<=1;side+=2)Beam(root,new(side*1.08f,1.55f,-.15f),new(side*1.36f,4.3f,-.15f),.045f,new Color(1.9f,1.6f,1));
            // Ivory standard reads at the strategic zoom without covering the team-coloured cloth.
            Block(root,"Sail heraldry",new(0,2.9f,.405f),new(.2f,1.15f,.025f),0,new Color(1.6f,1.5f,1.15f));
            Block(root,"Sail heraldry crossbar",new(0,3.1f,.405f),new(.8f,.18f,.025f),0,new Color(1.6f,1.5f,1.15f));
        }
        public static BuildingEntranceAnchor CreateHarbor(Harbor harbor)
        {
            var root=new GameObject("Harbor architecture").transform;root.SetParent(harbor.transform,false);root.position=harbor.Landing;
            var direction=harbor.Berth-harbor.Landing;direction.y=0;root.rotation=Quaternion.LookRotation(direction);float length=direction.magnitude;
            for(int i=0;i<18;i++)Block(root,"Pier oak plank",new(0,.15f+i/18f*(-harbor.Landing.y+.55f),i*length/18),new(2.9f,.19f,length/18*.86f),2,new Color(1.25f,1.08f,.74f));
            for(int i=0;i<4;i++)for(int side=-1;side<=1;side+=2)
            {
                float z=i*length/3,top=.72f+i/3f*(-harbor.Landing.y+.55f),baseY=-harbor.Landing.y-1.1f;
                Block(root,"Pier pile",new(side*1.28f,(top+baseY)*.5f,z),new(.27f,top-baseY,.27f));
                Block(root,"Mooring iron",new(side*1.28f,top-.05f,z),new(.32f,.13f,.32f),2,new Color(.4f,.45f,.48f));
            }
            Block(root,"Harbormaster stone foundation",new(-3,.2f,-.3f),new(3.2f,.4f,3),0);
            Block(root,"Timber boathouse",new(-3,1.2f,-.3f),new(2.65f,1.8f,2.5f));
            // This lies on the boathouse's landward facade, not at the naval landing/spawn point.
            var entrance=BuildingEntranceAnchor.Create(root,"Boathouse entrance anchor",new(-3,0,-1.55f),Vector3.back);
            var roof=VisualFactory.Cone(root,"Port team roof",new(-3,2.1f,-.3f),2.1f,1.4f,Color.white,4,45);roof.GetComponent<Renderer>().sharedMaterial=WorldArt.RoofMaterial(harbor.Owner);
            for(int i=0;i<3;i++)Block(root,"Dockside cargo",new(2.1f+i%2*.7f,.35f,-.3f+i/2*.7f),new(.6f,.7f,.6f),2,new Color(1.2f,1,.65f));
            Beam(root,new(-1.35f,0,0),new(-1.35f,4.1f,0),.12f);
            var flag=Block(root,"Harbor standard",new(-.8f,3.5f,0),new(1.05f,.78f,.07f),0);
            var collider=harbor.gameObject.AddComponent<BoxCollider>();collider.center=harbor.transform.InverseTransformPoint(harbor.Landing)+Vector3.up;collider.size=new(9,4,7);collider.isTrigger=true;
            var appearance=harbor.gameObject.AddComponent<HarborAppearance>();appearance.Initialize(harbor,roof.GetComponent<Renderer>(),flag.GetComponent<Renderer>());
            return entrance;
        }
    }
    public sealed class ShipAppearance:MonoBehaviour
    {
        Ship ship;Transform model;LineRenderer ring;public void Initialize(Ship s,Transform m,LineRenderer r){ship=s;model=m;ring=r;ring.enabled=false;}
        void LateUpdate(){if(!ship)return;ring.enabled=ship.Selected;model.localPosition=Vector3.up*(.025f+Mathf.Sin(Time.time*1.9f+GetInstanceID())*.045f);model.localRotation=Quaternion.Euler(Mathf.Sin(Time.time*1.2f)*.7f,0,Mathf.Sin(Time.time*1.6f)*1.2f);}
    }
    public sealed class HarborAppearance:MonoBehaviour
    {
        Harbor harbor;Renderer roof,flag;int owner=-9;public void Initialize(Harbor h,Renderer r,Renderer f){harbor=h;roof=r;flag=f;Refresh();}
        void Update(){if(owner!=harbor.Owner)Refresh();}void Refresh(){owner=harbor.Owner;roof.sharedMaterial=WorldArt.RoofMaterial(owner);flag.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamColor(owner));}
    }
}
