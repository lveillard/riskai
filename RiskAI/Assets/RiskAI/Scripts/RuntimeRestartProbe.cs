using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace RiskAI
{
    /// <summary>Opt-in scene restart measurement. It has no effect unless explicitly requested at launch.</summary>
    public sealed class RuntimeRestartProbe : MonoBehaviour
    {
        const string Flag="--riskai-restart-probe";
        const string CyclesFlag="--riskai-restart-cycles";
        const int DefaultCycles=3;
        const float BattlefieldRealSeconds=3f;
        static bool created;

        struct Metrics
        {
            public long UnityAllocated, UnityReserved, Managed;
            public int Meshes, Materials, Renderers, NavMeshData;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateWhenRequested()
        {
            if(created||!HasFlag(Flag))return;
            if(HasFlag("--riskai-probe")||HasFlag("--riskai-capture")||HasFlag("--riskai-capture-menu")||HasFlag("--riskai-ui-capture"))
            {
                Debug.LogError("RISKAI_RESTART_RESULT success=false phase=arguments reason=conflicts-with-command-probe-or-capture");
                return;
            }
            created=true;
            var probe=new GameObject("RiskAI Runtime Restart Probe");
            DontDestroyOnLoad(probe);
            probe.AddComponent<RuntimeRestartProbe>();
        }

        IEnumerator Start()
        {
            if(!TryReadCycles(out int cycles,out string error))
            {
                Finish(false,"phase=arguments reason="+error);
                yield break;
            }
            Application.runInBackground=true;
            Debug.Log($"RISKAI_RESTART_START cycles={cycles} battleRealSeconds={BattlefieldRealSeconds:F1} map={BattleSession.MapForNewMatch} seed={BattleSession.SeedForNewMatch} players={BattleSession.PlayerCountForNewMatch}");
            Metrics firstFrontend=default,lastFrontend=default;
            for(int cycle=1;cycle<=cycles;cycle++)
            {
                var loadBattle=SceneManager.LoadSceneAsync(FrontEndController.BattlefieldSceneName,LoadSceneMode.Single);
                if(loadBattle==null){Finish(false,$"phase=load-battle cycle={cycle} reason=missing-scene");yield break;}
                yield return loadBattle;
                while(!BattleSession.Current)yield return null;
                var session=BattleSession.Current;
                session.AiEnabled=false;
                var ready=CaptureMetrics();
                LogMetrics("battle-ready",cycle,ready);
                yield return new WaitForSecondsRealtime(BattlefieldRealSeconds);
                LogMetrics("battle-after-wait",cycle,CaptureMetrics());

                var loadFrontEnd=SceneManager.LoadSceneAsync(FrontEndController.FrontEndSceneName,LoadSceneMode.Single);
                if(loadFrontEnd==null){Finish(false,$"phase=load-frontend cycle={cycle} reason=missing-scene");yield break;}
                yield return loadFrontEnd;
                yield return null;
                LogMetrics("frontend-before-cleanup",cycle,CaptureMetrics());
                yield return Resources.UnloadUnusedAssets();
                GC.Collect();
                yield return null;
                GC.Collect();
                lastFrontend=CaptureMetrics();
                if(cycle==1)firstFrontend=lastFrontend;
                LogMetrics("frontend-after-cleanup",cycle,lastFrontend);
            }
            Debug.Log($"RISKAI_RESTART_RESULT success=true cycles={cycles} map={BattleSession.MapForNewMatch} seed={BattleSession.SeedForNewMatch} players={BattleSession.PlayerCountForNewMatch} " +
                $"baseline=first-frontend-after-cleanup unityallocDelta={lastFrontend.UnityAllocated-firstFrontend.UnityAllocated} reserveDelta={lastFrontend.UnityReserved-firstFrontend.UnityReserved} managedDelta={lastFrontend.Managed-firstFrontend.Managed} " +
                $"MeshesDelta={lastFrontend.Meshes-firstFrontend.Meshes} MaterialsDelta={lastFrontend.Materials-firstFrontend.Materials} RenderersDelta={lastFrontend.Renderers-firstFrontend.Renderers} NavMeshDataDelta={lastFrontend.NavMeshData-firstFrontend.NavMeshData}");
#if UNITY_WEBGL && !UNITY_EDITOR
            // A browser probe leaves its configuration scene visible for inspection.
#else
            Application.Quit(0);
#endif
        }

        static Metrics CaptureMetrics() => new Metrics
        {
            UnityAllocated=Profiler.GetTotalAllocatedMemoryLong(),
            UnityReserved=Profiler.GetTotalReservedMemoryLong(),
            Managed=GC.GetTotalMemory(false),
            Meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,
            Materials=Resources.FindObjectsOfTypeAll<Material>().Length,
            Renderers=Resources.FindObjectsOfTypeAll<Renderer>().Length,
            NavMeshData=Resources.FindObjectsOfTypeAll<NavMeshData>().Length
        };

        static void LogMetrics(string checkpoint,int cycle,Metrics metrics)
        {
            Debug.Log($"RISKAI_RESTART_METRICS checkpoint={checkpoint} cycle={cycle} unityalloc={metrics.UnityAllocated} reserve={metrics.UnityReserved} managed={metrics.Managed} Meshes={metrics.Meshes} Materials={metrics.Materials} Renderers={metrics.Renderers} NavMeshData={metrics.NavMeshData}");
        }

        static bool TryReadCycles(out int cycles,out string error)
        {
            cycles=DefaultCycles;error=null;
            var arguments=LaunchArguments.Get();
            for(int i=0;i<arguments.Length;i++)
            {
                string text=null;
                if(string.Equals(arguments[i],CyclesFlag,StringComparison.OrdinalIgnoreCase))
                {
                    if(i+1>=arguments.Length){error=CyclesFlag+"-missing-value";return false;}
                    text=arguments[++i];
                }
                else if(arguments[i].StartsWith(CyclesFlag+"=",StringComparison.OrdinalIgnoreCase))text=arguments[i].Substring(CyclesFlag.Length+1);
                if(text==null)continue;
                if(!int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out cycles)||cycles<2||cycles>5)
                {
                    error=CyclesFlag+"-requires-integer-2-to-5";
                    return false;
                }
            }
            return true;
        }

        static bool HasFlag(string flag)
        {
            foreach(var argument in LaunchArguments.Get())
                if(string.Equals(argument,flag,StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }

        static void Finish(bool success,string details)
        {
            string result="RISKAI_RESTART_RESULT success="+success+" "+details;
            if(success)Debug.Log(result);else Debug.LogError(result);
#if UNITY_WEBGL && !UNITY_EDITOR
#else
            Application.Quit(success?0:1);
#endif
        }
    }
}
