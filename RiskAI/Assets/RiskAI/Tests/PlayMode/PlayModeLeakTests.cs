using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    /// <summary>
    /// Native-memory regression across whole match teardowns. One PlayMode run loads hundreds of
    /// maps in a single process, so any runtime mesh, material or texture that survives its scene
    /// (a combined static-batch mesh, a procedural world mesh, a session texture) grows the process
    /// until the suite exhausts memory.
    /// </summary>
    public sealed class PlayModeLeakTests
    {
        // A match may retain shared process-wide caches, but nothing that scales with the match count.
        const long MaxRetainedPerCycle = 5 * 1024 * 1024;
        const int MeasuredCycles = 5;
        // Without an asset sweep only truly missing ownership shows up; a small frame of slack
        // absorbs renderer and editor noise.
        const long MaxUnsweptPerCycle = 2 * 1024 * 1024;
        const int UnsweptCycles = 3;
        // Every asynchronous wait gets a hard deadline: a stuck scene unload or asset sweep must
        // fail the test in seconds instead of hanging the batch run for ever.
        const float WaitTimeoutSeconds = 60f;

        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;
        bool previousExpanded;
        float previousTimeScale;
        long settledBytes;
        Scene previousScene, matchScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch;
            previousMode = BattleSession.ModeForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch;
            previousSeed = BattleSession.SeedForNewMatch;
            previousExpanded = BattleSession.ExpandedMapForNewMatch;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (matchScene.IsValid() && matchScene.isLoaded) yield return UnloadMatch();
            Time.timeScale = previousTimeScale;
            BattleSession.MapForNewMatch = previousMap;
            BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.ModeForNewMatch = previousMode;
            BattleSession.PlayerCountForNewMatch = previousPlayers;
            BattleSession.SeedForNewMatch = previousSeed;
            BattleSession.ExpandedMapForNewMatch = previousExpanded;
            MapLayout.Configure(previousMap);
            yield return null;
        }

        /// <summary>
        /// The supported contract: a match, its scene and every asset the session created must be
        /// gone after teardown. The asset sweep before each reading drops anything orphaned but
        /// unreferenced, so what is measured is real retention, not pending garbage collection.
        /// </summary>
        [UnityTest]
        public IEnumerator ClassicAndEuropeMatchTeardownRetainsNoNativeMemory()
        {
            foreach (var map in new[] { ScenarioMap.Classic, ScenarioMap.Europe })
            {
                // Warm-up one: every process-wide cache (team materials, foliage meshes, audio
                // clips, the territory field) is built once, outside the measured window.
                yield return RunMatch(map);
                yield return Settle(true);
                // Warm-up two: the first asset sweep can drop assets a later match reloads and keeps,
                // so the baseline must be taken after that reload, from a stable post-sweep state.
                yield return RunMatch(map);
                yield return Settle(true);
                long baseline = settledBytes;
                Log("swept", map, 0, baseline, baseline);
                long previous = baseline;
                for (int cycle = 1; cycle <= MeasuredCycles; cycle++)
                {
                    yield return RunMatch(map);
                    yield return Settle(true);
                    Log("swept", map, cycle, settledBytes, settledBytes - previous);
                    Assert.That(settledBytes - previous, Is.LessThan(MaxRetainedPerCycle),
                        $"{map} session {cycle} retained {Mc(settledBytes - previous)} in one cycle. " +
                        "Native assets created by a session must be released on teardown.\n" + DescribeLiveRuntimeAssets());
                    Assert.That(settledBytes - baseline, Is.LessThan(MaxRetainedPerCycle * cycle),
                        $"{map} sessions retained {Mc(settledBytes - baseline)} over {cycle} warm match cycle(s) " +
                        $"({Mc((settledBytes - baseline) / cycle)} per cycle). Native assets created by a session must be released on teardown.\n" +
                        DescribeLiveRuntimeAssets());
                    previous = settledBytes;
                }
            }
        }

        /// <summary>
        /// The same teardown without an asset sweep. Resources.UnloadUnusedAssets would free an
        /// orphaned runtime mesh and hide a missing owner, so this measures only what the session
        /// owners themselves destroy when the scene goes away.
        /// </summary>
        [UnityTest]
        public IEnumerator MatchTeardownReleasesGeneratedAssetsWithoutAnAssetSweep()
        {
            yield return RunMatch(ScenarioMap.Classic);
            yield return Settle(false);
            long baseline = settledBytes;
            Log("unswept", ScenarioMap.Classic, 0, baseline, baseline);
            for (int cycle = 1; cycle <= UnsweptCycles; cycle++)
            {
                yield return RunMatch(ScenarioMap.Classic);
                yield return Settle(false);
                Log("unswept", ScenarioMap.Classic, cycle, settledBytes, settledBytes - baseline);
            }
            long growth = settledBytes - baseline;
            Assert.That(growth, Is.LessThan(MaxUnsweptPerCycle * UnsweptCycles),
                $"Classic sessions retained {Mc(growth)} over {UnsweptCycles} cycles without an asset sweep " +
                $"({Mc(growth / UnsweptCycles)} per cycle). Every generated asset needs an owner that destroys it.\n" +
                DescribeLiveRuntimeAssets());
        }

        /// <summary>Builds and tears down one complete match in its own scene, exactly as a test fixture does.</summary>
        IEnumerator RunMatch(ScenarioMap map)
        {
            BattleSession.MapForNewMatch = map;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.PlayerCountForNewMatch = 2;
            BattleSession.SeedForNewMatch = 160212 + (int)map;
            BattleSession.ExpandedMapForNewMatch = false;

            previousScene = SceneManager.GetActiveScene();
            matchScene = SceneManager.CreateScene("Leak cycle " + map);
            SceneManager.SetActiveScene(matchScene);
            new GameObject("Leak cycle bootstrap " + map).AddComponent<RiskBootstrap>();
            var battle = BattleSession.Current;
            Assert.That(battle, Is.Not.Null, "The bootstrap must build a " + map + " session.");
            battle.AiEnabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            yield return null;
            yield return null;
            yield return UnloadMatch();
        }

        /// <summary>
        /// The teardown every PlayMode fixture uses (e.g. SixteenPlayersGameplayTests.UnloadScenario,
        /// SharedHarborGarrisonTests.RunMap): restore the previous scene, then unload the match scene
        /// only while it is still valid and loaded. The unload itself is deadline-bounded.
        /// </summary>
        IEnumerator UnloadMatch()
        {
            SceneManager.SetActiveScene(previousScene);
            if (matchScene.IsValid() && matchScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(matchScene);
                Assert.That(unload, Is.Not.Null, "Unloading the match scene must return an operation.");
                yield return Within(unload, "Unloading the match scene");
            }
            matchScene = default;
        }

        /// <summary>Yields an async operation until it is done, failing the test when the deadline passes.</summary>
        static IEnumerator Within(AsyncOperation operation, string what)
        {
            float deadline = Time.realtimeSinceStartup + WaitTimeoutSeconds;
            while (!operation.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(operation.isDone, Is.True,
                what + " did not finish within " + WaitTimeoutSeconds + " s; the test fails instead of hanging the suite.");
        }

        /// <summary>Collects managed garbage, optionally sweeps unreferenced assets, then reads total native memory.</summary>
        IEnumerator Settle(bool sweepUnreferencedAssets)
        {
            yield return null;
            for (int i = 0; i < 2; i++)
            {
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
            }
            if (sweepUnreferencedAssets) yield return Within(Resources.UnloadUnusedAssets(), "The asset sweep");
            // Batch mode never reaches EndOfFrame, so settle with plain frames.
            for (int i = 0; i < 3; i++) yield return null;
            settledBytes = Profiler.GetTotalAllocatedMemoryLong();
        }

        static string Mc(long bytes) => (bytes / (1024f * 1024f)).ToString("F2") + " MB";

        static void Log(string stage, ScenarioMap map, int cycle, long settled, long delta) =>
            Debug.Log($"RISKAI_LEAK stage={stage} map={map} cycle={cycle} settledB={settled} deltaB={delta} " +
                $"meshes={Resources.FindObjectsOfTypeAll<Mesh>().Count(mesh => mesh)}");

        static string DescribeLiveRuntimeAssets()
        {
            var meshes = Resources.FindObjectsOfTypeAll<Mesh>().Where(mesh => mesh).ToArray();
            var report = new StringBuilder("live Meshes=").Append(meshes.Length);
            foreach (var group in meshes.GroupBy(mesh => GroupName(mesh.name)).OrderByDescending(group => group.Count()).Take(8))
                report.Append(" · ").Append(group.Key).Append('=').Append(group.Count());
            return report.ToString();
        }

        // Unity names runtime static batches "Combined Mesh (root: <root>)". Grouping keeps a failing
        // report readable while still naming the untracked batch that survived teardown.
        static string GroupName(string name) =>
            !string.IsNullOrEmpty(name) && name.StartsWith("Combined Mesh") ? "Combined Mesh (untracked static batch)" : name;
    }
}
