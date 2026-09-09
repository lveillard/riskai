using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public sealed partial class RuntimeCommandProbe
    {
        const int SustainedCohortSize = 900;
        const float SustainedGridSpacing = 4f;
        const float SustainedRoundSeconds = 6f;
        const float SustainedHoldSeconds = .3f;

        // A synthetic navigation/render workload, not an advanced combat match.
        // No respawning, health changes, per-round warps or runtime path calculations.
        IEnumerator RunSustained(BattleSession session, float duration)
        {
            Application.runInBackground = true;
            Time.timeScale = 1;
            if (session.Paused) session.TogglePause();
            session.AiEnabled = false;
            session.Reinforcements.SuspendedForProbe = true;
            var originalUnits = new List<Soldier>(session.Units);
            var originalTargets = new List<CombatTarget>(session.Targets);
            var controller = FindFirstObjectByType<RtsController>();
            if (controller) { controller.HelpVisible = false; controller.enabled = false; }
            // Retain ownership/presence to avoid victory, while freezing original
            // actors. Only the newly spawned cohort runs soldier simulation.
            foreach (var unit in session.Units)
            {
                if (!unit) continue;
                unit.SetSimulationPaused(true);
                unit.enabled = false;
            }
            foreach (var tower in session.Towers) if (tower) tower.enabled = false;
            foreach (var town in session.Towns) if (town) town.enabled = false;
            if (session.Naval)
            {
                foreach (var ship in session.Naval.Ships) if (ship) ship.enabled = false;
                foreach (var harbor in session.Naval.Harbors) if (harbor) harbor.enabled = false;
            }

            var cohort = new List<TrackedUnit>(SustainedCohortSize);
            var ends = new List<Vector3>(SustainedCohortSize);
            var candidates = new List<Vector3>();
            var minimum = MapLayout.PlayableMin;
            var maximum = MapLayout.PlayableMax;
            // Shuffle the fixed grid using the match seed to spread the load over
            // the map instead of filling a single corner or a single town rally.
            // Four metres keeps agents separated while supplying enough clear
            // corridors on Europe's smaller connected land regions.
            for (float z = minimum.y + 12; z < maximum.y - 12; z += SustainedGridSpacing)
                for (float x = minimum.x + 12; x < maximum.x - 52; x += SustainedGridSpacing)
                    candidates.Add(new Vector3(x, 0, z));
            var random = new System.Random(session.Seed);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                var swap = candidates[i]; candidates[i] = candidates[other]; candidates[other] = swap;
            }
            uint fingerprint = 2166136261;
            int candidatesTested = 0;
            foreach (var candidate in candidates)
            {
                candidatesTested++;
                if (!TryFindLandNavMeshPoint(candidate, out var start) ||
                    !TryFindLandNavMeshPoint(candidate + Vector3.right * 40, out var end)) continue;
                // Tight sampling prevents adjacent grid points collapsing onto the
                // same shoreline. Straight corridors also prevent shared bottlenecks.
                if (HorizontalDistanceSquared(candidate, start) > 1 ||
                    HorizontalDistanceSquared(candidate + Vector3.right * 40, end) > 1 ||
                    NavMesh.Raycast(start, end, out _, NavMesh.AllAreas)) continue;
                bool nearActor = false;
                foreach (var target in originalTargets)
                {
                    if (!target) continue;
                    var delta = end - start; delta.y = 0;
                    var offset = target.transform.position - start; offset.y = 0;
                    float along = Mathf.Clamp01(Vector3.Dot(offset, delta) / delta.sqrMagnitude);
                    if (HorizontalDistanceSquared(start + delta * along, target.transform.position) < 225)
                    { nearActor = true; break; }
                }
                if (nearActor) continue;
                var soldier = session.Spawn(0, UnitKind.Archer, start);
                if (!soldier) continue;
                cohort.Add(new TrackedUnit { Soldier = soldier, EntityId = soldier.EntityId, Start = start });
                ends.Add(end);
                fingerprint = HashPoint(HashPoint(fingerprint, start), end);
                if (cohort.Count == SustainedCohortSize) break;
            }
            if (cohort.Count != SustainedCohortSize)
            {
                Finish(false, $"phase=fixture valid=false reason=insufficient-separated-corridors cohort={cohort.Count} gridSpacing={SustainedGridSpacing} gridCandidates={candidates.Count} candidatesTested={candidatesTested}");
                yield break;
            }
            SubmitHolds(session, cohort);
            yield return new WaitForSecondsRealtime(StabilizeSeconds);
            // Avoidance settling is excluded and the exact starting coordinates
            // are restored once before both arms, never during measurement.
            foreach (var unit in cohort)
                if (!unit.Soldier.Agent.Warp(unit.Start))
                {
                    Finish(false, "phase=fixture valid=false reason=initial-warp-failed"); yield break;
                }
            var diagnostics = FindFirstObjectByType<RuntimeDiagnostics>();
            if (diagnostics) diagnostics.BeginProbeMeasurement();
            var frameTrace=FindFirstObjectByType<RuntimeFrameProbe>();if(frameTrace)frameTrace.BeginMeasurement();
            float started = Time.realtimeSinceStartup, simStarted = session.BattleTime;
            long appliedStart = session.Commands.AppliedCount, rejectedStart = session.Commands.RejectedCount;
            int round = -1, issuedRound = -1, submitted = 0, minAlive = cohort.Count;
            int minMoving = cohort.Count, maxPending = 0, sampleCount = 0;
            long movingTotal = 0;
            float nextSample = 0, maxPendingAge = 0;
            bool scheduleValid = true;
            bool actorsUnchanged = true;
            var frames = new FrameSampler();
            string fixtureHash = fingerprint.ToString("X8", CultureInfo.InvariantCulture);
            Debug.Log($"RISKAI_PROBE_PHASE phase=measurement scenario=sustained-navigation-v2 map={BattleSession.MapForNewMatch} seed={session.Seed} " +
                $"cohort={cohort.Count} fixtureHash={fixtureHash} navBudget={NavMesh.pathfindingIterationsPerFrame} timeScale=1 duration={duration:F1} " +
                $"gridSpacing={SustainedGridSpacing} gridCandidates={candidates.Count} candidatesTested={candidatesTested} " +
                $"roundSeconds={SustainedRoundSeconds} holdSeconds={SustainedHoldSeconds} ai=false combat=false reinforcements=false originalActors=frozen controller=disabled");
            while (Time.realtimeSinceStartup - started < duration && session.Winner < 0)
            {
                float elapsed = Time.realtimeSinceStartup - started;
                frames.Record(Time.unscaledDeltaTime);
                int scheduledRound = Mathf.FloorToInt(elapsed / SustainedRoundSeconds);
                if (scheduledRound != round)
                {
                    if (scheduledRound != round + 1 || (round >= 0 && issuedRound != round)) scheduleValid = false;
                    round = scheduledRound;
                    SubmitHolds(session, cohort);
                }
                if (issuedRound != round && elapsed >= round * SustainedRoundSeconds + SustainedHoldSeconds)
                {
                    for (int i = 0; i < cohort.Count; i++)
                    {
                        var unit = cohort[i];
                        if (!unit.CanMove) continue;
                        Vector3 destination = (round & 1) == 0 ? ends[i] : unit.Start;
                        if (session.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move,
                            destination.x, destination.y, destination.z))) submitted++;
                    }
                    issuedRound = round;
                }
                int alive = 0, moving = 0, pending = 0;
                foreach (var unit in cohort)
                {
                    if (!unit.CanMove) continue;
                    alive++;
                    if (unit.Soldier.CurrentTarget) actorsUnchanged = false;
                    var agent = unit.Soldier.Agent;
                    if (agent.velocity.sqrMagnitude > .04f) moving++;
                    if (agent.pathPending) pending++;
                    maxPendingAge = Mathf.Max(maxPendingAge, unit.Soldier.PathPendingAgeForTelemetry);
                }
                minAlive = Mathf.Min(minAlive, alive);
                if (session.Units.Count != originalUnits.Count + cohort.Count) actorsUnchanged = false;
                foreach (var original in originalUnits)
                    if (!original || !original.IsAlive || original.enabled || original.CurrentTarget) actorsUnchanged = false;
                minMoving = Mathf.Min(minMoving, moving);
                maxPending = Mathf.Max(maxPending, pending);
                movingTotal += moving; sampleCount++;
                RecordDisplacement(cohort);
                if (elapsed >= nextSample)
                {
                    Debug.Log($"RISKAI_SUSTAINED_SAMPLE elapsed={elapsed:F2} cohortAlive={alive} moving={moving} pathPending={pending} originalActorsUnchanged={actorsUnchanged} units={session.Units.Count} timeScale={Time.timeScale:F1}");
                    nextSample += 1;
                }
                if (Time.timeScale != 1 || alive < 800 || !actorsUnchanged) break;
                yield return null;
            }
            if (diagnostics) diagnostics.EndProbeMeasurement();
            if(frameTrace)frameTrace.EndMeasurement();
            int moved = 0;
            foreach (var unit in cohort) if (unit.MaxDisplacement >= MovedDistance) moved++;
            float realSeconds = Time.realtimeSinceStartup - started;
            long rejected = session.Commands.RejectedCount - rejectedStart;
            bool valid = realSeconds >= duration && minAlive >= 800 && moved >= 800 && scheduleValid && actorsUnchanged &&
                session.Winner < 0 && Time.timeScale == 1 && rejected == 0 && session.Commands.PendingCount == 0;
            Finish(valid, $"phase=measurement scenario=sustained-navigation-v2 valid={valid} fixtureHash={fixtureHash} " +
                $"navBudget={NavMesh.pathfindingIterationsPerFrame} cohort={cohort.Count} cohortMinAlive={minAlive} movedUnits={moved} " +
                $"movingMin={minMoving} movingFrameAvg={(sampleCount > 0 ? (double)movingTotal / sampleCount : 0):F2} " +
                $"pathPendingMax={maxPending} pathPendingMaxAgeMs={maxPendingAge * 1000:F2} " +
                $"realSeconds={realSeconds:F2} simSecondsDelta={session.BattleTime - simStarted:F2} originalActorsUnchanged={actorsUnchanged} scheduleValid={scheduleValid} rounds={round + 1} " +
                $"probeMovesSubmitted={submitted} applied={session.Commands.AppliedCount - appliedStart} rejected={rejected} queued={session.Commands.PendingCount} {frames.Describe()}");
        }

        static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        { float x = a.x - b.x, z = a.z - b.z; return x * x + z * z; }

        static uint HashPoint(uint hash, Vector3 point)
        {
            unchecked
            {
                hash = (hash ^ (uint)Mathf.RoundToInt(point.x * 1000)) * 16777619;
                hash = (hash ^ (uint)Mathf.RoundToInt(point.y * 1000)) * 16777619;
                return (hash ^ (uint)Mathf.RoundToInt(point.z * 1000)) * 16777619;
            }
        }
    }
}
