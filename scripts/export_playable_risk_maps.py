"""Export reproducible, numeric Unity map resources from the local WC3 maps.

The released .w3x archives remain research inputs under references/maps (which
is ignored).  This exporter reads their extracted W3E/JASS/DOO members and
emits only numeric terrain, city, claim-circle, recruitment-spawn, camera-bound,
and static-tree data.

W3E version 11 uses 128 native units per cell.  The coordinate conversion here
keeps Warcraft x as Unity x and Warcraft y as Unity z, scales by 50, and shifts
the source grid center to Unity (0, 0).  The common encoded sea surface is
normalized to Unity height -0.24, as required by the runtime water plane.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import struct
import uuid
from collections import Counter, defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "RiskAI/Assets/RiskAI/Resources/Maps"
NATIVE_PER_UNITY = 50.0
NATIVE_CELL_SIZE = 128.0
UNITY_WATER_LEVEL = -0.24
# Water.slk's default water zero is -0.7 terrain levels * 128 native units.
WATER_ZERO_NATIVE = -89.6
WATER_FLAG = 0x40
BOUNDARY_FLAG = 0x4000

# `war3map.w3b` in the Europe source identifies these custom rawcodes as
# variants of Warcraft's destructible tree bases.  `B00Q` is intentionally
# absent despite inheriting a tree base: one is placed at every country spawn
# centre and it is a gameplay marker rather than landscape vegetation. B00R is
# the city claim ring; B00T is New World's one-off special marker.
TREE_SPECIES = {
    "B000": "BTtw", "B001": "BTtw", "B002": "ATtc", "B003": "BTtc",
    "B004": "BTtc", "B005": "ATtc", "B006": "BTtw", "B007": "BTtw",
    "B008": "BTtw", "B009": "WTst", "B00A": "VTlt", "B00B": "ATtc",
    "B00C": "ATtc", "B00D": "ATtc", "B00F": "BTtc", "B00G": "BTtc",
    "B00H": "BTtc", "B00I": "WTst", "B00J": "WTst", "B00K": "WTst",
    "B00L": "VTlt", "B00M": "VTlt", "B00N": "VTlt", "B00O": "WTst",
    "B00P": "VTlt",
    # Native rawcodes are retained only where the map JASS/tree tables use a
    # tree destructible. Other native DOO props (ice, rocks, lamps, etc.) are
    # deliberately not guessed to be vegetation.
    "YTfc": "YTfc", "YTpc": "YTpc", "YTfb": "YTfb", "YTpb": "YTpb",
    "WTst": "WTst",
}


MAPS = (
    {
        "map_id": "Europe",
        "name": "Risk Reforged Europe",
        "source_dir": ROOT / "references/maps/reforged-v3-source",
        "archive": ROOT / "references/maps/Risk_Reforged_v3.0_by_Saran_OPEN_SOURCE.w3x",
        "placements": ROOT / "data/derived/risk-reforged-v3-placements.json",
        "expected_cities": 212,
        "expected_countries": 69,
    },
    {
        "map_id": "NewWorld",
        "name": "Risk New World",
        "source_dir": ROOT / "references/maps/risk-new-world-v3-source",
        "archive": ROOT / "references/maps/Risk-New-World-v3.0.w3x",
        "placements": ROOT / "data/derived/risk-new-world-v3-jass-placements.json",
        "expected_cities": 293,
        "expected_countries": 100,
    },
)


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def number(text: str) -> float:
    """JASS permits a space between a sign and its literal."""
    return float(re.sub(r"\s+", "", text))


def w3e(path: Path) -> dict:
    data = path.read_bytes()
    if data[:4] != b"W3E!":
        raise ValueError(f"{path}: missing W3E! magic")
    p = 4
    version = struct.unpack_from("<i", data, p)[0]
    p += 4
    if version != 11:
        raise ValueError(f"{path}: W3E version {version}; exporter supports verified version 11")
    tileset = chr(data[p])
    p += 1
    custom_tileset = struct.unpack_from("<i", data, p)[0]
    p += 4
    ground_count = struct.unpack_from("<i", data, p)[0]
    p += 4
    ground_tiles = [data[p + i * 4:p + i * 4 + 4].decode("latin-1") for i in range(ground_count)]
    p += ground_count * 4
    cliff_count = struct.unpack_from("<i", data, p)[0]
    p += 4
    cliff_tiles = [data[p + i * 4:p + i * 4 + 4].decode("latin-1") for i in range(cliff_count)]
    p += cliff_count * 4
    width, height = struct.unpack_from("<ii", data, p)
    p += 8
    source_origin_x, source_origin_y = struct.unpack_from("<ff", data, p)
    p += 8
    count = width * height
    if width < 2 or height < 2 or len(data) != p + count * 7:
        raise ValueError(f"{path}: invalid {width}x{height} W3E payload length")

    ground_native: list[float] = []
    water_native: list[float] = []
    land: list[int] = []
    tiles: list[int] = []
    water_raw_values: list[int] = []
    for i in range(count):
        ground_raw, water_raw_with_boundary, flags, variation, cliff = struct.unpack_from("<HHBBB", data, p + i * 7)
        water_raw = water_raw_with_boundary & 0x3FFF
        # WC3's lower cliff nibble has baseline 2; every cliff layer is 512
        # encoded quarters = 128 native world units.
        ground = ((ground_raw - 8192) + ((cliff & 0x0F) - 2) * 512) / 4.0
        water = (water_raw - 8192) / 4.0 + WATER_ZERO_NATIVE
        water_enabled = bool(flags & WATER_FLAG)
        # An enabled water field does not cover ground that is higher than its
        # local water surface.  Equality remains water to avoid opening a
        # zero-depth visual water tile to ground navigation.
        is_land = not water_enabled or ground > water
        ground_native.append(ground)
        water_native.append(water)
        land.append(1 if is_land else 0)
        water_raw_values.append(water_raw)
        # Runtime format: variation byte | cliff/layer byte << 8 |
        # terrain flags/texture byte << 16 | W3E water-boundary bit << 24.
        tiles.append(variation | (cliff << 8) | (flags << 16) | ((water_raw_with_boundary & BOUNDARY_FLAG) << 10))
    dominant_water_raw, dominant_count = Counter(water_raw_values).most_common(1)[0]
    sea_native = (dominant_water_raw - 8192) / 4.0 + WATER_ZERO_NATIVE
    return {
        "width": width,
        "height": height,
        "source_origin_x": source_origin_x,
        "source_origin_y": source_origin_y,
        "source_center_x": source_origin_x + (width - 1) * NATIVE_CELL_SIZE / 2.0,
        "source_center_y": source_origin_y + (height - 1) * NATIVE_CELL_SIZE / 2.0,
        "cell_size": NATIVE_CELL_SIZE / NATIVE_PER_UNITY,
        "height_samples": [(value - sea_native) / NATIVE_PER_UNITY + UNITY_WATER_LEVEL for value in ground_native],
        "water_samples": [(value - sea_native) / NATIVE_PER_UNITY + UNITY_WATER_LEVEL for value in water_native],
        "land_samples": land,
        "tile_samples": tiles,
        "source": {
            "path": rel(path), "sha256": sha256(path), "version": version, "tileset": tileset,
            "customTileset": custom_tileset, "groundTiles": ground_tiles, "cliffTiles": cliff_tiles,
            "dominantWaterRaw": dominant_water_raw, "dominantWaterCount": dominant_count,
            "dominantWaterNative": sea_native,
        },
    }


def cstring(data: bytes, offset: int, path: Path) -> tuple[str, int]:
    end = data.find(b"\0", offset)
    if end < 0:
        raise ValueError(f"{path}: unterminated string at 0x{offset:x}")
    return data[offset:end].decode("utf-8", errors="replace"), end + 1


def w3i(path: Path) -> dict:
    """Read Reforged W3I v31's authored camera rectangle.

    The rectangle is exported as a useful playable/view limit but deliberately
    does not crop W3E samples or shift existing city coordinates.  Consumers
    can tighten camera/minimap bounds without changing source-relative XY.
    """
    data = path.read_bytes()
    if len(data) < 128:
        raise ValueError(f"{path}: truncated W3I")
    version, editor_version, map_version, game_major, game_minor, game_patch, game_build = struct.unpack_from("<7i", data)
    if version != 31:
        raise ValueError(f"{path}: W3I version {version}; exporter supports verified version 31")
    p = 28
    strings = []
    for _ in range(4):
        value, p = cstring(data, p, path)
        strings.append(value)
    left, bottom, right, top, complement_left, complement_top, complement_right, complement_bottom = struct.unpack_from("<8f", data, p)
    p += 32
    boundary_complements = struct.unpack_from("<4i", data, p)
    p += 16
    playable_width, playable_height = struct.unpack_from("<2i", data, p)
    if not (left < right and bottom < top and playable_width > 0 and playable_height > 0):
        raise ValueError(f"{path}: invalid camera bounds")
    return {
        "left": left, "bottom": bottom, "right": right, "top": top,
        "source": {
            "path": rel(path), "sha256": sha256(path), "version": version,
            "editorVersion": editor_version, "mapVersion": map_version,
            "gameVersion": [game_major, game_minor, game_patch, game_build],
            "strings": strings, "complementBoundsNative": [complement_left, complement_top, complement_right, complement_bottom],
            "boundaryComplements": list(boundary_complements),
            "playableSizeCells": [playable_width, playable_height],
        },
    }


def parse_placements(path: Path, map_id: str) -> tuple[list[dict], dict[int, str], list[dict]]:
    """Use pre-extracted placement data for Europe and robust JASS extraction for World."""
    if map_id == "Europe":
        payload = json.loads(path.read_text(encoding="utf-8"))
        return payload["placements"], {int(k): v for k, v in payload["region_names"].items()}, []

    payload = json.loads(path.read_text(encoding="utf-8"))
    # The older derived JASS extractor intentionally accepted only no-space
    # negative literals, so reconstruct the full World placement list here.
    jass_path = ROOT / "references/maps/risk-new-world-v3-source/war3map.j"
    text = jass_path.read_text(encoding="utf-8", errors="replace")
    literal = r"([-+]?\s*\d+(?:\.\d+)?)"
    call = re.compile(
        rf"(?:gg_unit_([A-Za-z0-9]{{4}})_(\d+)\s*=\s*)?BlzCreateUnitWithSkin\(\s*p\s*,\s*'([A-Za-z0-9]{{4}})'\s*,\s*{literal}\s*,\s*{literal}\s*,\s*{literal}\s*,\s*'([A-Za-z0-9]{{4}})'\s*\)")
    by_key: dict[tuple[str, int], dict] = {}
    for match in call.finditer(text):
        lhs_id, lhs_number, raw_id, x, y, _angle, skin = match.groups()
        if lhs_id == raw_id and lhs_number:
            by_key[(raw_id, int(lhs_number))] = {
                "raw_id": raw_id, "skin_id": skin, "creation_number": int(lhs_number),
                "x": number(x), "y": number(y), "source_offset": match.start(),
            }
    assignments = payload["city_assignments"]
    missing = [(row["raw_id"], row["creation_number"]) for row in assignments if (row["raw_id"], row["creation_number"]) not in by_key]
    if missing:
        raise ValueError(f"{jass_path}: could not recover {len(missing)} assigned city calls; first {missing[:3]}")
    placements = []
    for assignment in assignments:
        row = dict(by_key[(assignment["raw_id"], assignment["creation_number"])])
        row.update(assignment)
        placements.append(row)
    countries = {int(k): v for k, v in payload["country_texts"].items()}
    return placements, countries, []


def parse_doodads(path: Path) -> list[dict]:
    data = path.read_bytes()
    if data[:4] != b"W3do" or len(data) < 16:
        raise ValueError(f"{path}: missing W3do header")
    version, subversion, count = struct.unpack_from("<iii", data, 4)
    expected = 16 + count * 54 + 8
    if (version, subversion) != (8, 11) or len(data) != expected or data[-8:] != b"\0" * 8:
        raise ValueError(f"{path}: unsupported doodad layout {version}/{subversion}, length {len(data)}")
    records = []
    for index in range(count):
        offset = 16 + index * 54
        raw_id = data[offset:offset + 4].decode("ascii", errors="replace")
        variation = struct.unpack_from("<i", data, offset + 4)[0]
        x, y, z, rotation, scale_x, scale_y, scale_z = struct.unpack_from("<7f", data, offset + 8)
        skin_id = data[offset + 36:offset + 40].decode("ascii", errors="replace")
        records.append({
            "id": raw_id, "variation": variation, "x": x, "y": y, "z": z,
            "rotationRadians": rotation, "scaleX": scale_x, "scaleY": scale_y, "scaleZ": scale_z,
            "skinId": skin_id, "pathingFlags": data[offset + 40], "lifePercent": data[offset + 41],
            "sourceRecord": index,
        })
    return records


def parse_circles(doodads: list[dict]) -> list[dict]:
    return [
        {"x": row["x"], "y": row["y"], "sourceRecord": row["sourceRecord"]}
        for row in doodads if row["id"] == "B00R"
    ]


def parse_trees(doodads: list[dict], terrain: dict) -> tuple[list[dict], list[str]]:
    """Convert only inspected source tree destructibles, never procedural trees."""
    used_species = sorted({row["id"] for row in doodads if row["id"] in TREE_SPECIES})
    species_index = {raw_id: index for index, raw_id in enumerate(used_species)}
    trees = []
    for row in doodads:
        raw_id = row["id"]
        if raw_id not in species_index:
            continue
        x, z = centered(row["x"], row["y"], terrain)
        trees.append({
            "x": round(x, 6), "z": round(z, 6),
            "rotationDegrees": round(math.degrees(row["rotationRadians"]), 6),
            "scaleX": round(row["scaleX"], 6), "scaleY": round(row["scaleY"], 6), "scaleZ": round(row["scaleZ"], 6),
            "species": species_index[raw_id], "variation": row["variation"], "pathingFlags": row["pathingFlags"],
            "lifePercent": row["lifePercent"], "sourceRecord": row["sourceRecord"],
        })
    # Keep strings out of every instance: index maps to rawcode/base type.
    species = [f"{raw_id}:{TREE_SPECIES[raw_id]}" for raw_id in used_species]
    return trees, species


def match_circles(cities: list[dict], circles: list[dict]) -> dict[int, dict]:
    if len(cities) != len(circles):
        raise ValueError(f"city/circle count mismatch: {len(cities)} cities versus {len(circles)} B00R doodads")
    result: dict[int, dict] = {}
    used: set[int] = set()
    for circle in circles:
        ranked = sorted(
            ((math.hypot(circle["x"] - city["x"], circle["y"] - city["y"]), city) for city in cities),
            key=lambda candidate: candidate[0],
        )
        distance, city = ranked[0]
        if distance > 512 or (len(ranked) > 1 and ranked[1][0] <= 512) or city["city_index"] in used:
            raise ValueError(f"B00R record {circle['sourceRecord']} has no unique city match within 512 native units")
        used.add(city["city_index"])
        result[city["city_index"]] = circle | {"distanceNative": distance}
    if len(result) != len(cities):
        raise ValueError("circle matching did not cover every city")
    return result


def parse_spawn_centers(jass_path: Path, country_count: int) -> dict[int, tuple[float, float]]:
    text = jass_path.read_text(encoding="utf-8", errors="replace")
    literal = r"([-+]?\s*\d+(?:\.\d+)?)"
    rect_re = re.compile(rf"set\s+(gg_rct_[A-Za-z0-9_]+)\s*=\s*Rect\(\s*{literal}\s*,\s*{literal}\s*,\s*{literal}\s*,\s*{literal}\s*\)")
    rects = {
        match.group(1): ((number(match.group(2)) + number(match.group(4))) / 2.0, (number(match.group(3)) + number(match.group(5))) / 2.0)
        for match in rect_re.finditer(text)
    }
    assignment_re = re.compile(r"set\s+udg_CountrySpawnRegions\[(\d+)\]\s*=\s*(gg_rct_[A-Za-z0-9_]+)")
    result: dict[int, tuple[float, float]] = {}
    missing_rects: list[str] = []
    for match in assignment_re.finditer(text):
        index, rect_name = int(match.group(1)), match.group(2)
        if index > country_count:
            continue
        if rect_name not in rects:
            missing_rects.append(rect_name)
        else:
            result[index] = rects[rect_name]
    missing = sorted(set(range(1, country_count + 1)) - set(result))
    if missing or missing_rects:
        raise ValueError(f"{jass_path}: missing country spawn centers {missing}; missing rects {sorted(set(missing_rects))}")
    return result


def centered(native_x: float, native_y: float, terrain: dict) -> tuple[float, float]:
    return ((native_x - terrain["source_center_x"]) / NATIVE_PER_UNITY, (native_y - terrain["source_center_y"]) / NATIVE_PER_UNITY)


def node_index(native_x: float, native_y: float, terrain: dict) -> int | None:
    ix = round((native_x - terrain["source_origin_x"]) / NATIVE_CELL_SIZE)
    iy = round((native_y - terrain["source_origin_y"]) / NATIVE_CELL_SIZE)
    if not (0 <= ix < terrain["width"] and 0 <= iy < terrain["height"]):
        return None
    return iy * terrain["width"] + ix


def validate_positions(cities: list[dict], terrain: dict) -> list[dict]:
    defects = []
    for city in cities:
        for label, native_x, native_y in (("city", city["nativeX"], city["nativeY"]), ("claim", city["claimNativeX"], city["claimNativeY"])):
            index = node_index(native_x, native_y, terrain)
            if index is None:
                defects.append({"city": city["id"], "position": label, "reason": "outside terrain grid"})
            elif not terrain["land_samples"][index]:
                defects.append({"city": city["id"], "position": label, "reason": "nearest terrain node is water"})
    return defects


def export(spec: dict) -> dict:
    source_dir: Path = spec["source_dir"]
    terrain_path = source_dir / "war3map.w3e"
    info_path = source_dir / "war3map.w3i"
    jass_path = source_dir / "war3map.j"
    doodad_path = source_dir / "war3map.doo"
    for path in (terrain_path, info_path, jass_path, doodad_path, spec["placements"]):
        if not path.exists():
            raise FileNotFoundError(f"missing required local research input: {path}")
    terrain = w3e(terrain_path)
    info = w3i(info_path)
    doodads = parse_doodads(doodad_path)
    trees, tree_species = parse_trees(doodads, terrain)
    placements, country_names, _ = parse_placements(spec["placements"], spec["map_id"])
    cities_source = [row for row in placements if row.get("city_index") is not None]
    cities_source.sort(key=lambda row: row["city_index"])
    expected = spec["expected_cities"]
    if len(cities_source) != expected or [row["city_index"] for row in cities_source] != list(range(1, expected + 1)):
        raise ValueError(f"{spec['map_id']}: expected consecutive city indices 1..{expected}, got {len(cities_source)}")
    if len(country_names) != spec["expected_countries"]:
        raise ValueError(f"{spec['map_id']}: expected {spec['expected_countries']} countries, got {len(country_names)}")
    circles = match_circles(cities_source, parse_circles(doodads))
    spawn_centers = parse_spawn_centers(jass_path, spec["expected_countries"])
    cities = []
    for row in cities_source:
        circle = circles[row["city_index"]]
        x, z = centered(row["x"], row["y"], terrain)
        claim_x, claim_z = centered(circle["x"], circle["y"], terrain)
        country = country_names[row["region_id"]]
        city = {
            "id": f"{spec['map_id'].lower()}-{row['city_index']:03d}",
            # The sources assign cities to countries but contain no individual
            # city toponyms. This stable label deliberately does not invent one.
            "name": f"{country} {row['city_index']}",
            "x": round(x, 6), "z": round(z, 6),
            "claimX": round(claim_x, 6), "claimZ": round(claim_z, 6),
            # Country is zero-based so it indexes the source-ordered countries
            # array directly.  The group display name remains in city.name.
            "country": row["region_id"] - 1, "port": row["raw_id"] == "h00O",
            "nativeX": row["x"], "nativeY": row["y"],
            "claimNativeX": circle["x"], "claimNativeY": circle["y"],
        }
        cities.append(city)
    counts = Counter(row["region_id"] for row in cities_source)
    countries = []
    for index in range(1, spec["expected_countries"] + 1):
        name = country_names[index]
        x, z = centered(*spawn_centers[index], terrain)
        countries.append({"name": name, "x": round(x, 6), "z": round(z, 6), "count": counts[index]})
    defects = validate_positions(cities, terrain)
    # Native audit fields are only used above; omit them from the runtime schema.
    for city in cities:
        for key in ("nativeX", "nativeY", "claimNativeX", "claimNativeY"):
            del city[key]
    cell_size = terrain["cell_size"]
    playable_min_x, playable_min_z = centered(info["left"], info["bottom"], terrain)
    playable_max_x, playable_max_z = centered(info["right"], info["top"], terrain)
    output = {
        "mapId": spec["map_id"], "name": spec["name"],
        "width": terrain["width"], "height": terrain["height"],
        "originX": round(-(terrain["width"] - 1) * cell_size / 2.0, 6),
        "originZ": round(-(terrain["height"] - 1) * cell_size / 2.0, 6),
        "cellSize": cell_size,
        # Authored W3I camera rectangle, transformed with the same grid centre
        # as cities. It is a limit only: keeping the samples untrimmed makes
        # existing city and country coordinates byte-for-byte stable.
        "playableMinX": round(playable_min_x, 6), "playableMaxX": round(playable_max_x, 6),
        "playableMinZ": round(playable_min_z, 6), "playableMaxZ": round(playable_max_z, 6),
        "heightSamples": terrain["height_samples"], "waterSamples": terrain["water_samples"],
        "landSamples": terrain["land_samples"], "tileSamples": terrain["tile_samples"],
        "tileNames": terrain["source"]["groundTiles"],
        "cities": cities, "countries": countries,
        "sourceTrees": trees, "treeSpecies": tree_species,
        "metadata": {
            "generatedBy": "scripts/export_playable_risk_maps.py",
            "coordinateTransform": {
                "nativeToUnity": "x=(nativeX-sourceGridCenterX)/50; z=(nativeY-sourceGridCenterY)/50",
                "sourceGridCenterNative": [terrain["source_center_x"], terrain["source_center_y"]],
                "sourceGridOriginNative": [terrain["source_origin_x"], terrain["source_origin_y"]],
                "seaNormalization": "all heights subtract dominant encoded water surface, then add -0.24 Unity metres",
            },
            "terrainDecoding": {
                "groundNative": "((groundRaw-8192)+((cliffLayer&15)-2)*512)/4",
                "waterNative": "((waterRaw&0x3fff)-8192)/4-89.6",
                "waterFlag": "terrainFlags&0x40", "landRule": "no water flag OR groundNative > waterNative",
                "tileSamples": "variation | cliffTextureLayer<<8 | terrainFlagsAndTexture<<16 | waterBoundaryBit<<24",
            },
            "sources": {
                "archive": {"path": rel(spec["archive"]), "sha256": sha256(spec["archive"])},
                "terrain": terrain["source"],
                "mapInfo": info["source"],
                "jass": {"path": rel(jass_path), "sha256": sha256(jass_path)},
                "circleDoodads": {"path": rel(doodad_path), "sha256": sha256(doodad_path), "id": "B00R", "count": len(circles)},
                "treeDoodads": {
                    "path": rel(doodad_path), "sha256": sha256(doodad_path), "count": len(trees),
                    "excludedIds": ["B00Q", "B00R", "B00T"],
                    "treeObjectTable": {"path": rel(source_dir / "war3map.w3b"), "sha256": sha256(source_dir / "war3map.w3b")} if (source_dir / "war3map.w3b").exists() else None,
                },
                "placementInventory": {"path": rel(spec["placements"]), "sha256": sha256(spec["placements"])},
            },
            "validation": {
                "cityCount": len(cities), "countryCount": len(countries), "circleCount": len(circles),
                "treeCount": len(trees), "treeTypeCounts": dict(sorted(Counter(tree_species[row["species"]].split(":", 1)[0] for row in trees).items())),
                "citySourceTypeCounts": dict(sorted(Counter(row["raw_id"] for row in cities_source).items())),
                "positionDefects": defects,
                "heightMin": min(terrain["height_samples"]), "heightMax": max(terrain["height_samples"]),
                "waterMin": min(terrain["water_samples"]), "waterMax": max(terrain["water_samples"]),
                "landNodeCount": sum(terrain["land_samples"]), "waterNodeCount": len(terrain["land_samples"]) - sum(terrain["land_samples"]),
            },
        },
    }
    return output


SLK_CELL_RE = re.compile(r'C;(?:Y(\d+);)?X(\d+);K(?:"(.*)"|(.*))$')


def slk_rows(path: Path) -> dict[str, dict[str, str]]:
    """Read the sparse, text SLK rows used for inherited UnitData fields."""
    grid: dict[int, dict[int, str]] = defaultdict(dict)
    current_row: int | None = None
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        match = SLK_CELL_RE.match(line)
        if not match:
            continue
        y_text, x_text, quoted, bare = match.groups()
        if y_text is not None:
            current_row = int(y_text)
        if current_row is None:
            continue
        grid[current_row][int(x_text)] = quoted if quoted is not None else bare
    headers = grid.get(1)
    if not headers or headers.get(1) not in {"unitID", "unitBalanceID"}:
        raise ValueError(f"{path}: missing SLK unit header")
    result = {}
    for row in grid.values():
        raw_id = row.get(1)
        if raw_id and raw_id != headers[1]:
            result[raw_id] = {headers[column]: value for column, value in row.items() if column in headers}
    return result


def parse_w3u_geometry(path: Path) -> dict[str, dict]:
    """Use the repository's complete v1–v3 object reader, narrowed to geometry."""
    # Keeping one proven object reader avoids silently desynchronising on rare
    # Warcraft object modification types while retaining source field offsets.
    from audit_reforged_units import parse_w3u
    _version, base, custom, _parsed_bytes = parse_w3u(path)
    records: dict[str, dict] = {}
    for record in (*base, *custom):
        key = record.old_id if record.new_id == "\0\0\0\0" else record.new_id
        fields = {
            field: {"value": modification.value, "offset": modification.offset}
            for field, modification in record.modifications.items()
            if field in {"ucol", "umvs", "umdl", "usca"}
        }
        records[key] = {"oldId": record.old_id, "offset": record.offset, "fields": fields}
    return records


def source_geometry() -> dict:
    """Publish source collisions plus verified inherited MDX standing extents."""
    source_dir = MAPS[0]["source_dir"]
    w3u_path = source_dir / "war3map.w3u"
    unit_data_path = ROOT / "references/owned-disc-data/RoC-Units-UnitData.slk"
    unit_balance_path = ROOT / "references/owned-disc-data/RoC-Units-UnitBalance.slk"
    mdx_geometry_path = ROOT / "data/source-mdx-geometry.json"
    objects = parse_w3u_geometry(w3u_path)
    unit_data, unit_balance = slk_rows(unit_data_path), slk_rows(unit_balance_path)
    mdx_geometry = json.loads(mdx_geometry_path.read_text(encoding="utf-8"))
    if mdx_geometry.get("nativePerUnity") != NATIVE_PER_UNITY:
        raise ValueError(f"{mdx_geometry_path}: nativePerUnity must equal {NATIVE_PER_UNITY}")
    inherited_models = mdx_geometry.get("models", {})
    entries = []
    for raw_id in ("h00B", "h00G", "h00E", "h00H", "h00N", "h00O"):
        record = objects[raw_id]
        inherited_id = record["oldId"]
        fields = record["fields"]
        collision = fields.get("ucol", {}).get("value")
        collision_source = "war3map.w3u" if collision is not None else "RoC-Units-UnitData.slk"
        if collision is None:
            collision = float(unit_data[inherited_id]["collision"])
        speed = fields.get("umvs", {}).get("value")
        speed_source = "war3map.w3u" if speed is not None else "RoC-Units-UnitBalance.slk"
        if speed is None:
            speed_text = unit_balance.get(inherited_id, {}).get("spd")
            speed = float(speed_text) if speed_text and speed_text not in {"-", "_"} else None
        inherited_model = inherited_models.get(inherited_id)
        if not inherited_model:
            raise ValueError(f"{mdx_geometry_path}: no inherited MDX metric for {raw_id}/{inherited_id}")
        is_town_base = inherited_id == "hbar"
        entries.append({
            "id": raw_id, "inherits": inherited_id,
            "collisionNative": collision, "collisionUnity": collision / NATIVE_PER_UNITY,
            "collisionSource": collision_source,
            "moveSpeedNative": speed, "moveSpeedUnity": speed / NATIVE_PER_UNITY if speed is not None else None,
            "moveSpeedSource": speed_source if speed is not None else None,
            "mapOverrides": {field: details["value"] for field, details in fields.items()},
            "modelPath": fields.get("umdl", {}).get("value", inherited_model["modelPath"]),
            "modelScale": fields.get("usca", {}).get("value", inherited_model["modelScale"]),
            "standingSequence": inherited_model["standingSequence"],
            "standingHeightNative": inherited_model["standingHeightNative"],
            "standingWidthNative": inherited_model["standingWidthNative"],
            "standingHeightUnity": inherited_model["standingHeightUnity"],
            "standingWidthUnity": inherited_model["standingWidthUnity"],
            "modelMetricsStatus": (
                "verified inherited MDX standing extents; local town compound calibration remains separate"
                if is_town_base else "verified inherited MDX standing extents"
            ),
        })
    return {
        "schemaVersion": 2, "nativePerUnity": NATIVE_PER_UNITY,
        "units": entries,
        "metadata": {
            "generatedBy": "scripts/export_playable_risk_maps.py",
            "sources": {
                "w3u": {"path": rel(w3u_path), "sha256": sha256(w3u_path)},
                "unitData": {"path": rel(unit_data_path), "sha256": sha256(unit_data_path), "format": "text SLK"},
                "unitBalance": {"path": rel(unit_balance_path), "sha256": sha256(unit_balance_path), "format": "text SLK"},
                "mdxGeometry": {"path": rel(mdx_geometry_path), "sha256": sha256(mdx_geometry_path),
                                "schemaVersion": mdx_geometry.get("schemaVersion"),
                                "provenance": mdx_geometry.get("sources")},
            },
            "limits": "ucol is Warcraft's source collision-size field, not an MDX mesh bounding box. MDX standing bounds are numeric metadata only; no source MDX is included or distributed.",
        },
    }


def write_meta(path: Path) -> None:
    guid = uuid.uuid5(uuid.NAMESPACE_URL, f"riskai/{path.name}").hex
    path.with_suffix(path.suffix + ".meta").write_text(
        f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUT)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    for spec in MAPS:
        payload = export(spec)
        target = args.output / f"{spec['map_id']}.json"
        target.write_text(json.dumps(payload, separators=(",", ":"), ensure_ascii=False, allow_nan=False), encoding="utf-8")
        write_meta(target)
        validation = payload["metadata"]["validation"]
        print(f"{target.name}: {validation['cityCount']} cities, {validation['countryCount']} countries, "
              f"{payload['width']}x{payload['height']} samples, {len(validation['positionDefects'])} terrain-position defects")
    geometry_target = args.output / "SourceGeometry.json"
    geometry_target.write_text(json.dumps(source_geometry(), separators=(",", ":"), ensure_ascii=False, allow_nan=False), encoding="utf-8")
    write_meta(geometry_target)
    print(f"{geometry_target.name}: verified source collision and movement fields")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
