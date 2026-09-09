import copy
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from measure_navigation_ab import compare_runs, latency_summary, measurement_window, records, run_player


class NavigationComparisonTests(unittest.TestCase):
    def fixture(self):
        return [{"scenario": "sustained", "budget": budget, "exitCode": 0, "contentionObserved": False,
                 "latencySummary": {"applyToRoute": {"observations": 13500}, "submitToVelocity": {"observations": 13500}},
                 "result": {"valid": "True", "success": "True", "navBudget": str(budget),
                            "fixtureHash": "ABCD1234", "cohort": "900", "cohortMinAlive": "900",
                            "movedUnits": "900", "rounds": "15", "probeMovesSubmitted": "13500"}}
                for budget in (500, 1000, 2000)]

    def test_matching_healthy_arms_are_comparable(self):
        self.assertTrue(compare_runs(self.fixture())["comparable"])

    def test_changed_fixture_or_order_count_invalidates_comparison(self):
        for key in ("fixtureHash", "rounds", "probeMovesSubmitted"):
            with self.subTest(key=key):
                runs = self.fixture()
                runs[1]["result"][key] = "different"
                self.assertFalse(compare_runs(runs)["comparable"])

    def test_invalid_population_motion_budget_and_success_are_rejected(self):
        for key, value in (("cohortMinAlive", "799"), ("movedUnits", "799"),
                           ("navBudget", "500"), ("valid", "False"), ("success", "False")):
            with self.subTest(key=key):
                runs = self.fixture()
                runs[1]["result"][key] = value
                self.assertFalse(compare_runs(runs)["comparable"])

    def test_contention_and_failed_exit_are_not_clean_evidence(self):
        for key, value in (("contentionObserved", True), ("exitCode", 1)):
            runs = self.fixture()
            runs[1][key] = value
            self.assertFalse(compare_runs(runs)["comparable"])

    def test_advanced_run_does_not_claim_sustained_combat(self):
        runs = self.fixture()
        advanced = copy.deepcopy(runs[0]); advanced["scenario"] = "advanced"
        runs.append(advanced)
        comparison = compare_runs(runs)
        self.assertTrue(comparison["comparable"])
        self.assertFalse(comparison["combatSustained800Validated"])
        self.assertFalse(comparison["advancedValidated"])

    def test_advanced_requires_full_clean_window_and_command_success(self):
        advanced = {"scenario": "advanced", "budget": 500, "exitCode": 0, "contentionObserved": False,
                    "latencySummary": {"applyToRoute": {"observations": 60}, "submitToVelocity": {"observations": 60}},
                    "result": {"valid": "True", "success": "True", "navBudget": "500",
                               "warmupCompletedSimSeconds": "900.1", "measurementElapsedRealSeconds": "90.01",
                               "rejected": "0", "queued": "0"}}
        self.assertTrue(compare_runs([advanced])["advancedValidated"])
        self.assertFalse(compare_runs([advanced])["comparable"])
        for key, value in (("warmupCompletedSimSeconds", "899"), ("measurementElapsedRealSeconds", "31"),
                           ("rejected", "1"), ("queued", "1"), ("navBudget", "1000"), ("success", "False")):
            with self.subTest(key=key):
                failed = copy.deepcopy(advanced); failed["result"][key] = value
                self.assertFalse(compare_runs([failed])["advancedValidated"])
        advanced["contentionObserved"] = True
        self.assertFalse(compare_runs([advanced])["advancedValidated"])

    def test_displacement_without_observed_route_and_velocity_is_not_latency_validation(self):
        for key in ("applyToRoute", "submitToVelocity"):
            runs = self.fixture()
            runs[1]["latencySummary"][key]["observations"] = 0
            self.assertFalse(compare_runs(runs)["comparable"])
        runs = self.fixture(); del runs[1]["latencySummary"]
        self.assertFalse(compare_runs(runs)["comparable"])

    def test_summary_weights_observations_and_ignores_empty_partial_window(self):
        rows = [{"speedHumanObservedCount": "100", "submitSpeedHumanActiveAvgMs": "10", "submitSpeedHumanActiveMaxMs": "20"},
                {"speedHumanObservedCount": "10", "submitSpeedHumanActiveAvgMs": "100", "submitSpeedHumanActiveMaxMs": "200"},
                {"speedHumanObservedCount": "0", "submitSpeedHumanActiveAvgMs": "999", "submitSpeedHumanActiveMaxMs": "999"}]
        summary = latency_summary(rows, {})
        self.assertEqual(summary["submitToVelocity"], {"observations": 110, "averageMs": 18.182, "maximumMs": 200})
        self.assertIsNone(summary["applyToRoute"]["averageMs"])

    def test_preflight_refusal_persists_environment_without_starting_player(self):
        sample = {"timestamp": 123, "freeGiB": 1, "competingProcesses": [{"name": "rustc.exe", "pid": 10}]}
        with tempfile.TemporaryDirectory() as temporary:
            args = SimpleNamespace(output=Path(temporary), player=Path(temporary) / "RiskAI.exe",
                                   seed=160212, seconds=90, minimum_free_gib=6)
            with patch("measure_navigation_ab.environment_sample", return_value=sample), patch("measure_navigation_ab.subprocess.Popen") as launch:
                with self.assertRaisesRegex(RuntimeError, "Measurement not started"):
                    run_player(args, 500, "sustained")
                launch.assert_not_called()
            saved = json.loads((args.output / "sustained-500-environment.json").read_text())
            self.assertEqual(saved, [sample])

    def test_missing_or_single_arm_does_not_claim_comparison(self):
        self.assertFalse(compare_runs([])["comparable"])
        self.assertFalse(compare_runs(self.fixture()[:1])["comparable"])
        runs = self.fixture(); runs[1]["result"] = {}
        self.assertFalse(compare_runs(runs)["comparable"])

    def test_log_records_retain_stages_and_exclude_other_lines(self):
        raw = "RISKAI_STARTUP phase=terrain phaseMs=100.25\nRuntimeDiagnostics 30s applyRouteReadyHumanActiveMaxMs=321.5 units=950\n"
        self.assertEqual(records(raw, "RISKAI_STARTUP "), [{"phase": "terrain", "phaseMs": "100.25"}])
        self.assertEqual(records(raw, "RuntimeDiagnostics 30s ")[0]["applyRouteReadyHumanActiveMaxMs"], "321.5")

    def test_advanced_latency_excludes_warmup_and_post_result_windows(self):
        raw = "\n".join(["RISKAI_PROBE_PHASE phase=warmup",
                         "RuntimeDiagnostics 30s applyRouteReadyHumanActiveMaxMs=9999",
                         "RISKAI_PROBE_PHASE phase=measurement warmupCompletedSimSeconds=900",
                         "RuntimeDiagnostics 30s applyRouteReadyHumanActiveMaxMs=123",
                         "RISKAI_PROBE_RESULT phase=measurement success=True",
                         "RuntimeDiagnostics 30s applyRouteReadyHumanActiveMaxMs=8888"])
        self.assertEqual(records(measurement_window(raw), "RuntimeDiagnostics 30s "),
                         [{"applyRouteReadyHumanActiveMaxMs": "123"}])
        self.assertEqual(measurement_window("RuntimeDiagnostics 30s applyRouteReadyHumanActiveMaxMs=9999"), "")


if __name__ == "__main__":
    unittest.main()
