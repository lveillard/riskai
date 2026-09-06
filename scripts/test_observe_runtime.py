import contextlib
import io
import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from observe_runtime import MAX_PARTIAL_BYTES, LogFollower, main, parse_complete_lines, parse_line


DIAG = (
    b"RuntimeDiagnostics 30s avgMs=8.38 maxMs=12.50 managedHeapDeltaB=1024 gcGen0=1 "
    b"units=212 simTime=10.0 simTicks=200 commandsPending=0 commandsApplied=3 commandsRejected=0\n"
)


class ObserveRuntimeTests(unittest.TestCase):
    def test_partial_diagnostic_waits_for_newline(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "live.log"
            path.write_bytes(DIAG[:-1])
            follower = LogFollower(path, start_at_end=False)
            events, truncated, capped = follower.read_new()
            self.assertEqual(events, [])
            self.assertFalse(truncated)
            self.assertFalse(capped)
            with path.open("ab") as handle:
                handle.write(b"\n")
            events, _, _ = follower.read_new()
            self.assertEqual([event.kind for event in events], ["diagnostic"])

    def test_truncation_resets_offset_and_reads_new_complete_event(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "live.log"
            path.write_bytes(DIAG)
            follower = LogFollower(path, start_at_end=False)
            first, _, _ = follower.read_new()
            self.assertEqual(len(first), 1)
            path.write_text("RISKAI_ORDER_REJECTED: id=4 kind=Move tick=20 reason=off mesh\n", encoding="utf-8")
            second, truncated, capped = follower.read_new()
            self.assertTrue(truncated)
            self.assertFalse(capped)
            self.assertEqual([event.kind for event in second], ["order_rejected"])

    def test_complete_line_parser_retains_trailing_fragment(self):
        events, partial = parse_complete_lines(DIAG + b"RISKAI_ROUTE_BLOCKED: id=7")
        self.assertEqual([event.kind for event in events], ["diagnostic"])
        self.assertEqual(partial, b"RISKAI_ROUTE_BLOCKED: id=7")

    def test_oversized_partial_is_discarded_until_its_newline(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "live.log"
            path.write_bytes(b"x" * (MAX_PARTIAL_BYTES + 1))
            follower = LogFollower(path, start_at_end=False)
            events, _, capped = follower.read_new()
            self.assertEqual(events, [])
            self.assertFalse(capped)
            self.assertTrue(follower.partial_discarded)
            self.assertTrue(follower.discarding_partial)
            self.assertEqual(follower.partial, b"")

            with path.open("ab") as handle:
                handle.write(b"\n" + DIAG)
            events, _, _ = follower.read_new()
            self.assertEqual([event.kind for event in events], ["diagnostic"])
            self.assertFalse(follower.discarding_partial)

    def test_replacement_larger_than_current_offset_is_detected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "live.log"
            path.write_bytes(DIAG)
            follower = LogFollower(path, start_at_end=False)
            first, _, _ = follower.read_new()
            self.assertEqual([event.kind for event in first], ["diagnostic"])

            replacement = Path(directory) / "rotated.log"
            replacement.write_bytes(b"ignored replacement prefix\n" + DIAG)
            replacement.replace(path)
            events, replaced, capped = follower.read_new()
            self.assertTrue(replaced)
            self.assertFalse(capped)
            self.assertEqual([event.kind for event in events], ["diagnostic"])

    def test_diagnostic_keeps_appended_key_value_fields(self):
        event = parse_line(
            "RuntimeDiagnostics 30s units=212 extraFlag=yes avgMs=8.38 maxMs=12.50 "
            "managedHeapDeltaB=1024 gcGen0=1 simTime=10.0 simTicks=200 "
            "commandsPending=0 commandsApplied=3 commandsRejected=0 worldMs=4.25 paths=17"
        )
        self.assertIsNotNone(event)
        self.assertEqual(event.kind, "diagnostic")
        self.assertEqual(event.detail["worldMs"], "4.25")
        self.assertEqual(event.detail["paths"], "17")
        self.assertEqual(event.detail["extraFlag"], "yes")

    def test_duration_rejects_nan_and_infinity(self):
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                main(["--follow", "--duration", "nan"])
            with self.assertRaises(SystemExit):
                main(["--follow", "--duration", "inf"])


if __name__ == "__main__":
    unittest.main()
