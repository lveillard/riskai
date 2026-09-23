using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Original mounted silhouette for the Caballero and the mounted variants (Marine Major,
    /// Marine General, Army General). Geometry is built once per variant and shared by every
    /// instance; one palette material per (variant, team) colours it, so a knight costs one
    /// material and eleven renderers. Animation reads movement and the attack timeline; it
    /// never resolves combat.
    /// </summary>
    public sealed class MountedKnightView : MonoBehaviour
    {
        sealed class LegPose
        {
            public Transform hip, knee;
            public readonly float phase;
            public LegPose(Transform hip,Transform knee,float phase){this.hip=hip;this.knee=knee;this.phase=phase;}
        }

        // Lance pose: couched and raised at rest, lowered and driven forward at contact so
        // the tip reaches a target standing at the edge of the melee reach (see Soldier).
        const float LanceRestPitch=-20f,LanceContactPitch=28f,LanceThrust=.3f,LanceWindup=.12f;
        static readonly Vector3 LancePivot=new Vector3(.44f,2.0f,.22f);
        static readonly Vector3 TailPivot=new Vector3(0,1.56f,-.98f);

        Soldier soldier;
        readonly List<LegPose> legs=new();
        Transform motionRig,tail,lance;
        Vector3 lanceRestPosition;
        float gaitBlend,gaitPhase,lastGaitTime=-1,lanceThrust;
        bool previewing;

        public static GameObject Create(Soldier owner) => CreateVariant(owner.transform,owner.Team,UnitKind.Knight,owner);
        public static GameObject Create(Transform parent,int teamId,Soldier owner=null) => CreateVariant(parent,teamId,UnitKind.Knight,owner);

        public static bool IsMounted(UnitKind kind) =>
            kind==UnitKind.Knight||kind==UnitKind.MarineMajor||kind==UnitKind.MarineGeneral||kind==UnitKind.ArmyGeneral;

        /// <summary>Builds the mounted model for <paramref name="kind"/>, calibrated to its standing height.</summary>
        public static GameObject CreateVariant(Transform parent,int teamId,UnitKind kind,Soldier owner=null)
        {
            if(!IsMounted(kind))kind=UnitKind.Knight;
            var style=MountStyle.For(kind);
            var model=new GameObject(kind==UnitKind.ArmyGeneral?"ArmyGeneral(Clone)":"RoyalGuard(Clone)");model.transform.SetParent(parent,false);
            var view=model.AddComponent<MountedKnightView>();view.soldier=owner;
            var material=PaletteMaterial(style,teamId);
            var meshes=Meshes(style);
            var root=model.transform;
            view.motionRig=new GameObject("Mounted motion rig").transform;view.motionRig.SetParent(root,false);
            Part(view.motionRig,"Mounted geometry",meshes.Body,material);
            for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)
            {
                bool front=end>0;
                var hip=new GameObject("Horse leg").transform;hip.SetParent(view.motionRig,false);
                hip.localPosition=new Vector3(side*(front?.25f:.26f),front?1.12f:1.14f,end*.66f);
                Part(hip,"Mounted geometry",front?meshes.FrontUpper:meshes.HindUpper,material);
                var knee=new GameObject("Horse knee").transform;knee.SetParent(hip,false);
                knee.localPosition=front?new Vector3(0,-.52f,0):new Vector3(0,-.54f,-.12f);
                Part(knee,"Mounted geometry",front?meshes.FrontLower:meshes.HindLower,material);
                // Diagonal pairs move together (trot/gallop), as before.
                view.legs.Add(new LegPose(hip,knee,side*end>0?0:Mathf.PI));
            }
            view.tail=new GameObject("Horse tail pivot").transform;view.tail.SetParent(view.motionRig,false);view.tail.localPosition=TailPivot;
            Part(view.tail,"Mounted geometry",meshes.Tail,material);
            view.lance=new GameObject("Lance pivot").transform;view.lance.SetParent(view.motionRig,false);
            view.lance.localPosition=LancePivot;view.lance.localRotation=Quaternion.Euler(LanceRestPitch,0,0);
            view.lanceRestPosition=LancePivot;
            Part(view.lance,"Mounted geometry",meshes.Lance,material);
            ModelMetrics.MatchStandingHeight(model,kind);
            // Command standard: added after calibration so it never shrinks the rider.
            if(meshes.Standard)Part(view.motionRig,"Mounted standard",meshes.Standard,material);
            return model;
        }

        static void Part(Transform parent,string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        /// <summary>Presentation review only: holds a gait blend/phase and a lance thrust (-1..1).</summary>
        public void PreviewPose(float gait,float phase,float thrust)
        {
            previewing=gait>0||phase!=0||thrust!=0;
            ApplyPose(gait,phase,thrust);
        }

        void OnEnable(){ResetPose();RefreshPose();}
        void OnDisable() => ResetPose();
        void ResetPose()
        {
            gaitBlend=0;gaitPhase=0;lastGaitTime=-1;lanceThrust=0;
            if(motionRig)ApplyPose(0,0,0);
        }
        void Update()=>RefreshPose();
        void RefreshPose()
        {
            var battle=BattleSession.Current;
            if(previewing||!soldier||!soldier.IsAlive||!soldier.Agent||!soldier.Agent.enabled||!battle||battle.Paused||battle.Winner>=0)return;
            float speed=soldier.Agent.velocity.magnitude;
            // Normalise by the unit's base speed, not the forest-scaled agent speed: a knight
            // slowed by trees must take shorter, slower strides instead of galloping in place.
            float normalizedSpeed=Mathf.Clamp01(speed/Mathf.Max(.01f,BattleRules.Speed(soldier.Kind)));
            float targetGait=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.075f,normalizedSpeed));
            gaitBlend=Mathf.MoveTowards(gaitBlend,targetGait,Time.deltaTime*(targetGait>gaitBlend?6f:4f));
            float elapsed=lastGaitTime<0?0:Mathf.Max(0,battle.BattleTime-lastGaitTime);
            lastGaitTime=battle.BattleTime;
            // Stride length grows from a walk (~1.6 m) to the authored gallop (~4.2 m per cycle at
            // full speed), so hoof cadence follows the ground actually covered.
            float strideLength=Mathf.Lerp(1.6f,4.2f,normalizedSpeed);
            float strideFrequency=Mathf.Max(1.4f,speed/strideLength*Mathf.PI*2)*gaitBlend;
            gaitPhase=Mathf.Repeat(gaitPhase+elapsed*strideFrequency,Mathf.PI*2);
            lanceThrust=AttackPresentationTiming.ContactPose(soldier.AttackPresentationProgress,
                AttackPresentationTiming.ContactNormalizedTime(soldier.Kind));
            ApplyPose(gaitBlend,gaitPhase+soldier.EntityId*.41f,lanceThrust);
        }

        void ApplyPose(float gait,float stridePhase,float thrust)
        {
            for(int i=0;i<legs.Count;i++)
            {
                var leg=legs[i];float swing=Mathf.Sin(stridePhase+leg.phase);
                float lift=Mathf.Max(0,swing);
                if(leg.hip)leg.hip.localRotation=Quaternion.Euler((-swing*25f-lift*7f)*gait,0,0);
                if(leg.knee)leg.knee.localRotation=Quaternion.Euler((lift*38f-Mathf.Max(0,-swing)*9f)*gait,0,0);
            }
            float bob=Mathf.Sin(stridePhase*2)*gait;
            if(motionRig)
            {
                motionRig.localPosition=new Vector3(0,.008f+Mathf.Abs(bob)*.032f*gait,0);
                motionRig.localRotation=Quaternion.Euler(bob*2.3f*gait,0,Mathf.Sin(stridePhase)*1.15f*gait);
            }
            if(tail)tail.localRotation=Quaternion.Euler(-bob*9f-gait*14f,0,Mathf.Sin(stridePhase*.5f)*8f*gait);
            if(lance)
            {
                float forward=thrust>0?thrust*LanceThrust:thrust*LanceWindup;
                lance.localPosition=lanceRestPosition+Vector3.forward*forward;
                lance.localRotation=Quaternion.Euler(Mathf.LerpUnclamped(LanceRestPitch,LanceContactPitch,Mathf.Max(0,thrust))+Mathf.Min(0,thrust)*6f,0,0);
            }
        }

        /// <summary>Tip of the lance in the unit's local space for a given thrust, before height calibration.</summary>
        public static Vector3 LanceTip(float thrust)
        {
            float forward=thrust>0?thrust*LanceThrust:thrust*LanceWindup;
            float pitch=Mathf.LerpUnclamped(LanceRestPitch,LanceContactPitch,Mathf.Max(0,thrust));
            return LancePivot+Vector3.forward*forward+Quaternion.Euler(pitch,0,0)*new Vector3(0,0,MountMeshes.LanceLength);
        }

        // ---------------------------------------------------------------- palette and meshes

        enum Cell { Coat, CoatDark, Steel, Iron, Team, Gold, Leather, Wood, Black, TeamDark, Cream, Navy, Hoof, Plume, Count }

        sealed class MountStyle
        {
            public UnitKind Kind;public Color Coat,CoatDark,Armour,Plume;public bool Marine,General,Veteran;
            public static MountStyle For(UnitKind kind)
            {
                switch(kind)
                {
                    case UnitKind.MarineMajor:return new MountStyle{Kind=kind,Coat=new Color(.52f,.50f,.47f),CoatDark=new Color(.2f,.19f,.18f),Armour=new Color(.55f,.6f,.64f),Plume=Color.white,Marine=true};
                    case UnitKind.MarineGeneral:return new MountStyle{Kind=kind,Coat=new Color(.86f,.84f,.8f),CoatDark=new Color(.42f,.4f,.38f),Armour=new Color(.55f,.6f,.64f),Plume=Color.white,Marine=true,Veteran=true};
                    case UnitKind.ArmyGeneral:return new MountStyle{Kind=kind,Coat=new Color(.12f,.1f,.09f),CoatDark=new Color(.05f,.045f,.04f),Armour=new Color(.72f,.62f,.38f),Plume=Color.white,General=true};
                    default:return new MountStyle{Kind=UnitKind.Knight,Coat=new Color(.36f,.21f,.11f),CoatDark=new Color(.1f,.07f,.05f),Armour=new Color(.62f,.68f,.73f),Plume=Color.white};
                }
            }
        }

        static readonly Dictionary<long,Material> materials=new();
        static readonly Dictionary<UnitKind,MountMeshes> meshCache=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches(){materials.Clear();meshCache.Clear();}

        static Vector2 UV(Cell cell) => new Vector2(((int)cell+.5f)/16f,.5f);

        static Material PaletteMaterial(MountStyle style,int team)
        {
            long key=(long)style.Kind*1000+team+100;
            if(materials.TryGetValue(key,out var found)&&found)return found;
            Color teamColor=VisualFactory.TeamMaterialColor(team);
            var pixels=new Color[16];
            pixels[(int)Cell.Coat]=style.Coat;pixels[(int)Cell.CoatDark]=style.CoatDark;
            pixels[(int)Cell.Steel]=style.Armour;pixels[(int)Cell.Iron]=new Color(.26f,.28f,.3f);
            pixels[(int)Cell.Team]=teamColor;pixels[(int)Cell.Gold]=new Color(.86f,.66f,.26f);
            pixels[(int)Cell.Leather]=new Color(.3f,.17f,.08f);pixels[(int)Cell.Wood]=new Color(.78f,.62f,.4f);
            pixels[(int)Cell.Black]=new Color(.03f,.03f,.035f);pixels[(int)Cell.TeamDark]=Color.Lerp(teamColor,Color.black,.45f);
            pixels[(int)Cell.Cream]=new Color(.9f,.86f,.76f);pixels[(int)Cell.Navy]=new Color(.08f,.12f,.24f);
            pixels[(int)Cell.Hoof]=new Color(.16f,.14f,.12f);pixels[(int)Cell.Plume]=style.Plume;
            for(int i=(int)Cell.Count;i<16;i++)pixels[i]=Color.magenta;
            var texture=new Texture2D(16,1,TextureFormat.RGBA32,false,false){name="Mounted palette "+style.Kind+" "+team,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(pixels);texture.Apply(false,true);
            var template=Resources.Load<Material>("RiskAILit");
            var material=template?new Material(template):new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));
            material.name="Mounted knight · "+style.Kind+" · team "+team;
            material.color=Color.white;material.mainTexture=texture;
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.16f);
            materials[key]=material;return material;
        }

        static MountMeshes Meshes(MountStyle style)
        {
            if(meshCache.TryGetValue(style.Kind,out var found)&&found.Body)return found;
            var built=MountMeshes.Build(style);meshCache[style.Kind]=built;return built;
        }

        sealed class MountMeshes
        {
            public const float LanceLength=2.1f;
            public Mesh Body,FrontUpper,FrontLower,HindUpper,HindLower,Tail,Lance,Standard;

            public static MountMeshes Build(MountStyle style)
            {
                var m=new MountMeshes();
                m.Body=Horse(style).ToMesh("Mounted body · "+style.Kind);
                var b=new Builder();FrontUpperLeg(b);m.FrontUpper=b.ToMesh("Mounted foreleg upper");
                b=new Builder();LowerLeg(b,true);m.FrontLower=b.ToMesh("Mounted foreleg lower");
                b=new Builder();HindUpperLeg(b);m.HindUpper=b.ToMesh("Mounted hind leg upper");
                b=new Builder();LowerLeg(b,false);m.HindLower=b.ToMesh("Mounted hind leg lower");
                b=new Builder();TailMesh(b);m.Tail=b.ToMesh("Mounted tail");
                b=new Builder();LanceMesh(b,style);m.Lance=b.ToMesh("Mounted lance");
                if(style.General){b=new Builder();StandardMesh(b);m.Standard=b.ToMesh("Mounted command standard");}
                return m;
            }

            static Builder Horse(MountStyle s)
            {
                var b=new Builder();
                // Horse: deep chest, barrel and rounded haunch, arched neck and a long, tapering head.
                b.Ellipsoid(new Vector3(0,1.3f,.02f),new Vector3(.78f,.8f,1.56f),Quaternion.identity,Cell.Coat);
                b.Ellipsoid(new Vector3(0,1.36f,.6f),new Vector3(.72f,.86f,.74f),Quaternion.Euler(-12,0,0),Cell.Coat);
                b.Ellipsoid(new Vector3(0,1.38f,-.58f),new Vector3(.8f,.84f,.8f),Quaternion.identity,Cell.Coat);
                b.Ellipsoid(new Vector3(0,1.6f,-.5f),new Vector3(.58f,.34f,.62f),Quaternion.identity,Cell.Coat);
                b.Tube(new Vector3(0,1.55f,.76f),new Vector3(0,2.12f,1.12f),.27f,.16f,8,Cell.Coat);
                b.Ellipsoid(new Vector3(0,1.9f,.98f),new Vector3(.3f,.72f,.44f),Quaternion.Euler(-34,0,0),Cell.Coat);
                b.Tube(new Vector3(0,2.2f,1.18f),new Vector3(0,1.92f,1.6f),.15f,.11f,8,Cell.Coat);
                b.Ellipsoid(new Vector3(0,2.08f,1.3f),new Vector3(.25f,.3f,.36f),Quaternion.Euler(30,0,0),Cell.Coat);
                b.Ellipsoid(new Vector3(0,1.92f,1.62f),new Vector3(.2f,.19f,.22f),Quaternion.identity,Cell.CoatDark);
                for(int side=-1;side<=1;side+=2)
                {
                    b.Tube(new Vector3(side*.08f,2.24f,1.16f),new Vector3(side*.11f,2.42f,1.11f),.05f,.008f,5,Cell.Coat);
                    b.Ellipsoid(new Vector3(side*.12f,2.12f,1.34f),new Vector3(.05f,.06f,.07f),Quaternion.identity,Cell.Black);
                    b.Ellipsoid(new Vector3(side*.08f,1.93f,1.72f),new Vector3(.04f,.04f,.03f),Quaternion.identity,Cell.Black);
                }
                // Steel chanfron on the face and crinet plates along the neck.
                b.Box(new Vector3(0,2.08f,1.4f),new Vector3(.2f,.06f,.44f),Quaternion.Euler(33,0,0),Cell.Steel);
                // Mane along the crest.
                for(int i=0;i<7;i++)
                {
                    float t=i/6f;var p=Vector3.Lerp(new Vector3(0,2.26f,1.08f),new Vector3(0,1.72f,.6f),t);
                    b.Box(p+new Vector3(0,.04f,-.03f),new Vector3(.07f,.2f,.14f),Quaternion.Euler(-40+t*20,0,0),Cell.CoatDark);
                }
                // Caparison: a team-coloured cloth skirt, dagged at the hem, with a gold hem band and emblem.
                b.Skirt(new Vector3(0,1.58f,-.02f),.47f,1.0f,.53f,1.1f,.62f,Cell.Team,Cell.TeamDark);
                b.Skirt(new Vector3(0,1.01f,-.02f),.535f,1.105f,.54f,1.115f,.07f,Cell.Gold,Cell.Gold,false);
                for(int side=-1;side<=1;side+=2)
                {
                    b.Box(new Vector3(side*.53f,1.3f,-.1f),new Vector3(.02f,.36f,.08f),Quaternion.Euler(0,0,side*-4),Cell.Gold);
                    b.Box(new Vector3(side*.53f,1.34f,-.1f),new Vector3(.02f,.08f,.28f),Quaternion.Euler(0,0,side*-4),Cell.Gold);
                }
                // Peytral over the chest, saddle and cantle.
                b.Ellipsoid(new Vector3(0,1.5f,.9f),new Vector3(.66f,.38f,.28f),Quaternion.Euler(-20,0,0),Cell.Team);
                b.Ellipsoid(new Vector3(0,1.7f,-.12f),new Vector3(.56f,.2f,.72f),Quaternion.identity,Cell.Leather);
                b.Box(new Vector3(0,1.84f,-.42f),new Vector3(.42f,.18f,.08f),Quaternion.Euler(-10,0,0),Cell.Leather);
                Rider(b,s);
                return b;
            }

            static void Rider(Builder b,MountStyle s)
            {
                Cell armour=s.Marine?Cell.Navy:Cell.Steel;
                for(int side=-1;side<=1;side+=2)
                {
                    var hip=new Vector3(side*.19f,1.8f,-.12f);var knee=new Vector3(side*.38f,1.52f,.12f);var ankle=new Vector3(side*.41f,1.1f,.02f);
                    b.Tube(hip,knee,.12f,.1f,7,s.Marine?Cell.Cream:Cell.Steel);
                    b.Ellipsoid(knee,new Vector3(.17f,.15f,.17f),Quaternion.identity,Cell.Steel);
                    b.Tube(knee,ankle,.095f,.075f,7,s.Marine?Cell.Black:Cell.Steel);
                    b.Box(ankle+new Vector3(0,-.05f,.08f),new Vector3(.12f,.09f,.24f),Quaternion.identity,Cell.Iron);
                    b.Box(ankle+new Vector3(0,-.1f,.05f),new Vector3(.16f,.03f,.12f),Quaternion.identity,Cell.Iron);
                    // Pauldrons.
                    b.Ellipsoid(new Vector3(side*.33f,2.36f,-.12f),new Vector3(.3f,.24f,.32f),Quaternion.Euler(0,0,side*-18),s.General||s.Veteran?Cell.Gold:Cell.Steel);
                }
                // Torso: cuirass (or naval coat), team tabard, gold belt.
                b.Ellipsoid(new Vector3(0,2.12f,-.12f),new Vector3(.56f,.66f,.42f),Quaternion.identity,armour);
                b.Box(new Vector3(0,2.0f,.1f),new Vector3(.44f,.6f,.04f),Quaternion.identity,Cell.Team);
                b.Ellipsoid(new Vector3(0,1.8f,-.1f),new Vector3(.62f,.3f,.52f),Quaternion.identity,Cell.Team);
                b.Box(new Vector3(0,1.9f,-.1f),new Vector3(.58f,.06f,.46f),Quaternion.identity,Cell.Gold);
                if(s.Marine)b.Box(new Vector3(0,2.18f,.12f),new Vector3(.08f,.42f,.03f),Quaternion.identity,Cell.Gold);
                else b.Box(new Vector3(0,2.02f,.125f),new Vector3(.08f,.36f,.02f),Quaternion.identity,Cell.Gold);
                // Right arm couches the lance; left arm carries the shield.
                b.Tube(new Vector3(.34f,2.3f,-.12f),new Vector3(.43f,2.03f,.0f),.085f,.075f,6,armour);
                b.Tube(new Vector3(.43f,2.03f,.0f),new Vector3(.44f,2.0f,.2f),.075f,.065f,6,armour);
                b.Ellipsoid(new Vector3(.44f,2.0f,.22f),new Vector3(.13f,.13f,.13f),Quaternion.identity,Cell.Iron);
                b.Tube(new Vector3(-.34f,2.3f,-.12f),new Vector3(-.45f,2.02f,-.02f),.085f,.075f,6,armour);
                b.Tube(new Vector3(-.45f,2.02f,-.02f),new Vector3(-.32f,1.93f,.24f),.075f,.065f,6,armour);
                b.Ellipsoid(new Vector3(-.3f,1.92f,.26f),new Vector3(.12f,.12f,.12f),Quaternion.identity,Cell.Iron);
                b.Ellipsoid(new Vector3(0,2.45f,-.12f),new Vector3(.26f,.12f,.26f),Quaternion.identity,Cell.Iron);
                if(s.Marine)MarineHat(b,s);else Helm(b,s);
                Shield(b,s);
            }

            static void Helm(Builder b,MountStyle s)
            {
                // Great helm with a visor slit, gold brow band and a team plume sweeping back.
                b.Tube(new Vector3(0,2.46f,-.12f),new Vector3(0,2.8f,-.12f),.2f,.19f,10,Cell.Steel);
                b.Ellipsoid(new Vector3(0,2.8f,-.12f),new Vector3(.39f,.2f,.39f),Quaternion.identity,Cell.Steel);
                b.Box(new Vector3(0,2.67f,.075f),new Vector3(.26f,.04f,.03f),Quaternion.identity,Cell.Black);
                b.Box(new Vector3(0,2.58f,.08f),new Vector3(.03f,.14f,.02f),Quaternion.identity,Cell.Black);
                b.Tube(new Vector3(0,2.72f,-.12f),new Vector3(0,2.755f,-.12f),.205f,.205f,10,Cell.Gold);
                if(s.General)
                {
                    // Command crown instead of a plume.
                    b.Tube(new Vector3(0,2.86f,-.12f),new Vector3(0,2.96f,-.12f),.17f,.19f,10,Cell.Gold);
                    for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;b.Box(new Vector3(Mathf.Sin(a)*.17f,3.0f,-.12f+Mathf.Cos(a)*.17f),new Vector3(.05f,.1f,.05f),Quaternion.Euler(0,a*Mathf.Rad2Deg,0),Cell.Gold);}
                    return;
                }
                b.Ellipsoid(new Vector3(0,2.98f,-.1f),new Vector3(.12f,.26f,.18f),Quaternion.Euler(-15,0,0),Cell.Team);
                b.Ellipsoid(new Vector3(0,3.02f,-.26f),new Vector3(.1f,.2f,.28f),Quaternion.Euler(20,0,0),Cell.Team);
                b.Ellipsoid(new Vector3(0,2.93f,-.44f),new Vector3(.08f,.14f,.24f),Quaternion.Euler(40,0,0),Cell.TeamDark);
            }

            static void MarineHat(Builder b,MountStyle s)
            {
                // Face, then a navy tricorn (bicorne with plume for the Marine General).
                b.Ellipsoid(new Vector3(0,2.64f,-.1f),new Vector3(.3f,.34f,.3f),Quaternion.identity,Cell.Cream);
                b.Box(new Vector3(0,2.66f,.05f),new Vector3(.18f,.03f,.02f),Quaternion.identity,Cell.Black);
                if(s.Veteran)
                {
                    b.Ellipsoid(new Vector3(0,2.86f,-.1f),new Vector3(.62f,.22f,.24f),Quaternion.identity,Cell.Navy);
                    b.Box(new Vector3(0,2.86f,-.1f),new Vector3(.6f,.04f,.25f),Quaternion.identity,Cell.Gold);
                    b.Ellipsoid(new Vector3(.12f,2.98f,-.1f),new Vector3(.1f,.24f,.1f),Quaternion.Euler(0,0,-18),Cell.Plume);
                    b.Ellipsoid(new Vector3(-.2f,2.9f,.03f),new Vector3(.08f,.08f,.03f),Quaternion.identity,Cell.Team);
                    return;
                }
                b.Tube(new Vector3(0,2.76f,-.1f),new Vector3(0,2.88f,-.1f),.17f,.15f,8,Cell.Navy);
                for(int i=0;i<3;i++)
                {
                    float a=i*120f;var dir=Quaternion.Euler(0,a,0)*Vector3.forward;
                    b.Box(new Vector3(0,2.84f,-.1f)+dir*.19f,new Vector3(.36f,.1f,.05f),Quaternion.Euler(-20,a,0),Cell.Navy);
                }
                b.Ellipsoid(new Vector3(.14f,2.88f,.06f),new Vector3(.08f,.08f,.03f),Quaternion.identity,Cell.Team);
            }

            static void Shield(Builder b,MountStyle s)
            {
                // Heater shield on the left side: steel rim, team field, gold emblem.
                var rotation=Quaternion.Euler(0,-14,6);var centre=new Vector3(-.52f,1.95f,.06f);
                b.Heater(centre,rotation,.46f,.62f,.05f,Cell.Steel,Cell.Steel);
                b.Heater(centre+rotation*new Vector3(-.03f,.01f,0),rotation,.4f,.55f,.02f,Cell.Team,Cell.Team);
                var face=centre+rotation*new Vector3(-.045f,0,0);
                if(s.Marine)
                {
                    // Anchor emblem.
                    b.Box(face,new Vector3(.01f,.3f,.04f),rotation,Cell.Gold);
                    b.Box(face+rotation*new Vector3(0,.1f,0),new Vector3(.01f,.03f,.16f),rotation,Cell.Gold);
                    b.Box(face+rotation*new Vector3(0,-.14f,0),new Vector3(.01f,.04f,.22f),rotation,Cell.Gold);
                }
                else
                {
                    // Cross emblem.
                    b.Box(face+rotation*new Vector3(0,.02f,0),new Vector3(.01f,.36f,.06f),rotation,Cell.Gold);
                    b.Box(face+rotation*new Vector3(0,.08f,0),new Vector3(.01f,.06f,.26f),rotation,Cell.Gold);
                }
            }

            static void FrontUpperLeg(Builder b)
            {
                b.Ellipsoid(new Vector3(0,-.02f,0),new Vector3(.26f,.36f,.3f),Quaternion.identity,Cell.Coat);
                b.Tube(new Vector3(0,-.08f,0),new Vector3(0,-.52f,0),.12f,.075f,7,Cell.Coat);
            }
            static void HindUpperLeg(Builder b)
            {
                b.Ellipsoid(new Vector3(0,-.1f,-.02f),new Vector3(.28f,.5f,.38f),Quaternion.Euler(12,0,0),Cell.Coat);
                b.Tube(new Vector3(0,-.25f,-.05f),new Vector3(0,-.54f,-.12f),.11f,.075f,7,Cell.Coat);
            }
            static void LowerLeg(Builder b,bool front)
            {
                // Knee (or hock), slim cannon, fetlock with light feathering, pastern and hoof.
                b.Ellipsoid(Vector3.zero,new Vector3(.14f,.14f,.15f),Quaternion.identity,Cell.Coat);
                var fetlock=new Vector3(0,-.44f,front?.03f:.07f);
                b.Tube(Vector3.zero,fetlock,.065f,.058f,6,Cell.Coat);
                b.Ellipsoid(fetlock,new Vector3(.14f,.13f,.15f),Quaternion.identity,Cell.CoatDark);
                b.Ellipsoid(fetlock+new Vector3(0,-.03f,-.02f),new Vector3(.16f,.08f,.17f),Quaternion.identity,Cell.Cream);
                var pastern=fetlock+new Vector3(0,-.06f,.05f);
                b.Tube(fetlock,pastern,.058f,.06f,6,Cell.Coat);
                b.Tube(pastern,pastern+new Vector3(0,-.1f,.02f),.075f,.095f,7,Cell.Hoof,true);
            }
            static void TailMesh(Builder b)
            {
                b.Ellipsoid(new Vector3(0,-.02f,-.06f),new Vector3(.14f,.2f,.18f),Quaternion.identity,Cell.CoatDark);
                b.Tube(new Vector3(0,-.05f,-.1f),new Vector3(0,-.5f,-.3f),.1f,.085f,7,Cell.CoatDark);
                b.Tube(new Vector3(0,-.5f,-.3f),new Vector3(0,-.95f,-.34f),.085f,.03f,7,Cell.CoatDark);
            }
            static void LanceMesh(Builder b,MountStyle s)
            {
                // Tapered shaft with team rings, a funnel vamplate over the grip, steel head and a pennon.
                b.Tube(new Vector3(0,0,-.62f),new Vector3(0,0,-.1f),.04f,.055f,8,Cell.Wood,true);
                b.Tube(new Vector3(0,0,-.1f),new Vector3(0,0,1.82f),.055f,.026f,8,Cell.Wood);
                b.Tube(new Vector3(0,0,.55f),new Vector3(0,0,.62f),.052f,.05f,8,Cell.Team);
                b.Tube(new Vector3(0,0,1.0f),new Vector3(0,0,1.07f),.044f,.042f,8,Cell.Team);
                b.Tube(new Vector3(0,0,.1f),new Vector3(0,0,.38f),.19f,.05f,10,Cell.Steel,true);
                b.Tube(new Vector3(0,0,1.8f),new Vector3(0,0,LanceLength),.045f,0,6,Cell.Steel,true);
                // Swallow-tailed pennon trailing from the head, double-sided.
                var p=new[]{new Vector3(0,-.01f,1.76f),new Vector3(0,-.26f,1.72f),new Vector3(0,-.18f,1.42f),new Vector3(0,-.3f,1.2f),new Vector3(0,-.02f,1.3f)};
                b.Flag(p,Cell.Team);
            }
            static void StandardMesh(Builder b)
            {
                // Command standard on the rider's back: pole, gold finial and a tall team banner.
                var foot=new Vector3(-.26f,1.95f,-.46f);var top=new Vector3(-.3f,3.62f,-.52f);
                b.Tube(foot,top,.03f,.026f,6,Cell.Wood);
                b.Tube(top,top+new Vector3(0,.16f,0),.05f,0,6,Cell.Gold,true);
                b.Box(new Vector3(-.3f,3.5f,-.34f),new Vector3(.03f,.03f,.4f),Quaternion.identity,Cell.Gold);
                var f=new[]{new Vector3(-.3f,3.49f,-.52f),new Vector3(-.3f,3.49f,-.12f),new Vector3(-.3f,2.84f,-.12f),new Vector3(-.3f,2.96f,-.32f),new Vector3(-.3f,2.84f,-.52f)};
                b.Flag(f,Cell.Team,true);
            }
        }

        /// <summary>Minimal mesh builder: every primitive samples one palette cell through its UVs.</summary>
        sealed class Builder
        {
            readonly List<Vector3> v=new();readonly List<Vector3> n=new();readonly List<Vector2> uv=new();readonly List<int> t=new();

            public Mesh ToMesh(string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateBounds();return mesh;
            }
            void Tri(int a,int b,int c,Vector3 outward)
            {
                // Unity front faces wind clockwise when seen along -normal.
                if(Vector3.Dot(Vector3.Cross(v[b]-v[a],v[c]-v[a]),outward)<0){t.Add(a);t.Add(c);t.Add(b);}else{t.Add(a);t.Add(b);t.Add(c);}
            }
            int Add(Vector3 p,Vector3 normal,Cell cell){v.Add(p);n.Add(normal.normalized);uv.Add(UV(cell));return v.Count-1;}

            public void Ellipsoid(Vector3 centre,Vector3 size,Quaternion rotation,Cell cell)
            {
                const int rings=6,sides=10;int start=v.Count;
                for(int r=0;r<=rings;r++)for(int s=0;s<=sides;s++)
                {
                    float lat=-Mathf.PI*.5f+r*Mathf.PI/rings,lon=s*Mathf.PI*2/sides;
                    var unit=new Vector3(Mathf.Cos(lat)*Mathf.Cos(lon),Mathf.Sin(lat),Mathf.Cos(lat)*Mathf.Sin(lon));
                    var p=Vector3.Scale(unit,size*.5f);
                    var normal=new Vector3(unit.x/Mathf.Max(.001f,size.x),unit.y/Mathf.Max(.001f,size.y),unit.z/Mathf.Max(.001f,size.z));
                    Add(centre+rotation*p,rotation*normal,cell);
                }
                for(int r=0;r<rings;r++)for(int s=0;s<sides;s++)
                {
                    int a=start+r*(sides+1)+s,b2=a+sides+1;
                    var mid=(v[a]+v[b2+1])*.5f-centre;
                    Tri(a,b2,b2+1,mid);Tri(a,b2+1,a+1,mid);
                }
            }
            public void Box(Vector3 centre,Vector3 size,Quaternion rotation,Cell cell)
            {
                var h=size*.5f;
                Vector3[] axes={Vector3.right,Vector3.up,Vector3.forward};
                for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
                {
                    var normal=axes[axis]*sign;var u=axes[(axis+1)%3];var w=axes[(axis+2)%3];
                    Vector3 P(float a,float c)=>centre+rotation*Vector3.Scale(normal+u*a+w*c,h);
                    int k=Add(P(-1,-1),rotation*normal,cell);Add(P(1,-1),rotation*normal,cell);Add(P(1,1),rotation*normal,cell);Add(P(-1,1),rotation*normal,cell);
                    Tri(k,k+1,k+2,rotation*normal);Tri(k,k+2,k+3,rotation*normal);
                }
            }
            /// <summary>Round tapered tube between two points; caps optional.</summary>
            public void Tube(Vector3 from,Vector3 to,float r0,float r1,int sides,Cell cell,bool caps=false)
            {
                var axis=to-from;float length=axis.magnitude;if(length<1e-5f)return;axis/=length;
                var side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized;var up=Vector3.Cross(side,axis);
                float slope=(r0-r1)/length;int start=v.Count;
                for(int i=0;i<=sides;i++)
                {
                    float a=i*Mathf.PI*2/sides;var radial=side*Mathf.Cos(a)+up*Mathf.Sin(a);var normal=radial+axis*slope;
                    Add(from+radial*r0,normal,cell);Add(to+radial*r1,normal,cell);
                }
                for(int i=0;i<sides;i++)
                {
                    int a=start+i*2;var mid=(v[a]+v[a+3])*.5f-(from+to)*.5f;var outward=mid-axis*Vector3.Dot(mid,axis);
                    Tri(a,a+1,a+3,outward);Tri(a,a+3,a+2,outward);
                }
                if(!caps)return;
                for(int end=0;end<2;end++)
                {
                    var c=end==0?from:to;float r=end==0?r0:r1;if(r<=1e-4f)continue;var normal=end==0?-axis:axis;
                    int centre=Add(c,normal,cell);int ring=v.Count;
                    for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;Add(c+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*r,normal,cell);}
                    for(int i=0;i<sides;i++)Tri(centre,ring+i,ring+i+1,normal);
                }
            }
            // Superellipse (p=3): boxier than an ellipse, so the cloth clears the horse's shoulders and haunches.
            static Vector3 Squircle(float a)
            {
                float x=Mathf.Sin(a),z=Mathf.Cos(a);
                return new Vector3(Mathf.Sign(x)*Mathf.Pow(Mathf.Abs(x),2f/3f),0,Mathf.Sign(z)*Mathf.Pow(Mathf.Abs(z),2f/3f));
            }
            /// <summary>Elliptical cloth skirt hanging from a top ring; dagged hem when requested.</summary>
            public void Skirt(Vector3 top,float rx0,float rz0,float rx1,float rz1,float height,Cell outside,Cell inside,bool dagged=true)
            {
                const int sides=24;
                for(int face=0;face<2;face++)
                {
                    int start=v.Count;Cell cell=face==0?outside:inside;float inset=face==0?0:-.012f;
                    for(int i=0;i<=sides;i++)
                    {
                        float a=i*Mathf.PI*2/sides;var dir=Squircle(a);
                        float drop=height+(dagged&&i%2==1?.1f:0);
                        var p0=top+new Vector3(dir.x*(rx0+inset),0,dir.z*(rz0+inset));
                        var p1=top+new Vector3(dir.x*(rx1+inset),-drop,dir.z*(rz1+inset));
                        var normal=new Vector3(dir.x/rx1,0,dir.z/rz1)*(face==0?1:-1);
                        Add(p0,normal,cell);Add(p1,normal,cell);
                    }
                    for(int i=0;i<sides;i++)
                    {
                        int a=start+i*2;var outward=((v[a]+v[a+3])*.5f-top);outward.y=0;if(face==1)outward=-outward;
                        Tri(a,a+1,a+3,outward);Tri(a,a+3,a+2,outward);
                    }
                }
                if(!dagged)return;
                // Close the top of the cloth over the back so the barrel never shows through.
                int centre=Add(top+Vector3.up*.02f,Vector3.up,outside);int ringStart=v.Count;
                for(int i=0;i<=sides;i++){var d=Squircle(i*Mathf.PI*2/sides);Add(top+new Vector3(d.x*rx0,.0f,d.z*rz0),Vector3.up,outside);}
                for(int i=0;i<sides;i++)Tri(centre,ringStart+i,ringStart+i+1,Vector3.up);
            }
            /// <summary>Heater shield plate in the local YZ plane, thickness along -X (outward).</summary>
            public void Heater(Vector3 centre,Quaternion rotation,float width,float height,float thickness,Cell front,Cell back)
            {
                float w=width*.5f,h=height*.5f;
                var outline=new List<Vector2>();
                outline.Add(new Vector2(h,-w));outline.Add(new Vector2(h,w));
                for(int i=1;i<=5;i++){float t=i/6f;outline.Add(new Vector2(h-t*height*.95f,w*Mathf.Cos(t*Mathf.PI*.5f)));}
                outline.Add(new Vector2(-h,0));
                for(int i=5;i>=1;i--){float t=i/6f;outline.Add(new Vector2(h-t*height*.95f,-w*Mathf.Cos(t*Mathf.PI*.5f)));}
                for(int face=0;face<2;face++)
                {
                    float x=face==0?-thickness*.5f:thickness*.5f;var normal=rotation*(face==0?Vector3.left:Vector3.right);
                    int centreIndex=Add(centre+rotation*new Vector3(x-(face==0?.02f:0),0,0),normal,face==0?front:back);int ring=v.Count;
                    foreach(var p in outline)Add(centre+rotation*new Vector3(x,p.x,p.y),normal,face==0?front:back);
                    for(int i=0;i<outline.Count;i++)Tri(centreIndex,ring+i,ring+(i+1)%outline.Count,normal);
                }
                for(int i=0;i<outline.Count;i++)
                {
                    var a=outline[i];var c=outline[(i+1)%outline.Count];
                    Vector3 P(Vector2 q,float x)=>centre+rotation*new Vector3(x,q.x,q.y);
                    var edge=new Vector3(0,(a.x+c.x)*.5f,(a.y+c.y)*.5f);var normal=rotation*new Vector3(0,edge.y,edge.z);
                    int k=Add(P(a,-thickness*.5f),normal,back);Add(P(c,-thickness*.5f),normal,back);Add(P(c,thickness*.5f),normal,back);Add(P(a,thickness*.5f),normal,back);
                    Tri(k,k+1,k+2,normal);Tri(k,k+2,k+3,normal);
                }
            }
            /// <summary>Double-sided flat polygon fan (pennons and banners), first vertex is the hub.</summary>
            public void Flag(Vector3[] points,Cell cell,bool acrossX=false)
            {
                var normalA=acrossX?Vector3.right:Vector3.right;
                for(int face=0;face<2;face++)
                {
                    var normal=face==0?normalA:-normalA;int start=v.Count;
                    foreach(var p in points)Add(p+normal*.004f,normal,cell);
                    for(int i=1;i<points.Length-1;i++)Tri(start,start+i,start+i+1,normal);
                }
            }
        }
    }
}
