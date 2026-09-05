using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public static class VisualFactory
    {
        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        public static Color TeamColor(int team) => team == 0 ? new Color(.17f,.55f,.95f) : team == 1 ? new Color(.85f,.22f,.19f) : new Color(.74f,.65f,.43f);
        public static Material Mat(Color color)
        {
            if (Materials.TryGetValue(color, out var found) && found) return found;
            var template = Resources.Load<Material>("RiskAILit");
            var mat = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = color; mat.SetFloat("_Smoothness", .12f); Materials[color] = mat; return mat;
        }
        public static GameObject Shape(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            var collider = go.GetComponent<Collider>(); if (!solid) { collider.enabled = false; if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider); }
            return go;
        }
        public static LineRenderer Ring(Transform parent, float radius, float width, Color color)
        {
            var go = new GameObject("Selection ring"); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = 64;
            var template = Resources.Load<Material>("RiskAIRing");
            line.sharedMaterial = template ? template : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = color; line.widthMultiplier = width;
            for(int i=0;i<64;i++) { float angle=i*Mathf.PI*2/64; line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,.09f,Mathf.Sin(angle)*radius)); }
            return line;
        }
        public static Renderer Town(Transform root, int team, bool capital) => WorldArt.Town(root,team,capital);
        public static void Tower(Transform root, int team, out GameObject upper, out GameObject scaffold, out Renderer banner)
            => WorldArt.Tower(root,team,out upper,out scaffold,out banner);
        public static void TownUpgrade(Transform root)
        {
            Color gold=new Color(.85f,.68f,.32f);
            var ornament=new GameObject("City level II");ornament.transform.SetParent(root,false);ornament.transform.localScale=Vector3.one*VisualMetrics.TownScale;root=ornament.transform;
            Shape(root,PrimitiveType.Cube,"Fortress cornice",new Vector3(0,2.62f,0),new Vector3(2.9f,.18f,2.9f),gold);
            for(int side=-1;side<=1;side+=2)
                Shape(root,PrimitiveType.Cube,"Fortress banner",new Vector3(side*.82f,1.65f,-1.34f),new Vector3(.4f,1.2f,.08f),gold);
        }
        public static GameObject Cone(Transform parent,string name,Vector3 position,float radius,float height,Color color,int sides=8,float rotation=0)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=(i*360f/sides+rotation)*Mathf.Deg2Rad,b=((i+1)*360f/sides+rotation)*Mathf.Deg2Rad;
                int start=vertices.Count;vertices.Add(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));vertices.Add(Vector3.up*height);vertices.Add(new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius));
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Mat(color);return go;
        }
        public static void Road(Transform root,Vector3 from,Vector3 to,float width) => WorldArt.Road(from,to,width);
        public static void Soldier(Soldier soldier)
        {
            var root=soldier.transform; var team=TeamColor(soldier.Team);
            WorldArt.GroundShadow(root,new Vector3(.07f,.045f,.1f),new Vector2(1.1f,.95f));
            var prefab=Resources.Load<GameObject>("Units/"+BattleRules.Model(soldier.Kind));
            if(prefab)
            {
                var model=Object.Instantiate(prefab,root,false);
                model.transform.localScale*=VisualMetrics.UnitScale;
                UnitTeamColor.Apply(model,soldier.Kind,soldier.Team);
                soldier.gameObject.AddComponent<SoldierAnimator>().Initialize(soldier,model);
                Ring(root,.33f,.022f,team);return;
            }
            if(soldier.Kind==UnitKind.Mortar)
            {
                MortarModel(root,team);Ring(root,.48f,.025f,team);
                return;
            }
            Color metal=new Color(.71f,.75f,.77f), leather=new Color(.25f,.18f,.13f), skin=new Color(.83f,.63f,.43f);
            Shape(root,PrimitiveType.Capsule,"Tunic",new Vector3(0,1.15f,0),new Vector3(.67f,.47f,.45f),team);
            Shape(root,PrimitiveType.Sphere,"Head",new Vector3(0,1.83f,.03f),new Vector3(.47f,.49f,.45f),skin);
            Shape(root,PrimitiveType.Sphere,"Helmet",new Vector3(0,2.02f,-.025f),new Vector3(.53f,.3f,.5f),soldier.Kind==UnitKind.Footman?metal:team*.65f);
            Shape(root,PrimitiveType.Cube,"Face",new Vector3(0,1.87f,.25f),new Vector3(.25f,.045f,.035f),leather);
            for(int side=-1;side<=1;side+=2)
            {
                var leg=new GameObject("Leg pivot"); leg.transform.SetParent(root,false); leg.transform.localPosition=new Vector3(side*.18f,.8f,0);
                Shape(leg.transform,PrimitiveType.Capsule,"Boot",new Vector3(0,-.37f,.02f),new Vector3(.24f,.38f,.27f),leather);
                if(side<0) soldier.LeftLeg=leg.transform; else soldier.RightLeg=leg.transform;
                Shape(root,PrimitiveType.Sphere,"Shoulder",new Vector3(side*.4f,1.45f,0),new Vector3(.34f,.3f,.35f),soldier.Kind==UnitKind.Footman?metal:team);
                Shape(root,PrimitiveType.Capsule,"Arm",new Vector3(side*.4f,1.15f,.1f),new Vector3(.22f,.28f,.23f),team);
            }
            var weapon=new GameObject("Weapon pivot"); weapon.transform.SetParent(root,false); weapon.transform.localPosition=new Vector3(.46f,1.1f,.18f); soldier.Weapon=weapon.transform;
            if(soldier.Kind==UnitKind.Footman)
            {
                Shape(weapon.transform,PrimitiveType.Cube,"Sword",new Vector3(0,.37f,0),new Vector3(.11f,.95f,.055f),metal);
                Shape(weapon.transform,PrimitiveType.Cube,"Guard",Vector3.zero,new Vector3(.3f,.07f,.13f),leather);
                Shape(root,PrimitiveType.Cube,"Shield",new Vector3(-.48f,1.1f,.3f),new Vector3(.48f,.72f,.12f),team*.7f);
                Shape(root,PrimitiveType.Cube,"Shield crest",new Vector3(-.48f,1.1f,.37f),new Vector3(.08f,.58f,.025f),new Color(.94f,.78f,.4f));
            }
            else
            {
                var bow=Shape(weapon.transform,PrimitiveType.Capsule,"Bow",new Vector3(0,.25f,.12f),new Vector3(.07f,.55f,.1f),leather);
                bow.transform.localRotation=Quaternion.Euler(0,0,-12);
                Shape(root,PrimitiveType.Cube,"Quiver",new Vector3(.15f,1.35f,-.31f),new Vector3(.24f,.75f,.22f),leather);
            }
        }
        public static void MortarModel(Transform root,Color team)
        {
            Color metal=new Color(.52f,.56f,.61f),wood=new Color(.32f,.19f,.09f);
            Shape(root,PrimitiveType.Cube,"Oak carriage",new Vector3(0,.42f,0),new Vector3(.86f,.24f,.94f),wood);
            Shape(root,PrimitiveType.Cube,"Faction panel",new Vector3(0,.56f,-.28f),new Vector3(.78f,.16f,.16f),team);
            for(int side=-1;side<=1;side+=2)
            {
                var wheel=Shape(root,PrimitiveType.Cylinder,"Iron bound wheel",new Vector3(side*.52f,.34f,0),new Vector3(.64f,.09f,.64f),wood);
                wheel.transform.localRotation=Quaternion.Euler(0,0,90);
                var hub=Shape(root,PrimitiveType.Cylinder,"Iron wheel hub",new Vector3(side*.63f,.34f,0),new Vector3(.18f,.025f,.18f),metal);
                hub.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            var pivot=new GameObject("Mortar barrel");pivot.transform.SetParent(root,false);pivot.transform.localPosition=new Vector3(0,.54f,.03f);pivot.transform.localRotation=Quaternion.Euler(43,0,0);
            Shape(pivot.transform,PrimitiveType.Cylinder,"Cast iron tube",new Vector3(0,.35f,0),new Vector3(.36f,.43f,.36f),metal);
            Shape(pivot.transform,PrimitiveType.Cylinder,"Brass muzzle rim",new Vector3(0,.75f,0),new Vector3(.44f,.065f,.44f),new Color(.57f,.43f,.21f));
            Shape(pivot.transform,PrimitiveType.Cylinder,"Bore",new Vector3(0,.818f,0),new Vector3(.31f,.003f,.31f),new Color(.025f,.026f,.023f));
        }
        public static void Arrow(Vector3 from, Vector3 to, CombatTarget target=null, float damage=0, int team=0, CombatTarget source=null, AttackKind attack=AttackKind.Piercing)
        {
            bool magic=attack==AttackKind.Magic,mortar=attack==AttackKind.Siege;
            var color=magic?new Color(.48f,.66f,1):mortar?new Color(.72f,.68f,.54f):new Color(.97f,.84f,.45f);
            var go=Shape(null,magic||mortar?PrimitiveType.Sphere:PrimitiveType.Cube,magic?"Arcane bolt":mortar?"Mortar shell":"Arrow",from,magic?Vector3.one*.2f:mortar?Vector3.one*.16f:new Vector3(.04f,.04f,.5f),color);
            go.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            Impact(from,color,.16f);
            go.AddComponent<ArrowFlight>().Init(from,to,target,damage,team,source,attack);
        }
        public static void Arrow(Vector3 from, Vector3 to, CombatTarget target, float damage, int team, CombatTarget source, bool magic)
        {
            Arrow(from,to,target,damage,team,source,magic?AttackKind.Magic:AttackKind.Piercing);
        }
        public static void Impact(Vector3 point, Color color, float size)
        {
            var go=Shape(null,PrimitiveType.Sphere,"Impact",point,Vector3.one*size,color);
            go.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<ImpactPulse>();
        }
    }
    public sealed class ArrowFlight : MonoBehaviour
    {
        Vector3 from,to; float elapsed,damage,duration;int team;CombatTarget target,source;AttackKind attack;bool impacted;
        public void Init(Vector3 a,Vector3 b,CombatTarget victim,float hit,int attacker,CombatTarget shooter,bool arcane)
        { Init(a,b,victim,hit,attacker,shooter,arcane?AttackKind.Magic:AttackKind.Piercing); }
        public void Init(Vector3 a,Vector3 b,CombatTarget victim,float hit,int attacker,CombatTarget shooter,AttackKind kind)
        { from=a;to=b;target=victim;damage=hit;team=attacker;source=shooter;attack=kind;duration=Mathf.Clamp(Vector3.Distance(a,b)/25,.15f,.6f);transform.rotation=Quaternion.LookRotation(b-a); }
        void Update()
        {
            elapsed+=Time.deltaTime;float t=elapsed/duration;if(target)to=target.AimPoint;
            float arc=attack==AttackKind.Siege?2f:.5f;
            transform.position=Vector3.Lerp(from,to,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*arc;
            if(t>=1&&!impacted)
            {
                impacted=true;
                if(target&&target.IsAlive)target.ReceiveAttack(damage,attack,team,source);
                if(attack==AttackKind.Magic&&BattleSession.Current)
                {
                    foreach(var other in BattleSession.Current.Units.ToArray())
                        if(other&&other!=target&&other.Team!=team&&Vector3.Distance(other.AimPoint,to)<2.4f)other.ReceiveAttack(damage*.5f,attack,team,source);
                    VisualFactory.Impact(to,new Color(.55f,.7f,1),.8f);
                }
                else if(attack==AttackKind.Siege&&BattleSession.Current)
                {
                    foreach(var other in BattleSession.Current.Targets.ToArray())
                        if(other&&other!=target&&other.Team!=team&&Vector3.Distance(other.AimPoint,to)<1.5f)other.ReceiveAttack(damage*.35f,attack,team,source);
                    VisualFactory.Impact(to,new Color(.78f,.62f,.32f),.75f);
                }
                else VisualFactory.Impact(to,new Color(1,.72f,.35f),.32f);
                Destroy(gameObject);
            }
        }
    }
    public sealed class ImpactPulse : MonoBehaviour
    {
        float remaining=.22f;
        void Update() { remaining-=Time.deltaTime;transform.localScale*=Mathf.Exp(-Time.deltaTime*6);if(remaining<=0)Destroy(gameObject); }
    }
}

