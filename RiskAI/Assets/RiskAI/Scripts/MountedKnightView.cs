using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Original mounted silhouette. Animation reads movement; it never resolves combat.</summary>
    public sealed class MountedKnightView : MonoBehaviour
    {
        Soldier soldier;
        readonly List<Transform> legs=new();
        readonly List<Mesh> ownedMeshes=new();
        Transform lance;
        Vector3 lanceRestPosition;
        static Mesh oval;

        public static GameObject Create(Soldier owner) => Create(owner.transform,owner.Team,owner);
        public static GameObject Create(Transform parent,int teamId,Soldier owner=null)
        {
            var model=new GameObject("RoyalGuard(Clone)");model.transform.SetParent(parent,false);
            var view=model.AddComponent<MountedKnightView>();view.soldier=owner;
            var root=model.transform;
            Color coat=new Color(.29f,.17f,.085f),dark=new Color(.075f,.060f,.045f),steel=new Color(.57f,.65f,.70f);
            Color team=VisualFactory.TeamColor(teamId),gold=new Color(.77f,.58f,.23f);
            Oval(root,"Horse barrel",new Vector3(0,1.12f,0),new Vector3(.88f,.93f,1.85f),coat);
            Oval(root,"Horse haunch",new Vector3(0,1.17f,-.69f),new Vector3(.95f,1.02f,.91f),coat);
            Oval(root,"Horse chest",new Vector3(0,1.24f,.65f),new Vector3(.82f,1.12f,.78f),coat);
            var neck=Oval(root,"Raised horse neck",new Vector3(0,1.76f,.89f),new Vector3(.47f,1.15f,.56f),coat);
            neck.localRotation=Quaternion.Euler(-24,0,0);
            Oval(root,"Horse head",new Vector3(0,2.18f,1.16f),new Vector3(.44f,.50f,.69f),coat);
            Oval(root,"Long muzzle",new Vector3(0,2.01f,1.49f),new Vector3(.37f,.30f,.56f),coat*.85f);
            for(int side=-1;side<=1;side+=2)
            {
                var ear=Oval(root,"Horse ear",new Vector3(side*.15f,2.49f,1.04f),new Vector3(.11f,.34f,.14f),coat);
                ear.localRotation=Quaternion.Euler(-12,0,side*-15);
                Oval(root,"Horse eye",new Vector3(side*.222f,2.24f,1.29f),new Vector3(.047f,.066f,.07f),dark);
                for(int end=-1;end<=1;end+=2)
                {
                    var pivot=new GameObject("Horse leg").transform;pivot.SetParent(root,false);
                    pivot.localPosition=new Vector3(side*.31f,1.12f,end*.65f);view.legs.Add(pivot);
                    Oval(pivot,"Leg",new Vector3(0,-.43f,0),new Vector3(.23f,.89f,.27f),coat);
                    Oval(pivot,"Hoof",new Vector3(0,-.99f,.055f),new Vector3(.29f,.22f,.36f),dark);
                    view.CombineStatic(pivot);
                }
                Oval(root,"Rider boot",new Vector3(side*.42f,1.15f,-.02f),new Vector3(.24f,.66f,.33f),dark);
                Oval(root,"Armoured thigh",new Vector3(side*.34f,1.59f,-.09f),new Vector3(.26f,.55f,.47f),steel);
                Oval(root,"Pauldron",new Vector3(side*.36f,2.29f,-.14f),new Vector3(.34f,.31f,.37f),steel);
                var arm=Oval(root,"Rider arm",new Vector3(side*.39f,2.00f,.015f),new Vector3(.23f,.53f,.26f),steel);
                arm.localRotation=Quaternion.Euler(-22,0,side*12);
            }
            Oval(root,"Saddlecloth",new Vector3(0,1.52f,-.18f),new Vector3(1.0f,.22f,1.16f),team);
            Oval(root,"Saddle",new Vector3(0,1.67f,-.17f),new Vector3(.65f,.21f,.73f),dark);
            Oval(root,"Rider cuirass",new Vector3(0,2.05f,-.17f),new Vector3(.66f,.70f,.46f),steel);
            Oval(root,"Heraldic tabard",new Vector3(0,2.00f,.075f),new Vector3(.41f,.51f,.05f),team);
            Oval(root,"Helmet",new Vector3(0,2.60f,-.15f),new Vector3(.48f,.57f,.48f),steel);
            VisualFactory.Shape(root,PrimitiveType.Cube,"Dark visor",new Vector3(0,2.66f,.10f),new Vector3(.32f,.07f,.035f),dark);
            Oval(root,"Helmet crest",new Vector3(0,2.96f,-.23f),new Vector3(.19f,.28f,.61f),team);
            var shield=Oval(root,"Team shield",new Vector3(-.57f,1.95f,.12f),new Vector3(.12f,.80f,.63f),team);
            shield.localRotation=Quaternion.Euler(0,-18,-8);
            Oval(root,"Shield boss",new Vector3(-.65f,1.97f,.12f),new Vector3(.10f,.21f,.21f),gold);
            for(int i=0;i<5;i++)
                Oval(root,"Mane",new Vector3(0,2.04f-i*.13f,.76f-i*.05f),new Vector3(.20f,.32f,.28f),dark);
            var tail=Oval(root,"Horse tail",new Vector3(0,.90f,-1.02f),new Vector3(.25f,1.08f,.27f),dark);
            tail.localRotation=Quaternion.Euler(-25,0,0);
            view.lance=new GameObject("Lance pivot").transform;view.lance.SetParent(root,false);
            view.lance.localPosition=new Vector3(.50f,2.06f,.18f);
            view.lanceRestPosition=view.lance.localPosition;
            VisualFactory.Shape(view.lance,PrimitiveType.Cylinder,"Lance shaft",new Vector3(0,0,.63f),new Vector3(.065f,.79f,.065f),gold).transform.localRotation=Quaternion.Euler(90,0,0);
            VisualFactory.Cone(view.lance,"Steel lance tip",new Vector3(0,0,1.4f),.105f,.35f,steel,6).transform.localRotation=Quaternion.Euler(90,0,0);
            view.CombineStatic(view.lance);view.CombineStatic(root);
            ModelMetrics.MatchStandingHeight(model,Core.UnitKind.Guard);
            return model;
        }
        static Transform Oval(Transform root,string name,Vector3 position,Vector3 size,Color color)
        {
            if(!oval)oval=BuildOval();
            var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.AddComponent<MeshFilter>().sharedMesh=oval;go.AddComponent<MeshRenderer>().sharedMaterial=VisualFactory.Mat(color);return go.transform;
        }
        static Mesh BuildOval()
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            Vector3 Point(int ring,int side)
            {
                float latitude=-Mathf.PI*.5f+ring*Mathf.PI/5,angle=side*Mathf.PI/5;
                return new Vector3(Mathf.Cos(latitude)*Mathf.Cos(angle),Mathf.Sin(latitude),Mathf.Cos(latitude)*Mathf.Sin(angle))*.5f;
            }
            for(int r=0;r<5;r++)for(int s=0;s<10;s++)
            {
                int n=vertices.Count;vertices.Add(Point(r,s));vertices.Add(Point(r+1,s));vertices.Add(Point(r+1,s+1));vertices.Add(Point(r,s+1));
                indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);
            }
            var mesh=new Mesh{name="Original low polygon horse forms"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void CombineStatic(Transform root)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();var obsolete=new List<GameObject>();
            foreach(Transform child in root)
            {
                var filter=child.GetComponent<MeshFilter>();var renderer=child.GetComponent<MeshRenderer>();
                if(!filter||!renderer)continue;
                if(!groups.TryGetValue(renderer.sharedMaterial,out var parts)){parts=new List<CombineInstance>();groups.Add(renderer.sharedMaterial,parts);}
                parts.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*child.localToWorldMatrix});
                renderer.enabled=false;obsolete.Add(child.gameObject);
            }
            foreach(var group in groups)
            {
                var mesh=new Mesh{name="Mounted knight combined geometry"};mesh.CombineMeshes(group.Value.ToArray());
                ownedMeshes.Add(mesh);
                var go=new GameObject("Mounted geometry");go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var go in obsolete){if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
        }
        void OnDestroy()
        {
            foreach(var mesh in ownedMeshes)if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        }
        void Update()
        {
            var battle=BattleSession.Current;
            if(!soldier||!soldier.IsAlive||!soldier.Agent||!soldier.Agent.enabled||!battle||battle.Paused||battle.Winner>=0)return;
            float speed=soldier.Agent.velocity.magnitude;
            for(int i=0;i<legs.Count;i++)legs[i].localRotation=Quaternion.Euler(speed>.1f?Mathf.Sin(battle.BattleTime*10+(i==0||i==3?0:Mathf.PI))*25:0,0,0);
            bool engaged=soldier.CurrentTarget&&Vector3.Distance(soldier.transform.position,soldier.CurrentTarget.ApproachPoint(soldier.transform.position))<=Core.BattleRules.Range(Core.UnitKind.Guard)+.55f;
            float phase=Mathf.Repeat((battle.BattleTime+soldier.EntityId*.173f)*1.45f,1f);
            float thrust=engaged?Mathf.SmoothStep(0,1,Mathf.Clamp01(1f-Mathf.Abs(phase-.34f)/.19f)):0;
            lance.localPosition=lanceRestPosition+Vector3.forward*(thrust*.42f);
            lance.localRotation=Quaternion.Euler(-18f+thrust*17f,0,0);
        }
    }
}
