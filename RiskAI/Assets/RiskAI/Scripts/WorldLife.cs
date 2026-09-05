using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Lightweight ambient life added after the playable world has been built.</summary>
    public static class WorldLife
    {
        static readonly Color Smoke = new Color(.43f, .46f, .44f, .36f);
        static readonly Color Flame = new Color(1f, .55f, .12f, .65f);

        public static void Create(BattleSession battle, Transform root)
        {
            if (!battle || !root) return;
            foreach (var town in battle.Towns)
            {
                if (!town) continue;
                AddSmoke(town.transform);
                if (town.IsCapital) AddCampfire(root, town.transform.position);
                AddBannerMotion(town.transform);
            }
            AddBirds(root);
            AddPondLife(root,new Vector2(-11,-30),new Vector2(8,5),29);
            AddPondLife(root,new Vector2(16,-4),new Vector2(3,2),11);
            TerrainHydrology.Create(root);
        }

        static void AddPondLife(Transform root,Vector2 center,Vector2 size,int count)
        {
            var random=new System.Random(count*171);var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int k=0;k<count;k++)
            {
                float angle=k*Mathf.PI*2/count,rad=.94f+(float)random.NextDouble()*.1f;
                var point=MapLayout.Point((center.x+Mathf.Cos(angle)*size.x*rad)*MapLayout.Spacing,(center.y+Mathf.Sin(angle)*size.y*rad)*MapLayout.Spacing);
                if(k%4==0){WorldArt.Rock(root,point,.45f+(float)random.NextDouble()*.5f,k);continue;}
                for(int s=0;s<5;s++)
                {
                    var p=point+new Vector3((float)random.NextDouble()*.4f,0,(float)random.NextDouble()*.4f);
                    float height=.4f+(float)random.NextDouble()*.6f;var lean=new Vector3(Mathf.Cos(s*2.4f),0,Mathf.Sin(s*2.4f))*.18f;
                    int v=vertices.Count;vertices.Add(p-Vector3.right*.035f);vertices.Add(p+Vector3.right*.035f);vertices.Add(p+Vector3.up*height+lean);
                    triangles.Add(v);triangles.Add(v+1);triangles.Add(v+2);triangles.Add(v+2);triangles.Add(v+1);triangles.Add(v);
                }
            }
            var reeds=new GameObject("Lakeside reeds");reeds.transform.SetParent(root,false);var mesh=new Mesh{name="Original reed tufts"};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            reeds.AddComponent<MeshFilter>().sharedMesh=mesh;reeds.AddComponent<MeshRenderer>().sharedMaterial=VisualFactory.Mat(new Color(.31f,.39f,.13f));
        }
        static Material AtmosphereMaterial() => Resources.Load<Material>("Atmosphere") ?? VisualFactory.Mat(Color.white);

        static void AddSmoke(Transform town)
        {
            var cap = FindNamed(town, "Chimney cap");
            if (!cap) return;
            var anchor = new GameObject("Chimney smoke").transform;
            anchor.SetParent(cap, false); anchor.localPosition = Vector3.up * .18f;
            var ps = anchor.gameObject.AddComponent<ParticleSystem>();
            ConfigureParticles(ps, 3, new Vector2(2.5f, 4f), new Vector2(.25f, .45f), new Vector2(.28f, .65f), Smoke);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(.15f); velocity.y = new ParticleSystem.MinMaxCurve(.3f); velocity.z = new ParticleSystem.MinMaxCurve(.03f);
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.7f,1,2.2f));
        }

        static void AddCampfire(Transform root, Vector3 townPosition)
        {
            var basePoint = townPosition + new Vector3(-2.5f, 0, -3.3f);
            basePoint.y = MapLayout.Height(basePoint.x, basePoint.z) + .12f;
            for (int i = 0; i < 3; i++)
            {
                var log = VisualFactory.Shape(root, PrimitiveType.Cylinder, "Campfire log", basePoint + Vector3.up * (.16f + i * .03f), new Vector3(.16f, 1.05f, .16f), new Color(.28f, .12f, .055f));
                log.transform.localRotation = Quaternion.Euler(0, i * 60f, 68f);
            }
            var flame = new GameObject("Capital campfire flame"); flame.transform.SetParent(root, false); flame.transform.position = basePoint + Vector3.up * .55f;
            var ps = flame.AddComponent<ParticleSystem>();
            ConfigureParticles(ps, 6, new Vector2(.45f, .9f), new Vector2(.5f, .8f), new Vector2(.12f, .28f), Flame);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.y = new ParticleSystem.MinMaxCurve(.25f);
            var light = flame.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f, .48f, .16f); light.intensity = .45f; light.range = 3f; light.shadows = LightShadows.None;
        }

        static void ConfigureParticles(ParticleSystem ps, int emission, Vector2 lifetime, Vector2 speed, Vector2 size, Color color)
        {
            var main = ps.main; main.loop = true; main.playOnAwake = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y); main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y); main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y); main.startColor = color;
            var emissionModule = ps.emission; emissionModule.rateOverTime = emission;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .12f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.sharedMaterial = AtmosphereMaterial(); renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            var colorOverLifetime = ps.colorOverLifetime; colorOverLifetime.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); colorOverLifetime.color = gradient;
            ps.Play();
        }

        static void AddBannerMotion(Transform town)
        {
            foreach (var renderer in town.GetComponentsInChildren<Renderer>())
                if (renderer.name == "Banner") renderer.gameObject.AddComponent<AmbientBanner>();
        }

        static void AddBirds(Transform root)
        {
            var flock = new GameObject("Ambient birds"); flock.transform.SetParent(root, false);
            var random = new System.Random(1307);
            for (int i = 0; i < 3; i++)
            {
                var bird = new GameObject("Sea bird"); bird.transform.SetParent(flock.transform, false);
                AddWing(bird.transform, "Left wing", true, i); AddWing(bird.transform, "Right wing", false, i);
                bird.AddComponent<AmbientBird>().Initialize(new Vector3(-55*MapLayout.Spacing,0,44*MapLayout.Spacing),10f+(float)random.NextDouble()*7f,8f+(float)random.NextDouble()*4f,i*2.1f);
            }
        }

        static void AddWing(Transform bird, string name, bool left, int seed)
        {
            var wing = new GameObject(name); wing.transform.SetParent(bird, false); wing.transform.localPosition = new Vector3(left ? -.12f : .12f, 0, 0);
            wing.AddComponent<MeshFilter>().sharedMesh = WingMesh(left);
            wing.AddComponent<MeshRenderer>().sharedMaterial = VisualFactory.Mat(seed % 2 == 0 ? new Color(.68f, .7f, .68f) : new Color(.35f, .37f, .36f));
        }

        static Mesh WingMesh(bool left)
        {
            var mesh = new Mesh(); float side = left ? -1 : 1;
            mesh.vertices = new[] { Vector3.zero, new Vector3(side * .65f, .08f, .08f), new Vector3(side * .25f, .02f, -.24f) };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 0 }; mesh.RecalculateNormals(); return mesh;
        }

        static Transform FindNamed(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>()) if (child.name == name) return child;
            return null;
        }
    }

    public sealed class AmbientBanner : MonoBehaviour
    {
        Quaternion initial;
        void Awake() { initial = transform.localRotation; }
        void Update() { transform.localRotation = initial * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 1.6f) * 4f); }
    }

    public sealed class AmbientBird : MonoBehaviour
    {
        Vector3 center; float radius, altitude, phase;Transform[] wings;
        public void Initialize(Vector3 flockCenter, float orbitRadius, float height, float startPhase) { center = flockCenter; radius = orbitRadius; altitude = height; phase = startPhase;wings=GetComponentsInChildren<Transform>(); }
        void Update()
        {
            float t = Time.time * .16f + phase; transform.position = center + new Vector3(Mathf.Cos(t) * radius, altitude + Mathf.Sin(t * 1.7f) * .7f, Mathf.Sin(t) * radius); transform.rotation = Quaternion.Euler(Mathf.Sin(t * 3f) * 5f, -t * Mathf.Rad2Deg, Mathf.Sin(t * 3f) * 7f);
            float flap = Mathf.Sin(Time.time * 8f + phase) * 24f;
            if(wings!=null)foreach (var child in wings) if (child != transform) child.localRotation = Quaternion.Euler(0, 0, (child.localPosition.x < 0 ? flap : -flap));
        }
    }
}

