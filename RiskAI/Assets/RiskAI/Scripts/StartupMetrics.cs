using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;

namespace RiskAI
{
    /// <summary>Coarse loading evidence; never called from the simulation tick.</summary>
    public sealed class StartupMetrics
    {
        readonly Stopwatch watch=Stopwatch.StartNew();
        double previous;
        public void Mark(string phase)
        {
            double now=watch.Elapsed.TotalMilliseconds;
            UnityEngine.Debug.Log($"RISKAI_STARTUP phase={phase} map={MapLayout.Scenario} phaseMs={now-previous:F2} totalMs={now:F2} unityAllocatedB={Profiler.GetTotalAllocatedMemoryLong()} unityReservedB={Profiler.GetTotalReservedMemoryLong()} managedB={System.GC.GetTotalMemory(false)}");
            previous=now;
        }
    }
}
