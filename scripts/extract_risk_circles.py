"""Extract and coordinate-match B00R city circles from war3map.doo.

This intentionally supports only the local Saran Reforged v3 variant that was
verified during the v0.13 audit: W3do version 8/subversion 11, 4925 records,
54 bytes per fixed record, and eight zero trailing bytes.  A changed header,
record length, variable item section, or trailing payload fails loudly instead
of guessing at offsets.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import statistics
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_INPUT = ROOT / "references/maps/reforged-v3-source/war3map.doo"
DEFAULT_CITIES = ROOT / "data/derived/risk-reforged-v3-placements.json"
DEFAULT_OUTPUT = ROOT / "data/derived/risk-reforged-v3-circles.json"

HEADER_BYTES = 16
RECORD_BYTES = 54
TRAILING_BYTES = 8
SUPPORTED_VERSION = (8, 11)
CIRCLE_ID = "B00R"
CITY_IDS = {"h00N", "h00O"}
NATIVE_UNITS_PER_UNITY_METRE = 50.0


def source_label(path: Path) -> str:
    """Return a portable repository-relative path when possible."""
    try:
        return path.resolve().relative_to(ROOT.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def read_i32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<i", data, offset)[0]


def read_f32(data: bytes, offset: int) -> float:
    return struct.unpack_from("<f", data, offset)[0]


def parse_doo(path: Path) -> tuple[dict, list[dict]]:
    data = path.read_bytes()
    if len(data) < HEADER_BYTES:
        raise ValueError(f"truncated war3map.doo header: {len(data)} bytes")
    if data[:4] != b"W3do":
        raise ValueError("unsupported .doo: missing W3do magic")

    version, subversion, record_count = struct.unpack_from("<iii", data, 4)
    if (version, subversion) != SUPPORTED_VERSION:
        raise ValueError(f"unsupported .doo version {version}/{subversion}; expected 8/11")
    if record_count < 0:
        raise ValueError(f"invalid negative .doo record count {record_count}")

    payload_bytes = record_count * RECORD_BYTES
    expected_length = HEADER_BYTES + payload_bytes + TRAILING_BYTES
    if len(data) != expected_length:
        raise ValueError(
            "unsupported .doo record layout: "
            f"header={HEADER_BYTES}, count={record_count}, expected fixed payload="
            f"{expected_length} bytes, actual={len(data)}"
        )
    trailing = data[HEADER_BYTES + payload_bytes :]
    if trailing != b"\x00" * TRAILING_BYTES:
        raise ValueError("unsupported .doo trailing payload: expected eight zero bytes")

    records: list[dict] = []
    for index in range(record_count):
        offset = HEADER_BYTES + index * RECORD_BYTES
        end = offset + RECORD_BYTES
        raw_id = data[offset : offset + 4].decode("latin-1")
        variation = read_i32(data, offset + 4)
        x, y, z, angle, sx, sy, sz = struct.unpack_from("<7f", data, offset + 8)
        if not all(math.isfinite(value) for value in (x, y, z, angle, sx, sy, sz)):
            raise ValueError(f"non-finite placement at record {index} (offset {offset})")
        skin_id = data[offset + 36 : offset + 40].decode("latin-1")
        flags = data[offset + 40]
        life_percent = data[offset + 41]
        item_table = read_i32(data, offset + 42)
        item_set_count = read_i32(data, offset + 46)
        creation_number = read_i32(data, offset + 50)
        if item_set_count != 0:
            raise ValueError(
                f"unsupported variable item section at record {index} (offset {offset}): "
                f"item_set_count={item_set_count}"
            )
        if end > len(data):
            raise ValueError(f"truncated .doo record {index} at offset {offset}")
        records.append(
            {
                "source_record_index": index,
                "source_byte_offset": offset,
                "raw_id": raw_id,
                "variation": variation,
                "x": x,
                "y": y,
                "z": z,
                "angle_rad": angle,
                "scale": [sx, sy, sz],
                "skin_id": skin_id,
                "flags": flags,
                "life_percent": life_percent,
                "item_table": item_table,
                "item_set_count": item_set_count,
                "creation_number": creation_number,
            }
        )

    return (
        {
            "magic": "W3do",
            "version": version,
            "subversion": subversion,
            "header_bytes": HEADER_BYTES,
            "record_count": record_count,
            "record_size_bytes": RECORD_BYTES,
            "payload_bytes": payload_bytes,
            "trailing_bytes": TRAILING_BYTES,
            "trailing_zero_verified": True,
        },
        records,
    )


def load_cities(path: Path) -> tuple[dict, list[dict]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    placements = payload.get("placements")
    if not isinstance(placements, list):
        raise ValueError("city JSON has no placements list")
    cities = [row for row in placements if row.get("raw_id") in CITY_IDS and row.get("city_index") is not None]
    if len(cities) != 212:
        raise ValueError(f"expected 212 city rows in city JSON, found {len(cities)}")
    city_indices = [row["city_index"] for row in cities]
    if len(set(city_indices)) != len(city_indices):
        raise ValueError("city JSON has duplicate city_index values")
    for row in cities:
        for field in ("x", "y", "z"):
            if not isinstance(row.get(field), (int, float)) or not math.isfinite(row[field]):
                raise ValueError(f"city {row.get('city_index')} has invalid numeric {field}")
    return payload, sorted(cities, key=lambda row: row["city_index"])


def match_circles(circles: list[dict], cities: list[dict]) -> list[dict]:
    if len(circles) != len(cities):
        raise ValueError(f"circle/city count mismatch: {len(circles)} B00R vs {len(cities)} cities")
    used: set[int] = set()
    output: list[dict] = []
    for circle in circles:
        candidates = sorted(
            [
                (math.hypot(circle["x"] - city["x"], circle["y"] - city["y"]), city)
                for city in cities
            ],
            key=lambda item: item[0],
        )
        distance_xy, city = candidates[0]
        # JASS associates a city with its circle inside a 512-native-unit search.
        # A unique nearest point alone would also accept a wildly wrong dataset.
        if distance_xy > 512:
            raise ValueError(f"circle record {circle['source_record_index']} has no city within 512 native units")
        if len(candidates) > 1 and candidates[1][0] <= 512:
            raise ValueError(f"circle record {circle['source_record_index']} has multiple cities within the source search radius")
        if len(candidates) > 1 and abs(candidates[1][0] - distance_xy) <= 1e-6:
            raise ValueError(
                f"ambiguous nearest city for B00R record {circle['source_record_index']}: "
                f"distances {distance_xy} and {candidates[1][0]}"
            )
        city_index = city["city_index"]
        if city_index in used:
            raise ValueError(
                f"duplicate coordinate association: B00R record {circle['source_record_index']} "
                f"also maps to city_index {city_index}"
            )
        used.add(city_index)
        dx = circle["x"] - city["x"]
        dy = circle["y"] - city["y"]
        dz = circle["z"] - city["z"]
        distance_3d = math.sqrt(dx * dx + dy * dy + dz * dz)
        output.append(
            {
                "source_record_index": circle["source_record_index"],
                "source_byte_offset": circle["source_byte_offset"],
                "raw_id": circle["raw_id"],
                "skin_id": circle["skin_id"],
                "variation": circle["variation"],
                "flags": circle["flags"],
                "life_percent": circle["life_percent"],
                "creation_number": circle["creation_number"],
                "x": circle["x"],
                "y": circle["y"],
                "z": circle["z"],
                "city_index": city_index,
                "city_raw_id": city["raw_id"],
                "city_creation_number": city.get("creation_number"),
                "city_x": city["x"],
                "city_y": city["y"],
                "city_z": city["z"],
                "offset_x": dx,
                "offset_y": dy,
                "offset_z": dz,
                "distance_xy_native": distance_xy,
                "distance_3d_native": distance_3d,
                "distance_xy_unity_m": distance_xy / NATIVE_UNITS_PER_UNITY_METRE,
            }
        )
    if len(used) != len(cities):
        raise ValueError(f"association did not cover every city: {len(used)} of {len(cities)}")
    return output


def build_output(input_path: Path, cities_path: Path) -> dict:
    fmt, records = parse_doo(input_path)
    city_payload, cities = load_cities(cities_path)
    circles = [row for row in records if row["raw_id"] == CIRCLE_ID]
    if len(circles) != 212:
        raise ValueError(f"expected 212 {CIRCLE_ID} records, found {len(circles)}")
    placements = match_circles(circles, cities)
    distances = [row["distance_xy_native"] for row in placements]
    return {
        "generated_by": "scripts/extract_risk_circles.py",
        "source": source_label(input_path),
        "source_sha256": hashlib.sha256(input_path.read_bytes()).hexdigest(),
        "city_source": source_label(cities_path),
        "city_source_sha256": hashlib.sha256(cities_path.read_bytes()).hexdigest(),
        "city_source_format": city_payload.get("format"),
        "format": fmt,
        "circle_id": CIRCLE_ID,
        "circle_count": len(placements),
        "city_count": len(cities),
        "association": {
            "method": "unique nearest city center by native Warcraft III XY Euclidean distance",
            "coordinate_space": "native Warcraft III map units",
            "duplicate_city_matches": 0,
            "ambiguous_nearest_matches": 0,
            "native_units_per_unity_metre": NATIVE_UNITS_PER_UNITY_METRE,
        },
        "metrics": {
            "distance_xy_native_min": min(distances),
            "distance_xy_native_median": statistics.median(distances),
            "distance_xy_native_mean": statistics.mean(distances),
            "distance_xy_native_max": max(distances),
            "distance_xy_unity_m_min": min(distances) / NATIVE_UNITS_PER_UNITY_METRE,
            "distance_xy_unity_m_median": statistics.median(distances) / NATIVE_UNITS_PER_UNITY_METRE,
            "distance_xy_unity_m_mean": statistics.mean(distances) / NATIVE_UNITS_PER_UNITY_METRE,
            "distance_xy_unity_m_max": max(distances) / NATIVE_UNITS_PER_UNITY_METRE,
        },
        "placements": placements,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", nargs="?", type=Path, default=DEFAULT_INPUT)
    parser.add_argument("--cities", type=Path, default=DEFAULT_CITIES)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    payload = build_output(args.input, args.cities)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(
        f"parsed {payload['format']['record_count']} .doo records, "
        f"matched {payload['circle_count']} {CIRCLE_ID} circles to unique cities; "
        f"wrote {source_label(args.output)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
