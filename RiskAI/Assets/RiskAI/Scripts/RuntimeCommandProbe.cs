using System;
using System.Collections;
using System.Collections.Generic;
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
        const float DurationSeconds = 70f;
        const float CommandIntervalSeconds = 2f;
        const float DestinationDistance = 20f;
        const float MovedDistance = 1f;

        sealed class TrackedUnit
        {
            public Soldier Soldier;
            public Vector3 Start;
            public Vector3 Direction;
            public float MaxDisplacement;
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
            // Let the bootstrap construct the terrain, NavMesh, session, and UI first.
            yield return null;
            yield return null;

            var session = BattleSession.Current;
            if (!session)
            {
                Finish(false, "session=unavailable simSecondsDelta=0.00 applied=0 rejected=0 queued=0 movedUnits=0/0 maxDisplacement=0.00");
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

            var tracked = SpawnPlayerArchers(session);
            long appliedAtStart = session.Commands.AppliedCount;
            long rejectedAtStart = session.Commands.RejectedCount;
            float simulationAtStart = session.BattleTime;
            float realtimeAtStart = Time.realtimeSinceStartup;

            Debug.Log(
                $"RISKAI_PROBE_START cpu={SystemInfo.processorType} gpu={SystemInfo.graphicsDeviceName} " +
                $"screen={Screen.width}x{Screen.height} players={session.PlayerCount} towns={session.Towns.Count} " +
                $"units={session.Units.Count} tracked={tracked.Count} blueStarts={DescribeTrackedUnits(tracked, false)}");

            int commandRound = 0;
            float nextCommandAt = 0f;
            while (Time.realtimeSinceStartup - realtimeAtStart < DurationSeconds)
            {
                float elapsed = Time.realtimeSinceStartup - realtimeAtStart;
                if (elapsed >= nextCommandAt)
                {
                    SubmitMoves(session, tracked, commandRound++);
                    nextCommandAt += CommandIntervalSeconds;
                }

                RecordDisplacement(tracked);
                yield return null;
            }

            RecordDisplacement(tracked);
            float simulationDelta = session.BattleTime - simulationAtStart;
            long applied = session.Commands.AppliedCount - appliedAtStart;
            long rejected = session.Commands.RejectedCount - rejectedAtStart;
            int moved = 0;
            float greatestDisplacement = 0f;
            foreach (var unit in tracked)
            {
                if (unit.MaxDisplacement >= MovedDistance) moved++;
                greatestDisplacement = Mathf.Max(greatestDisplacement, unit.MaxDisplacement);
            }

            bool passed = simulationDelta > 30f && applied > 10 && moved >= 1;
            Finish(passed,
                $"simSecondsDelta={simulationDelta:F2} applied={applied} rejected={rejected} " +
                $"queued={session.Commands.PendingCount} movedUnits={moved}/{tracked.Count} " +
                $"maxDisplacement={greatestDisplacement:F2} blue={DescribeTrackedUnits(tracked, true)}");
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
                    if(player==0)result.Add(new TrackedUnit{Soldier=soldier,Start=soldier.transform.position,Direction=directions[index%directions.Length]});
                }
            }
            return result;
        }

        static void SubmitMoves(BattleSession session, List<TrackedUnit> tracked, int round)
        {
            float sign = (round & 1) == 0 ? 1f : -1f;
            foreach (var unit in tracked)
            {
                if (!unit.Soldier || !unit.Soldier.IsAlive || unit.Soldier.IsGarrison) continue;
                var desired = unit.Start + unit.Direction * (sign * DestinationDistance);
                if (!TryFindLandNavMeshPoint(desired, out var destination)) continue;
                session.Commands.Submit(new UnitCommand(0, unit.Soldier.EntityId, UnitCommandKind.Move, destination.x, destination.y, destination.z));
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
                if (!unit.Soldier) continue;
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
                int id = unit.Soldier ? unit.Soldier.EntityId : 0;
                text.Append(id).Append('@').Append(unit.Start.x.ToString("F1")).Append(',').Append(unit.Start.z.ToString("F1"));
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
