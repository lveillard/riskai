using System;
using Unity.Profiling;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Opt-in hitch correlation for RuntimeCommandProbe. It samples engine counters only
    /// when explicitly requested and logs at most 64 frames; it does not establish causality.
    /// </summary>
    public sealed class RuntimeFrameProbe : MonoBehaviour
    {
        const float HitchSeconds = .050f;
        const int MaxHitchLogs = 64;

        readonly FrameTiming[] frameTimings = new FrameTiming[1];
        ProfilerRecorder mainThread;
        ProfilerRecorder renderThread;
        ProfilerRecorder gcAllocated;
        bool frameTimingsAvailable;
        int startedFrame;
        float startedRealtime;
        int gcGenerationAtStart;
        int hitchLogs;

        void Awake()
        {
            startedFrame = Time.frameCount;
            startedRealtime = Time.realtimeSinceStartup;
            gcGenerationAtStart = GC.CollectionCount(0);
            // Marker availability is platform/build dependent. Invalid recorders remain
            // observable in a hitch line as valid=false and raw=-1.
            mainThread = StartRecorder(ProfilerCategory.Internal, "Main Thread");
            renderThread = StartRecorder(ProfilerCategory.Internal, "Render Thread");
            gcAllocated = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            frameTimingsAvailable = FrameTimingManager.IsFeatureEnabled();
        }

        static ProfilerRecorder StartRecorder(ProfilerCategory category, string marker)
        {
            try
            {
                // Capacity one must wrap so a later hitch reads a recent frame,
                // rather than the first sample collected when the recorder started.
                return ProfilerRecorder.StartNew(category, marker, 1, ProfilerRecorderOptions.Default);
            }
            catch (ArgumentException)
            {
                return default;
            }
        }

        void Update()
        {
            if (frameTimingsAvailable) FrameTimingManager.CaptureFrameTimings();

            float frameSeconds = Time.unscaledDeltaTime;
            if (frameSeconds <= HitchSeconds || hitchLogs >= MaxHitchLogs) return;
            hitchLogs++;

            uint timingCount = frameTimingsAvailable
                ? FrameTimingManager.GetLatestTimings(1, frameTimings)
                : 0;
            double cpuMs = timingCount > 0 ? frameTimings[0].cpuFrameTime : -1;
            double gpuMs = timingCount > 0 ? frameTimings[0].gpuFrameTime : -1;
            double mainMs = timingCount > 0 ? frameTimings[0].cpuMainThreadFrameTime : -1;
            double renderMs = timingCount > 0 ? frameTimings[0].cpuRenderThreadFrameTime : -1;

            var session = BattleSession.Current;
            int units = session ? session.Units.Count : -1;
            Debug.Log(
                $"RISKAI_FRAME_HITCH frame={Time.frameCount} relativeFrame={Time.frameCount - startedFrame} " +
                $"relativeRealtimeMs={(Time.realtimeSinceStartup - startedRealtime) * 1000f:F2} " +
                $"frameMs={frameSeconds * 1000f:F2} units={units} gcGen0Delta={GC.CollectionCount(0) - gcGenerationAtStart} " +
                $"frameTimingEnabled={frameTimingsAvailable} frameTimingCount={timingCount} " +
                $"frameTimingCpuMs={cpuMs:F2} frameTimingGpuMs={gpuMs:F2} frameTimingMainMs={mainMs:F2} frameTimingRenderMs={renderMs:F2} " +
                $"mainThreadValid={mainThread.Valid} mainThreadCount={CountOrMissing(mainThread)} mainThreadRaw={ValueOrMissing(mainThread)} " +
                $"renderThreadValid={renderThread.Valid} renderThreadCount={CountOrMissing(renderThread)} renderThreadRaw={ValueOrMissing(renderThread)} " +
                $"gcAllocatedValid={gcAllocated.Valid} gcAllocatedCount={CountOrMissing(gcAllocated)} gcAllocatedB={ValueOrMissing(gcAllocated)} " +
                "frameTimingSampleAsync=true correlationOnly=true");
        }

        static int CountOrMissing(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.Count : -1;
        }

        static long ValueOrMissing(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.Count > 0 ? recorder.LastValue : -1;
        }

        void OnDestroy()
        {
            Dispose(ref mainThread);
            Dispose(ref renderThread);
            Dispose(ref gcAllocated);
        }

        static void Dispose(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid) recorder.Dispose();
            recorder = default;
        }
    }
}
