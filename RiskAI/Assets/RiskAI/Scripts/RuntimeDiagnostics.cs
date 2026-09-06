using System;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Reports coarse runtime health at a deliberately low cadence.</summary>
    public sealed class RuntimeDiagnostics : MonoBehaviour
    {
        const float ReportIntervalSeconds = 30f;
        BattleSession session;
        float nextReportAt, frameSeconds, maxFrameSeconds;
        int frameCount, framesOver50Milliseconds, framesOver100Milliseconds, framesOver250Milliseconds;
        int focusLost, focusRecovered, pauseEntered, pauseResumed;
        bool applicationFocused = true, discardNextGameplayFrame, pauseStateKnown, previousPaused;
        long managedHeapBytes, appliedCommands, rejectedCommands;
        int generation0Collections;

        public void Initialize(BattleSession battle)
        {
            session = battle;
            pauseStateKnown = false;
            if (session.Commands != null) session.Commands.SetTelemetryPauseState(session.Paused);
            ResetWindow(Time.unscaledTime);
        }
        void Awake()
        {
            applicationFocused = Application.isFocused;
            ResetWindow(Time.unscaledTime);
        }

        // Unity invokes this even when runInBackground prevents Update while unfocused.
        // The next focused sample is discarded, so the accumulated background frame is
        // never mistaken for a gameplay frame.
        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus == applicationFocused) return;
            if (hasFocus) focusRecovered++; else focusLost++;
            applicationFocused = hasFocus;
            discardNextGameplayFrame = true;
        }

        void Update()
        {
            bool paused = session != null && session.Paused;
            if (session != null && session.Commands != null) session.Commands.SetTelemetryPauseState(paused);
            bool pauseTransition = pauseStateKnown && paused != previousPaused;
            if (pauseStateKnown)
            {
                if (paused && !previousPaused) pauseEntered++;
                if (!paused && previousPaused) pauseResumed++;
            }
            previousPaused = paused;
            pauseStateKnown = true;

            if (applicationFocused && !paused)
            {
                // A recovered focus or resumed pause can supply a stale delta. Drop
                // exactly one eligible sample, then resume normal frame accounting.
                if (discardNextGameplayFrame || pauseTransition) discardNextGameplayFrame = false;
                else RecordGameplayFrame(Time.unscaledDeltaTime);
            }

            float now = Time.unscaledTime;
            if (now < nextReportAt) return;
            // Bootstrap may attach this before scene construction completes. Lookup is never per-frame.
            if (!session) session = BattleSession.Current;
            if (session != null)
            {
                paused = session.Paused;
                if (session.Commands != null) session.Commands.SetTelemetryPauseState(paused);
            }
            Report(applicationFocused, paused);
            ResetWindow(now);
        }

        void RecordGameplayFrame(float delta)
        {
            frameSeconds += delta;
            if (delta > maxFrameSeconds) maxFrameSeconds = delta;
            frameCount++;
            if (delta > .050f) framesOver50Milliseconds++;
            if (delta > .100f) framesOver100Milliseconds++;
            if (delta > .250f) framesOver250Milliseconds++;
        }

        void ResetWindow(float now)
        {
            nextReportAt = now + ReportIntervalSeconds;
            frameSeconds = maxFrameSeconds = 0;
            frameCount = framesOver50Milliseconds = framesOver100Milliseconds = framesOver250Milliseconds = 0;
            focusLost = focusRecovered = pauseEntered = pauseResumed = 0;
            managedHeapBytes = GC.GetTotalMemory(false);
            generation0Collections = GC.CollectionCount(0);
            var commands = session ? session.Commands : null;
            appliedCommands = commands != null ? commands.AppliedCount : 0;
            rejectedCommands = commands != null ? commands.RejectedCount : 0;
        }

        void Report(bool focused, bool paused)
        {
            if (!session) { Debug.Log("RuntimeDiagnostics 30s session=unavailable"); return; }
            var commands = session.Commands;
            var commandTelemetry = commands != null ? commands.ConsumeTelemetry() : default;
            var worldTelemetry = session.World != null ? session.World.ConsumeTelemetry() : default;
            var seaTelemetry = SeaNavigation.ConsumeTelemetry();
            long heapNow = GC.GetTotalMemory(false);
            long appliedNow = commands != null ? commands.AppliedCount : 0;
            long rejectedNow = commands != null ? commands.RejectedCount : 0;
            float averageMilliseconds = frameCount > 0 ? frameSeconds * 1000f / frameCount : 0;
            int pathPending = 0; float pendingAgeTotal = 0, pendingAgeMax = 0;
            var units = session.Units;
            // This is a point-in-time sample once per report, not per-frame polling.
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !unit.PathPendingForTelemetry) continue;
                pathPending++;
                float age = unit.PathPendingAgeForTelemetry;
                pendingAgeTotal += age;
                if (age > pendingAgeMax) pendingAgeMax = age;
            }
            double ticks = worldTelemetry.TickCount > 0 ? worldTelemetry.TickCount : 1;
            double humanApplyAverage = commandTelemetry.HumanApplied > 0 ? commandTelemetry.HumanSubmitToApplyMilliseconds / commandTelemetry.HumanApplied : 0;
            double aiApplyAverage = commandTelemetry.AiApplied > 0 ? commandTelemetry.AiSubmitToApplyMilliseconds / commandTelemetry.AiApplied : 0;
            double firstMoveAverage = commandTelemetry.HumanFirstMoveCount > 0 ? commandTelemetry.HumanFirstMoveMilliseconds / commandTelemetry.HumanFirstMoveCount : 0;
            float pendingAgeAverage = pathPending > 0 ? pendingAgeTotal * 1000f / pathPending : 0;

            Debug.Log(
                $"RuntimeDiagnostics 30s avgMs={averageMilliseconds:F2} maxMs={maxFrameSeconds * 1000f:F2} " +
                $"managedHeapDeltaB={heapNow - managedHeapBytes} gcGen0={GC.CollectionCount(0) - generation0Collections} " +
                $"units={session.Units.Count} simTime={session.BattleTime:F1} simTicks={session.Clock.TickCount} " +
                $"commandsPending={(commands != null ? commands.PendingCount : 0)} commandsApplied={appliedNow - appliedCommands} commandsRejected={rejectedNow - rejectedCommands} " +
                $"frames={frameCount} over50ms={framesOver50Milliseconds} over100ms={framesOver100Milliseconds} over250ms={framesOver250Milliseconds} " +
                $"focused={focused} paused={paused} focusLost={focusLost} focusRecovered={focusRecovered} pauseEntered={pauseEntered} pauseResumed={pauseResumed} " +
                $"worldTicks={worldTelemetry.TickCount} worldAvgMs={worldTelemetry.TotalMilliseconds / ticks:F2} worldMaxMs={worldTelemetry.MaxMilliseconds:F2} " +
                $"worldCommandsAvgMs={worldTelemetry.CommandsMilliseconds / ticks:F2} worldCommandsMaxMs={worldTelemetry.CommandsMaxMilliseconds:F2} " +
                $"worldSpatialAvgMs={worldTelemetry.SpatialMilliseconds / ticks:F2} worldSpatialMaxMs={worldTelemetry.SpatialMaxMilliseconds:F2} " +
                $"worldSoldiersAvgMs={worldTelemetry.SoldiersMilliseconds / ticks:F2} worldSoldiersMaxMs={worldTelemetry.SoldiersMaxMilliseconds:F2} " +
                $"worldTowersAvgMs={worldTelemetry.TowersMilliseconds / ticks:F2} worldTowersMaxMs={worldTelemetry.TowersMaxMilliseconds:F2} " +
                $"worldShipsAvgMs={worldTelemetry.ShipsMilliseconds / ticks:F2} worldShipsMaxMs={worldTelemetry.ShipsMaxMilliseconds:F2} " +
                $"worldCombatAvgMs={worldTelemetry.CombatMilliseconds / ticks:F2} worldCombatMaxMs={worldTelemetry.CombatMaxMilliseconds:F2} " +
                $"worldClaimsAvgMs={worldTelemetry.ClaimsMilliseconds / ticks:F2} worldClaimsMaxMs={worldTelemetry.ClaimsMaxMilliseconds:F2} " +
                $"worldRulesAvgMs={worldTelemetry.RulesMilliseconds / ticks:F2} worldRulesMaxMs={worldTelemetry.RulesMaxMilliseconds:F2} " +
                $"worldAiAvgMs={worldTelemetry.AiMilliseconds / ticks:F2} worldAiMaxMs={worldTelemetry.AiMaxMilliseconds:F2} " +
                $"worldLandAiAvgMs={worldTelemetry.LandAiMilliseconds / ticks:F2} worldLandAiMaxMs={worldTelemetry.LandAiMaxMilliseconds:F2} " +
                $"worldNavalAiAvgMs={worldTelemetry.NavalAiMilliseconds / ticks:F2} worldNavalAiMaxMs={worldTelemetry.NavalAiMaxMilliseconds:F2} " +
                $"worldPoolAvgMs={worldTelemetry.PoolMilliseconds / ticks:F2} worldPoolMaxMs={worldTelemetry.PoolMaxMilliseconds:F2} " +
                $"seaSearches={seaTelemetry.SearchCount} seaSearchAvgMs={(seaTelemetry.SearchCount > 0 ? seaTelemetry.TotalMilliseconds / seaTelemetry.SearchCount : 0):F2} seaSearchMaxMs={seaTelemetry.MaxMilliseconds:F2} " +
                $"seaExpandedMax={seaTelemetry.ExpandedMax} seaDirect={seaTelemetry.DirectCount} seaDisconnected={seaTelemetry.DisconnectedCount} seaGridBuildMs={SeaNavigation.GridBuildMilliseconds:F2} " +
                $"commandSubmitHuman={commandTelemetry.HumanSubmitted} commandSubmitAi={commandTelemetry.AiSubmitted} commandApplyHuman={commandTelemetry.HumanApplied} commandApplyAi={commandTelemetry.AiApplied} " +
                $"commandRejectHuman={commandTelemetry.HumanRejected} commandRejectAi={commandTelemetry.AiRejected} commandQueueMax={commandTelemetry.MaxQueueDepth} " +
                $"submitApplyHumanActiveAvgMs={humanApplyAverage:F2} submitApplyHumanActiveMaxMs={commandTelemetry.HumanSubmitToApplyMaxMilliseconds:F2} " +
                $"submitApplyAiActiveAvgMs={aiApplyAverage:F2} submitApplyAiActiveMaxMs={commandTelemetry.AiSubmitToApplyMaxMilliseconds:F2} commandObservedPauseMs={commandTelemetry.ObservedPauseMilliseconds:F1} " +
                $"firstMoveHumanEligible={commandTelemetry.HumanFirstMoveEligible} firstMoveHumanCancelled={commandTelemetry.HumanFirstMoveCancelled} " +
                $"firstMoveHumanCount={commandTelemetry.HumanFirstMoveCount} firstMoveHumanActiveAvgMs={firstMoveAverage:F2} firstMoveHumanActiveMaxMs={commandTelemetry.HumanFirstMoveMaxMilliseconds:F2} " +
                $"pathPending={pathPending} pathPendingAvgAgeMs={pendingAgeAverage:F1} pathPendingMaxAgeMs={pendingAgeMax * 1000f:F1} navIterationsPerFrame={UnityEngine.AI.NavMesh.pathfindingIterationsPerFrame}");
            managedHeapBytes = heapNow;
            generation0Collections = GC.CollectionCount(0);
            appliedCommands = appliedNow;
            rejectedCommands = rejectedNow;
        }
    }
}
