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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ReadLaunchSeed()
        {
            // Read launch arguments once, so F1 can choose a different seed on a scene restart.
            var args=System.Environment.GetCommandLineArgs();
            for(int a=0;a<args.Length-1;a++)if(args[a]=="--riskai-seed" && int.TryParse(args[a+1],out int seed))BattleSession.SeedForNewMatch=seed;
        }
        void Awake()
        {
            Application.targetFrameRate=120;WorldArt.ResetRoads();Shader.SetGlobalFloat("_RiskMapScale",MapLayout.Spacing);
            UnityEngine.InputSystem.InputSystem.settings.scrollDeltaBehavior=UnityEngine.InputSystem.InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            var session=gameObject.AddComponent<BattleSession>();var owners=session.StartingOwners();var capitals=new int[]{-1,-1};
            for(int i=0;i<owners.Length;i++)if(owners[i]>=0 && (capitals[owners[i]]<0 || MapLayout.Towns[i].Capital))capitals[owners[i]]=i;
            var terrain=new GameObject("Battlefield · NavMesh geometry");
            StrategicTerrain.Create(terrain.transform);
            for(int i=0;i<MapLayout.Towns.Length;i++)
            {
                var city=MapLayout.Towns[i];int owner=owners[i];bool capital=owner>=0 && capitals[owner]==i;
                var go=new GameObject(city.Name);go.transform.SetParent(terrain.transform);go.transform.position=city.Position;
                go.AddComponent<Settlement>().Initialize(session,city.Id,city.Name,owner,city.Region,capital,city.Country);
            }
            WorldArt.Cities(session.Towns);
            var nav=terrain.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;
            nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.overrideVoxelSize=true;nav.voxelSize=.15f;nav.BuildNavMesh();
            foreach(var town in session.Towns)
            {
                town.SetRally(MapLayout.Point(town.Rally.x,town.Rally.z));
                if(town.State.Owner<0)
                {
                    session.Spawn(2,UnitKind.Footman,town.transform.position+new Vector3(-1.9f,0,2.5f));
                    session.Spawn(2,UnitKind.Archer,town.transform.position+new Vector3(1.9f,0,2.5f));
                }
                else for(int i=0;i<(town.IsCapital?14:2);i++)session.Spawn(town.State.Owner,i%4==0?UnitKind.Archer:UnitKind.Footman,town.transform.position+new Vector3((i%5-2)*.9f,0,(town.State.Owner==0?-1:1)*(6+(i/5)*.9f)));
            }
            WorldLife.Create(session,terrain.transform);
            TerritoryMarkers.Create(session,terrain.transform);
            var assignedGarrisons = new HashSet<Soldier>();
            foreach (var town in session.Towns) town.InitializeGarrison(session.Units, assignedGarrisons);
            NavalWorld.Create(session,terrain.transform);
            var cameraObject=new GameObject("RTS Camera");var camera=cameraObject.AddComponent<Camera>();cameraObject.tag="MainCamera";
            camera.orthographic=false;camera.fieldOfView=44;camera.nearClipPlane=.3f;camera.farClipPlane=320;
            camera.transform.rotation=Quaternion.Euler(49,30,0);
            camera.backgroundColor=new Color(.035f,.075f,.13f);camera.clearFlags=CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.22f;sun.color=new Color(1,.95f,.83f);
            sun.transform.rotation=Quaternion.Euler(53,-38,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.95f;sun.shadowBias=.025f;sun.shadowNormalBias=.1f;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.43f,.53f,.68f);
            RenderSettings.ambientEquatorColor=new Color(.3f,.37f,.36f);RenderSettings.ambientGroundColor=new Color(.18f,.24f,.23f);
            RenderSettings.fog=true;RenderSettings.fogColor=camera.backgroundColor;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=155;RenderSettings.fogEndDistance=280;
            var controller=gameObject.AddComponent<RtsController>();controller.Initialize(session,camera);controller.FocusHome();
            gameObject.AddComponent<BattleHud>().Initialize(session,controller,camera);
            session.Message(session.LayoutName+" · semilla "+session.Seed+". Completa países para cobrar y recibir refuerzos.");
        }
    }
}
