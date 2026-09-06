using System.Collections;
using System.IO;
using UnityEngine;
namespace RiskAI
{
    // Optional developer capture: renders the real player and its IMGUI HUD.
    public sealed class RuntimeVisualCapture : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnableCapture()
        {
            var args=System.Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="--riskai-capture")
            {
                var capture=new GameObject("Visual capture").AddComponent<RuntimeVisualCapture>();capture.directory=args[i+1];break;
            }
        }
        string directory;
        IEnumerator Start()
        {
            Application.runInBackground=true;Directory.CreateDirectory(directory);var started=System.DateTime.UtcNow;
            var earlyInput=FindFirstObjectByType<RtsController>();if(earlyInput)earlyInput.enabled=false;
            for(int i=0;i<25;i++)yield return null;
            var battle=BattleSession.Current;var input=FindFirstObjectByType<RtsController>();
            input.enabled=false;battle.AiEnabled=false;yield return new WaitForSecondsRealtime(1);
            foreach(var tower in battle.Towers)
            {
                int visible=0;foreach(var renderer in tower.GetComponentsInChildren<MeshRenderer>())if(renderer.enabled)visible++;
                Debug.Log("RISKAI_TOWER_VISUAL: "+tower.HostName+" alive="+tower.IsAlive+" pos="+tower.transform.position+" renderers="+visible);
            }
            if(!battle.Paused)battle.TogglePause();input.SelectAll();
            for(int i=0;i<10;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-overview.png"));
            yield return new WaitForSecondsRealtime(.6f);
            input.CameraRig.ZoomAt(2,new Vector2(Screen.width*.5f,Screen.height*.5f));
            yield return new WaitForSecondsRealtime(.7f);
            input.FocusHome();
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-city.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.CameraRig.Focus(MapLayout.Point(26*MapLayout.Spacing,3*MapLayout.Spacing));input.SelectAll();
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-highlands.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.FocusHarbor();
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-harbor.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.SelectFleet();input.FocusFleet();
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-fleet.png"));
            yield return new WaitForSecondsRealtime(.7f);input.Clear();
            input.CameraRig.Focus(MapLayout.IsExpanded?TerrainHydrology.Samples[28]:MapLayout.Point(44*MapLayout.Spacing,39*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(3);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-river.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Focus(MapLayout.IsExpanded?TerrainHydrology.Samples[55]:MapLayout.Point(33*MapLayout.Spacing,50*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(2);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-estuary.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Focus(MapLayout.Point(MapLayout.Islands[0].x*MapLayout.Spacing,MapLayout.Islands[0].y*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(2);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-oaks.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Focus(MapLayout.Point(MapLayout.Islands[1].x*MapLayout.Spacing,MapLayout.Islands[1].y*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(3);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-island.png"));
            yield return new WaitForSecondsRealtime(.7f);
            if(battle.Camps.Count>0&&battle.Camps[0])
            {
                input.SelectCamp(battle.Camps[0]);input.CameraRig.Focus(battle.Camps[0].SpawnPoint);
                yield return new WaitForSecondsRealtime(4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-camp.png"));
                yield return new WaitForSecondsRealtime(.7f);
            }
            input.Clear();
            input.CameraRig.ZoomAt(-4,new Vector2(Screen.width*.5f,Screen.height*.5f));
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-zoomout.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Pan(Vector3.forward,1);
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-zoomout-pan.png"));
            yield return new WaitForSecondsRealtime(.7f);input.HelpVisible=true;
            for(int i=0;i<3;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v12-player-help.png"));
            yield return new WaitForSecondsRealtime(.7f);
            bool captured=true;foreach(string name in new[]{"overview","city","highlands","harbor","fleet","river","estuary","oaks","island","camp","zoomout","zoomout-pan","help"})captured&=File.GetLastWriteTimeUtc(Path.Combine(directory,"v12-player-"+name+".png"))>=started;
            if(captured)
                Debug.Log("RISKAI_PLAYER_CAPTURE_OK: "+directory);
            else Debug.LogError("RISKAI_PLAYER_CAPTURE_FAILED: player window must be visible.");
            Application.Quit();
        }
    }
}

