using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskAI
{
    public static class VisualFactory
    {
        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> EmissiveMaterials = new Dictionary<Color, Material>();
        static Mesh trainingDoorMesh;
        const int MaxProjectileViews = 128;
        const int MaxImpactViews = 192;
        static Transform fxRoot;
        static ProjectilePool projectilePool;
        static ImpactPool impactPool;
        static Material ringMaterial;
        static readonly AnimationCurve ringWidthProfile = BuildRingWidthProfile();

        static AnimationCurve BuildRingWidthProfile()
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                float width = (i & 1) == 0 ? .76f : 1.08f;
                curve.AddKey(new Keyframe(t, width, 0, 0));
            }
            return curve;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeState()
        {
            Materials.Clear();
            EmissiveMaterials.Clear();
            trainingDoorMesh = null;
            fxRoot = null;
            projectilePool = null;
            impactPool = null;
            ringMaterial = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallSceneHooks()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        static void OnSceneUnloaded(Scene scene)
        {
            // Pools contain Unity references, so invalidate them as soon as their owning
            // scene goes away. The next effect lazily creates a fresh scene-local root.
            if (!fxRoot || fxRoot.gameObject.scene == scene)
            {
                fxRoot = null;
                projectilePool = null;
                impactPool = null;
                ringMaterial = null;
            }
        }

        public static int ProjectilePoolCreatedCount => projectilePool == null ? 0 : projectilePool.CreatedCount;
        public static int ImpactPoolCreatedCount => impactPool == null ? 0 : impactPool.CreatedCount;
        public static int ActiveProjectileViewCount => projectilePool == null ? 0 : projectilePool.ActiveCount;
        public static int ActiveImpactViewCount => impactPool == null ? 0 : impactPool.ActiveCount;

        static Transform FxRoot()
        {
            // A scene transition destroys this root. Dropping the old pools here also drops
            // references to destroyed Unity objects before the next effect is rented.
            if (!fxRoot)
            {
                var root = new GameObject("RiskAI visual FX");
                root.hideFlags = HideFlags.DontSave;
                fxRoot = root.transform;
                projectilePool = new ProjectilePool(MaxProjectileViews);
                impactPool = new ImpactPool(MaxImpactViews);
            }
            return fxRoot;
        }

        static GameObject CreateFxPrimitive(PrimitiveType type, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(FxRoot(), false);
            var collider = go.GetComponent<Collider>();
            if (collider) collider.enabled = false;
            var renderer = go.GetComponent<Renderer>();
            if (renderer) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        internal static void ConfigureProjectile(ArrowFlight flight, AttackKind attack)
        {
            if (flight) flight.ConfigureAppearance(attack);
        }

        internal static void Release(ArrowFlight flight)
        {
            if (!flight) return;
            if (!fxRoot || projectilePool == null) FxRoot();
            projectilePool.Return(flight);
        }

        internal static void Release(ImpactPulse pulse)
        {
            if (!pulse) return;
            if (!fxRoot || impactPool == null) FxRoot();
            impactPool.Return(pulse);
        }

        internal static void Forget(ArrowFlight flight)
        {
            if (projectilePool != null) projectilePool.Forget(flight);
        }

        internal static void Forget(ImpactPulse pulse)
        {
            if (impactPool != null) impactPool.Forget(pulse);
        }
        // Warcraft III patch 1.29 player palette, in the engine's canonical player order.
        // Reference: https://www.hiveworkshop.com/threads/warcraft-iii-color-tags-and-linebreaks.31386/
        static readonly Color[] PlayerColors = {
            new Color32(255,3,3,255), new Color32(0,66,255,255), new Color32(28,230,185,255), new Color32(84,0,129,255),
            new Color32(255,252,1,255), new Color32(254,138,14,255), new Color32(32,192,0,255), new Color32(229,91,176,255),
            new Color32(149,150,151,255), new Color32(126,191,241,255), new Color32(16,98,70,255), new Color32(78,42,4,255),
            new Color32(155,0,0,255), new Color32(0,0,195,255), new Color32(0,234,255,255), new Color32(190,0,254,255)
        };
        static readonly string[] PlayerColorNames = {"Rojo","Azul","Turquesa","Violeta","Amarillo","Naranja","Verde","Rosa","Gris","Azul claro","Verde oscuro","Marrón","Granate","Azul marino","Cian","Magenta"};
        public static Color TeamColor(int team) => PlayerRules.IsPlayer(team)?PlayerColors[team]:Color.white;
        public static string TeamName(int team) => !PlayerRules.IsPlayer(team)?"Neutral":(team==0?"Tú":"IA "+team)+" · "+PlayerColorNames[team];
        public static Material Mat(Color color)
        {
            if (Materials.TryGetValue(color, out var found) && found) return found;
            var template = Resources.Load<Material>("RiskAILit");
            var mat = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = color; mat.SetFloat("_Smoothness", .12f); Materials[color] = mat; return mat;
        }
        public static Material EmissiveMat(Color color, float intensity)
        {
            Color key = color * (1f + intensity);
            if (EmissiveMaterials.TryGetValue(key, out var found) && found) return found;
            var mat = new Material(Mat(color));
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * intensity);
            }
            EmissiveMaterials[key] = mat;
            return mat;
        }
        public static GameObject TrapezoidQuad(Transform parent, string name, Vector3 position, float bottomWidth, float topWidth, float height, float thickness, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var filter = go.AddComponent<MeshFilter>();
            if (!trainingDoorMesh) trainingDoorMesh = CreateTrapezoidMesh(bottomWidth, topWidth, height, thickness);
            filter.sharedMesh = trainingDoorMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(color);
            return go;
        }
        static Mesh CreateTrapezoidMesh(float bottomWidth, float topWidth, float height, float thickness)
        {
            var mesh = new Mesh { name = "Training doorway trapezoid" };
            float z = thickness * .5f;
            mesh.SetVertices(new[]
            {
                new Vector3(-bottomWidth*.5f, 0, -z), new Vector3(bottomWidth*.5f, 0, -z),
                new Vector3(topWidth*.5f, height, -z), new Vector3(-topWidth*.5f, height, -z),
                new Vector3(-bottomWidth*.5f, 0, z), new Vector3(bottomWidth*.5f, 0, z),
                new Vector3(topWidth*.5f, height, z), new Vector3(-topWidth*.5f, height, z)
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 }, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
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
            line.numCornerVertices = 2; line.numCapVertices = 2; line.widthCurve = ringWidthProfile;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            var template = Resources.Load<Material>("RiskAIRing");
            if (!ringMaterial)
                ringMaterial = template ? template : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            line.sharedMaterial = ringMaterial;
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
            if(soldier.Kind==UnitKind.Guard)
            {
                MountedKnightView.Create(soldier);
                Ring(root,.74f,.025f,team);return;
            }
            if(soldier.Kind==UnitKind.Mortar)
            {
                var model=new GameObject("Mortar model");model.transform.SetParent(root,false);
                MortarModel(model.transform,team,soldier);ModelMetrics.MatchStandingHeight(model,soldier.Kind);Ring(root,.70f,.025f,team);
                return;
            }
            var prefab=Resources.Load<GameObject>("Units/"+BattleRules.Model(soldier.Kind));
            if(prefab)
            {
                var model=Object.Instantiate(prefab,root,false);
                model.transform.localScale*=VisualMetrics.UnitScale;
                ModelMetrics.MatchStandingHeight(model,soldier.Kind);
                UnitTeamColor.Apply(model,soldier.Kind,soldier.Team);
                soldier.gameObject.AddComponent<SoldierAnimator>().Initialize(soldier,model);
                Ring(root,Mathf.Max(.33f,SourceGeometry.AgentRadius(soldier.Kind)*1.1f),.022f,team);return;
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
        public static void MortarModel(Transform root,Color team) => MortarModel(root,team,null);
        public static void MortarModel(Transform root,Color team,Soldier soldier)
        {
            // A human gunner reads at unit scale; the short, flared hand-cannon is not a carriage.
            Color brass=new Color(.57f,.38f,.14f), steel=new Color(.34f,.37f,.38f), leather=new Color(.20f,.115f,.055f), skin=new Color(.80f,.57f,.36f);
            Shape(root,PrimitiveType.Capsule,"Mortar gunner coat",new Vector3(0,1.13f,0),new Vector3(.68f,.48f,.43f),team*.82f);
            Shape(root,PrimitiveType.Cube,"Mortar gunner team cloth",new Vector3(0,1.14f,.25f),new Vector3(.48f,.60f,.055f),team);
            Shape(root,PrimitiveType.Sphere,"Mortar gunner head",new Vector3(0,1.78f,.035f),new Vector3(.43f,.46f,.42f),skin);
            Shape(root,PrimitiveType.Cylinder,"Mortar gunner helmet",new Vector3(0,2.00f,.01f),new Vector3(.47f,.18f,.47f),steel);
            for(int side=-1;side<=1;side+=2)
            {
                var leg=new GameObject("Mortar leg pivot");leg.transform.SetParent(root,false);leg.transform.localPosition=new Vector3(side*.18f,.78f,-.03f);
                Shape(leg.transform,PrimitiveType.Capsule,"Mortar gunner boot",new Vector3(0,-.34f,.03f),new Vector3(.25f,.37f,.28f),leather);
                if(soldier){if(side<0)soldier.LeftLeg=leg.transform;else soldier.RightLeg=leg.transform;}
                Shape(root,PrimitiveType.Capsule,"Mortar gunner arm",new Vector3(side*.36f,1.28f,.18f),new Vector3(.19f,.32f,.21f),team*.76f);
            }
            var cannon=new GameObject("Hand cannon pivot");cannon.transform.SetParent(root,false);cannon.transform.localPosition=new Vector3(.18f,1.34f,.26f);cannon.transform.localRotation=Quaternion.Euler(76,0,0);
            Shape(cannon.transform,PrimitiveType.Cylinder,"Bronze hand cannon",new Vector3(0,.28f,0),new Vector3(.19f,.38f,.19f),brass);
            Shape(cannon.transform,PrimitiveType.Cylinder,"Flared hand cannon muzzle",new Vector3(0,.62f,0),new Vector3(.29f,.16f,.29f),brass);
            Shape(cannon.transform,PrimitiveType.Cylinder,"Hand cannon bore",new Vector3(0,.705f,0),new Vector3(.20f,.008f,.20f),new Color(.018f,.015f,.01f));
            Shape(cannon.transform,PrimitiveType.Cube,"Hand cannon stock",new Vector3(0,-.13f,0),new Vector3(.13f,.31f,.13f),leather);
            Shape(root,PrimitiveType.Sphere,"Gunner forward hand",new Vector3(.25f,1.42f,.26f),new Vector3(.18f,.16f,.18f),skin);
            Shape(root,PrimitiveType.Sphere,"Gunner rear hand",new Vector3(.09f,1.27f,.18f),new Vector3(.18f,.16f,.18f),skin);
            if(soldier)soldier.Weapon=cannon.transform;
        }
        public static void Arrow(Vector3 from, Vector3 to, CombatTarget target=null, float damage=0, int team=0, CombatTarget source=null, AttackKind attack=AttackKind.Piercing)
        {
            // CombatWorld owns projectile state and damage. This compatibility entry point
            // only forwards the request so legacy callers keep using the simulation API.
            var session = BattleSession.Current;
            if (session) session.Combat.FireProjectile(from, to, target, damage, team, source, attack);
        }
        public static void Arrow(Vector3 from, Vector3 to, CombatTarget target, float damage, int team, CombatTarget source, bool magic)
        {
            Arrow(from,to,target,damage,team,source,magic?AttackKind.Magic:AttackKind.Piercing);
        }
        public static void Impact(Vector3 point, Color color, float size) => Impact(point, color, size, AttackKind.Piercing);
        public static void Impact(Vector3 point, AttackKind attack, float size)
        {
            Color color = attack == AttackKind.Magic ? new Color(.55f, .70f, 1f) : attack == AttackKind.Siege ? new Color(1f, .42f, .12f) : new Color(1f, .72f, .35f);
            Impact(point, color, size, attack);
        }
        static void Impact(Vector3 point, Color color, float size, AttackKind attack)
        {
            if (!fxRoot || impactPool == null) FxRoot();
            var pulse = impactPool.Rent();
            if (pulse) pulse.Init(BattleSession.Current, point, color, size, attack);
        }

        public static void ProjectileView(BattleSession session, int projectileId, Vector3 from, Vector3 to, float duration, AttackKind attack)
        {
            if (!session) return;
            if (!fxRoot || projectilePool == null) FxRoot();
            var view = projectilePool.Rent();
            if (view) view.Init(session, projectileId, from, to, duration, attack);
        }

        sealed class ProjectilePool
        {
            readonly int capacity;
            readonly Stack<ArrowFlight> available = new Stack<ArrowFlight>();
            readonly HashSet<ArrowFlight> allocated = new HashSet<ArrowFlight>();
            int created;
            int active;

            public ProjectilePool(int max) { capacity = max; }
            public int CreatedCount => created;
            public int ActiveCount => active;

            public ArrowFlight Rent()
            {
                while (available.Count > 0)
                {
                    var view = available.Pop();
                    if (view)
                    {
                        view.MarkRented();
                        active++;
                        return view;
                    }
                }
                if (created >= capacity) return null;
                created++;
                var go = CreateFxPrimitive(PrimitiveType.Cube, "Projectile view");
                var flight = go.AddComponent<ArrowFlight>();
                flight.MarkPoolOwned();
                allocated.Add(flight);
                flight.MarkRented();
                active++;
                return flight;
            }

            public void Return(ArrowFlight view)
            {
                if (!view || view.IsPooled) return;
                if (!view.PoolOwned)
                {
                    view.PrepareForPool();
                    Object.Destroy(view.gameObject);
                    return;
                }
                active = Mathf.Max(0, active - 1);
                view.PrepareForPool();
                available.Push(view);
            }

            public void Forget(ArrowFlight view)
            {
                if (!allocated.Remove(view)) return;
                created = Mathf.Max(0, created - 1);
                if (!view.IsPooled) active = Mathf.Max(0, active - 1);
            }
        }

        sealed class ImpactPool
        {
            readonly int capacity;
            readonly Stack<ImpactPulse> available = new Stack<ImpactPulse>();
            readonly HashSet<ImpactPulse> allocated = new HashSet<ImpactPulse>();
            int created;
            int active;

            public ImpactPool(int max) { capacity = max; }
            public int CreatedCount => created;
            public int ActiveCount => active;

            public ImpactPulse Rent()
            {
                while (available.Count > 0)
                {
                    var pulse = available.Pop();
                    if (pulse)
                    {
                        pulse.MarkRented();
                        active++;
                        return pulse;
                    }
                }
                if (created >= capacity) return null;
                created++;
                var go = CreateFxPrimitive(PrimitiveType.Sphere, "Impact view");
                var pulseView = go.AddComponent<ImpactPulse>();
                allocated.Add(pulseView);
                pulseView.MarkRented();
                active++;
                return pulseView;
            }

            public void Return(ImpactPulse pulse)
            {
                if (!pulse || pulse.IsPooled) return;
                active = Mathf.Max(0, active - 1);
                pulse.PrepareForPool();
                available.Push(pulse);
            }

            public void Forget(ImpactPulse pulse)
            {
                if (!allocated.Remove(pulse)) return;
                created = Mathf.Max(0, created - 1);
                if (!pulse.IsPooled) active = Mathf.Max(0, active - 1);
            }
        }
    }
    public sealed class ArrowFlight : MonoBehaviour
    {
        BattleSession session;
        int projectileId = -1;
        Vector3 from, to;
        float elapsed, duration;
        AttackKind attack;
        bool legacy;
        bool pooled;
        bool poolOwned;
        AttackKind configuredAttack;
        bool appearanceConfigured;
        Transform piercingView, magicView, siegeView;

        static Renderer Part(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var part=GameObject.CreatePrimitive(type);part.name=name;part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=scale;
            var collider=part.GetComponent<Collider>();if(collider){collider.enabled=false;Object.Destroy(collider);}
            var renderer=part.GetComponent<Renderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sharedMaterial=VisualFactory.Mat(color);
            return renderer;
        }
        static Transform Group(Transform parent,string name)
        {
            var group=new GameObject(name).transform;group.SetParent(parent,false);return group;
        }
        void EnsureAppearance()
        {
            if(piercingView)return;
            var rootRenderer=GetComponent<Renderer>();if(rootRenderer)rootRenderer.enabled=false;
            Color wood=new Color(.30f,.16f,.065f),metal=new Color(.72f,.76f,.79f),feather=new Color(.84f,.73f,.46f);
            piercingView=Group(transform,"Piercing projectile");
            Part(piercingView,PrimitiveType.Cube,"Bolt shaft",Vector3.zero,new Vector3(.035f,.035f,.48f),wood);
            var tip=Part(piercingView,PrimitiveType.Capsule,"Bolt metal point",new Vector3(0,0,.30f),new Vector3(.07f,.13f,.07f),metal);tip.transform.localRotation=Quaternion.Euler(90,0,0);
            Part(piercingView,PrimitiveType.Cube,"Bolt fletching top",new Vector3(0,.045f,-.22f),new Vector3(.10f,.018f,.10f),feather);
            Part(piercingView,PrimitiveType.Cube,"Bolt fletching side",new Vector3(.045f,0,-.22f),new Vector3(.018f,.10f,.10f),feather);

            magicView=Group(transform,"Magic projectile");
            Part(magicView,PrimitiveType.Sphere,"Arcane orb",Vector3.zero,Vector3.one*.22f,new Color(.42f,.55f,1f));
            Part(magicView,PrimitiveType.Sphere,"Arcane core",Vector3.zero,Vector3.one*.11f,new Color(.72f,.48f,1f));

            siegeView=Group(transform,"Siege projectile");
            Part(siegeView,PrimitiveType.Sphere,"Mortar shell",Vector3.zero,Vector3.one*.18f,new Color(.16f,.17f,.16f));
            Part(siegeView,PrimitiveType.Cube,"Mortar ember trail",new Vector3(0,0,-.23f),new Vector3(.045f,.045f,.30f),new Color(1f,.39f,.12f));
            piercingView.gameObject.SetActive(false);magicView.gameObject.SetActive(false);siegeView.gameObject.SetActive(false);
        }
        internal void ConfigureAppearance(AttackKind kind)
        {
            EnsureAppearance();
            if(appearanceConfigured&&configuredAttack==kind)return;
            appearanceConfigured=true;configuredAttack=kind;transform.localScale=Vector3.one;
            piercingView.gameObject.SetActive(kind!=AttackKind.Magic&&kind!=AttackKind.Siege);
            magicView.gameObject.SetActive(kind==AttackKind.Magic);
            siegeView.gameObject.SetActive(kind==AttackKind.Siege);
        }

        internal bool IsPooled => pooled;
        internal bool PoolOwned => poolOwned;
        internal void MarkRented() { pooled = false; }
        internal void MarkPoolOwned() { poolOwned = true; }
        internal void PrepareForPool()
        {
            if (pooled) return;
            pooled = true;
            session = null;
            projectileId = -1;
            legacy = false;
            gameObject.SetActive(false);
        }

        internal void Init(BattleSession owner, int id, Vector3 a, Vector3 b, float travelDuration, AttackKind kind)
        {
            session = owner;
            projectileId = id;
            from = a;
            to = b;
            duration = Mathf.Max(.01f, travelDuration);
            elapsed = 0;
            attack = kind;
            legacy = false;
            pooled = false;
            VisualFactory.ConfigureProjectile(this, attack);
            gameObject.SetActive(true);
            transform.position = from;
            var direction = to - from;
            if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        public void Init(Vector3 a,Vector3 b,CombatTarget victim,float hit,int attacker,CombatTarget shooter,bool arcane)
        { Init(a,b,victim,hit,attacker,shooter,arcane?AttackKind.Magic:AttackKind.Piercing); }
        public void Init(Vector3 a,Vector3 b,CombatTarget victim,float hit,int attacker,CombatTarget shooter,AttackKind kind)
        {
            // Kept for old callers and tests. Legacy initialization is presentation-only;
            // damage and target references are deliberately ignored.
            session = null;
            projectileId = -1;
            from = a;
            to = b;
            duration = Mathf.Clamp(Vector3.Distance(a, b) / 25f, .15f, .6f);
            elapsed = 0;
            attack = kind;
            legacy = true;
            pooled = false;
            VisualFactory.ConfigureProjectile(this, attack);
            gameObject.SetActive(true);
            transform.position = from;
            var direction = to - from;
            if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        void Update()
        {
            if (session)
            {
                if (session.Paused || session.Winner >= 0) return;
                if (!session.Combat.TryGetProjectile(projectileId, out var state))
                {
                    VisualFactory.Release(this);
                    return;
                }
                from = state.From;
                to = state.To;
                attack = state.Attack;
                if (attack != configuredAttack)
                {
                    VisualFactory.ConfigureProjectile(this, attack);
                }
                SetPosition(state.Progress);
                return;
            }

            if (!legacy) { VisualFactory.Release(this); return; }
            elapsed += Time.unscaledDeltaTime;
            SetPosition(elapsed / duration);
            if (elapsed >= duration) VisualFactory.Release(this);
        }

        void OnDestroy() { VisualFactory.Forget(this); }

        void SetPosition(float progress)
        {
            float t = Mathf.Clamp01(progress);
            float arc = attack == AttackKind.Siege ? Mathf.Lerp(1.6f, 3.4f, Mathf.Clamp01(Vector3.Distance(from, to) / 18f)) : .5f;
            transform.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * arc;
            var direction = to - from + Vector3.up * Mathf.Cos(t * Mathf.PI) * Mathf.PI * arc;
            if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    public sealed class ImpactPulse : MonoBehaviour
    {
        enum Profile { Piercing, Magic, Siege }
        const float Lifetime = .22f;
        BattleSession session;
        float remaining;
        bool pooled;
        Profile profile;
        Vector3 baseScale;
        Transform ring, sparks, runes;
        Renderer coreRenderer, ringRenderer;
        internal bool IsPooled => pooled;
        internal void MarkRented() { pooled = false; }
        internal void PrepareForPool()
        {
            if (pooled) return;
            pooled = true;
            session = null;
            gameObject.SetActive(false);
        }

        static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>(); if (collider) { collider.enabled = false; Object.Destroy(collider); }
            var renderer = go.GetComponent<Renderer>(); renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go.transform;
        }
        void EnsureAppearance()
        {
            if (ring) return;
            coreRenderer = GetComponent<Renderer>();
            if (coreRenderer) { coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; coreRenderer.receiveShadows = false; }
            ring = Part(transform, PrimitiveType.Cylinder, "Impact shock ring", Vector3.zero, new Vector3(1f, .045f, 1f));
            ringRenderer = ring.GetComponent<Renderer>();
            sparks = new GameObject("Impact sparks").transform; sparks.SetParent(transform, false);
            Part(sparks, PrimitiveType.Cube, "Impact spark east", new Vector3(.28f, .09f, .04f), new Vector3(.52f, .035f, .04f));
            Part(sparks, PrimitiveType.Cube, "Impact spark west", new Vector3(-.18f, .15f, -.06f), new Vector3(.035f, .42f, .035f));
            runes = new GameObject("Mage rune shockwave").transform; runes.SetParent(transform, false);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                var rune = Part(runes, PrimitiveType.Cube, "Arcane rune", new Vector3(Mathf.Cos(angle) * .38f, .025f, Mathf.Sin(angle) * .38f), new Vector3(.13f, .018f, .31f));
                rune.localRotation = Quaternion.Euler(0, -i * 90, 0);
            }
            ring.gameObject.SetActive(false); sparks.gameObject.SetActive(false); runes.gameObject.SetActive(false);
        }

        internal void Init(BattleSession owner, Vector3 point, Color color, float size, AttackKind attack)
        {
            EnsureAppearance();
            session = owner;
            remaining = Lifetime;
            transform.position = point;
            profile = attack == AttackKind.Magic ? Profile.Magic : attack == AttackKind.Siege ? Profile.Siege : Profile.Piercing;
            baseScale = Vector3.one * size; transform.localScale = baseScale;
            var material = VisualFactory.Mat(color);
            if (coreRenderer) coreRenderer.sharedMaterial = material;
            if (ringRenderer) ringRenderer.sharedMaterial = material;
            foreach (var renderer in sparks.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
            foreach (var renderer in runes.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
            ring.gameObject.SetActive(profile != Profile.Piercing);
            sparks.gameObject.SetActive(profile != Profile.Magic);
            runes.gameObject.SetActive(profile == Profile.Magic);
            ring.localScale = profile == Profile.Siege ? new Vector3(.65f, .045f, .65f) : new Vector3(.42f, .025f, .42f);
            pooled = false;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (session && (session.Paused || session.Winner >= 0)) return;
            float delta = Time.unscaledDeltaTime;
            remaining -= delta;
            float t = Mathf.Clamp01(1 - remaining / Lifetime);
            transform.localScale = baseScale * Mathf.Lerp(1f, .24f, t);
            if (ring && ring.gameObject.activeSelf)
            {
                float radius = profile == Profile.Siege ? Mathf.Lerp(.65f, 2f, t) : Mathf.Lerp(.42f, 1.28f, t);
                ring.localScale = new Vector3(radius, profile == Profile.Siege ? .045f : .025f, radius);
            }
            if (sparks && sparks.gameObject.activeSelf)
            {
                sparks.localScale = Vector3.one * Mathf.Lerp(1f, .1f, t);
                sparks.localRotation = Quaternion.Euler(0, t * 130f, 0);
            }
            if (runes && runes.gameObject.activeSelf)
            {
                runes.localScale = Vector3.one * Mathf.Lerp(.7f, 2.1f, t);
                runes.localRotation = Quaternion.Euler(0, t * 145f, 0);
            }
            if (remaining <= 0) VisualFactory.Release(this);
        }

        void OnDestroy() { VisualFactory.Forget(this); }
    }
}

