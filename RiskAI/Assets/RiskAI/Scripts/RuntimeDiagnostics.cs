using System;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Reports coarse runtime health at a deliberately low cadence. Attach after a
    /// BattleSession is initialized, or call Initialize from the bootstrap.
    /// </summary>
    public sealed class RuntimeDiagnostics : MonoBehaviour
    {
        const float ReportIntervalSeconds = 30f;

        BattleSession session;
        float nextReportAt;
        float frameSeconds;
        float maxFrameSeconds;
        int frameCount;
        long managedHeapBytes;
        int generation0Collections;
        long appliedCommands;
        long rejectedCommands;

        public void Initialize(BattleSession battle)
        {
            session = battle;
            ResetWindow(Time.unscaledTime);
        }

        void Awake()
        {
            ResetWindow(Time.unscaledTime);
        }

        void Update()
        {
            float delta = Time.unscaledDeltaTime;
            frameSeconds += delta;
            if (delta > maxFrameSeconds) maxFrameSeconds = delta;
            frameCount++;

            float now = Time.unscaledTime;
            if (now < nextReportAt) return;

            // Bootstrap may attach this before it has completed scene construction.
            // This lookup is amortized to the reporting cadence, never per frame.
            if (!session) session = BattleSession.Current;
            Report();
            ResetWindow(now);
        }

        void ResetWindow(float now)
        {
            nextReportAt = now + ReportIntervalSeconds;
            frameSeconds = 0;
            maxFrameSeconds = 0;
            frameCount = 0;
            managedHeapBytes = GC.GetTotalMemory(false);
            generation0Collections = GC.CollectionCount(0);

            var commands = session ? session.Commands : null;
            appliedCommands = commands != null ? commands.AppliedCount : 0;
            rejectedCommands = commands != null ? commands.RejectedCount : 0;
        }

        void Report()
        {
            if (!session)
            {
                Debug.Log("RuntimeDiagnostics 30s session=unavailable");
                return;
            }

            var commands = session.Commands;
            long heapNow = GC.GetTotalMemory(false);
            long appliedNow = commands != null ? commands.AppliedCount : 0;
            long rejectedNow = commands != null ? commands.RejectedCount : 0;
            float averageMilliseconds = frameCount > 0 ? frameSeconds * 1000f / frameCount : 0;
            Debug.Log(
                $"RuntimeDiagnostics 30s avgMs={averageMilliseconds:F2} maxMs={maxFrameSeconds * 1000f:F2} " +
                $"managedHeapDeltaB={heapNow - managedHeapBytes} gcGen0={GC.CollectionCount(0) - generation0Collections} " +
                $"units={session.Units.Count} simTime={session.BattleTime:F1} simTicks={session.Clock.TickCount} " +
                $"commandsPending={(commands != null ? commands.PendingCount : 0)} " +
                $"commandsApplied={appliedNow - appliedCommands} commandsRejected={rejectedNow - rejectedCommands}");

            managedHeapBytes = heapNow;
            generation0Collections = GC.CollectionCount(0);
            appliedCommands = appliedNow;
            rejectedCommands = rejectedNow;
        }
    }
}