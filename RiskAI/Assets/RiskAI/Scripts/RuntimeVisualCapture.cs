using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
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
                var captureObject=new GameObject("Visual capture");DontDestroyOnLoad(captureObject);
                var capture=captureObject.AddComponent<RuntimeVisualCapture>();capture.directory=args[i+1];break;
            }
        }
        string directory;
        IEnumerator Start()
        {
            Application.runInBackground=true;Directory.CreateDirectory(directory);
            if(HasFlag("--riskai-capture-menu"))
            {
                // This optional capture deliberately renders the separate setup scene before it starts a match.
                yield return null;yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-front-end.png"));
                Debug.Log("RISKAI_FRONTEND_CAPTURE: "+directory);
                yield return new WaitForEndOfFrame();yield return null;
                var frontEnd=FindFirstObjectByType<FrontEndController>();if(frontEnd)frontEnd.StartBattle();
            }
            // The front-end owns no session. Persist through its async scene transition.
            while(!BattleSession.Current)yield return null;
            var started=System.DateTime.UtcNow;
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
            Debug.Log("RISKAI_INITIAL_ROSTER: players="+battle.PlayerCount+" commanders="+battle.Commanders.Count+" towns="+battle.Towns.Count+" ports="+battle.Naval.Harbors.Count+" guards="+garrisons+" mobile="+mobile+" ships="+battle.Naval.Ships.Count+" blue="+battle.Population(0)+" red="+battle.Population(1)+" hurt="+hurt+" towerShots="+shots);
            for(int player=0;player<battle.PlayerCount;player++)
                Debug.Log("RISKAI_PLAYER: id="+player+" troops="+battle.Population(player)+" gold="+battle.Economy.Gold[player]);
            if(hurt>0||shots>0)Debug.LogError("RISKAI_INITIAL_CROSSFIRE: independent starting posts must not attack each other.");
            if(MapLayout.IsImported){yield return CaptureImported(battle,input);yield return CaptureTraining(battle,input);Application.Quit();yield break;}
            for(int i=0;i<10;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-overview.png"));
            yield return new WaitForSecondsRealtime(.6f);
            input.CameraRig.ZoomAt(2,new Vector2(Screen.width*.5f,Screen.height*.5f));
            yield return new WaitForSecondsRealtime(.7f);
            input.FocusHome();
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-city.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.CameraRig.Focus(MapLayout.Point(26*MapLayout.Spacing,3*MapLayout.Spacing));input.SelectAll();
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-highlands.png"));
            yield return new WaitForSecondsRealtime(.7f);
            if(!MapLayout.IsExpanded){input.CameraRig.Focus(MapLayout.Point(-40*MapLayout.Spacing,-72*MapLayout.Spacing));yield return new WaitForSecondsRealtime(1.5f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-southwest.png"));yield return new WaitForSecondsRealtime(.7f);}
            input.FocusHarbor();
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-harbor.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.SelectFleet();input.FocusFleet();
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-fleet.png"));
            yield return new WaitForSecondsRealtime(.7f);input.Clear();
            input.CameraRig.Focus(MapLayout.IsExpanded?TerrainHydrology.Samples[28]:MapLayout.Point(44*MapLayout.Spacing,39*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(3);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-river.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Focus(MapLayout.IsExpanded?TerrainHydrology.Samples[55]:MapLayout.Point(33*MapLayout.Spacing,50*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(2);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-estuary.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Focus(MapLayout.Point(MapLayout.Islands[0].x*MapLayout.Spacing,MapLayout.Islands[0].y*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(2);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-oaks.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Focus(MapLayout.Point(MapLayout.Islands[1].x*MapLayout.Spacing,MapLayout.Islands[1].y*MapLayout.Spacing));
            yield return new WaitForSecondsRealtime(3);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-island.png"));
            yield return new WaitForSecondsRealtime(.7f);
            yield return CaptureTacticalCamp(battle,input);
            yield return new WaitForSecondsRealtime(.7f);
            input.Clear();
            input.CameraRig.ZoomAt(-4,new Vector2(Screen.width*.5f,Screen.height*.5f));
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-zoomout.png"));
            yield return new WaitForSecondsRealtime(.7f);input.CameraRig.Pan(Vector3.forward,1);
            yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-zoomout-pan.png"));
            yield return new WaitForSecondsRealtime(.7f);input.HelpVisible=true;
            for(int i=0;i<3;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-help.png"));
            yield return new WaitForSecondsRealtime(.7f);
            bool captured=true;foreach(string name in new[]{"overview","city","highlands","harbor","fleet","river","estuary","oaks","island","camp-tactical","zoomout","zoomout-pan","help"})captured&=File.GetLastWriteTimeUtc(Path.Combine(directory,"v18-player-"+name+".png"))>=started;
            if(captured)
                Debug.Log("RISKAI_PLAYER_CAPTURE_OK: "+directory);
            else Debug.LogError("RISKAI_PLAYER_CAPTURE_FAILED: player window must be visible.");
            yield return CaptureFrontline(battle,input);
            yield return CaptureTraining(battle,input);
            Application.Quit();
        }
        static bool HasFlag(string flag)
        {
            foreach(var argument in System.Environment.GetCommandLineArgs())
                if(string.Equals(argument,flag,System.StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }

        IEnumerator CaptureRenderingComparison(Camera camera)
        {
            var strategic=StrategicMapView.Current;
            if(!strategic||!camera)yield break;
            bool wasEnabled=strategic.enabled,wasStrategic=strategic.IsStrategic;
            strategic.enabled=false;
            strategic.SetStrategic(false);yield return null;
            var tactical=new FrameSample();yield return SampleFrames(tactical,5f);
            strategic.SetStrategic(true);yield return null;
            var overview=new FrameSample();yield return SampleFrames(overview,5f);
            strategic.SetStrategic(wasStrategic);strategic.enabled=wasEnabled;
            Debug.Log("RISKAI_RENDER_COMPARE: v18 paused=true constantCamera=true sampleSeconds=5 renderFrameInterval="+OnDemandRendering.renderFrameInterval+
                " tacticalAvgMs="+tactical.AverageMs.ToString("F2")+" tacticalMaxMs="+tactical.MaxMs.ToString("F2")+
                " strategicAvgMs="+overview.AverageMs.ToString("F2")+" strategicMaxMs="+overview.MaxMs.ToString("F2")+
                " targetFps="+Application.targetFrameRate+" vsync="+QualitySettings.vSyncCount+" scope=wall-clock-frame-samples; includes-frame-cap-and-gpu-wait");
        }
        sealed class FrameSample
        {
            public int Count;public float Total,Max;
            public float AverageMs=>Count==0?0:Total*1000/Count;
            public float MaxMs=>Max*1000;
            public void Add(float seconds){Count++;Total+=seconds;if(seconds>Max)Max=seconds;}
        }
        IEnumerator SampleFrames(FrameSample sample,float seconds)
        {
            float until=Time.unscaledTime+seconds;
            while(Time.unscaledTime<until){yield return null;sample.Add(Time.unscaledDeltaTime);}
        }
        CountryCamp CampForCapture(BattleSession battle)
        {
            CountryCamp fallback=null;
            foreach(var camp in battle.Camps)
            {
                if(!camp)continue;if(!fallback)fallback=camp;
                if(HasPort(camp.Country))return camp;
            }
            return fallback;
        }
        IEnumerator CaptureTacticalCamp(BattleSession battle,RtsController input)
        {
            var camp=CampForCapture(battle);if(!camp)yield break;
            input.Clear();input.SelectCamp(camp);input.CameraRig.ResetView();input.CameraRig.Focus(camp.SpawnPoint);
            yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-camp-tactical.png"));
            Debug.Log("RISKAI_CAMP_CAPTURE: country="+camp.Country+" memberPort="+HasPort(camp.Country));
            yield return new WaitForSecondsRealtime(.6f);
            input.CameraRig.FrameMap();yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-country-map.png"));
            yield return new WaitForSecondsRealtime(.6f);
            input.Clear();
            if(!MapLayout.IsImported)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-frame-map.png"));
                yield return new WaitForSecondsRealtime(.6f);
            }
            input.CameraRig.ResetView();input.CameraRig.Focus(camp.SpawnPoint);
        }

        static bool HasPort(int country)
        {
            if(NavalWorld.Current)foreach(var port in NavalWorld.Current.Harbors)
                if(port&&(port.LinkedTown?port.LinkedTown.State.Country:port.State.Country)==country)return true;
            return false;
        }

        IEnumerator CaptureTraining(BattleSession battle,RtsController input)
        {
            Harbor port=null;Settlement firstTown=null;int queuedCities=0;
            foreach(var town in battle.Towns)if(town&&town.State.Owner==0&&!town.IsPort)
            {
                if(!firstTown)firstTown=town;
                if(queuedCities<2)queuedCities++;
            }
            foreach(var candidate in battle.Naval.Harbors)
                if(candidate.Owner==0&&candidate.CanLaunch){port=candidate;break;}
            if(!firstTown&&!port)yield break;
            input.HelpVisible=false;battle.Economy.Grant(0,500);
            if(battle.Paused)battle.TogglePause();
            int cityOrders=0,cityCount=0;
            var trainingTowns=new System.Collections.Generic.List<Settlement>();
            foreach(var town in battle.Towns)if(town&&town.State.Owner==0&&!town.IsPort&&cityCount++<2)
            {
                trainingTowns.Add(town);
                if(town.Recruit(Core.UnitKind.Footman)==null)cityOrders++;
                if(town.Recruit(Core.UnitKind.Archer)==null)cityOrders++;
            }
            if(port)
            {
                for(int i=0;i<Harbor.QueueCapacity;i++)port.Buy(i%2==0?ShipKind.Galley:ShipKind.Transport);
                port.RecruitLand(Core.UnitKind.MarinePrivate);
            }
            yield return new WaitForSecondsRealtime(.4f);
            if(!battle.Paused)battle.TogglePause();
            if(firstTown)
            {
                input.SelectTown(firstTown);input.CameraRig.ResetView();input.CameraRig.Focus(firstTown.ClaimPoint);
                yield return new WaitForSecondsRealtime(2);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-training-cities.png"));
            }
            if(port)
            {
                input.SelectHarbor(port);input.CameraRig.ResetView();input.CameraRig.Focus(port.Landing);
                yield return new WaitForSecondsRealtime(2);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-training-harbor.png"));
            }
            yield return new WaitForSecondsRealtime(.7f);
            input.SelectBuildings(trainingTowns,port?new[]{port}:null);
            if(firstTown)input.CameraRig.Focus(firstTown.ClaimPoint);
            yield return new WaitForSecondsRealtime(2);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-queues.png"));
            yield return new WaitForSecondsRealtime(.7f);
            Debug.Log("RISKAI_TRAINING_CAPTURE: v18 ownCities="+queuedCities+" cityOrders="+cityOrders+" naval="+(port?port.QueueCount:0)+" capacity="+Harbor.QueueCapacity+" land="+(port?port.LandQueueCount:0));
        }
        IEnumerator CaptureFrontline(BattleSession battle,RtsController input)
        {
            input.Clear();input.FocusHome();var home=input.SelectedTown;if(!home)yield break;
            Vector3 center=home.ClaimPoint+new Vector3(4,0,-2);
            var gunner=battle.Spawn(0,Core.UnitKind.Mortar,center+new Vector3(-1.4f,0,0));
            var knight=battle.Spawn(0,Core.UnitKind.Guard,center+new Vector3(1.3f,0,-.2f));
            var mage=battle.Spawn(0,Core.UnitKind.Mage,center+new Vector3(0,0,1.5f));
            input.CameraRig.ResetView();input.CameraRig.Focus(center);
            yield return new WaitForSecondsRealtime(1.5f);
            input.CameraRig.ZoomAt(3,new Vector2(Screen.width*.5f,(BattleHud.BottomPixels+Screen.height-BattleHud.TopPixels)*.5f));
            yield return new WaitForSecondsRealtime(2);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-frontline.png"));
            yield return new WaitForSecondsRealtime(.6f);
            if(battle.Paused)battle.TogglePause();
            Vector3 impact=center+new Vector3(0,0,4f);
            battle.Combat.FireProjectile(mage?mage.AimPoint:center+Vector3.up,impact,null,0,0,mage,Core.AttackKind.Magic);
            battle.Combat.FireProjectile(gunner?gunner.AimPoint:center+Vector3.up,impact+Vector3.right*1.2f,null,0,0,gunner,Core.AttackKind.Siege);
            yield return new WaitForSecondsRealtime(.19f);
            if(!battle.Paused)battle.TogglePause();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-impact-profiles.png"));
            Debug.Log("RISKAI_FRONTLINE_CAPTURE: v18 gunner="+(gunner!=null)+" knight="+(knight!=null)+" mage="+(mage!=null)+" effects=magic,siege damage=0");
        }
        IEnumerator CaptureImported(BattleSession battle,RtsController input)
        {
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-city.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.CameraRig.FrameMap();input.Clear();
            yield return new WaitForSecondsRealtime(5);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-frame-map.png"));
            yield return new WaitForSecondsRealtime(.6f);
            yield return CaptureRenderingComparison(Camera.main);
            yield return new WaitForSecondsRealtime(.7f);
            input.CameraRig.ResetView();input.FocusHarbor();
            yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-harbor.png"));
            yield return new WaitForSecondsRealtime(.7f);
            yield return CaptureTacticalCamp(battle,input);
            yield return new WaitForSecondsRealtime(.7f);
            input.Clear();input.CameraRig.ResetView();
            float sourceOffset=MapLayout.Scenario==ScenarioMap.NewWorld?163.84f:0;
            input.CameraRig.Focus(MapLayout.Point(-90+sourceOffset,-90));
            yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-alps.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.CameraRig.Focus(MapLayout.Point(-100+sourceOffset,-30));
            yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-rhine.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.HelpVisible=true;
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-help.png"));
            yield return new WaitForSecondsRealtime(1);
            FindFirstObjectByType<BattleHud>().ShowPlayers();
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-players.png"));
            yield return new WaitForSecondsRealtime(.7f);
            input.HelpVisible=false;
            if(UnityEngine.InputSystem.Keyboard.current!=null)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Tab));
                yield return null;yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-scores.png"));
                yield return new WaitForSecondsRealtime(.7f);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            yield return CaptureFrontline(battle,input);
            input.CameraRig.ResetView();input.FocusHarbor();
            var port=input.SelectedHarbor;
            if(port&&port.CanLaunch)
            {
                battle.Naval.Spawn(0,ShipKind.Transport,port.Berth);
                if(SeaNavigation.TryNearestOcean(port.Berth+new Vector3(9,0,6),18,out var other))battle.Naval.Spawn(0,ShipKind.Galley,other);
                input.CameraRig.Focus(port.Berth);
                yield return new WaitForSecondsRealtime(2);
                input.CameraRig.ZoomAt(1,new Vector2(Screen.width*.5f,Screen.height*.5f));
                yield return new WaitForSecondsRealtime(3);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"v18-player-naval.png"));
                yield return new WaitForSecondsRealtime(.7f);
            }
            bool complete=true;
            foreach(string name in new[]{"city","frame-map","harbor","camp-tactical","alps","rhine","frontline","help","players"})complete&=File.Exists(Path.Combine(directory,"v18-player-"+name+".png"));
            if(complete)Debug.Log("RISKAI_PLAYER_CAPTURE_OK: "+directory);else Debug.LogError("RISKAI_PLAYER_CAPTURE_FAILED: "+directory);
        }

    }
}

