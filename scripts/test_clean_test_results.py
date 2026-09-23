import os
from pathlib import Path
import tempfile
import time
import unittest

from clean_test_results import main, stale_reports


class CleanTestResultsTests(unittest.TestCase):
    def test_keeps_canonical_and_recent_reports(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            old = time.time() - 30 * 86400
            for name in ("editmode.xml", "playmode.xml", "old-run.xml", "recent.xml", "notes.txt"):
                (root / name).write_text("<test-run/>", encoding="utf-8")
            for name in ("editmode.xml", "playmode.xml", "old-run.xml", "notes.txt"):
                os.utime(root / name, (old, old))
            self.assertEqual([p.name for p in stale_reports(root, 14)], ["old-run.xml"])
            self.assertEqual(main(["--directory", directory, "--days", "14", "--quiet"]), 0)
            self.assertTrue((root / "old-run.xml").exists(), "dry run must not delete")
            self.assertEqual(main(["--directory", directory, "--days", "14", "--apply", "--quiet"]), 0)
            self.assertEqual(sorted(p.name for p in root.iterdir()),
                             ["editmode.xml", "notes.txt", "playmode.xml", "recent.xml"])

    def test_missing_directory_is_empty(self):
        self.assertEqual(stale_reports(Path(tempfile.gettempdir()) / "riskai-no-such-results", 1), [])


if __name__ == "__main__":
    unittest.main()
