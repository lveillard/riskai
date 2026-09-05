"""Extract runtime unit placements from a protected World map's JASS.

Some released Warcraft III maps keep their placement table in JASS while
shipping a nearly empty war3mapUnits.doo.  This intentionally records only
literal BlzCreateUnitWithSkin calls; dynamically created gameplay units are
outside the static placement inventory.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_INPUT = ROOT / "references/maps/risk-new-world-v3-source/war3map.j"
DEFAULT_JSON = ROOT / "data/derived/risk-new-world-v3-jass-placements.json"
DEFAULT_CSV = ROOT / "data/derived/risk-new-world-v3-jass-placements.csv"

CALL_RE = re.compile(
    r"(?:gg_unit_([A-Za-z0-9]{4})_(\d+)\s*=)?\s*BlzCreateUnitWithSkin\(p,\s*'([A-Za-z0-9]{4})',\s*"
    r"([-+]?\d+(?:\.\d+)?),\s*([-+]?\d+(?:\.\d+)?),\s*"
    r"([-+]?\d+(?:\.\d+)?),\s*'([A-Za-z0-9]{4})'\)",
)
COUNTRY_RE = re.compile(r'set\s+udg_CountryTexts\[(\d+)\]\s*=\s*"([^"]*)"')
CITY_RE = re.compile(r"set\s+udg_Cities\[udg_tempCityIt\]=gg_unit_([A-Za-z0-9]{4})_(\d+)")
REGION_INC_RE = re.compile(r"set\s+udg_tempRegionIt=\(udg_tempRegionIt\+1\)")


def source_label(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def parse_city_assignments(text: str, countries: dict[str, str]) -> list[dict]:
    start = text.index("function Trig_Set_Bases_Actions")
    end = text.index("endfunction", start)
    body = text[start:end]
    event_re = re.compile(
        r"set\s+udg_tempRegionIt=\(udg_tempRegionIt\+1\)|"
        r"set\s+udg_Cities\[udg_tempCityIt\]=gg_unit_([A-Za-z0-9]{4})_(\d+)"
    )
    region = 1
    city_index = 0
    rows = []
    for event in event_re.finditer(body):
        if event.group(0).startswith("set udg_tempRegionIt"):
            region += 1
            continue
        city_index += 1
        raw_id, number = event.group(1), event.group(2)
        rows.append({
            "city_index": city_index,
            "raw_id": raw_id,
            "creation_number": int(number),
            "region_id": region,
            "region_name": countries.get(str(region), f"Region {region}"),
        })
    return rows


def parse(path: Path) -> tuple[list[dict], dict[str, str], list[dict]]:
    text = path.read_text(encoding="utf-8", errors="replace")
    rows = []
    for index, match in enumerate(CALL_RE.finditer(text)):
        lhs_id, lhs_number, raw_id, x, y, angle, skin = match.groups()
        rows.append({
            "index": index,
            "raw_id": raw_id,
            "skin_id": skin,
            "x": float(x),
            "y": float(y),
            "angle_deg": float(angle),
            "creation_number": int(lhs_number) if lhs_id == raw_id and lhs_number else None,
            "source_offset": match.start(),
        })
    countries = {index: name for index, name in COUNTRY_RE.findall(text)}
    assignments = parse_city_assignments(text, countries)
    by_key = {(row["raw_id"], row["creation_number"]): row for row in assignments}
    for row in rows:
        assignment = by_key.get((row["raw_id"], row["creation_number"]))
        if assignment:
            row.update(assignment)
    return rows, countries, assignments


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("input", nargs="?", type=Path, default=DEFAULT_INPUT)
    ap.add_argument("--json", type=Path, default=DEFAULT_JSON)
    ap.add_argument("--csv", type=Path, default=DEFAULT_CSV)
    args = ap.parse_args()
    rows, countries, assignments = parse(args.input)
    counts = Counter(row["raw_id"] for row in rows)
    payload = {
        "source": source_label(args.input),
        "source_sha256": hashlib.sha256(args.input.read_bytes()).hexdigest(),
        "format": "JASS literal BlzCreateUnitWithSkin placements",
        "placement_count": len(rows),
        "raw_id_counts": dict(sorted(counts.items())),
        "country_texts": {str(index): name for index, name in sorted(countries.items())},
        "city_assignment_count": len(assignments),
        "city_assignments": assignments,
        "static_doo_note": "Risk-New World v3.0 war3mapUnits.doo is W3do v7/subversion 9 with one sloc record; this JASS inventory is the recoverable static placement source.",
        "placements": rows,
    }
    args.json.parent.mkdir(parents=True, exist_ok=True)
    args.json.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    fields = ["index", "raw_id", "skin_id", "x", "y", "angle_deg", "creation_number", "city_index", "region_id", "region_name", "source_offset"]
    with args.csv.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)
    print(f"parsed {len(rows)} literal JASS placements; raw IDs: {dict(sorted(counts.items()))}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
