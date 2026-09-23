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
    /// <summary>
    /// Deterministic art review of team colours, city towers and unit weapons in a
    /// 16-player match, without producing a player build. Batch usage:
    /// -executeMethod RiskAI.Editor.RiskArtReview.Capture --riskai-art-output DIR [--riskai-map europe|world|classic|riverlands] [--riskai-art-tag before]
    /// </summary>
    public static class RiskArtReview
    {
        const int Width=1600,Height=900;
        static readonly int[] CloseTeams={0,5,12,7,11,4};
        const float Spacing=4.4f;

        [MenuItem("RiskAI/Review/Capture art review")]
        public static void Capture()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            var previousMap=BattleSession.MapForNewMatch;var previousMode=BattleSession.ModeForNewMatch;
            var previousLayout=BattleSession.LayoutForNewMatch;var previousDifficulty=BattleSession.DifficultyForNewMatch;
            int previousSeed=BattleSession.SeedForNewMatch,previousPlayers=BattleSession.PlayerCountForNewMatch;
            bool previousCountdown=BattleSession.CountdownForNewMatch;var previousScenario=MapLayout.Scenario;
            try
            {
                string directory=Argument("--riskai-art-output")??Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Screenshots","art-review");
                directory=Path.GetFullPath(directory);Directory.CreateDirectory(directory);
                string tag=Argument("--riskai-art-tag")??"shot";
                string mapArg=(Argument("--riskai-map")??"europe").ToLowerInvariant();
                var map=mapArg=="world"||mapArg=="newworld"?ScenarioMap.NewWorld:mapArg=="classic"?ScenarioMap.Classic:mapArg=="riverlands"?ScenarioMap.Riverlands:ScenarioMap.Europe;
                CaptureMap(map,directory,tag);
                Debug.Log("RISKAI_ART_REVIEW_OK: "+directory);
            }
            catch(Exception error){Debug.LogException(error);throw;}
            finally
            {
                BattleSession.MapForNewMatch=previousMap;BattleSession.ModeForNewMatch=previousMode;BattleSession.LayoutForNewMatch=previousLayout;
                BattleSession.DifficultyForNewMatch=previousDifficulty;BattleSession.SeedForNewMatch=previousSeed;
                BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.CountdownForNewMatch=previousCountdown;
                MapLayout.Configure(previousScenario);
                if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        static string Argument(string name)
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];
            return null;
        }

        static void CaptureMap(ScenarioMap map,string directory,string tag)
        {
            BattleSession.MapForNewMatch=map;BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;BattleSession.DifficultyForNewMatch=BattleSession.AiDifficulty.Relaxed;
            BattleSession.SeedForNewMatch=19031;BattleSession.PlayerCountForNewMatch=PlayerRules.MaxPlayers;BattleSession.CountdownForNewMatch=false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var bootstrapObject=new GameObject("Riesgus · Art review · "+map);
            var bootstrap=bootstrapObject.AddComponent<RiskBootstrap>();
            if(!bootstrapObject.GetComponent<BattleSession>())
            {
                // The world is complete before the HUD is attached; the review renders
                // cameras directly, so an edit-mode-only UI Toolkit failure is not fatal.
                try{typeof(RiskBootstrap).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(bootstrap,null);}
                catch(TargetInvocationException error)when(StrategicMapView.Current){Debug.LogWarning("RISKAI_ART_REVIEW_HUD_SKIPPED: "+error.InnerException?.Message);}
            }
            var session=bootstrapObject.GetComponent<BattleSession>();
            if(!session)throw new InvalidOperationException("Art review bootstrap did not create a battle session.");
            Debug.Log("RISKAI_ART_REVIEW_BEGIN: map="+map+" players="+session.PlayerCount+" colorSpace="+QualitySettings.activeColorSpace);
            string prefix=tag+"-"+map.ToString().ToLowerInvariant();

            var camera=Camera.main;if(!camera)throw new InvalidOperationException("Art review has no main camera.");
            camera.aspect=Width/(float)Height;
            var lineup=BuildLineup(session);
            SampleIdlePoses();
            var readback=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){antiAliasing=1,name="Risk art review readback"};readback.Create();
            try
            {
                Vector3 origin=lineup.position;
                Render(camera,readback,directory,prefix+"-lineup16",origin+new Vector3(15*Spacing*.5f,0,1),27,55,0);
                Render(camera,readback,directory,prefix+"-closeup-red-orange",origin+new Vector3(Spacing,0,1.2f),7.5f,55,0);
                Render(camera,readback,directory,prefix+"-closeup-units",origin+new Vector3(Spacing*.5f,0,3.5f),3.2f,55,0);
                Render(camera,readback,directory,prefix+"-towers-side",origin+new Vector3(Spacing*.5f,0,-1.5f),5.5f,30,0);
                Render(camera,readback,directory,prefix+"-towers-oblique",origin+new Vector3(Spacing*.5f,0,-1.5f),5.5f,55,35);
                Render(camera,readback,directory,prefix+"-detached-tower",origin+new Vector3(Spacing,0,-8.5f),6.5f,55,0);
                Render(camera,readback,directory,prefix+"-harbor-tower",origin+new Vector3(3*Spacing,0,-8.5f),6.5f,55,0);
                // Weapon close-ups: idle, aim and release.
                // A lone archer away from the lineup, so side views are unobstructed.
                Unit(lineup,UnitKind.Archer,0,new Vector3(-14,0,4),"Weapon review archer");
                var archers=lineup.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Weapon review archer").Take(1).ToArray();
                if(archers.Length>0)
                {
                    var archer=archers[0];var anim=archer.GetComponentInChildren<Animation>();
                    Vector3 chest=archer.position+Vector3.up*.9f;
                    foreach(var pose in new[]{("idle","Idle",.2f),("aim","2H_Ranged_Aiming",.5f),("fire","2H_Ranged_Shoot",AttackPresentationTiming.ContactNormalizedTime(UnitKind.Archer))})
                    {
                        if(anim&&anim[pose.Item2]){var clip=anim[pose.Item2].clip;clip.SampleAnimation(anim.gameObject,clip.length*pose.Item3);}
                        Render(camera,readback,directory,prefix+"-crossbow-"+pose.Item1+"-rts",chest,1.7f,55,0);
                        Render(camera,readback,directory,prefix+"-crossbow-"+pose.Item1+"-side",chest,1.1f,12,90);
                        Render(camera,readback,directory,prefix+"-crossbow-"+pose.Item1+"-front",chest,1.1f,20,180);
                    }
                    LogWeapon(archer);
                    if(anim&&anim["Idle"])anim["Idle"].clip.SampleAnimation(anim.gameObject,.3f);
                }
                // Real 16-player match presentations.
                var home=session.Towns.FirstOrDefault(t=>t.State.Owner==0)??session.Towns[0];
                var orange=session.Towns.FirstOrDefault(t=>t.State.Owner==5)??session.Towns[1];
                Render(camera,readback,directory,prefix+"-match-red-z22",home.transform.position,22,55,0);
                Render(camera,readback,directory,prefix+"-match-orange-z22",orange.transform.position,22,55,0);
                Render(camera,readback,directory,prefix+"-match-tower-z9",home.transform.position,9,55,0);
                Render(camera,readback,directory,prefix+"-match-z60",Vector3.Lerp(home.transform.position,orange.transform.position,.5f),60,55,0);
                foreach(var view in UnityEngine.Object.FindObjectsByType<UnitPresentationLodView>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))view.SetGlobalProxy(true);
                Render(camera,readback,directory,prefix+"-match-proxy-z100",home.transform.position,100,55,0);
                foreach(var view in UnityEngine.Object.FindObjectsByType<UnitPresentationLodView>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))view.SetGlobalProxy(false);
                var strategic=StrategicMapView.Current;
                if(strategic)
                {
                    strategic.SetStrategic(true);
                    Vector2 min=MapLayout.PlayableMin,max=MapLayout.PlayableMax;
                    var center=new Vector3((min.x+max.x)*.5f,0,(min.y+max.y)*.5f);
                    float zoom=Mathf.Max((max.y-min.y)*.5f,(max.x-min.x)*.5f*Height/Width)*1.02f;
                    Render(camera,readback,directory,prefix+"-strategic-full",center,zoom,55,0);
                    Render(camera,readback,directory,prefix+"-strategic-home",home.transform.position,150,55,0);
                    strategic.SetStrategic(false);
                }
                for(int team=0;team<16;team++)
                {
                    Color c=VisualFactory.TeamColor(team),m=VisualFactory.TeamMaterialColor(team);
                    Debug.Log($"RISKAI_ART_TEAM: team={team} canonical={ColorUtility.ToHtmlStringRGB(c)} material={ColorUtility.ToHtmlStringRGB(m)} roofShaderLinear={(Vector4)WorldArt.RoofMaterial(team).GetVector("_Tint")}");
                }
            }
            finally{if(RenderTexture.active==readback)RenderTexture.active=null;readback.Release();UnityEngine.Object.DestroyImmediate(readback);}
        }

        static Transform BuildLineup(BattleSession session)
        {
            Vector2 min=MapLayout.PlayableMin,max=MapLayout.PlayableMax;
            var root=new GameObject("Art review lineup").transform;
            root.position=new Vector3(max.x+400,0,(min.y+max.y)*.5f);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Review meadow";ground.transform.SetParent(root,false);
            ground.transform.localPosition=new Vector3(20,-.5f,0);ground.transform.localScale=new Vector3(200,1,120);
            ground.GetComponent<Renderer>().sharedMaterial=VisualFactory.Mat(new Color(.20f,.30f,.15f));
            // Order: canonical 16, but place the close-up comparison teams first.
            var order=CloseTeams.Concat(Enumerable.Range(0,16).Where(t=>!CloseTeams.Contains(t))).ToArray();
            for(int column=0;column<order.Length;column++)
            {
                int team=order[column];float x=column*Spacing;
                var cell=new GameObject("Team "+team).transform;cell.SetParent(root,false);cell.localPosition=new Vector3(x,0,0);
                var town=new GameObject("Review town").transform;town.SetParent(cell,false);town.localPosition=new Vector3(0,0,-1.5f);
                WorldArt.Town(town,team,false,BuildingVariant.IntegratedTown);
                WorldArt.Tower(town,team,BuildingVariant.IntegratedTown,out _,out var scaffold,out _);scaffold.SetActive(false);
                Unit(cell,UnitKind.Archer,team,new Vector3(-.45f,0,3.4f),"Review archer");
                Unit(cell,UnitKind.Footman,team,new Vector3(.55f,0,3.4f),"Review footman");
                if(column<3)
                {
                    var tower=new GameObject("Review detached tower").transform;tower.SetParent(cell,false);tower.localPosition=new Vector3(0,0,-8.5f);
                    WorldArt.Tower(tower,team,BuildingVariant.DetachedTown,out _,out var detachedScaffold,out _);detachedScaffold.SetActive(false);
                }
                if(column==3)
                {
                    var harbor=new GameObject("Review harbor").transform;harbor.SetParent(cell,false);harbor.localPosition=new Vector3(0,0,-8.5f);
                    NavalArt.CreateIntegratedHarborBuilding(harbor,team,harbor.position,Vector3.back);
                    var tower=new GameObject("Review harbor tower").transform;tower.SetParent(harbor,false);
                    WorldArt.Tower(tower,team,BuildingVariant.IntegratedHarbor,out _,out var harborScaffold,out _);harborScaffold.SetActive(false);
                }
            }
            return root;
        }

        static void Unit(Transform parent,UnitKind kind,int team,Vector3 position,string name)
        {
            var holder=new GameObject(name).transform;holder.SetParent(parent,false);holder.localPosition=position;holder.localRotation=Quaternion.Euler(0,180,0);
            var prefab=Resources.Load<GameObject>("Units/"+BattleRules.Model(kind));if(!prefab)return;
            var model=UnityEngine.Object.Instantiate(prefab,holder,false);
            model.transform.localScale*=VisualMetrics.UnitScale;
            ModelMetrics.MatchStandingHeight(model,kind);
            UnitTeamColor.Apply(model,kind,team);
            // Future unit presentation hooks, when present, are applied like VisualFactory.Soldier.
            var hook=typeof(VisualFactory).Assembly.GetType("RiskAI.CrossbowView")?.GetMethod("Apply",BindingFlags.Public|BindingFlags.Static);
            if(hook!=null&&kind==UnitKind.Archer)hook.Invoke(null,new object[]{model});
            VisualFactory.Ring(holder,.33f,.022f,VisualFactory.TeamMaterialColor(team));
        }

        static void LogWeapon(Transform archer)
        {
            foreach(var t in archer.GetComponentsInChildren<Transform>(true))
            {
                if(t.name!="2H_Crossbow"&&t.name!="handslot.r"&&t.name!="handslot.l"&&!t.name.StartsWith("Procedural crossbow"))continue;
                var filter=t.GetComponent<MeshFilter>();var skinned=t.GetComponent<SkinnedMeshRenderer>();
                Mesh mesh=filter?filter.sharedMesh:skinned?skinned.sharedMesh:null;
                Debug.Log($"RISKAI_ART_WEAPON: name={t.name} active={t.gameObject.activeInHierarchy} parent={(t.parent?t.parent.name:"-")} localPos={t.localPosition:F4} localRot={t.localEulerAngles:F1} localScale={t.localScale:F4} lossy={t.lossyScale:F4} worldPos={t.position:F3} "+
                    $"fwd={t.forward:F2} up={t.up:F2} right={t.right:F2} mesh={(mesh?mesh.name+" bounds="+mesh.bounds.center.ToString("F4")+"/"+mesh.bounds.size.ToString("F4")+" verts="+mesh.vertexCount:"none")} renderer={(t.GetComponent<Renderer>()?t.GetComponent<Renderer>().sharedMaterial?.name:"none")}");
            }
        }

        static void SampleIdlePoses()
        {
            foreach(var animation in UnityEngine.Object.FindObjectsByType<Animation>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(animation["Idle"]&&animation["Idle"].clip)animation["Idle"].clip.SampleAnimation(animation.gameObject,.3f);
        }

        static void Render(Camera camera,RenderTexture readback,string directory,string label,Vector3 focus,float zoom,float pitch,float yaw)
        {
            camera.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            camera.orthographicSize=zoom;camera.transform.position=focus-camera.transform.forward*(zoom/Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad));
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=readback};
            for(int pass=0;pass<3;pass++)RenderPipeline.SubmitRenderRequest(camera,request);
            var previous=RenderTexture.active;RenderTexture.active=readback;var image=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();
            string path=Path.Combine(directory,label+".png");
            File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);camera.targetTexture=null;
            Debug.Log("RISKAI_ART_REVIEW_RENDER: "+path);
        }
    }
}
