using System.Collections.Generic;
using RiskAI.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
namespace RiskAI
{
    public sealed class RiskBootstrap : MonoBehaviour
    {
        public const int DefaultPathfindingIterationsPerFrame = 500;
        public const int MinPathfindingIterationsPerFrame = 100;
        public const int MaxPathfindingIterationsPerFrame = 2000;
        public static int PathfindingIterationsPerFrame { get; private set; } = DefaultPathfindingIterationsPerFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ReadLaunchSeed()
        {
            // Read launch arguments once, so F1 can choose a different seed on a scene restart.
            var args=LaunchArguments.Get();
            for(int a=0;a<args.Length-1;a++)if(args[a]=="--riskai-map")BattleSession.MapForNewMatch=args[a+1]=="europe"?ScenarioMap.Europe:args[a+1]=="world"||args[a+1]=="newworld"?ScenarioMap.NewWorld:args[a+1]=="riverlands"?ScenarioMap.Riverlands:ScenarioMap.Classic;
            for(int a=0;a<args.Length-1;a++)if(args[a]=="--riskai-seed" && int.TryParse(args[a+1],out int seed))BattleSession.SeedForNewMatch=seed;
            for(int a=0;a<args.Length-1;a++)if(args[a]=="--riskai-players" && int.TryParse(args[a+1],out int players))BattleSession.PlayerCountForNewMatch=Mathf.Clamp(players,2,PlayerRules.MaxPlayers);
            for(int a=0;a<args.Length-1;a++)if(args[a]=="--riskai-path-budget" && int.TryParse(args[a+1],out int budget))PathfindingIterationsPerFrame=Mathf.Clamp(budget,MinPathfindingIterationsPerFrame,MaxPathfindingIterationsPerFrame);
        }
        void Start()
        {
            // Apply after scene navigation settings and the generated NavMesh are
            // loaded: scene initialization can overwrite an earlier Awake value.
            NavMesh.pathfindingIterationsPerFrame = PathfindingIterationsPerFrame;
            Debug.Log($"RISKAI_NAV_BUDGET applied={PathfindingIterationsPerFrame} requestedRange={MinPathfindingIterationsPerFrame}..{MaxPathfindingIterationsPerFrame}");
        }
        void Awake()
        {
            var startup=new StartupMetrics();
            MapLayout.Configure(BattleSession.MapForNewMatch);
            Application.targetFrameRate=Application.platform==RuntimePlatform.WebGLPlayer||PlatformPresentation.TouchCapable?60:120;
            WorldArt.ResetRoads();Shader.SetGlobalFloat("_RiskMapScale",MapLayout.Spacing);
            UnityEngine.InputSystem.InputSystem.settings.scrollDeltaBehavior=UnityEngine.InputSystem.InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            var session=gameObject.AddComponent<BattleSession>();session.Initialize();var owners=session.StartingOwners();var capitals=new int[session.PlayerCount];
            gameObject.AddComponent<RuntimeDiagnostics>().Initialize(session);
            for(int player=0;player<capitals.Length;player++)capitals[player]=-1;
            for(int i=0;i<owners.Length;i++)if(owners[i]>=0 && (capitals[owners[i]]<0 || MapLayout.Towns[i].Capital))capitals[owners[i]]=i;
            var terrain=new GameObject("Battlefield · NavMesh geometry");
            if(MapLayout.IsImported)
            {
                ImportedTerrain.Create(terrain.transform);
            }
            else StrategicTerrain.Create(terrain.transform);
            startup.Mark("terrain");
            for(int i=0;i<MapLayout.Towns.Length;i++)
            {
                var city=MapLayout.Towns[i];int owner=owners[i];bool capital=owner>=0 && capitals[owner]==i;
                var go=new GameObject(city.Name);go.transform.SetParent(terrain.transform);go.transform.position=city.Position;
                go.AddComponent<Settlement>().Initialize(session,city.Id,city.Name,owner,city.Region,capital,city.Country,MapLayout.IsImported?city.ClaimPoint:(Vector3?)null,city.IsPort);
            }
            WorldArt.Cities(session.Towns);
            if(!MapLayout.IsImported)TerrainHydrology.CreateCrossings(terrain.transform);
            startup.Mark("buildings");
            var nav=terrain.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;
            nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.overrideVoxelSize=true;nav.voxelSize=MapLayout.IsImported?.12f:.15f;nav.BuildNavMesh();
            // NavMeshSurface detaches its NavMeshData on disable but does not own-destroy
            // the runtime object; the terrain scene owner gives it the same lifetime as its colliders.
            GeneratedResourceOwner.For(terrain.transform).Track(nav.navMeshData);
            startup.Mark("navigation");
            var assignedGarrisons = new HashSet<Soldier>();
            foreach(var town in session.Towns)
            {
                town.SetRally(town.IsPort?town.ClaimPoint:MapLayout.Point(town.Rally.x,town.Rally.z));
                // Saran creates one h00B at each circle, including neutral posts.
                session.Spawn(PlayerRules.ToCombatTeam(town.State.Owner),UnitKind.Archer,town.ClaimPoint);
                town.InitializeGarrison(session.Units,assignedGarrisons);
            }
            if(!MapLayout.IsImported)WorldLife.Create(session,terrain.transform);
            for(int c=0;c<MapLayout.Countries.Length;c++)session.Camps.Add(CountryCamp.Create(session,c,terrain.transform));
            TerritoryMarkers.Create(session,terrain.transform);
            NavalWorld.Create(session,terrain.transform);
            // Geometry is complete and the NavMesh is baked. Resolve static
            // cargo approaches once instead of during the first AI offensive.
            foreach(var harbor in NavalWorld.Current.Harbors)harbor.TryTransportLanding(out _,out _);
            startup.Mark("roster_and_ports");
            GroundCover.Create(session,terrain.transform);
            startup.Mark("ground_cover");
            session.Canopies.Build(terrain.transform);
            startup.Mark("canopy_index");
            var cameraObject=new GameObject("RTS Camera");var camera=cameraObject.AddComponent<Camera>();cameraObject.tag="MainCamera";
            camera.orthographic=false;camera.fieldOfView=44;camera.nearClipPlane=.3f;camera.farClipPlane=Mathf.Max(440,(MapLayout.HalfWidth+MapLayout.HalfDepth)*5);
            camera.transform.rotation=RtsCameraRig.DefaultRotation;
            camera.backgroundColor=new Color(.035f,.075f,.13f);camera.clearFlags=CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.22f;sun.color=new Color(1,.95f,.83f);
            sun.transform.rotation=Quaternion.Euler(53,-38,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.95f;sun.shadowBias=.025f;sun.shadowNormalBias=.1f;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.43f,.53f,.68f);
            RenderSettings.ambientEquatorColor=new Color(.3f,.37f,.36f);RenderSettings.ambientGroundColor=new Color(.18f,.24f,.23f);
            RenderSettings.fog=!MapLayout.IsImported;RenderSettings.fogColor=camera.backgroundColor;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=MapLayout.IsImported?1500:260;RenderSettings.fogEndDistance=MapLayout.IsImported?2100:420;
            var controller=gameObject.AddComponent<RtsController>();controller.Initialize(session,camera);controller.FocusHome();
            gameObject.AddComponent<StrategicMapView>().Initialize(session,camera,terrain.transform);
            startup.Mark("territory_atlas");
            gameObject.AddComponent<BattleHud>().Initialize(session,controller,camera);
            if(BattleSession.CountdownForNewMatch)session.BeginStartCountdown();
            startup.Mark("ready");
        }
    }
}
