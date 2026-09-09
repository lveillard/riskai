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
            var root=CreateShipModel(ship.transform,ship.Team,ship.Kind);bool war=ship.Kind==ShipKind.Galley;float length=war?7.2f:5.15f,width=war?1.65f:2.65f;
            var collider=ship.gameObject.AddComponent<BoxCollider>();collider.center=new(0,1.3f,0);collider.size=new(width,3,length*.82f);collider.isTrigger=true;
            var visual=ship.gameObject.AddComponent<ShipAppearance>();visual.Initialize(ship,root,VisualFactory.Ring(ship.transform,ShipAppearance.SelectionRadius,.09f,new Color(.5f,1,.55f)));
        }
        // The editor portrait generator calls the same model builder as live ships, so UI art
        // cannot drift from the silhouettes and team treatment seen in the world.
        public static Transform CreateShipModel(Transform parent,int team,ShipKind kind)
        {
            var root=new GameObject("Ship model").transform;root.SetParent(parent,false);var resources=GeneratedResourceOwner.For(parent);bool war=kind==ShipKind.Galley;float length=war?7.2f:5.15f,width=war?1.65f:2.65f;
            var v=new List<Vector3>();var t=new List<int>();const int sections=12;
            for(int level=0;level<3;level++)for(int s=0;s<=sections;s++)
            {
                float angle=s*Mathf.PI*2/sections,beam=level==0?.45f:level==1?1:1.02f;
                v.Add(new(Mathf.Sin(angle)*width*.5f*beam,level==0?-.22f:level==1?.32f:.7f,Mathf.Cos(angle)*length*.5f*(level==0?.72f:1)));
                if(level==2||s==sections)continue;int i=level*(sections+1)+s,b=i+sections+1;t.Add(i);t.Add(b);t.Add(b+1);t.Add(i);t.Add(b+1);t.Add(i+1);
            }
            var mesh=resources.Track(new Mesh{name="Carvel planked ship hull"});mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
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
            Sail(root,resources,team,war);
            Block(root,"Raised stern",new(0,.8f,-length*.33f),new(width*.78f,.3f,.85f));
            if(war)
            {
                var cannon=VisualFactory.Shape(root,PrimitiveType.Cylinder,"Deck ballista",new(0,.94f,length*.23f),new(.3f,.6f,.3f),new Color(.21f,.26f,.28f));cannon.transform.localRotation=Quaternion.Euler(90,0,0);
                Beam(root,new(-.7f,.95f,length*.22f),new(.7f,.95f,length*.22f),.12f);
            }
            else for(int i=0;i<6;i++)Block(root,"Transport cargo hold",new(-.85f+i%3*.85f,.88f,-1.15f+i/3*.8f),new(.68f,.55f,.62f));
            Beam(root,new(0,.65f,length*.35f),new(0,1.05f,length*.58f),.13f,new Color(1.6f,1.15f,.4f));
            return root;
        }
        static void Sail(Transform root,GeneratedResourceOwner resources,int team,bool war)
        {
            var v=new List<Vector3>();var t=new List<int>();const int nx=8,ny=6;
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
            {
                float u=x/(float)nx,f=y/(float)ny,w=Mathf.Lerp(1.08f,1.36f,f);
                v.Add(new((u*2-1)*w,Mathf.Lerp(1.55f,4.3f,f),-.15f+.55f*Mathf.Sin(u*Mathf.PI)*Mathf.Sin(f*Mathf.PI)));
                if(x==nx||y==ny)continue;int k=y*(nx+1)+x,b=k+nx+1;t.Add(k);t.Add(b);t.Add(b+1);t.Add(k);t.Add(b+1);t.Add(k+1);
            }
            var mesh=resources.Track(new Mesh{name="Wind filled team sail"});mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();
            var go=new GameObject("Team sail");go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var cloth=resources.Track(new Material(VisualFactory.Mat(Color.Lerp(VisualFactory.TeamColor(team),Color.white,war?.08f:.35f))));cloth.SetFloat("_Cull",0);go.AddComponent<MeshRenderer>().sharedMaterial=cloth;
            for(int side=-1;side<=1;side+=2)Beam(root,new(side*1.08f,1.55f,-.15f),new(side*1.36f,4.3f,-.15f),.045f,new Color(1.9f,1.6f,1));
            // Ivory standard reads at the strategic zoom without covering the team-coloured cloth.
            Block(root,"Sail heraldry",new(0,2.9f,.405f),new(.2f,1.15f,.025f),0,new Color(1.6f,1.5f,1.15f));
            Block(root,"Sail heraldry crossbar",new(0,3.1f,.405f),new(.8f,.18f,.025f),0,new Color(1.6f,1.5f,1.15f));
        }
        public sealed class HarborVisual
        {
            public readonly Transform Root;public readonly BuildingEntranceAnchor Entrance;public readonly Renderer Roof,Flag;
            public HarborVisual(Transform root,BuildingEntranceAnchor entrance,Renderer roof,Renderer flag){Root=root;Entrance=entrance;Roof=roof;Flag=flag;}
        }
        public static void CreatePierDeck(Transform parent,Vector3 from,Vector3 to,float width,string label,bool walkable,bool trim=true,float surfaceLift=0)
        {
            var direction=to-from;float length=Mathf.Max(width,direction.magnitude+2);
            var rotation=direction.sqrMagnitude>.02f?Quaternion.LookRotation(direction):Quaternion.identity;
            var go=new GameObject(label);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation((from+to)*.5f,rotation);
            go.layer=MapLayout.TerrainLayer;
            int count=Mathf.Max(2,Mathf.CeilToInt(length/.42f));float step=length/count;
            var vertices=new List<Vector3>(count*8);var triangles=new List<int>(count*36);
            int[] box={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
            for(int i=0;i<count;i++)
            {
                float z=-length*.5f+(i+.5f)*step,a=z-step*.46f,b=z+step*.46f,x=width*.5f;int offset=vertices.Count;
                vertices.AddRange(new[]{new Vector3(-x,-.22f,a),new Vector3(x,-.22f,a),new Vector3(x,-.22f,b),new Vector3(-x,-.22f,b),
                    new Vector3(-x,0,a),new Vector3(x,0,a),new Vector3(x,0,b),new Vector3(-x,0,b)});
                for(int face=0;face<box.Length;face+=3){triangles.Add(offset+box[face]);triangles.Add(offset+box[face+2]);triangles.Add(offset+box[face+1]);}
            }
            // Joining platforms overlap for continuous pathing. Separate their
            // visible boards by centimetres so coplanar quays cannot flicker.
            if(surfaceLift!=0)for(int i=0;i<vertices.Count;i++)vertices[i]+=Vector3.up*surfaceLift;
            var mesh=new Mesh { name="Common pier planking" };mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            GeneratedResourceOwner.For(parent).Track(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=WorldArt.Painted(2,new Color(.82f,.61f,.34f),.46f);
            if(walkable){var collider=go.AddComponent<BoxCollider>();collider.center=Vector3.down*.16f;collider.size=new Vector3(width,.32f,length);}
            if(trim)
            {
                var flat=direction;flat.y=0;
                CreateQuayTrim(parent,(from+to)*.5f,flat.sqrMagnitude>.02f?Quaternion.LookRotation(flat):Quaternion.identity,width,length,direction.y);
            }
        }
        public static void CreateQuayTrim(Transform parent,Vector3 center,Quaternion rotation,float width,float length,float rise=0)
        {
            var root=new GameObject("Quay timber frame").transform;root.SetParent(parent,false);root.SetPositionAndRotation(center,rotation);
            for(int side=-1;side<=1;side+=2)
            {
                Beam(root,new(side*(width*.5f-.08f),-.12f-rise*.5f,-length*.5f),new(side*(width*.5f-.08f),-.12f+rise*.5f,length*.5f),.14f,new Color(.66f,.43f,.22f));
                for(int end=-1;end<=1;end+=2)
                {
                    float top=-.03f+end*rise*.5f;var point=new Vector3(side*(width*.5f-.18f),top-.9f,end*(length*.5f-.35f));
                    Block(root,"Mooring pile",point,new(.32f,1.8f,.32f),2,new Color(.65f,.43f,.24f));
                    Block(root,"Mooring iron cap",new(point.x,top,point.z),new(.39f,.1f,.39f),2,new Color(.32f,.34f,.31f));
                }
            }
        }

        public static HarborVisual CreateHarborBuilding(Transform parent,int owner,Vector3 worldPosition,Vector3 waterward,bool solid)
        {
            waterward.y=0;if(waterward.sqrMagnitude<.01f)waterward=Vector3.forward;
            var root=new GameObject("Common harbor building").transform;root.SetParent(parent,false);root.SetPositionAndRotation(worldPosition,Quaternion.LookRotation(waterward));
            const float houseX=-2.15f,houseZ=-.3f;
            Block(root,"Harbormaster stone foundation",new(houseX,.22f,houseZ),new(3.65f,.44f,3.35f),0,new Color(.92f,.88f,.76f));
            var house=Block(root,"Harbormaster plaster",new(houseX,1.25f,houseZ),new(3.05f,1.75f,2.75f),0,new Color(1.12f,1.04f,.82f));
            if(solid)
            {
                // The navigation obstacle follows the visible house, never an
                // invisible former town hall in the middle of the quay.
                house.AddComponent<BoxCollider>();
            }
            for(int side=-1;side<=1;side+=2)
            {
                Block(root,"Boathouse stone corner",new(houseX+side*1.39f,1.1f,houseZ-1.28f),new(.3f,1.75f,.3f),0);
                Beam(root,new(houseX+side*1.48f,.42f,houseZ-1.43f),new(houseX+side*1.48f,2.2f,houseZ-1.43f),.11f);
            }
            Block(root,"Recessed boathouse door",new(houseX,.92f,houseZ-1.42f),new(1.12f,1.42f,.12f),2,new Color(.38f,.25f,.14f));
            VisualFactory.Shape(root,PrimitiveType.Cube,"Warm harbor window",new(houseX+.9f,1.45f,houseZ-1.44f),new(.42f,.54f,.05f),new Color(1,.61f,.18f));
            var entrance=BuildingEntranceAnchor.Create(root,"Boathouse entrance anchor",new(houseX,0,houseZ-1.44f),Vector3.back);
            var roof=VisualFactory.Cone(root,"Faction roof",new(houseX,2.12f,houseZ),2.25f,1.55f,Color.white,4,45).GetComponent<Renderer>();
            roof.sharedMaterial=WorldArt.RoofMaterial(owner);
            // Cargo and a working jib give the shared silhouette its Warcraft shipyard read.
            Beam(root,new(.95f,.42f,.4f),new(.95f,3.15f,.4f),.15f,new Color(.92f,.63f,.33f));
            Beam(root,new(.95f,3.05f,.4f),new(.95f,3.05f,2.75f),.13f,new Color(.92f,.63f,.33f));
            Beam(root,new(.95f,1.9f,.4f),new(.95f,3.05f,2.75f),.09f,new Color(.68f,.45f,.25f));
            Beam(root,new(.95f,3.02f,2.7f),new(.95f,.5f,2.7f),.025f,new Color(.18f,.15f,.11f));
            for(int i=0;i<4;i++)Block(root,"Dockside cargo",new(1.85f+i%2*.68f,.32f,-.55f+i/2*.68f),new(.58f,.64f,.58f),2,new Color(1.18f,.91f,.52f));
            var barrel=VisualFactory.Shape(root,PrimitiveType.Cylinder,"Dockside barrel",new(2.05f,.43f,.9f),new(.48f,.62f,.48f),Color.white);barrel.GetComponent<Renderer>().sharedMaterial=WorldArt.Painted(2,new Color(.75f,.48f,.24f),.45f);
            Beam(root,new(houseX+1.65f,0,houseZ+.3f),new(houseX+1.65f,4.25f,houseZ+.3f),.12f);
            var flag=Block(root,"Harbor standard",new(houseX+2.22f,3.63f,houseZ+.3f),new(1.08f,.82f,.07f),0).GetComponent<Renderer>();
            flag.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamColor(owner));
            return new HarborVisual(root,entrance,roof,flag);
        }

        public static BuildingEntranceAnchor CreateHarbor(Harbor harbor)
        {
            var root=new GameObject("Harbor architecture").transform;root.SetParent(harbor.transform,false);root.position=harbor.Landing;
            var grade=harbor.Berth-harbor.Landing;var direction=grade;direction.y=0;if(direction.sqrMagnitude<.01f)direction=Vector3.forward;
            root.rotation=Quaternion.LookRotation(direction);float length=direction.magnitude;
            Vector3 pierStart=harbor.Landing+Vector3.up*.15f,pierEnd=harbor.Berth+Vector3.up*.15f;
            CreatePierDeck(root,pierStart,pierEnd,3.15f,"Harbor pier",false);
            var visual=CreateHarborBuilding(root,harbor.Owner,harbor.Landing,direction,false);
            var collider=harbor.gameObject.AddComponent<BoxCollider>();collider.center=harbor.transform.InverseTransformPoint(harbor.Landing)+Vector3.up;collider.size=new(9,4,7);collider.isTrigger=true;
            var appearance=harbor.gameObject.AddComponent<HarborAppearance>();appearance.Initialize(harbor,visual.Roof,visual.Flag);
            return visual.Entrance;
        }
    }
    public sealed class ShipAppearance:MonoBehaviour
    {
        public const float SelectionRadius = 4.3f;
        Ship ship;Transform model;LineRenderer ring;public void Initialize(Ship s,Transform m,LineRenderer r){ship=s;model=m;ring=r;ring.enabled=false;}
        void LateUpdate(){if(!ship)return;ring.enabled=ship.Selected;ring.transform.localScale=Vector3.one;model.localPosition=Vector3.up*(.025f+Mathf.Sin(Time.time*1.9f+GetInstanceID())*.045f);model.localRotation=Quaternion.Euler(Mathf.Sin(Time.time*1.2f)*.7f,0,Mathf.Sin(Time.time*1.6f)*1.2f);}
    }
    public sealed class HarborAppearance:MonoBehaviour
    {
        Harbor harbor;Renderer roof,flag;int owner=-9;public void Initialize(Harbor h,Renderer r,Renderer f){harbor=h;roof=r;flag=f;Refresh();}
        void Update(){if(owner!=harbor.Owner)Refresh();}void Refresh(){owner=harbor.Owner;roof.sharedMaterial=WorldArt.RoofMaterial(owner);flag.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamColor(owner));}
    }
}
