import copy
import unittest

from measure_navigation_ab import compare_runs, records


class NavigationComparisonTests(unittest.TestCase):
    def fixture(self):
        return [{"scenario": "sustained", "budget": budget, "exitCode": 0, "contentionObserved": False,
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

    def test_missing_or_single_arm_does_not_claim_comparison(self):
        self.assertFalse(compare_runs([])["comparable"])
        self.assertFalse(compare_runs(self.fixture()[:1])["comparable"])
        runs = self.fixture(); runs[1]["result"] = {}
        self.assertFalse(compare_runs(runs)["comparable"])

    def test_log_records_retain_stages_and_exclude_other_lines(self):
        raw = "RISKAI_STARTUP phase=terrain phaseMs=100.25\nRuntimeDiagnostics 30s applyRouteReadyHumanActiveMaxMs=321.5 units=950\n"
        self.assertEqual(records(raw, "RISKAI_STARTUP "), [{"phase": "terrain", "phaseMs": "100.25"}])
        self.assertEqual(records(raw, "RuntimeDiagnostics 30s ")[0]["applyRouteReadyHumanActiveMaxMs"], "321.5")


if __name__ == "__main__":
    unittest.main()
