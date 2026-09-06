using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    /// <summary>
    /// A command-line-only smoke probe for diagnosing live-player responsiveness.
    /// It is deliberately absent unless the player is launched with --riskai-probe.
    /// </summary>
    public sealed class RuntimeCommandProbe : MonoBehaviour
    {
        const string Flag = "--riskai-probe";
        const string WarmupFlag = "--riskai-probe-warmup";
        const string DurationFlag = "--riskai-probe-seconds";
        const string WarmupCommanderFlag = "--riskai-probe-warmup-commander";
        const float DefaultDurationSeconds = 70f;
        const float CommandIntervalSeconds = 2f;
        const float HoldTimeoutSeconds = 1.5f;
        const float StabilizeSeconds = 2f;
        const float DestinationDistance = 20f;
        const float MovedDistance = 1f;

        struct ProbeOptions
        {
            public float WarmupSimulationSeconds;
            public float MeasurementRealtimeSeconds;
            public bool WarmupBlueCommander;
        }

        struct FrameSampler
        {
            public int Count, Over50Milliseconds, Over100Milliseconds, Over250Milliseconds;
            public float TotalSeconds, MaxSeconds;
            public void Record(float delta)
            {
                Count++;
                TotalSeconds += delta;
                if (delta > MaxSeconds) MaxSeconds = delta;
                if (delta > .050f) Over50Milliseconds++;
                if (delta > .100f) Over100Milliseconds++;
                if (delta > .250f) Over250Milliseconds++;
            }
            public string Describe()
            {
                float average = Count > 0 ? TotalSeconds * 1000f / Count : 0;
                return $"measureFrames={Count} measureFrameAvgMs={average:F2} measureFrameMaxMs={MaxSeconds * 1000f:F2} " +
                    $"measureOver50ms={Over50Milliseconds} measureOver100ms={Over100Milliseconds} measureOver250ms={Over250Milliseconds} " +
                    "measureFrameSampler=unscaled_background";
            }
        }

        sealed class TrackedUnit
        {
            public Soldier Soldier;
            public int EntityId;
            public Vector3 Start;
            public Vector3 Direction;
            public float MaxDisplacement;
            // Pooled views can later represent a completely different unit.
            public bool SameLifetime => Soldier && Soldier.EntityId == EntityId && Soldier.IsAlive;
            public bool CanMove => SameLifetime && !Soldier.IsGarrison;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateWhenRequested()
        {
            if (!HasProbeFlag()) return;
            var probe = new GameObject("RiskAI Runtime Command Probe");
            DontDestroyOnLoad(probe);
            probe.AddComponent<RuntimeCommandProbe>();
        }

        static bool HasProbeFlag()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
                if (string.Equals(argument, Flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        IEnumerator Start()
        {
            if (!TryReadOptions(out var options, out var optionError))
            {
                Finish(false, "phase=arguments valid=false reason=" + optionError);
                yield break;
            }

            // Let bootstrap construct terrain, NavMesh, session, and UI first.
            yield return null;
            yield return null;
            var session = BattleSession.Current;
            if (!session)
            {
                Finish(false, "phase=bootstrap valid=false reason=session-unavailable simSecondsDelta=0.00 applied=0 rejected=0 queued=0 movedUnits=0/0 maxDisplacement=0.00");
                yield break;
            }

            Application.runInBackground = true;
            if (session.Paused) session.TogglePause();
            var controller = FindFirstObjectByType<RtsController>();
            if (controller)
            {
                controller.HelpVisible = false;
                controller.enabled = false;
            }
            session.AiEnabled = true;
            Debug.Log($"RISKAI_PROBE_START phase=setup cpu={SystemInfo.processorType} gpu={SystemInfo.graphicsDeviceName} screen={Screen.width}x{Screen.height} " +
                $"map={BattleSession.MapForNewMatch} seed={session.Seed} players={session.PlayerCount} towns={session.Towns.Count} camps={session.Camps.Count} units={session.Units.Count} " +
                $"warmupRequestedSimSeconds={options.WarmupSimulationSeconds:F1} warmupBlueAI={options.WarmupBlueCommander} measurementRequestedRealSeconds={options.MeasurementRealtimeSeconds:F1} controller=disabled picking=synthetic");

            float warmupStartedAt = session.BattleTime;
            float warmupCompleted = 0;
            if (options.WarmupSimulationSeconds > 0)
            {
                float target = warmupStartedAt + options.WarmupSimulationSeconds;
                var warmupBlueCommander = options.WarmupBlueCommander ? new SkirmishCommander(session, 0) : null;
                long observedClockTick = session.Clock.TickCount;
                Time.timeScale = 8f;
                Debug.Log($"RISKAI_PROBE_PHASE phase=warmup map={BattleSession.MapForNewMatch} seed={session.Seed} simStart={warmupStartedAt:F1} simTarget={target:F1} timeScale=8 warmupBlueAI={options.WarmupBlueCommander}");
                while (session.BattleTime < target && session.Winner < 0)
                {
                    yield return null;
                    long currentClockTick = session.Clock.TickCount;
                    if (warmupBlueCommander != null && currentClockTick > observedClockTick)
                        warmupBlueCommander.Tick((float)((currentClockTick - observedClockTick) * SimClock.StepSeconds));
                    observedClockTick = currentClockTick;
                }
                warmupCompleted = session.BattleTime - warmupStartedAt;
                Time.timeScale = 1f;
                if (session.Winner >= 0)
                {
                    Finish(false, $"phase=warmup valid=false reason=winner winner={session.Winner} warmupRequestedSimSeconds={options.WarmupSimulationSeconds:F1} " +
                        $"warmupCompletedSimSeconds={warmupCompleted:F1} map={BattleSession.MapForNewMatch} seed={session.Seed} unitsFinal={session.Units.Count}");
                    yield break;
                }
                Debug.Log($"RISKAI_PROBE_PHASE phase=stabilize realSeconds={StabilizeSeconds:F1} timeScale=1 warmupCompletedSimSeconds={warmupCompleted:F1}");
                yield return new WaitForSecondsRealtime(StabilizeSeconds);
                if (session.Winner >= 0)
                {
                    Finish(false, $"phase=stabilize valid=false reason=winner winner={session.Winner} warmupRequestedSimSeconds={options.WarmupSimulationSeconds:F1} " +
                        $"warmupCompletedSimSeconds={warmupCompleted:F1} map={BattleSession.MapForNewMatch} seed={session.Seed} unitsFinal={session.Units.Count}");
                    yield break;
                }
            }
            else Time.timeScale = 1f;

            // Spawn only after warmup/stabilization, so tracked player units do not
            // alter the advanced-state simulation that the warmup intends to create.
            var tracked = SpawnPlayerArchers(session);
            if (tracked.Count == 0)
            {
                Finish(false, $"phase=fixture valid=false reason=no-player-zero-mobile-cohort map={BattleSession.MapForNewMatch} seed={session.Seed} warmupCompletedSimSeconds={warmupCompleted:F1}");
                yield break;
            }
            int unitsInitial = session.Units.Count;
            long appliedAtStart = session.Commands.AppliedCount;
            long rejectedAtStart = session.Commands.RejectedCount;
            float simulationAtStart = session.BattleTime;
            float realtimeAtStart = Time.realtimeSinceStartup;
            var camera = Camera.main;
            var fixedPointer = new Vector2(Screen.width * .5f, Screen.height * .5f);
            Debug.Log($"RISKAI_PROBE_PHASE phase=measurement map={BattleSession.MapForNewMatch} seed={session.Seed} unitsInitial={unitsInitial} tracked={tracked.Count} " +
                $"warmupCompletedSimSeconds={warmupCompleted:F1} warmupBlueAI=false picking=synthetic pointer=viewport-center frameSampler=unscaled_background " +
                $"blueStarts={DescribeTrackedUnits(tracked, false)}");

            int commandRound = 0, holdIssuedFrame = -1, heldObservedFrame = -1, holdTimeouts = 0;
            int submittedMoves = 0, unavailableDestinations = 0;
            long holdAppliedAtLeast = 0;
            bool waitingForStoppedFrame = false;
            float holdStartedAt = 0, nextHoldAt = 0;
            var frameSampler = new FrameSampler();
            while (Time.realtimeSinceStartup - realtimeAtStart < options.MeasurementRealtimeSeconds && session.Winner < 0)
            {
                frameSampler.Record(Time.unscaledDeltaTime);
                // This is intentionally synthetic: a fixed center pointer exercises
                // the same target scan each frame while the human controller is off.
                if (camera) RtsPicking.Target(session, camera, fixedPointer);

                float elapsed = Time.realtimeSinceStartup - realtimeAtStart;
                if (!waitingForStoppedFrame && elapsed >= nextHoldAt)
                {
                    int holdsSubmitted = SubmitHolds(session, tracked);
                    holdAppliedAtLeast = session.Commands.AppliedCount + holdsSubmitted;
                    commandRound++;
                    waitingForStoppedFrame = true;
                    holdStartedAt = elapsed;
                    holdIssuedFrame = Time.frameCount;
                    heldObservedFrame = -1;
                }
                else if (waitingForStoppedFrame)
                {
                    if (session.Commands.AppliedCount >= holdAppliedAtLeast &&
                        AllTrackedUnitsStopped(tracked) && Time.frameCount > holdIssuedFrame)
                    {
                        if (heldObservedFrame < 0) heldObservedFrame = Time.frameCount;
                        else if (Time.frameCount > heldObservedFrame)
                        {
                            SubmitMoves(session, tracked, commandRound, ref submittedMoves, ref unavailableDestinations);
                            waitingForStoppedFrame = false;
                            nextHoldAt = elapsed + CommandIntervalSeconds;
                        }
                    }
                    else if (elapsed - holdStartedAt >= HoldTimeoutSeconds)
                    {
                        // Preserve cadence if combat prevents a hold state. First-move
                        // telemetry itself marks these units ineligible rather than faking a sample.
                        holdTimeouts++;
                        SubmitMoves(session, tracked, commandRound, ref submittedMoves, ref unavailableDestinations);
                        waitingForStoppedFrame = false;
                        nextHoldAt = elapsed + CommandIntervalSeconds;
                    }
                }
                RecordDisplacement(tracked);
                // Try both destination directions before classifying a missing land
                // fixture. Do not spend a full benchmark claiming to exercise moves.
                if (commandRound >= 2 && !waitingForStoppedFrame && submittedMoves == 0 && unavailableDestinations > 0) break;
                yield return null;
            }

            RecordDisplacement(tracked);
            float simulationDelta = session.BattleTime - simulationAtStart;
            long applied = session.Commands.AppliedCount - appliedAtStart;
            long rejected = session.Commands.RejectedCount - rejectedAtStart;
            int moved = 0, survivingMovables = 0, unmovedSurvivors = 0;
            float greatestDisplacement = 0f;
            var unmoved = new StringBuilder();
            foreach (var unit in tracked)
            {
                if (unit.MaxDisplacement >= MovedDistance) moved++;
                greatestDisplacement = Mathf.Max(greatestDisplacement, unit.MaxDisplacement);
                if (!unit.CanMove) continue;
                survivingMovables++;
                if (unit.MaxDisplacement < MovedDistance)
                {
                    unmovedSurvivors++;
                    if (unmoved.Length > 0) unmoved.Append(',');
                    unmoved.Append(unit.EntityId);
                }
            }

            bool completedMeasurement = session.Winner < 0 && simulationDelta > 30f && submittedMoves > 0;
            bool passed = completedMeasurement && simulationDelta > 30f && applied > 10 && tracked.Count > 0 &&
                moved >= 1 && survivingMovables >= 1 && unmovedSurvivors == 0;
            Finish(passed,
                $"phase=measurement valid={completedMeasurement} winner={session.Winner} map={BattleSession.MapForNewMatch} seed={session.Seed} " +
                $"warmupRequestedSimSeconds={options.WarmupSimulationSeconds:F1} warmupCompletedSimSeconds={warmupCompleted:F1} " +
                $"measurementRequestedRealSeconds={options.MeasurementRealtimeSeconds:F1} simSecondsDelta={simulationDelta:F2} " +
                $"applied={applied} rejected={rejected} queued={session.Commands.PendingCount} unitsInitial={unitsInitial} unitsFinal={session.Units.Count} " +
                $"probeMovesSubmitted={submittedMoves} probeDestinationsUnavailable={unavailableDestinations} " +
                $"movedUnits={moved}/{tracked.Count} survivingMovables={survivingMovables} unmovedSurvivors={unmovedSurvivors} unmovedIds={(unmoved.Length > 0 ? unmoved.ToString() : "none")} " +
                $"holdRounds={commandRound} holdTimeouts={holdTimeouts} maxDisplacement={greatestDisplacement:F2} {frameSampler.Describe()} blue={DescribeTrackedUnits(tracked, true)}");
        }

        static bool TryReadOptions(out ProbeOptions options, out string error)
        {
            options = default;
            error = null;
            float warmup, duration;
            if (!TryReadNonNegativeFloat(WarmupFlag, 0, out warmup, out error)) return false;
            if (!TryReadNonNegativeFloat(DurationFlag, DefaultDurationSeconds, out duration, out error)) return false;
            options.WarmupSimulationSeconds = warmup;
            options.MeasurementRealtimeSeconds = duration;
            options.WarmupBlueCommander = HasExactFlag(WarmupCommanderFlag);
            return true;
        }

        static bool HasExactFlag(string flag)
        {
            foreach (var argument in Environment.GetCommandLineArgs())
                if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool TryReadNonNegativeFloat(string flag, float fallback, out float value, out string error)
        {
            value = fallback;
            error = null;
            var arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                string raw = arguments[i];
                string text = null;
                if (string.Equals(raw, flag, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= arguments.Length) { error = flag + "-missing-value"; return false; }
                    text = arguments[++i];
                }
                else if (raw.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase)) text = raw.Substring(flag.Length + 1);
                if (text == null) continue;
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                    float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                {
                    error = flag + "-invalid-value";
                    return false;
                }
            }
            return true;
        }

        static List<TrackedUnit> SpawnPlayerArchers(BattleSession session)
        {
            var result = new List<TrackedUnit>(6);
            var directions = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            var ownedTowns = new List<Settlement>();
            for(int player=0;player<session.PlayerCount;player++)
            {
                ownedTowns.Clear();
                foreach(var town in session.Towns)if(town&&town.State.Owner==player)ownedTowns.Add(town);
                for(int index=0;index<6&&ownedTowns.Count>0;index++)
                {
                    var town=ownedTowns[index%ownedTowns.Count];
                    if(!TryFindLandNavMeshPoint(town.Rally,out var spawn))continue;
                    var soldier=session.Spawn(player,UnitKind.Archer,spawn);
                    if(!soldier||soldier.IsGarrison)continue;
                    if(player==0)result.Add(new TrackedUnit{Soldier=soldier,EntityId=soldier.EntityId,Start=soldier.transform.position,Direction=directions[index%directions.Length]});
                }
            }
            return result;
        }

        static int SubmitHolds(BattleSession session, List<TrackedUnit> tracked)
        {
            int accepted = 0;
            foreach (var unit in tracked)
                if (unit.CanMove &&
                    session.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Hold)))
                    accepted++;
            return accepted;
        }

        static bool AllTrackedUnitsStopped(List<TrackedUnit> tracked)
        {
            bool found = false;
            foreach (var unit in tracked)
            {
                var soldier = unit.Soldier;
                if (!unit.CanMove) continue;
                found = true;
                var agent = soldier.Agent;
                if (!agent || !agent.enabled || agent.pathPending || agent.hasPath || agent.velocity.sqrMagnitude >= .0025f) return false;
            }
            return found;
        }

        static void SubmitMoves(BattleSession session, List<TrackedUnit> tracked, int round, ref int submitted, ref int unavailable)
        {
            float sign = (round & 1) == 0 ? 1f : -1f;
            foreach (var unit in tracked)
            {
                if (!unit.CanMove) continue;
                var desired = unit.Start + unit.Direction * (sign * DestinationDistance);
                if (!TryFindLandNavMeshPoint(desired, out var destination)) { unavailable++; continue; }
                if (session.Commands.Submit(new UnitCommand(0, unit.EntityId, UnitCommandKind.Move, destination.x, destination.y, destination.z))) submitted++;
            }
        }

        static bool TryFindLandNavMeshPoint(Vector3 requested, out Vector3 point)
        {
            if (NavMesh.SamplePosition(requested, out var hit, 4f, NavMesh.AllAreas) && MapLayout.IsLand(hit.position.x, hit.position.z))
            {
                point = hit.position;
                return true;
            }
            point = default;
            return false;
        }

        static void RecordDisplacement(List<TrackedUnit> tracked)
        {
            foreach (var unit in tracked)
            {
                if (!unit.CanMove) continue;
                var delta = unit.Soldier.transform.position - unit.Start;
                delta.y = 0;
                unit.MaxDisplacement = Mathf.Max(unit.MaxDisplacement, delta.magnitude);
            }
        }

        static string DescribeTrackedUnits(List<TrackedUnit> tracked, bool includeDisplacement)
        {
            if (tracked.Count == 0) return "none";
            var text = new StringBuilder();
            foreach (var unit in tracked)
            {
                if (text.Length > 0) text.Append(';');
                text.Append(unit.EntityId).Append('@').Append(unit.Start.x.ToString("F1")).Append(',').Append(unit.Start.z.ToString("F1"));
                if (includeDisplacement) text.Append(':').Append(unit.MaxDisplacement.ToString("F2"));
            }
            return text.ToString();
        }

        static void Finish(bool passed, string details)
        {
            string result = "RISKAI_PROBE_RESULT " + details + " success=" + passed;
            if (passed) Debug.Log(result); else Debug.LogError(result);
            Application.Quit(passed ? 0 : 1);
        }
    }
}
