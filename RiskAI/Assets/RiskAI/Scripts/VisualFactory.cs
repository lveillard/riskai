using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskAI
{
    public static class VisualFactory
    {
        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
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
        static readonly Color[] PlayerColors = {
            new Color(.17f,.55f,.95f), new Color(.85f,.22f,.19f), new Color(.08f,.82f,.73f), new Color(.65f,.26f,.85f),
            new Color(.95f,.81f,.10f), new Color(1,.45f,.08f), new Color(.30f,.78f,.20f), new Color(.96f,.38f,.68f),
            new Color(.48f,.61f,.70f), new Color(.59f,.32f,.15f), new Color(.10f,.39f,.25f), new Color(.25f,.28f,.72f),
            new Color(.75f,.58f,.94f), new Color(.98f,.64f,.45f), new Color(.58f,.62f,.13f), new Color(.39f,.85f,.96f)
        };
        static readonly string[] PlayerColorNames = {"Azul","Carmesí","Turquesa","Violeta","Oro","Naranja","Verde","Rosa","Acero","Cobre","Bosque","Índigo","Lavanda","Coral","Oliva","Celeste"};
        public static Color TeamColor(int team) => PlayerRules.IsPlayer(team)?PlayerColors[team]:new Color(.96f,.91f,.72f);
        public static string TeamName(int team) => !PlayerRules.IsPlayer(team)?"Neutral":(team==0?"Tú":"IA "+team)+" · "+PlayerColorNames[team];
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
            if(soldier.Kind==UnitKind.Mortar)
            {
                var model=new GameObject("Mortar model");model.transform.SetParent(root,false);
                MortarModel(model.transform,team);ModelMetrics.MatchStandingHeight(model,soldier.Kind);Ring(root,.70f,.025f,team);
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
            // CombatWorld owns projectile state and damage. This compatibility entry point
            // only forwards the request so legacy callers keep using the simulation API.
            var session = BattleSession.Current;
            if (session) session.Combat.FireProjectile(from, to, target, damage, team, source, attack);
        }
        public static void Arrow(Vector3 from, Vector3 to, CombatTarget target, float damage, int team, CombatTarget source, bool magic)
        {
            Arrow(from,to,target,damage,team,source,magic?AttackKind.Magic:AttackKind.Piercing);
        }
        public static void Impact(Vector3 point, Color color, float size)
        {
            if (!fxRoot || impactPool == null) FxRoot();
            var pulse = impactPool.Rent();
            if (pulse) pulse.Init(BattleSession.Current, point, color, size);
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
            float arc = attack == AttackKind.Siege ? 2f : .5f;
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
        Transform ring;
        Renderer ringRenderer;
        internal bool IsPooled => pooled;
        internal void MarkRented() { pooled = false; }
        internal void PrepareForPool()
        {
            if (pooled) return;
            pooled = true;
            session = null;
            gameObject.SetActive(false);
        }

        void EnsureAppearance()
        {
            if(ring)return;
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Impact shock ring";go.transform.SetParent(transform,false);
            go.transform.localPosition=Vector3.zero;go.transform.localScale=new Vector3(1f,.045f,1f);
            var collider=go.GetComponent<Collider>();if(collider){collider.enabled=false;Object.Destroy(collider);}
            ringRenderer=go.GetComponent<Renderer>();ringRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;ringRenderer.receiveShadows=false;
            ring=go.transform;ring.gameObject.SetActive(false);
        }

        internal void Init(BattleSession owner, Vector3 point, Color color, float size)
        {
            EnsureAppearance();
            session = owner;
            remaining = Lifetime;
            transform.position = point;
            profile=color.b>color.r?Profile.Magic:size>=.6f?Profile.Siege:Profile.Piercing;
            baseScale=Vector3.one*size;transform.localScale=baseScale;
            var renderer = GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = VisualFactory.Mat(color);
            if(ringRenderer)ringRenderer.sharedMaterial=VisualFactory.Mat(color);
            ring.gameObject.SetActive(profile!=Profile.Piercing);
            if(ring)ring.localScale=profile==Profile.Siege?new Vector3(.65f,.045f,.65f):new Vector3(.42f,.025f,.42f);
            pooled = false;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (session && (session.Paused || session.Winner >= 0)) return;
            float delta = Time.unscaledDeltaTime;
            remaining -= delta;
            float t=Mathf.Clamp01(1-remaining/Lifetime);
            transform.localScale=baseScale*Mathf.Lerp(1f,.24f,t);
            if(ring&&ring.gameObject.activeSelf)
            {
                float radius=profile==Profile.Siege?Mathf.Lerp(.65f,2f,t):Mathf.Lerp(.42f,1.2f,t);
                ring.localScale=new Vector3(radius,profile==Profile.Siege?.045f:.025f,radius);
            }
            if (remaining <= 0) VisualFactory.Release(this);
        }

        void OnDestroy() { VisualFactory.Forget(this); }
    }
}

