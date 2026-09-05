"""Extract placed units/buildings from a Warcraft III war3mapUnits.doo file.

The Reforged v8/subversion 11 record is fixed-size for this map (115 bytes,
including the four-byte skin id).  The parser still walks all variable-length
sections so a future map with dropped items or modified abilities is handled.
Coordinates are the native Warcraft III map x/y values; angles are radians.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_INPUT = ROOT / "references/maps/reforged-v3-source/war3mapUnits.doo"
DEFAULT_JSON = ROOT / "data/derived/risk-reforged-v3-placements.json"
DEFAULT_CSV = ROOT / "data/derived/risk-reforged-v3-placements.csv"
DEFAULT_JASS = ROOT / "references/maps/reforged-v3-source/war3map.j"


def source_label(path: Path) -> str:
    """Keep generated provenance portable when inputs live in this repo."""
    try:
        return path.resolve().relative_to(ROOT.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.p = 0

    def take(self, n: int) -> bytes:
        if self.p + n > len(self.data):
            raise ValueError(f"truncated .doo at offset {self.p}, need {n} bytes")
        out = self.data[self.p : self.p + n]
        self.p += n
        return out

    def unpack(self, fmt: str):
        n = struct.calcsize(fmt)
        return struct.unpack(fmt, self.take(n))

    def i32(self) -> int:
        return self.unpack("<i")[0]

    def u8(self) -> int:
        return self.unpack("<B")[0]

    def u16(self) -> int:
        return self.unpack("<H")[0]

    def text4(self) -> str:
        return self.take(4).decode("latin-1")


def parse(path: Path) -> list[dict]:
    r = Reader(path.read_bytes())
    if r.take(4) != b"W3do":
        raise ValueError("not a war3mapUnits.doo file (missing W3do)")
    version, subversion, count = r.unpack("<iii")
    if version != 8 or subversion != 11:
        raise ValueError(f"unsupported units file version {version}/{subversion}")
    rows = []
    for index in range(count):
        raw_id = r.text4()
        variation = r.i32()
        x, y, z, angle, sx, sy, sz = r.unpack("<7f")
        # Reforged maps carry a four-byte skin id before flags.  It is present
        # in every record in this source (record length is 115 bytes at zero
        # variable-length sections), so retain it for provenance.
        skin = r.text4()
        flags = r.u8()
        owner = r.i32()
        unknown = r.u16()
        hitpoints, mana, dropped_table = r.unpack("<iii")
        dropped_sets = []
        for _ in range(r.i32()):
            items = []
            for _ in range(r.i32()):
                item_id = r.text4()
                chance = r.i32()
                items.append({"id": item_id, "chance": chance})
            dropped_sets.append(items)
        gold, target_acquisition, hero_level = r.unpack("<ifi")
        hero_strength, hero_agility, hero_intelligence = r.unpack("<iii")
        inventory = []
        for _ in range(r.i32()):
            slot = r.i32()
            inventory.append({"slot": slot, "id": r.text4()})
        modified_abilities = []
        for _ in range(r.i32()):
            modified_abilities.append({"id": r.text4(), "autocast": r.i32(), "hero_level": r.i32()})
        random_flag = r.i32()
        random_data = None
        if random_flag == 0:
            random_data = {"level": list(r.take(3)), "item_class": r.u8()}
        elif random_flag == 1:
            random_data = {"group": r.i32(), "position": r.i32()}
        elif random_flag == 2:
            random_data = []
            for _ in range(r.i32()):
                random_data.append({"id": r.text4(), "chance": r.i32()})
        custom_team_color, waygate, creation_number = r.unpack("<iii")
        rows.append(
            {
                "index": index,
                "raw_id": raw_id,
                "variation": variation,
                "x": round(x, 5),
                "y": round(y, 5),
                "z": round(z, 5),
                "angle_rad": round(angle, 7),
                "scale": [round(sx, 5), round(sy, 5), round(sz, 5)],
                "skin_id": skin,
                "flags": flags,
                "owner": owner,
                "unknown": unknown,
                "hitpoints": hitpoints,
                "mana": mana,
                "dropped_table": dropped_table,
                "dropped_sets": dropped_sets,
                "gold": gold,
                "target_acquisition": round(target_acquisition, 5),
                "hero_level": hero_level,
                "hero_stats": [hero_strength, hero_agility, hero_intelligence],
                "inventory": inventory,
                "modified_abilities": modified_abilities,
                "random_flag": random_flag,
                "random_data": random_data,
                "custom_team_color": custom_team_color,
                "waygate": waygate,
                "creation_number": creation_number,
            }
        )
    if r.p != len(r.data):
        raise ValueError(f"unparsed trailing bytes: {len(r.data) - r.p}")
    return rows


def parse_city_assignments(jass_path: Path) -> tuple[list[dict], dict[int, str]]:
    """Read the authoritative city order/region grouping from Set Bases."""
    text = jass_path.read_text(encoding="utf-8", errors="replace")
    start = text.index("function Trig_Set_Bases_Actions")
    end = text.index("endfunction", start)
    body = text[start:end]
    region = 1
    country_names = {
        int(index): name
        for index, name in re.findall(r'set udg_CountryTexts\[(\d+)\]="([^"]+)"', text)
    }
    city_index = 0
    rows = []
    for line in body.splitlines():
        match = re.search(r"set udg_Cities\[udg_tempCityIt\]=gg_unit_([A-Za-z0-9]{4})_(\d+)", line)
        if match:
            city_index += 1
            rows.append({"city_index": city_index, "raw_id": match.group(1), "creation_number": int(match.group(2)), "region_id": region, "region_name": country_names.get(region, f"Region {region}")})
        elif "set udg_tempRegionIt=( udg_tempRegionIt + 1 )" in line:
            region += 1
    if city_index == 0:
        raise ValueError("no city assignments found in Trig_Set_Bases_Actions")
    return rows, country_names


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("input", nargs="?", type=Path, default=DEFAULT_INPUT)
    ap.add_argument("--jass", type=Path, default=DEFAULT_JASS)
    ap.add_argument("--json", type=Path, default=DEFAULT_JSON)
    ap.add_argument("--csv", type=Path, default=DEFAULT_CSV)
    args = ap.parse_args()
    rows = parse(args.input)
    assignments, country_names = parse_city_assignments(args.jass)
    by_key = {(row["raw_id"], row["creation_number"]): row for row in assignments}
    if len(by_key) != len(assignments):
        raise ValueError("duplicate city creation number/raw ID in JASS assignments")
    for row in rows:
        assignment = by_key.get((row["raw_id"], row["creation_number"]))
        if assignment:
            row.update(assignment)
    city_keys = {(row["raw_id"], row["creation_number"]) for row in rows if row["raw_id"] in {"h00N", "h00O"}}
    if city_keys != set(by_key):
        raise ValueError(f"JASS/.doo city key mismatch: {len(set(by_key) - city_keys)} missing, {len(city_keys - set(by_key))} extra")
    args.json.parent.mkdir(parents=True, exist_ok=True)
    args.json.write_text(json.dumps({
        "source": source_label(args.input),
        "source_sha256": hashlib.sha256(args.input.read_bytes()).hexdigest(),
        "format": "W3do v8/subversion 11",
        "city_assignment_source": source_label(args.jass),
        "count": len(rows),
        "city_count": len(assignments),
        "region_count": max(row["region_id"] for row in assignments),
        "region_names": {str(index): name for index, name in sorted(country_names.items())},
        "placements": rows,
    }, indent=2) + "\n", encoding="utf-8")
    fields = ["index", "raw_id", "skin_id", "owner", "x", "y", "z", "angle_rad", "variation", "flags", "hitpoints", "mana", "custom_team_color", "waygate", "creation_number", "city_index", "region_id", "region_name"]
    with args.csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows({k: row.get(k, "") for k in fields} for row in rows)
    print(f"parsed {len(rows)} placements and {len(assignments)} JASS city assignments; final offset matches {args.input}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
