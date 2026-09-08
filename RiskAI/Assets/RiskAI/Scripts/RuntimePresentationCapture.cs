using System.Collections;
using System.IO;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;

namespace RiskAI
{
    // Explicit scene-rendering fixture; its actors/camera changes are QA setup,
    // not evidence of player input, natural combat outcomes or frame performance.
    public sealed class RuntimePresentationCapture : MonoBehaviour
    {
        string directory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnableWhenRequested()
        {
#if !UNITY_WEBGL
            var args = LaunchArguments.Get();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--riskai-presentation-capture")
                {
                    var host = new GameObject("Presentation capture");
                    DontDestroyOnLoad(host);
                    host.AddComponent<RuntimePresentationCapture>().directory = args[i + 1];
                    break;
                }
#endif
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Directory.CreateDirectory(directory);
            yield return null;
            if (!BattleSession.Current)
            {
                var front = FindFirstObjectByType<FrontEndController>();
                if (front) front.StartBattle();
            }
            float timeout = Time.realtimeSinceStartup + 120;
            while (!BattleSession.Current && Time.realtimeSinceStartup < timeout) yield return null;
            var battle = BattleSession.Current;
            if (!battle) { Debug.LogError("RISKAI_PRESENTATION_FAILED: no battle"); Application.Quit(1); yield break; }
            battle.AiEnabled = false;
            var input = FindFirstObjectByType<RtsController>();
            input.enabled = false;
            if (!battle.Paused) battle.TogglePause();
            battle.Economy.Grant(0, 1000);
            foreach (var argument in LaunchArguments.Get())
                if (argument == "--riskai-capture-ships")
                {
                    yield return CaptureShips(battle,input);
                    Application.Quit();
                    yield break;
                }
            foreach (var argument in LaunchArguments.Get())
                if (argument == "--riskai-capture-knight")
                {
                    yield return CaptureKnight(battle, input);
                    Application.Quit();
                    yield break;
                }
            foreach (var town in battle.Towns)
            {
                if (!town || town.State.Owner != 0 || town.IsPort) continue;
                input.SelectTown(town);
                Frame(input, town.DefaultLandEntry);
                yield return Capture("town-idle");
                battle.TogglePause();
                var error = town.Recruit(UnitKind.Archer);
                if (error != null) Debug.LogError("RISKAI_PRESENTATION_FAILED: recruit " + error);
                town.SimTick(.05f);
                battle.TogglePause();
                yield return Capture("town-training");
                break;
            }
            foreach (var port in battle.Naval.Harbors)
            {
                if (!port || port.Owner != 0 || !port.CanLaunch) continue;
                input.SelectHarbor(port);
                Frame(input, port.Landing);
                battle.TogglePause();
                port.Buy(ShipKind.Transport);
                port.RecruitLand(UnitKind.MarinePrivate);
                port.SimTick(.05f);
                battle.TogglePause();
                yield return Capture("port-training");
                battle.TogglePause();
                var previous = port.Defender;
                port.ClaimZone.SetDefender(null);
                if (previous) previous.gameObject.SetActive(false);
                var guard = battle.Naval.Spawn(0, ShipKind.Galley, port.Berth);
                if (!guard) { Debug.LogError("RISKAI_PRESENTATION_FAILED: guard spawn"); break; }
                if (port.IsImportedPort) port.LinkedTown.SimTick(.05f);
                port.SimTick(.05f);
                guard.SimTick(.05f);
                battle.TogglePause();
                input.Clear();
                Frame(input, port.Berth);
                yield return Capture("guard-idle");
                input.SelectShip(guard);
                yield return Capture("guard-selected");
                Debug.Log("RISKAI_PRESENTATION_GUARD: garrison=" + guard.IsGarrison + " distance=" + Vector3.Distance(guard.transform.position, port.Berth));
                break;
            }
            input.Clear();
            bool sand = false, rock = false, green = false, steep = false;
            var min = MapLayout.PlayableMin; var max = MapLayout.PlayableMax;
            for (float z = min.y + 4; z < max.y - 4 && !(sand && rock && green && steep); z += 4)
                for (float x = min.x + 4; x < max.x - 4 && !(sand && rock && green && steep); x += 4)
                {
                    if (!MapLayout.IsLand(x, z)) continue;
                    if (MapLayout.IsLand(x + 4, z) && MapLayout.IsLand(x - 4, z) &&
                        MapLayout.IsLand(x, z + 4) && MapLayout.IsLand(x, z - 4)) continue;
                    var weights = ShoreAccess.SurfaceWeights(x, z);
                    var position = MapLayout.Point(x, z);
                    string kind = null;
                    if (!steep && weights.x > .55f && Physics.Raycast(position + Vector3.up * 100, Vector3.down, out var face, 200, 1 << MapLayout.TerrainLayer) && face.normal.y < .75f)
                    { kind = "steep"; steep = true; }
                    else if (!sand && weights.x > .65f && ShoreAccess.TryLanding(position, out _, out _)) { kind = "sand"; sand = true; }
                    else if (!rock && weights.x < .3f && weights.y > .6f) { kind = "rock"; rock = true; }
                    else if (!green && weights.x < .2f && weights.y < .2f) { kind = "green"; green = true; }
                    if (kind == null) continue;
                    bool allowed = ShoreAccess.TryLanding(position, out var landing, out var error);
                    Frame(input, position);
                    Debug.Log("RISKAI_PRESENTATION_SHORE: kind=" + kind + " at=" + position + " weights=" + weights + " allowed=" + allowed + " landing=" + landing + " reason=" + error);
                    yield return Capture("shore-" + kind);
                }
            Debug.Log("RISKAI_PRESENTATION_OK: map=" + MapLayout.Scenario + " sand=" + sand + " rock=" + rock + " green=" + green + " steep=" + steep + " directory=" + directory);
            Application.Quit();
        }

        static void Frame(RtsController input, Vector3 position)
        {
            input.CameraRig.ResetView();
            input.CameraRig.Focus(position);
            input.CameraRig.ZoomAt(3, new Vector2(Screen.width * .5f, Screen.height * .55f));
        }

        IEnumerator CaptureShips(BattleSession battle,RtsController input)
        {
            var min=MapLayout.PlayableMin;var max=MapLayout.PlayableMax;
            for(float z=min.y+30;z<max.y-50;z+=20)
            for(float x=min.x+30;x<max.x-40;x+=20)
            {
                var start=new Vector3(x,-.24f,z);var other=start+Vector3.right*10;
                var end=start+Vector3.forward*28;var otherEnd=other+Vector3.forward*28;
                if(!SeaNavigation.ClearSegment(start,end)||!SeaNavigation.ClearSegment(other,otherEnd))continue;
                bool nearPort=false;
                foreach(var port in battle.Naval.Harbors)if(Vector3.Distance(port.Berth,start)<40){nearPort=true;break;}
                if(nearPort)continue;
                var galley=battle.Naval.Spawn(0,ShipKind.Galley,start);
                var transport=battle.Naval.Spawn(0,ShipKind.Transport,other);
                if(!galley||!transport)continue;
                input.Clear();Frame(input,start+new Vector3(5,0,7));
                galley.MoveTo(end);transport.MoveTo(otherEnd);
                battle.TogglePause();
                yield return Capture("ships-underway-0");
                yield return Capture("ships-underway-1");
                float distance=Vector3.Distance(galley.transform.position,start);
                float cargoDistance=Vector3.Distance(transport.transform.position,other);
                Debug.Log((distance>2&&cargoDistance>2?"RISKAI_PRESENTATION_OK: ":"RISKAI_PRESENTATION_FAILED: ")+"ships galleyMoved="+distance+" transportMoved="+cargoDistance);
                yield break;
            }
            Debug.LogError("RISKAI_PRESENTATION_FAILED: no open sea fixture");
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            RenderScene(name);
        }

        IEnumerator CaptureKnight(BattleSession battle, RtsController input)
        {
            Settlement home = null;
            foreach (var town in battle.Towns) if (town && town.State.Owner == 0 && !town.IsPort) { home = town; break; }
            if (!home) { Debug.LogError("RISKAI_PRESENTATION_FAILED: knight home"); yield break; }
            var knight = battle.Spawn(0, UnitKind.Guard, home.DefaultLandEntry + Vector3.right * 5);
            if (!knight) { Debug.LogError("RISKAI_PRESENTATION_FAILED: knight spawn"); yield break; }
            input.Clear(); input.CameraRig.enabled = false;
            // Close-up animation evidence must remain visible under decorative crowns.
            // Hiding these renderers in this opt-in fixture does not change pathing/forest cost.
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (renderer.name == "Layered evergreen boughs" || renderer.name == "Painted foliage crown") renderer.enabled = false;
            var camera = Camera.main;
            Vector3 start = knight.transform.position, destination = start;
            for (int i = 0; i < 8; i++)
            {
                var direction = Quaternion.Euler(0, i * 45, 0) * Vector3.forward;
                if (NavMesh.SamplePosition(start + direction * 25, out var hit, 1, NavMesh.AllAreas) &&
                    !NavMesh.Raycast(start, hit.position, out _, NavMesh.AllAreas)) { destination = hit.position; break; }
            }
            battle.TogglePause();
            knight.MoveTo(destination, false, false);
            int frames = 0, windups = 0, moving = 0;
            foreach (string stage in new[] { "trot", "pause", "attack" })
            {
                if (stage == "pause") { knight.HoldPosition(); battle.TogglePause(); }
                if (stage == "attack")
                {
                    battle.TogglePause();
                    var enemy = battle.Spawn(1, UnitKind.Guard, knight.transform.position + knight.transform.forward * 1.4f);
                    if (enemy) knight.Attack(enemy);
                }
                int count = stage == "pause" ? 8 : 32;
                for (int i = 0; i < count; i++)
                {
                    yield return new WaitForSecondsRealtime(.05f);
                    var position = knight.transform.position;
                    camera.transform.position = position + new Vector3(7, 6, -9);
                    camera.transform.LookAt(position + Vector3.up * 1.4f);
                    if (knight.StrikeWindupProgress >= 0) windups++;
                    if (knight.Agent.velocity.sqrMagnitude > .1f) moving++;
                    RenderScene("knight-" + frames.ToString("D3") + "-" + stage);
                    Debug.Log("RISKAI_KNIGHT_FRAME: index=" + frames + " stage=" + stage + " time=" + battle.BattleTime + " windup=" + knight.StrikeWindupProgress);
                    frames++;
                }
            }
            if (moving == 0 || windups == 0) Debug.LogError("RISKAI_PRESENTATION_FAILED: knight did not exercise locomotion and attack");
            else Debug.Log("RISKAI_PRESENTATION_OK: knightFrames=" + frames + " movingFrames=" + moving + " windupFrames=" + windups);
        }

        void RenderScene(string name)
        {
            string path = Path.Combine(directory, name + ".png");
            // Explicit URP render works without a visible desktop swap chain.
            // Screen-overlay retained UI is outside this scene-presentation fixture.
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var readback = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(Camera.main, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                readback.Apply();
                File.WriteAllBytes(path, readback.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(readback);
            }
            Debug.Log("RISKAI_PRESENTATION_IMAGE: " + name);
        }
    }
}
