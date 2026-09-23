using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RiskAI.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RiskAI.Editor
{
    /// <summary>Fast, deterministic water review without producing a player build.</summary>
    public static class RiskWaterPreview
    {
        const int Width=1600,Height=900;

        [MenuItem("RiskAI/Capture Water Preview")]
        public static void Capture()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            int quality=QualitySettings.GetQualityLevel(),targetFrameRate=Application.targetFrameRate;
            float timeScale=Time.timeScale;var random=UnityEngine.Random.state;
            var previousMap=BattleSession.MapForNewMatch;var previousMode=BattleSession.ModeForNewMatch;
            var previousLayout=BattleSession.LayoutForNewMatch;var previousDifficulty=BattleSession.DifficultyForNewMatch;
            int previousSeed=BattleSession.SeedForNewMatch,previousPlayers=BattleSession.PlayerCountForNewMatch;
            bool previousCountdown=BattleSession.CountdownForNewMatch,previousRelief=ImportedLandscapeAugment.Enabled;
            var previousScenario=MapLayout.Scenario;
            try
            {
                RiskProjectSetup.Prepare();
                int mobile=Array.IndexOf(QualitySettings.names,"Mobile");
                if(mobile>=0)QualitySettings.SetQualityLevel(mobile,true);
                var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                int samples=pipeline?Mathf.Max(1,pipeline.msaaSampleCount):1;
                string directory=OutputDirectory();Directory.CreateDirectory(directory);
                Debug.Log($"RISKAI_WATER_PREVIEW_BEGIN: output={directory} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} pipeline={(pipeline?pipeline.name:"none")} depth={(pipeline&&pipeline.supportsCameraDepthTexture)} opaque={(pipeline&&pipeline.supportsCameraOpaqueTexture)} msaa={samples}");
                // Use one fresh scene per process: the Input System's editor
                // defaults do not support repeated manual runtime bootstraps.
                string[] args=Environment.GetCommandLineArgs();
                bool world=false;
                for(int i=0;i<args.Length-1;i++)if(args[i]=="--riskai-map")world=args[i+1]=="world";
                CaptureMap(world?ScenarioMap.NewWorld:ScenarioMap.Europe,world?"newworld-033":"europe-033",19031,directory);
                Debug.Log("RISKAI_WATER_PREVIEW_OK: "+directory);
            }
            catch(Exception error){Debug.LogException(error);throw;}
            finally
            {
                QualitySettings.SetQualityLevel(quality,true);Application.targetFrameRate=targetFrameRate;Time.timeScale=timeScale;UnityEngine.Random.state=random;
                BattleSession.MapForNewMatch=previousMap;BattleSession.ModeForNewMatch=previousMode;BattleSession.LayoutForNewMatch=previousLayout;
                BattleSession.DifficultyForNewMatch=previousDifficulty;BattleSession.SeedForNewMatch=previousSeed;
                BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.CountdownForNewMatch=previousCountdown;
                ImportedLandscapeAugment.Enabled=previousRelief;MapLayout.Configure(previousScenario);
                if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        static void CaptureMap(ScenarioMap map,string postId,int seed,string directory)
        {
            BattleSession.MapForNewMatch=map;BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;BattleSession.DifficultyForNewMatch=BattleSession.AiDifficulty.Relaxed;
            BattleSession.SeedForNewMatch=seed;BattleSession.PlayerCountForNewMatch=PlayerRules.MaxPlayers;BattleSession.CountdownForNewMatch=false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var bootstrapObject=new GameObject("Riesgus · Water preview · "+map);
            var bootstrap=bootstrapObject.AddComponent<RiskBootstrap>();
            if(!bootstrapObject.GetComponent<BattleSession>())
                typeof(RiskBootstrap).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(bootstrap,null);
            var session=bootstrapObject.GetComponent<BattleSession>();
            if(!session)throw new InvalidOperationException("Water preview bootstrap did not create a battle session.");
            var town=session.Towns.FirstOrDefault(item=>item.State.Id==postId);
            if(!town)throw new InvalidOperationException("Water preview source post is unavailable: "+postId);
            int sampled=SampleIdlePoses();LogGeometry(map,sampled);
            var camera=Camera.main;if(!camera)throw new InvalidOperationException("Water preview has no main camera.");
            camera.aspect=Width/(float)Height;camera.transform.rotation=RtsCameraRig.DefaultRotation;
            Debug.Log("RISKAI_WATER_PREVIEW_SEARCH: "+map);
            Vector3 post=town.ClaimPoint,sea=FindOpenSea(MapLayout.Imported,post);
            Debug.Log("RISKAI_WATER_PREVIEW_SEARCH_OK: "+map);
            // Create transient targets after switching scenes: NewScene destroys
            // editor-owned textures from the previous scene even with managed refs.
            var readback=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32)
                {antiAliasing=1,name="Risk water preview readback"};readback.Create();
            try
            {
                Render(camera,readback,directory,map,postId+"-near",post,12);
                Render(camera,readback,directory,map,postId+"-normal",post,22);
                Render(camera,readback,directory,map,"open-sea",sea,22);
                var inland=session.Towns.First(item=>!item.IsPort);
                Render(camera,readback,directory,map,"integrated-town",inland.transform.position,12);
                // Static close-up of the same pooled projectile appearance;
                // gameplay flight/contact timing is covered by PlayMode tests.
                var bolt=new GameObject("Preview physical crossbow bolt").AddComponent<ArrowFlight>();
                Vector3 origin=post+new Vector3(2.1f,1.1f,0);
                bolt.InitVisual(origin,origin+Vector3.right*4,AttackKind.Piercing);
                try{Render(camera,readback,directory,map,"bolt-detail",origin,1.65f);}
                finally{UnityEngine.Object.DestroyImmediate(bolt.gameObject);}
            }
            finally{if(RenderTexture.active==readback)RenderTexture.active=null;Release(readback);}
        }

        static Vector3 FindOpenSea(ImportedMapData data,Vector3 anchor)
        {
            Vector3 best=default;float score=float.MaxValue;
            for(int z=8;z<data.pathingHeight-8;z+=8)for(int x=8;x<data.pathingWidth-8;x+=8)
            {
                Vector2 point=data.PathingCellCenter(x,z);float depth=data.WaterAt(point.x,point.y)-data.HeightAt(point.x,point.y);
                if(depth<1||!data.IsShipNavigable(point.x,point.y)||data.IsWalkable(point.x,point.y))continue;
                const float radius=12;
                if(!data.IsShipNavigable(point.x+radius,point.y)||!data.IsShipNavigable(point.x-radius,point.y)||
                    !data.IsShipNavigable(point.x,point.y+radius)||!data.IsShipNavigable(point.x,point.y-radius))continue;
                float candidate=(new Vector2(point.x-anchor.x,point.y-anchor.z)).sqrMagnitude-depth*20;
                if(candidate>=score)continue;score=candidate;best=new Vector3(point.x,data.WaterAt(point.x,point.y),point.y);
            }
            if(score==float.MaxValue)throw new InvalidOperationException("No open-sea preview point found for "+data.mapId);
            return best;
        }

        static int SampleIdlePoses()
        {
            int count=0;
            foreach(var animation in UnityEngine.Object.FindObjectsByType<Animation>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(animation["Idle"]&&animation["Idle"].clip){animation["Idle"].clip.SampleAnimation(animation.gameObject,.3f);count++;}
            return count;
        }

        static void LogGeometry(ScenarioMap map,int poses)
        {
            var meshes=new HashSet<Mesh>();int filters=0,renderers=0,waterMeshes=0;long vertices=0,triangles=0;
            foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                filters++;var mesh=filter.sharedMesh;if(!mesh||!meshes.Add(mesh))continue;
                vertices+=mesh.vertexCount;for(int sub=0;sub<mesh.subMeshCount;sub++)triangles+=(long)mesh.GetIndexCount(sub)/3;
                string label=(filter.name+" "+mesh.name).ToLowerInvariant();if(label.Contains("water")||label.Contains("sea"))waterMeshes++;
            }
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))if(renderer.enabled)renderers++;
            Debug.Log($"RISKAI_WATER_PREVIEW_GEOMETRY: map={map} filters={filters} uniqueMeshes={meshes.Count} vertices={vertices} triangles={triangles} waterMeshes={waterMeshes} enabledRenderers={renderers} idlePoses={poses}");
        }

        static void Render(Camera camera,RenderTexture readback,string directory,ScenarioMap map,string label,Vector3 focus,float zoom)
        {
            camera.orthographicSize=zoom;camera.transform.position=focus-camera.transform.forward*(zoom/Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad));
            Debug.Log("RISKAI_WATER_PREVIEW_RENDER: "+map+" "+label);
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=readback};
            for(int pass=0;pass<3;pass++)RenderPipeline.SubmitRenderRequest(camera,request);
            var previous=RenderTexture.active;RenderTexture.active=readback;var image=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();
            var pixels=image.GetPixels32();int minimum=255,maximum=0;
            for(int i=0;i<pixels.Length;i+=113){int value=(pixels[i].r+pixels[i].g+pixels[i].b)/3;minimum=Mathf.Min(minimum,value);maximum=Mathf.Max(maximum,value);}
            if(maximum-minimum<12)throw new InvalidOperationException("Water preview is empty or flat; no visual validation was produced.");
            string path=AvailablePath(directory,map.ToString().ToLowerInvariant()+"-"+label+"-z"+zoom+".png");
            File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);camera.targetTexture=null;
            var data=MapLayout.Imported;Debug.Log($"RISKAI_WATER_PREVIEW_POSE: map={map} shot={label} zoom={zoom} focus={focus:F3} camera={camera.transform.position:F3} ground={data.HeightAt(focus.x,focus.z):F3} water={data.WaterAt(focus.x,focus.z):F3} walkable={data.IsWalkable(focus.x,focus.z)} ship={data.IsShipNavigable(focus.x,focus.z)} png={path}");
        }

        static string OutputDirectory()
        {
            string[] args=Environment.GetCommandLineArgs();string value=null;
            for(int i=0;i<args.Length;i++)if(args[i]=="--riskai-water-preview-output")
            {if(i+1>=args.Length)throw new ArgumentException("--riskai-water-preview-output requires a directory.");value=args[i+1];break;}
            string project=Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(string.IsNullOrWhiteSpace(value)?Path.Combine(project,"Screenshots","water-review"):
                Path.IsPathRooted(value)?value:Path.Combine(project,value));
        }

        static string AvailablePath(string directory,string file)
        {
            string path=Path.Combine(directory,file);if(!File.Exists(path))return path;
            string stem=Path.GetFileNameWithoutExtension(file),extension=Path.GetExtension(file);
            for(int copy=2;;copy++){path=Path.Combine(directory,stem+"-"+copy.ToString("00")+extension);if(!File.Exists(path))return path;}
        }

        static void Release(RenderTexture texture){if(!texture)return;texture.Release();UnityEngine.Object.DestroyImmediate(texture);}
    }
}
