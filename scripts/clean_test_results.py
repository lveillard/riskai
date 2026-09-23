"""Delete stale Unity result reports from TestResults/.

Removes TestResults/*.xml files older than --days (default 14), always keeping
the canonical editmode.xml and playmode.xml. Dry run by default; pass --apply
to delete. Usage: python scripts/clean_test_results.py [--days N] [--apply]
"""
import argparse
import sys
import time
from pathlib import Path

RESULTS = Path(__file__).resolve().parents[1] / "TestResults"
KEEP = {"editmode.xml", "playmode.xml"}


def stale_reports(directory, days, now=None):
    now = time.time() if now is None else now
    cutoff = now - days * 86400
    if not directory.is_dir():
        return []
    return sorted(p for p in directory.glob("*.xml")
                  if p.is_file() and p.name.lower() not in KEEP and p.stat().st_mtime < cutoff)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--days", type=float, default=14, help="delete reports older than this many days (default 14)")
    parser.add_argument("--apply", action="store_true", help="actually delete; without it only lists")
    parser.add_argument("--directory", type=Path, default=RESULTS, help=argparse.SUPPRESS)
    parser.add_argument("--quiet", action="store_true", help="print only the summary line")
    args = parser.parse_args(argv)
    if args.days < 0:
        parser.error("--days must be >= 0")
    stale = stale_reports(args.directory, args.days)
    total = sum(p.stat().st_size for p in stale)
    for path in stale:
        if not args.quiet:
            print(("delete " if args.apply else "would delete ") + path.name)
        if args.apply:
            path.unlink()
    verb = "Deleted" if args.apply else "Dry run: would delete"
    print(f"{verb} {len(stale)} report(s), {total / 1024:.0f} KiB older than {args.days:g} days "
          f"in {args.directory} (kept {', '.join(sorted(KEEP))}).")
    if stale and not args.apply:
        print("Re-run with --apply to delete.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
