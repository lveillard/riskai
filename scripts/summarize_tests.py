"""Summarize one or more Unity NUnit result files: python scripts/summarize_tests.py TestResults/x*.xml"""
import sys, glob, xml.etree.ElementTree as ET
files = [f for a in sys.argv[1:] for f in glob.glob(a)]
total = passed = failed = skipped = 0
fails = []
for f in files:
    r = ET.parse(f).getroot()
    total += int(r.get("total")); passed += int(r.get("passed")); failed += int(r.get("failed")); skipped += int(r.get("skipped"))
    for c in r.iter("test-case"):
        if c.get("result") == "Failed":
            m = c.find("failure/message")
            fails.append((c.get("fullname"), ((m.text or "") if m is not None else "").strip().splitlines()[0][:200] if m is not None and m.text else ""))
print(f"{len(files)} files: total {total}, passed {passed}, failed {failed}, skipped {skipped}")
for name, msg in fails: print(f"- {name}\n    {msg}")
sys.exit(1 if failed else 0)
