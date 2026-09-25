using System.Collections.Generic;
using RiskAI.Core;
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
            var root=CreateShipModel(ship.transform,ship.Team,ship.Kind);var hull=ship.Type.Hull;
            // The clickable/attackable hull volume is the units.json hull.
            var collider=ship.gameObject.AddComponent<BoxCollider>();collider.center=new(0,hull.CenterHeight,0);collider.size=new(hull.Beam,hull.Height,hull.Length);collider.isTrigger=true;
            var visual=ship.gameObject.AddComponent<ShipAppearance>();visual.Initialize(ship,root,VisualFactory.Ring(ship.transform,ShipAppearance.SelectionRadius,.09f,new Color(.5f,1,.55f)));
        }
        // The editor portrait generator calls the same model builder as live ships, so UI art
        // cannot drift from the silhouettes and team treatment seen in the world.
        // units.json hull.model picks the Warcraft III ship each hull follows; every recipe is built
        // from the same hull, mast, sail and cannon parts, so only proportions and fittings differ.
        public static Transform CreateShipModel(Transform parent,int team,UnitKind kind)
        {
            var root=new GameObject("Ship model").transform;root.SetParent(parent,false);
            ref readonly var type=ref UnitCatalog.Get(kind);var hull=type.Hull;
            root.localScale=Vector3.one*hull.Scale;
            var kit=new ShipKit(root,GeneratedResourceOwner.For(parent),team);
            switch(hull.Model)
            {
                case ShipModel.Frigate: Frigate(kit,hull.Length,hull.Beam); break;
                case ShipModel.Juggernaught: Juggernaught(kit,hull.Length,hull.Beam); break;
                case ShipModel.Battleship: Battleship(kit,hull.Length,hull.Beam); break;
                case ShipModel.Transport: Transport(kit,hull.Length,hull.Beam); break;
                case ShipModel.ArmoredTransport: ArmoredTransport(kit,hull.Length,hull.Beam); break;
            }
            return root;
        }

        static readonly Color Oak=new(.9f,.67f,.36f),DarkOak=new(.55f,.36f,.2f),Deck=new(1.35f,1.12f,.75f),Gold=new(.92f,.7f,.24f),
            Iron=new(.2f,.21f,.23f),Plate=new(.52f,.56f,.6f),Canvas=new(.93f,.9f,.8f),Bone=new(.95f,.92f,.82f);

        /// <summary>Human Frigate: slim gold-trimmed hull, three forward-raked masts with white fore-and-aft sails, gun cabin amidships.</summary>
        static void Frigate(ShipKit k,float length,float width)
        {
            k.Hull(length,width,.78f,.36f,.05f,.62f,.2f,Oak);
            k.Rails(length,width,.78f,.2f,Gold);
            k.Box("Gun cabin",new(0,1.02f,-length*.04f),new(width*.7f,.48f,length*.36f),k.TeamTint(.25f));
            k.Box("Gun cabin roof trim",new(0,1.28f,-length*.04f),new(width*.74f,.06f,length*.38f),Gold,false);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<2;i++)
                k.Cannon(new(side*width*.36f,1.0f,-length*.12f+i*length*.18f),new(side,0,0),.5f,.09f,Iron);
            float[] masts={length*.3f,length*.02f,-length*.26f},heights={3.5f,4.2f,3.3f};
            for(int i=0;i<3;i++)
            {
                var top=k.Mast(new(0,.78f,masts[i]),heights[i],9f,.1f);
                k.ForeAftSail(new(0,1.25f,masts[i]+.05f),top+new Vector3(0,-.25f,0),new(0,1.25f,masts[i]-length*.2f),.28f,Canvas);
                k.Pennant(top);
            }
            k.Spar(new(0,.9f,length*.45f),new(0,1.35f,length*.66f),.08f,Gold);
        }

        /// <summary>Orc Juggernaught: short, tall, spiked hull with an iron ram, two masts of big square sails and a gun deck.</summary>
        static void Juggernaught(ShipKit k,float length,float width)
        {
            width*=1.2f;
            k.Hull(length,width,1.02f,.42f,.22f,.86f,.1f,DarkOak);
            k.Rails(length,width,1.02f,.1f,DarkOak);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<6;i++)
                k.Spike(new(side*width*.5f,1.08f,-length*.34f+i*length*.13f),new(side*.7f,1,0),.09f,.34f,Gold);
            k.Spike(new(0,.42f,length*.52f),Vector3.forward,.26f,1.1f,Iron);
            k.Box("Raised gun deck",new(0,1.26f,length*.02f),new(width*.78f,.34f,length*.34f),DarkOak);
            for(int i=-1;i<=1;i++)k.Cannon(new(i*.42f,1.52f,length*.14f),Vector3.forward,.62f,.12f,Iron);
            for(int side=-1;side<=1;side+=2)
            {
                k.Cannon(new(side*width*.42f,1.2f,-length*.08f),new(side,0,0),.46f,.11f,Iron);
                var wheel=VisualFactory.Shape(k.Root,PrimitiveType.Cylinder,"Spiked side wheel",new(side*width*.54f,.72f,-length*.2f),new(.95f,.06f,.95f),new Color(.24f,.42f,.2f));
                wheel.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            foreach(float z in new[]{length*.2f,-length*.18f})
            {
                var top=k.Mast(new(0,1.02f,z),3.2f,0,.13f);
                k.SquareSail(new(0,top.y-1.95f,z+.12f),width*1.35f,width*1.15f,1.75f,.42f,0,k.TeamTint(.1f));
                k.Box("Skull emblem",new(0,top.y-1.05f,z+.62f),new(.44f,.46f,.04f),Bone,false);
                k.Spar(new(-width*.7f,top.y-.2f,z+.12f),new(width*.7f,top.y-.2f,z+.12f),.08f,DarkOak);
                k.Pennant(top);
            }
        }

        /// <summary>Human Battleship: broad high galleon with gold hull, striped stacked square sails, stern castle, lion bow cannon and ram.</summary>
        static void Battleship(ShipKit k,float length,float width)
        {
            width*=1.1f;
            k.Hull(length,width,1.08f,.46f,.12f,.8f,.32f,Gold*.95f);
            k.Rails(length,width,1.08f,.32f,DarkOak);
            k.Box("Stern castle",new(0,1.55f,-length*.36f),new(width*.88f,.86f,length*.2f),DarkOak);
            k.Box("Stern castle gold rail",new(0,2.0f,-length*.36f),new(width*.92f,.08f,length*.22f),Gold,false);
            k.Spike(new(0,.44f,length*.5f),Vector3.forward,.3f,.9f,Gold);
            k.Box("Lion head",new(0,1.5f,length*.28f),new(.62f,.58f,.5f),Gold,false);
            k.Cannon(new(0,1.5f,length*.33f),Vector3.forward,1.3f,.2f,Gold);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
                k.Cannon(new(side*width*.46f,.92f,-length*.2f+i*length*.16f),new(side,0,0),.52f,.1f,Iron);
            float[] masts={length*.17f,-length*.03f,-length*.22f},heights={4.4f,5.1f,4.0f};
            for(int i=0;i<3;i++)
            {
                var top=k.Mast(new(0,1.08f,masts[i]),heights[i],0,.13f);
                float lower=width*(i==1?1.5f:1.3f);
                k.SquareSail(new(0,1.75f,masts[i]+.12f),lower,lower*.9f,1.35f,.36f,4,Canvas);
                k.SquareSail(new(0,3.2f,masts[i]+.12f),lower*.82f,lower*.66f,1.1f,.3f,4,Canvas);
                k.Spar(new(-lower*.5f,3.1f,masts[i]+.12f),new(lower*.5f,3.1f,masts[i]+.12f),.07f,DarkOak);
                if(i==1)VisualFactory.Shape(k.Root,PrimitiveType.Cylinder,"Crow's nest",top+new Vector3(0,-.75f,0),new(.5f,.12f,.5f),DarkOak);
                k.Pennant(top);
            }
        }

        /// <summary>Human Transport Ship: plain brown hull, one tall mast with a big team lateen sail, canvas cargo hut and bales.</summary>
        static void Transport(ShipKit k,float length,float width)
        {
            k.Hull(length,width,.72f,.34f,.1f,.7f,.24f,Oak);
            k.Rails(length,width,.72f,.24f,DarkOak);
            k.Box("Cargo hut",new(0,1.02f,-length*.12f),new(width*.62f,.56f,length*.3f),DarkOak);
            k.Roof("Canvas cargo roof",new(0,1.3f,-length*.12f),width*.72f,length*.32f,.34f,Canvas);
            for(int i=0;i<4;i++)k.Box("Cargo bale",new((i%2-.5f)*width*.42f,.96f,(i<2?length*.3f:-length*.36f)),new(.42f,.36f,.4f),new Color(.78f,.6f,.36f));
            var top=k.Mast(new(0,.72f,length*.08f),4.6f,0,.14f);
            // A long diagonal yard carries the lateen sail from low at the bow to high over the stern.
            Vector3 yardLow=new(0,1.35f,length*.46f),yardHigh=top+new Vector3(0,-.1f,-length*.34f);
            k.Spar(yardLow,yardHigh,.08f,DarkOak);
            k.ForeAftSail(yardLow,yardHigh,new(0,1.2f,-length*.22f),.5f,k.TeamTint(.15f));
            k.Pennant(top);
        }

        /// <summary>Orc Transport Ship: low wide hull decked over with riveted iron, bow skull, hide awning, oars and no mast.</summary>
        static void ArmoredTransport(ShipKit k,float length,float width)
        {
            k.Hull(length,width,.58f,.3f,.32f,.78f,.08f,DarkOak);
            VisualFactory.Shape(k.Root,PrimitiveType.Sphere,"Riveted iron shell",new(0,.7f,length*.06f),new(width*.94f,.86f,length*.72f),new Color(.44f,.48f,.53f));
            for(int i=0;i<4;i++)k.Box("Iron plate rib",new(0,1.06f,-length*.2f+i*length*.16f),new(width*.66f,.08f,.12f),Iron,false);
            k.Box("Hide awning",new(0,1.04f,-length*.3f),new(width*.74f,.46f,length*.24f),k.TeamTint(.2f));
            VisualFactory.Shape(k.Root,PrimitiveType.Sphere,"Bow skull",new(0,.78f,length*.45f),new(.42f,.38f,.4f),Bone);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<4;i++)
                {
                    float z=-length*.22f+i*length*.15f;
                    k.Spar(new(side*width*.42f,.62f,z),new(side*(width*.5f+.9f),-.05f,z-.2f),.06f,DarkOak);
                    k.Box("Red oar grip",new(side*width*.4f,.66f,z),new(.1f,.1f,.14f),new Color(.7f,.12f,.1f),false);
                }
                for(int i=0;i<3;i++)k.Spike(new(side*width*.47f,.66f,-length*.12f+i*length*.17f),new(side,.3f,0),.07f,.28f,Plate);
            }
        }

        /// <summary>The parts every ship recipe is assembled from. Meshes and cloth materials die with the ship.</summary>
        sealed class ShipKit
        {
            public readonly Transform Root;readonly GeneratedResourceOwner resources;readonly int team;
            readonly Dictionary<Color,Material> cloth=new Dictionary<Color,Material>();
            public ShipKit(Transform root,GeneratedResourceOwner owner,int team){Root=root;resources=owner;this.team=team;}

            public Color TeamTint(float whiten)=>Color.Lerp(VisualFactory.TeamMaterialColor(team),Color.white,whiten);

            Material Cloth(Color color)
            {
                if(cloth.TryGetValue(color,out var found))return found;
                var material=resources.Track(new Material(VisualFactory.Mat(color)));material.SetFloat("_Cull",0);cloth[color]=material;return material;
            }

            public GameObject Box(string name,Vector3 position,Vector3 size,Color tint,bool painted=true)
            {
                var go=VisualFactory.Shape(Root,PrimitiveType.Cube,name,position,size,painted?Color.white:tint);
                if(painted)go.GetComponent<Renderer>().sharedMaterial=WorldArt.Painted(2,tint);
                return go;
            }

            public void Spar(Vector3 a,Vector3 b,float radius,Color tint)
            {
                var go=VisualFactory.Shape(Root,PrimitiveType.Cube,"Rigging and oak spars",(a+b)*.5f,new(radius,(b-a).magnitude,radius),tint);
                go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            }

            /// <summary>A mast from <paramref name="foot"/>, raked forward by <paramref name="rake"/> degrees; returns its top.</summary>
            public Vector3 Mast(Vector3 foot,float height,float rake,float radius)
            {
                var top=foot+Quaternion.Euler(rake,0,0)*Vector3.up*height;
                Spar(foot,top,radius,new Color(1.1f,.8f,.48f));
                return top;
            }

            public void Pennant(Vector3 top)
            {
                var flag=VisualFactory.Shape(Root,PrimitiveType.Cube,"Team pennant",top+new Vector3(0,-.12f,-.36f),new(.03f,.24f,.7f),TeamTint(0));
                flag.transform.localRotation=Quaternion.Euler(0,0,0);
            }

            public void Cannon(Vector3 position,Vector3 direction,float length,float radius,Color color)
            {
                var barrel=VisualFactory.Shape(Root,PrimitiveType.Cylinder,"Ship cannon",position+direction.normalized*length*.5f,new(radius*2,length*.5f,radius*2),color);
                barrel.transform.localRotation=Quaternion.FromToRotation(Vector3.up,direction);
            }

            public void Spike(Vector3 position,Vector3 direction,float radius,float length,Color color)
            {
                var cone=VisualFactory.Cone(Root,"Hull spike",position,radius,length,color,6);
                cone.transform.localRotation=Quaternion.FromToRotation(Vector3.up,direction);
            }

            public void Roof(string name,Vector3 ridge,float width,float length,float rise,Color color)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    float slope=Mathf.Sqrt(width*width*.25f+rise*rise);
                    var half=VisualFactory.Shape(Root,PrimitiveType.Cube,name,ridge+new Vector3(side*width*.25f,-rise*.5f,0),new(slope,.06f,length),color);
                    half.transform.localRotation=Quaternion.Euler(0,0,-side*Mathf.Atan2(rise,width*.5f)*Mathf.Rad2Deg);
                }
            }

            /// <summary>
            /// Planked hull from stern to bow: stations with a pointed bow (<paramref name="bow"/> of the beam), a square
            /// transom (<paramref name="stern"/>), sheer rising to both ends and a deck inside the gunwale.
            /// </summary>
            public void Hull(float length,float width,float deck,float keel,float bow,float stern,float sheer,Color tint)
            {
                const int stations=16;
                var v=new List<Vector3>();var t=new List<int>();var deckV=new List<Vector3>();var deckT=new List<int>();
                for(int i=0;i<=stations;i++)
                {
                    float u=i/(float)stations,z=Mathf.Lerp(-length*.5f,length*.5f,u);
                    float shape=u<.45f?Mathf.Lerp(stern,1,Mathf.SmoothStep(0,1,u/.45f)):Mathf.Lerp(1,bow,Mathf.Pow((u-.45f)/.55f,1.7f));
                    float half=width*.5f*shape,top=deck+sheer*(2*u-1)*(2*u-1);
                    v.Add(new(-half,top,z));v.Add(new(-half*.86f,deck*.28f,z));v.Add(new(0,-keel,z));v.Add(new(half*.86f,deck*.28f,z));v.Add(new(half,top,z));
                    deckV.Add(new(-half*.94f,top-.1f,z));deckV.Add(new(half*.94f,top-.1f,z));
                    if(i==stations)continue;
                    int a=i*5,n=a+5;
                    for(int p=0;p<4;p++){t.Add(a+p);t.Add(a+p+1);t.Add(n+p);t.Add(a+p+1);t.Add(n+p+1);t.Add(n+p);}
                    int l=i*2;deckT.Add(l);deckT.Add(l+2);deckT.Add(l+1);deckT.Add(l+2);deckT.Add(l+3);deckT.Add(l+1);
                }
                // Transom and bow caps, both faces: the stern is flat, the bow nearly closed.
                foreach(int station in new[]{0,stations})
                {
                    int start=v.Count;for(int p=0;p<5;p++)v.Add(v[station*5+p]);
                    for(int p=1;p<4;p++){t.Add(start);t.Add(start+p);t.Add(start+p+1);t.Add(start);t.Add(start+p+1);t.Add(start+p);}
                }
                var hull=resources.Track(new Mesh{name="Carvel planked ship hull"});hull.SetVertices(v);hull.SetTriangles(t,0);hull.RecalculateNormals();hull.RecalculateBounds();
                Part("Oak hull",hull,WorldArt.Painted(2,tint,.8f));
                var planks=resources.Track(new Mesh{name="Planked deck"});planks.SetVertices(deckV);planks.SetTriangles(deckT,0);planks.RecalculateNormals();planks.RecalculateBounds();
                Part("Planked deck",planks,WorldArt.Painted(2,Deck));
            }

            /// <summary>Gunwale rails along both sides, following the same sheer as the hull.</summary>
            public void Rails(float length,float width,float deck,float sheer,Color color)
            {
                const int pieces=6;
                for(int side=-1;side<=1;side+=2)for(int i=0;i<pieces;i++)
                {
                    float u0=.08f+i*.8f/pieces,u1=u0+.8f/pieces;
                    Vector3 Point(float u){float shape=u<.45f?1:Mathf.Lerp(1,.4f,(u-.45f)/.55f);return new(side*width*.47f*shape,deck+sheer*(2*u-1)*(2*u-1)+.05f,Mathf.Lerp(-length*.5f,length*.5f,u));}
                    Spar(Point(u0),Point(u1),.09f,color);
                }
            }

            void Part(string name,Mesh mesh,Material material)
            {
                var go=new GameObject(name);go.transform.SetParent(Root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            }

            /// <summary>Square sail hanging below a yard, bellied forward; <paramref name="stripes"/> alternates canvas and team cloth.</summary>
            public void SquareSail(Vector3 foot,float top,float bottom,float height,float belly,int stripes,Color cloth)
            {
                Vector3 bl=foot+new Vector3(-bottom*.5f,0,0),br=foot+new Vector3(bottom*.5f,0,0),tl=foot+new Vector3(-top*.5f,height,0),tr=foot+new Vector3(top*.5f,height,0);
                Sail(bl,br,tl,tr,Vector3.forward*belly,stripes,cloth);
            }

            /// <summary>Fore-and-aft (lateen) sail in the keel plane: luff from <paramref name="low"/> to <paramref name="high"/>, clew at <paramref name="clew"/>.</summary>
            public void ForeAftSail(Vector3 low,Vector3 high,Vector3 clew,float belly,Color cloth)
            {
                Sail(low,clew,high,high,Vector3.right*belly,0,cloth);
            }

            void Sail(Vector3 bl,Vector3 br,Vector3 tl,Vector3 tr,Vector3 belly,int stripes,Color color)
            {
                const int nx=8,ny=6;
                var v=new List<Vector3>();var plain=new List<int>();var striped=new List<int>();
                for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
                {
                    float u=x/(float)nx,f=y/(float)ny;
                    v.Add(Vector3.Lerp(Vector3.Lerp(bl,br,u),Vector3.Lerp(tl,tr,u),f)+belly*(Mathf.Sin(u*Mathf.PI)*Mathf.Sin(f*Mathf.PI)));
                    if(x==nx||y==ny)continue;
                    int k=y*(nx+1)+x,b=k+nx+1;var list=stripes>0&&(x*stripes/nx)%2==1?striped:plain;
                    list.Add(k);list.Add(b);list.Add(b+1);list.Add(k);list.Add(b+1);list.Add(k+1);
                }
                var mesh=resources.Track(new Mesh{name="Wind filled sail",subMeshCount=stripes>0?2:1});mesh.SetVertices(v);mesh.SetTriangles(plain,0);
                if(stripes>0)mesh.SetTriangles(striped,1);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                var go=new GameObject("Team sail");go.transform.SetParent(Root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials=stripes>0?new[]{Cloth(color),Cloth(TeamTint(.05f))}:new[]{Cloth(color)};
            }
        }
        public sealed class HarborVisual
        {
            public readonly Transform Root;public readonly BuildingEntranceAnchor Entrance;public readonly Renderer Roof,Flag;
            public HarborVisual(Transform root,BuildingEntranceAnchor entrance,Renderer roof,Renderer flag){Root=root;Entrance=entrance;Roof=roof;Flag=flag;}
        }
        public static void CreatePierDeck(Transform parent,Vector3 from,Vector3 to,float width,string label,bool walkable,bool trim=true,float surfaceLift=0)
        {
            var direction=to-from;float length=Mathf.Max(width,direction.magnitude+.5f);
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
            var mesh=GeneratedResourceOwner.For(parent).Track(new Mesh { name="Common pier planking" });mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
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
            flag.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamMaterialColor(owner));
            return new HarborVisual(root,entrance,roof,flag);
        }

        public static HarborVisual CreateHarborBuildingCentered(Transform parent,int owner,Vector3 houseCenter,Vector3 waterward,bool solid)
            => CreateHarborBuildingCentered(parent,owner,houseCenter,waterward,solid,BuildingVariant.PierHarbor);

        public static HarborVisual CreateHarborBuildingCentered(Transform parent,int owner,Vector3 houseCenter,Vector3 waterward,bool solid,BuildingVariant variant)
        {
            if(variant==BuildingVariant.IntegratedHarbor)
                return CreateIntegratedHarborBuilding(parent,owner,houseCenter,waterward);
            if(variant!=BuildingVariant.PierHarbor)
                throw new System.ArgumentException("A harbor requires a harbor building variant.",nameof(variant));
            waterward.y=0;if(waterward.sqrMagnitude<.01f)waterward=Vector3.forward;else waterward.Normalize();
            Quaternion rotation=Quaternion.LookRotation(waterward);
            // The shared model leaves its central pier lane open by keeping the
            // house at local (-2.15, 0, -.3). Translate that root so the actual
            // house footprint, rather than its pivot, is centered on firm land.
            Vector3 root=houseCenter-rotation*new Vector3(-2.15f,0,-.3f);
            return CreateHarborBuilding(parent,owner,root,waterward,solid);
        }

        public static HarborVisual CreateIntegratedHarborBuilding(Transform parent,int owner,Vector3 sourceCenter,Vector3 waterward)
        {
            waterward.y=0;if(waterward.sqrMagnitude<.01f)waterward=Vector3.forward;else waterward.Normalize();
            var root=new GameObject("Integrated harbor building").transform;root.SetParent(parent,false);
            root.SetPositionAndRotation(sourceCenter,Quaternion.LookRotation(waterward));
            // This compact platform represents the source shallow-water shipyard.
            // It is visual only: terrain/pathing owns walkability and ship clearance.
            Block(root,"Shallow harbor platform",new(0,.08f,0),new(6.2f,.16f,4.6f),2,new Color(.9f,.68f,.39f));
            Block(root,"Integrated tower plinth",new(0,.2f,0),new(2.75f,.24f,2.65f),0,new Color(.94f,.9f,.78f));
            Block(root,"Integrated tower tie",new(-1.04f,.35f,-.9f),new(.63f,.14f,.14f),2,new Color(.72f,.46f,.24f));
            Block(root,"Integrated tower tie",new(-1.04f,.35f,.18f),new(.63f,.14f,.14f),2,new Color(.72f,.46f,.24f));
            for(int side=-1;side<=1;side+=2)
            {
                Beam(root,new(side*2.85f,-.55f,-1.85f),new(side*2.85f,.3f,-1.85f),.18f,new Color(.62f,.39f,.2f));
                Beam(root,new(side*2.85f,-.55f,1.85f),new(side*2.85f,.3f,1.85f),.18f,new Color(.62f,.39f,.2f));
                Beam(root,new(side*2.82f,.18f,-1.9f),new(side*2.82f,.18f,1.9f),.11f,new Color(.74f,.48f,.25f));
            }
            // Keep the source-to-claim lane clear for guards and Marines. Only
            // this compact side house contributes navigation collision.
            const float houseX=-1.9f,houseZ=-.72f;
            Block(root,"Integrated harbormaster foundation",new(houseX,.22f,houseZ),new(2.55f,.4f,2.45f),0,new Color(.94f,.9f,.78f));
            var house=Block(root,"Integrated harbormaster",new(houseX,1.08f,houseZ),new(2.2f,1.55f,2.05f),0,new Color(1.1f,1.03f,.82f));
            house.AddComponent<BoxCollider>();
            Block(root,"Integrated boathouse door",new(houseX,.78f,houseZ-1.06f),new(.9f,1.22f,.1f),2,new Color(.37f,.24f,.14f));
            var entrance=BuildingEntranceAnchor.Create(root,"Integrated harbor entrance anchor",new(houseX,0,houseZ-1.08f),Vector3.back);
            var roof=VisualFactory.Cone(root,"Faction roof",new(houseX,1.85f,houseZ),1.72f,1.05f,Color.white,4,45).GetComponent<Renderer>();
            roof.sharedMaterial=WorldArt.RoofMaterial(owner);
            Beam(root,new(.95f,.18f,-.4f),new(.95f,2.75f,-.4f),.13f,new Color(.9f,.6f,.31f));
            Beam(root,new(.95f,2.67f,-.4f),new(.95f,2.67f,1.8f),.11f,new Color(.9f,.6f,.31f));
            Beam(root,new(.95f,2.65f,1.75f),new(.95f,.32f,1.75f),.025f,new Color(.2f,.16f,.11f));
            for(int i=0;i<3;i++)Block(root,"Integrated dock cargo",new(1.75f+i*.58f,.35f,-1.05f),new(.48f,.62f,.5f),2,new Color(1.15f,.88f,.5f));
            var flag=Block(root,"Integrated harbor standard",new(2.15f,2.85f,-.12f),new(.98f,.72f,.06f),0).GetComponent<Renderer>();
            flag.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamMaterialColor(owner));
            return new HarborVisual(root,entrance,roof,flag);
        }

        public static BuildingEntranceAnchor CreateHarbor(Harbor harbor) => CreateHarbor(harbor,BuildingVariant.PierHarbor);
        public static BuildingEntranceAnchor CreateHarbor(Harbor harbor,BuildingVariant variant)
        {
            if(variant==BuildingVariant.IntegratedHarbor)
            {
                var integrated=CreateIntegratedHarborBuilding(harbor.transform,harbor.Owner,harbor.Landing,harbor.Berth-harbor.Landing);
                var integratedAppearance=harbor.gameObject.AddComponent<HarborAppearance>();
                integratedAppearance.Initialize(harbor,integrated.Roof,integrated.Flag);
                return integrated.Entrance;
            }
            if(variant!=BuildingVariant.PierHarbor)
                throw new System.ArgumentException("A harbor requires a harbor building variant.",nameof(variant));
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
        void Update(){if(owner!=harbor.Owner)Refresh();}void Refresh(){owner=harbor.Owner;roof.sharedMaterial=WorldArt.RoofMaterial(owner);flag.sharedMaterial=VisualFactory.Mat(VisualFactory.TeamMaterialColor(owner));}
    }
}
