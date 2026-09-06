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
            if(!battle.Paused)battle.TogglePause();input.FocusHome();
            int mobile=0,garrisons=0,hurt=0,shots=0;foreach(var unit in battle.Units)if(unit&&unit.IsAlive){if(unit.IsGarrison)garrisons++;else mobile++;if(unit.Health<unit.MaxHealth)hurt++;}
            foreach(var tower in battle.Towers)if(tower)shots+=tower.ShotsFired;
            Debug.Log("RISKAI_INITIAL_ROSTER: towns="+battle.Towns.Count+" ports="+battle.Naval.Harbors.Count+" guards="+garrisons+" mobile="+mobile+" ships="+battle.Naval.Ships.Count+" blue="+battle.Population(0)+" red="+battle.Population(1)+" hurt="+hurt+" towerShots="+shots);
            if(hurt>0||shots>0)Debug.LogError("RISKAI_INITIAL_CROSSFIRE: independent starting posts must not attack each other.");
            if(MapLayout.IsImported){yield return CaptureImported(battle,input);Application.Quit();yield break;}
            for(int i=0;i<10;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-overview.png"));
            yield return new WaitForSecondsRealtime(.6f);
            input.CameraRig.ZoomAt(2,new Vector2(Screen.width*.5f,Screen.height*.5f));
            yield return new WaitForSecondsRealtime(.7f);
            input.FocusHome();
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-city.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.CameraRig.Focus(MapLayout.Point(26*MapLayout.Spacing,3*MapLayout.Spacing));input.SelectAll();
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-highlands.png"));
            yield return new WaitForSecondsRealtime(.7f);
            if(!MapLayout.IsExpanded){input.CameraRig.Focus(MapLayout.Point(-40*MapLayout.Spacing,-72*MapLayout.Spacing));yield return new WaitForSecondsRealtime(1.5f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-southwest.png"));yield return new WaitForSecondsRealtime(.7f);}
            input.FocusHarbor();
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-harbor.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.SelectFleet();input.FocusFleet();
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-fleet.png"));
            yield return new WaitForSecondsRealtime(.7f);input.Clear();
            input.CameraRig.Focus(MapLayout.IsExpanded?TerrainHydrology.Samples[28]:MapLayout.Point(44*MapLayout.Spacing,39*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(3);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-river.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Focus(MapLayout.IsExpanded?TerrainHydrology.Samples[55]:MapLayout.Point(33*MapLayout.Spacing,50*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(2);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-estuary.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Focus(MapLayout.Point(MapLayout.Islands[0].x*MapLayout.Spacing,MapLayout.Islands[0].y*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(2);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-oaks.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Focus(MapLayout.Point(MapLayout.Islands[1].x*MapLayout.Spacing,MapLayout.Islands[1].y*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(3);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-island.png"));
            yield return new WaitForSecondsRealtime(.7f);
            if(battle.Camps.Count>0&&battle.Camps[0])
            {
                input.SelectCamp(battle.Camps[0]);input.CameraRig.Focus(battle.Camps[0].SpawnPoint);
                yield return new WaitForSecondsRealtime(4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-camp.png"));
                yield return new WaitForSecondsRealtime(.7f);
            }
            input.Clear();
            input.CameraRig.ZoomAt(-4,new Vector2(Screen.width*.5f,Screen.height*.5f));
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-zoomout.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Pan(Vector3.forward,1);
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-zoomout-pan.png"));
            yield return new WaitForSecondsRealtime(.7f);input.HelpVisible=true;
            for(int i=0;i<3;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-help.png"));
            yield return new WaitForSecondsRealtime(.7f);
            bool captured=true;foreach(string name in new[]{"overview","city","highlands","harbor","fleet","river","estuary","oaks","island","camp","zoomout","zoomout-pan","help"})captured&=File.GetLastWriteTimeUtc(Path.Combine(directory,"v14-player-"+name+".png"))>=started;
            if(captured)
                Debug.Log("RISKAI_PLAYER_CAPTURE_OK: "+directory);
            else Debug.LogError("RISKAI_PLAYER_CAPTURE_FAILED: player window must be visible.");
            Application.Quit();
        }
        IEnumerator CaptureImported(BattleSession battle,RtsController input)
        {
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-city.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.CameraRig.FrameMap();input.Clear();
            yield return new WaitForSecondsRealtime(5);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-geography.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.CameraRig.ResetView();input.FocusHarbor();
            yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-harbor.png"));
            yield return new WaitForSecondsRealtime(.7f);
            foreach(var camp in battle.Camps)if(camp){input.SelectCamp(camp);input.CameraRig.Focus(camp.SpawnPoint);break;}
            yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-camp.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.HelpVisible=true;
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v14-player-help.png"));
            yield return new WaitForSecondsRealtime(1);
            bool complete=true;
            foreach(string name in new[]{"city","geography","harbor","camp","help"})complete&=File.Exists(Path.Combine(directory,"v14-player-"+name+".png"));
            if(complete)Debug.Log("RISKAI_PLAYER_CAPTURE_OK: "+directory);else Debug.LogError("RISKAI_PLAYER_CAPTURE_FAILED: "+directory);
        }

    }
}

