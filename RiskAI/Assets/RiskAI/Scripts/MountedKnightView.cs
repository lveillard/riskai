using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Original mounted silhouette. Animation reads movement; it never resolves combat.</summary>
    public sealed class MountedKnightView : MonoBehaviour
    {
        sealed class LegPose
        {
            public Transform hip, knee;
            public readonly float phase;
            public LegPose(Transform hip,Transform knee,float phase){this.hip=hip;this.knee=knee;this.phase=phase;}
        }
        Soldier soldier;
        readonly List<LegPose> legs=new();
        readonly List<Mesh> ownedMeshes=new();
        Transform motionRig,tail,lance;
        Vector3 lanceRestPosition;
        float gaitBlend,gaitPhase,lastGaitTime=-1,lanceThrust;
        static Mesh oval;

        public static GameObject Create(Soldier owner) => Create(owner.transform,owner.Team,owner);
        public static GameObject Create(Transform parent,int teamId,Soldier owner=null)
        {
            var model=new GameObject("RoyalGuard(Clone)");model.transform.SetParent(parent,false);
            var view=model.AddComponent<MountedKnightView>();view.soldier=owner;
            var root=model.transform;
            view.motionRig=new GameObject("Mounted motion rig").transform;view.motionRig.SetParent(root,false);
            var artRoot=view.motionRig;
            Color coat=new Color(.29f,.17f,.085f),dark=new Color(.075f,.060f,.045f),steel=new Color(.57f,.65f,.70f);
            Color team=VisualFactory.TeamColor(teamId),gold=new Color(.77f,.58f,.23f);
            Oval(artRoot,"Horse barrel",new Vector3(0,1.12f,0),new Vector3(.88f,.93f,1.85f),coat);
            Oval(artRoot,"Horse haunch",new Vector3(0,1.17f,-.69f),new Vector3(.95f,1.02f,.91f),coat);
            Oval(artRoot,"Horse chest",new Vector3(0,1.24f,.65f),new Vector3(.82f,1.12f,.78f),coat);
            var neck=Oval(artRoot,"Raised horse neck",new Vector3(0,1.76f,.89f),new Vector3(.47f,1.15f,.56f),coat);
            neck.localRotation=Quaternion.Euler(-24,0,0);
            Oval(artRoot,"Horse head",new Vector3(0,2.18f,1.16f),new Vector3(.44f,.50f,.69f),coat);
            Oval(artRoot,"Long muzzle",new Vector3(0,2.01f,1.49f),new Vector3(.37f,.30f,.56f),coat*.85f);
            for(int side=-1;side<=1;side+=2)
            {
                var ear=Oval(artRoot,"Horse ear",new Vector3(side*.15f,2.49f,1.04f),new Vector3(.11f,.34f,.14f),coat);
                ear.localRotation=Quaternion.Euler(-12,0,side*-15);
                Oval(artRoot,"Horse eye",new Vector3(side*.222f,2.24f,1.29f),new Vector3(.047f,.066f,.07f),dark);
                for(int end=-1;end<=1;end+=2)
                {
                    var pivot=new GameObject("Horse leg").transform;pivot.SetParent(artRoot,false);
                    pivot.localPosition=new Vector3(side*.31f,1.12f,end*.65f);
                    Oval(pivot,"Upper leg",new Vector3(0,-.285f,0),new Vector3(.23f,.62f,.27f),coat);
                    var knee=new GameObject("Horse knee").transform;knee.SetParent(pivot,false);knee.localPosition=new Vector3(0,-.57f,0);
                    Oval(knee,"Lower leg",new Vector3(0,-.21f,0),new Vector3(.19f,.48f,.23f),coat);
                    Oval(knee,"Hoof",new Vector3(0,-.48f,.055f),new Vector3(.29f,.18f,.36f),dark);
                    view.legs.Add(new LegPose(pivot,knee,side*end>0?0:Mathf.PI));
                    view.CombineStatic(knee);
                    view.CombineStatic(pivot);
                }
                Oval(artRoot,"Rider boot",new Vector3(side*.42f,1.15f,-.02f),new Vector3(.24f,.66f,.33f),dark);
                Oval(artRoot,"Armoured thigh",new Vector3(side*.34f,1.59f,-.09f),new Vector3(.26f,.55f,.47f),steel);
                Oval(artRoot,"Pauldron",new Vector3(side*.36f,2.29f,-.14f),new Vector3(.34f,.31f,.37f),steel);
                var arm=Oval(artRoot,"Rider arm",new Vector3(side*.39f,2.00f,.015f),new Vector3(.23f,.53f,.26f),steel);
                arm.localRotation=Quaternion.Euler(-22,0,side*12);
            }
            Oval(artRoot,"Saddlecloth",new Vector3(0,1.52f,-.18f),new Vector3(1.0f,.22f,1.16f),team);
            Oval(artRoot,"Saddle",new Vector3(0,1.67f,-.17f),new Vector3(.65f,.21f,.73f),dark);
            Oval(artRoot,"Rider cuirass",new Vector3(0,2.05f,-.17f),new Vector3(.66f,.70f,.46f),steel);
            Oval(artRoot,"Heraldic tabard",new Vector3(0,2.00f,.075f),new Vector3(.41f,.51f,.05f),team);
            Oval(artRoot,"Helmet",new Vector3(0,2.60f,-.15f),new Vector3(.48f,.57f,.48f),steel);
            VisualFactory.Shape(artRoot,PrimitiveType.Cube,"Dark visor",new Vector3(0,2.66f,.10f),new Vector3(.32f,.07f,.035f),dark);
            Oval(artRoot,"Helmet crest",new Vector3(0,2.96f,-.23f),new Vector3(.19f,.28f,.61f),team);
            var shield=Oval(artRoot,"Team shield",new Vector3(-.57f,1.95f,.12f),new Vector3(.12f,.80f,.63f),team);
            shield.localRotation=Quaternion.Euler(0,-18,-8);
            Oval(artRoot,"Shield boss",new Vector3(-.65f,1.97f,.12f),new Vector3(.10f,.21f,.21f),gold);
            for(int i=0;i<5;i++)
                Oval(artRoot,"Mane",new Vector3(0,2.04f-i*.13f,.76f-i*.05f),new Vector3(.20f,.32f,.28f),dark);
            var tailRig=new GameObject("Horse tail pivot").transform;tailRig.SetParent(artRoot,false);
            view.tail=Oval(tailRig,"Horse tail",new Vector3(0,.90f,-1.02f),new Vector3(.25f,1.08f,.27f),dark);
            view.tail.localRotation=Quaternion.Euler(-25,0,0);
            view.lance=new GameObject("Lance pivot").transform;view.lance.SetParent(artRoot,false);
            view.lance.localPosition=new Vector3(.50f,2.06f,.18f);
            view.lanceRestPosition=view.lance.localPosition;
            VisualFactory.Shape(view.lance,PrimitiveType.Cylinder,"Lance shaft",new Vector3(0,0,.63f),new Vector3(.065f,.79f,.065f),gold).transform.localRotation=Quaternion.Euler(90,0,0);
            VisualFactory.Cone(view.lance,"Steel lance tip",new Vector3(0,0,1.4f),.105f,.35f,steel,6).transform.localRotation=Quaternion.Euler(90,0,0);
            view.CombineStatic(view.lance);view.CombineStatic(artRoot);
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
        void OnEnable() => ResetPose();
        void OnDisable() => ResetPose();
        void ResetPose()
        {
            gaitBlend=0;
            gaitPhase=0;
            lastGaitTime=-1;
            lanceThrust=0;
            if(motionRig){motionRig.localPosition=Vector3.zero;motionRig.localRotation=Quaternion.identity;}
            foreach(var leg in legs)
            {
                if(leg.hip)leg.hip.localRotation=Quaternion.identity;
                if(leg.knee)leg.knee.localRotation=Quaternion.identity;
            }
            if(tail)tail.localRotation=Quaternion.Euler(-25,0,0);
            if(lance){lance.localPosition=lanceRestPosition;lance.localRotation=Quaternion.Euler(-18,0,0);}
        }
        void Update()
        {
            var battle=BattleSession.Current;
            if(!soldier||!soldier.IsAlive||!soldier.Agent||!soldier.Agent.enabled||!battle||battle.Paused||battle.Winner>=0)return;
            float speed=soldier.Agent.velocity.magnitude;
            float normalizedSpeed=Mathf.Clamp01(speed/Mathf.Max(.01f,soldier.Agent.speed));
            float targetGait=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.075f,normalizedSpeed));
            gaitBlend=Mathf.MoveTowards(gaitBlend,targetGait,Time.deltaTime*(targetGait>gaitBlend?6f:4f));
            float elapsed=lastGaitTime<0?0:Mathf.Max(0,battle.BattleTime-lastGaitTime);
            lastGaitTime=battle.BattleTime;
            float strideFrequency=Mathf.Lerp(1.4f,10.5f,normalizedSpeed)*gaitBlend;
            gaitPhase=Mathf.Repeat(gaitPhase+elapsed*strideFrequency,Mathf.PI*2);
            float stridePhase=gaitPhase+soldier.EntityId*.41f;
            for(int i=0;i<legs.Count;i++)
            {
                var leg=legs[i];float swing=Mathf.Sin(stridePhase+leg.phase);
                float lift=Mathf.Max(0,swing);
                leg.hip.localRotation=Quaternion.Euler((-swing*25f-lift*7f)*gaitBlend,0,0);
                leg.knee.localRotation=Quaternion.Euler((lift*34f-Mathf.Max(0,-swing)*9f)*gaitBlend,0,0);
            }
            float bob=Mathf.Sin(stridePhase*2)*gaitBlend;
            motionRig.localPosition=new Vector3(0,.008f+Mathf.Abs(bob)*.032f*gaitBlend,0);
            motionRig.localRotation=Quaternion.Euler(bob*2.3f*gaitBlend,0,Mathf.Sin(stridePhase)*1.15f*gaitBlend);
            tail.localRotation=Quaternion.Euler(-25f-bob*7f,0,Mathf.Sin(stridePhase*.5f)*8f*gaitBlend);
            lanceThrust=AttackPresentationTiming.ContactPose(soldier.AttackPresentationProgress,
                AttackPresentationTiming.ContactNormalizedTime(soldier.Kind));
            lance.localPosition=lanceRestPosition+Vector3.forward*(lanceThrust>.0f?lanceThrust*.52f:lanceThrust*.16f);
            lance.localRotation=Quaternion.Euler(-18f+lanceThrust*15f,0,0);
        }
    }
}
