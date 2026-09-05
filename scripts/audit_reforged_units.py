#!/usr/bin/env python3
"""Audit active Warcraft III Reforged unit stats from an exploded map.

The unit object file is parsed directly instead of treating missing fields as
zero.  This matters because a custom object inherits every field it does not
override from its old object.  The report intentionally publishes raw
overrides and source offsets, rather than pretending to resolve the full game
SLK database.

Default inputs are the checked-in Reforged v3 extraction.  A JSON result is
written next to the source map unless --json is supplied.
"""

from __future__ import annotations

import argparse
import ctypes
import json
import math
import re
import struct
from dataclasses import dataclass
from pathlib import Path
from typing import Any, BinaryIO


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MAP = ROOT / "references" / "maps" / "reforged-v3-source"
INHERITED = "INHERITED"

# UnitData.slk field IDs used by the compact audit table.
STAT_FIELDS: dict[str, str] = {
    "hp": "uhpm",
    "damage_base": "ua1b",
    # Verified against War3Net's checked JASS common.j fixture:
    # NUMBER_OF_DICE is ua1d; SIDES_PER_DIE is ua1s.
    "damage_dice": "ua1d",
    "damage_sides": "ua1s",
    "cooldown": "ua1c",
    "range": "ua1r",
    "acquisition": "uacq",
    "armor": "udef",
    "attack_type": "ua1t",
    "defense_type": "udty",
    "speed": "umvs",
    "gold_cost": "ugol",
    "lumber_cost": "ulum",
}

RAWCODE_RE = re.compile(r"'([A-Za-z0-9]{4})'")
# The object label can itself contain parentheses (for example
# ``Warship B (better)``), so capture greedily through the property marker.
WTS_UNIT_COMMENT_RE = re.compile(
    r"//\s*Units:\s*([A-Za-z0-9]{4})\s+\((.*)\),\s*([^\r\n]+)$",
    re.MULTILINE,
)


def rawcode(value: bytes) -> str:
    return value.decode("ascii", errors="replace")


@dataclass
class Modification:
    field: str
    type_id: int
    value: Any
    offset: int
    sanity_check: int

    def literal(self) -> str:
        if isinstance(self.value, str):
            return json.dumps(self.value, ensure_ascii=False)
        if isinstance(self.value, float):
            if math.isfinite(self.value):
                # repr() preserves the exact float32 value after Python's
                # conversion; shortening 1.399999976158142 to 1.4 would hide
                # what was actually stored in the binary.
                return repr(self.value)
        return str(self.value)


@dataclass
class ObjectRecord:
    old_id: str
    new_id: str
    unknown: list[int]
    modifications: dict[str, Modification]
    offset: int
    table: str


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.pos = 0

    def int32(self) -> int:
        if self.pos + 4 > len(self.data):
            raise ValueError(f"truncated int32 at 0x{self.pos:x}")
        value = struct.unpack_from("<i", self.data, self.pos)[0]
        self.pos += 4
        return value

    def float32(self) -> float:
        if self.pos + 4 > len(self.data):
            raise ValueError(f"truncated float32 at 0x{self.pos:x}")
        value = struct.unpack_from("<f", self.data, self.pos)[0]
        self.pos += 4
        return value

    def tag(self) -> str:
        if self.pos + 4 > len(self.data):
            raise ValueError(f"truncated rawcode at 0x{self.pos:x}")
        value = rawcode(self.data[self.pos : self.pos + 4])
        self.pos += 4
        return value

    def cstring(self) -> str:
        end = self.data.find(b"\0", self.pos)
        if end < 0:
            raise ValueError(f"unterminated string at 0x{self.pos:x}")
        value = self.data[self.pos : end].decode("utf-8", errors="replace")
        self.pos = end + 1
        return value

    def value(self, type_id: int) -> Any:
        if type_id == 0:
            return self.int32()
        if type_id in (1, 2):
            return self.float32()
        if type_id == 3:
            return self.cstring()
        # War3Net also accepts bool/char in the shared object reader. They are
        # uncommon in .w3u, but supporting them keeps this parser bounded and
        # useful for future maps.
        if type_id == 4:
            return bool(self.data[self.pos]) if self.pos < len(self.data) else False
        if type_id == 5:
            if self.pos + 2 > len(self.data):
                raise ValueError(f"truncated char at 0x{self.pos:x}")
            value = self.data[self.pos : self.pos + 2].decode("utf-16le", errors="replace")
            self.pos += 2
            return value
        raise ValueError(f"unsupported object modification type {type_id} at 0x{self.pos:x}")


def parse_w3u(path: Path) -> tuple[int, list[ObjectRecord], list[ObjectRecord], int]:
    data = path.read_bytes()
    reader = Reader(data)
    version = reader.int32()
    if version not in (1, 2, 3):
        raise ValueError(f"unsupported w3u format version {version}")

    tables: list[list[ObjectRecord]] = []
    for table_name in ("base", "custom"):
        count = reader.int32()
        if count < 0 or count > 100000:
            raise ValueError(f"implausible {table_name} object count {count}")
        records: list[ObjectRecord] = []
        for _ in range(count):
            offset = reader.pos
            old_id, new_id = reader.tag(), reader.tag()
            unknown: list[int] = []
            if version >= 3:
                unknown_count = reader.int32()
                if unknown_count < 0 or unknown_count > 1000:
                    raise ValueError(f"implausible v3 unknown count {unknown_count} at 0x{offset:x}")
                unknown = [reader.int32() for _ in range(unknown_count)]
            mod_count = reader.int32()
            if mod_count < 0 or mod_count > 100000:
                raise ValueError(f"implausible modification count {mod_count} at 0x{offset:x}")
            modifications: dict[str, Modification] = {}
            for _ in range(mod_count):
                mod_offset = reader.pos
                field = reader.tag()
                type_id = reader.int32()
                value = reader.value(type_id)
                sanity_check = reader.int32()
                modifications[field] = Modification(field, type_id, value, mod_offset, sanity_check)
            records.append(ObjectRecord(old_id, new_id, unknown, modifications, offset, table_name))
        tables.append(records)
    if reader.pos != len(data):
        raise ValueError(f"w3u has {len(data) - reader.pos} trailing bytes after 0x{reader.pos:x}")
    return version, tables[0], tables[1], reader.pos


def parse_wts(path: Path) -> tuple[dict[int, str], dict[str, str]]:
    """Return TRIGSTR text and best-effort object display names from comments."""
    text = path.read_text(encoding="utf-8", errors="replace")
    strings: dict[int, str] = {}
    trig_re = re.compile(r"^STRING\s+(\d+)\s*$", re.MULTILINE)
    for match in trig_re.finditer(text):
        body_start = text.find("{", match.end())
        if body_start < 0:
            continue
        body_end = text.find("}", body_start)
        if body_end < 0:
            continue
        body = text[body_start + 1 : body_end].strip("\r\n")
        strings[int(match.group(1))] = body

    names: dict[str, str] = {}
    for match in WTS_UNIT_COMMENT_RE.finditer(text):
        # Prefer the explicit Name entry; later comments for Tip/Ubertip must
        # not overwrite it. The fallback is still useful for units whose Name
        # text is inherited in the object data.
        unit_id, candidate, property_name = match.group(1), match.group(2), match.group(3)
        if unit_id not in names:
            names[unit_id] = candidate
        if property_name.strip() == "Name (Name)":
            names[unit_id] = candidate
    return strings, names


def resolve_text(value: Any, strings: dict[int, str]) -> Any:
    if not isinstance(value, str):
        return value
    match = re.fullmatch(r"TRIGSTR_(\d+)", value)
    return strings.get(int(match.group(1)), value) if match else value


def parse_jass(path: Path) -> dict[str, Any]:
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    literal_refs: dict[str, list[int]] = {}
    for number, line in enumerate(lines, 1):
        for match in RAWCODE_RE.finditer(line):
            literal_refs.setdefault(match.group(1), []).append(number)

    # Training/shop configuration is encoded in the utra modification on
    # h00N (Military Base), h00O (Shipyard), h004 (Capital), and h007 (Ruined
    # Capital). Parse the actual JASS availability calls as an independent
    # source of active unit IDs, preserving every source line.
    available_true: dict[str, list[int]] = {}
    available_false: dict[str, list[int]] = {}
    available_re = re.compile(
        r"SetPlayerUnitAvailableBJ\s*\(\s*'([A-Za-z0-9]{4})'\s*,\s*(true|false)\s*,",
        re.IGNORECASE,
    )
    for number, line in enumerate(lines, 1):
        match = available_re.search(line)
        if not match:
            continue
        target = available_true if match.group(2).lower() == "true" else available_false
        target.setdefault(match.group(1), []).append(number)

    assignments: dict[str, list[dict[str, Any]]] = {}
    assignment_re = re.compile(
        r"set\s+udg_(StartingDefenderNormal|StartingDefenderShipyard|RecruitmentSpawnUnitType)\s*=\s*'([A-Za-z0-9]{4})'",
        re.IGNORECASE,
    )
    for number, line in enumerate(lines, 1):
        match = assignment_re.search(line)
        if match:
            assignments.setdefault(match.group(1), []).append({"raw_id": match.group(2), "line": number})

    # Keep literal occurrences useful for source references while avoiding a
    # massive report of city instance declarations.
    return {
        "line_count": len(lines),
        "literal_refs": literal_refs,
        "available_true": available_true,
        "available_false": available_false,
        "assignments": assignments,
    }


def read_archive_file(archive_path: Path, member: str) -> tuple[bytes | None, str | None]:
    """Read one unextracted MPQ member in memory using the checked-in StormLib."""
    if not archive_path.exists():
        return None, None
    dll_path = ROOT / ".tools" / "stormlib" / "x64" / "StormLib.dll"
    if not dll_path.exists() or not hasattr(ctypes, "WinDLL"):
        return None, None
    try:
        storm = ctypes.WinDLL(str(dll_path))
        Handle = ctypes.c_void_p
        storm.SFileOpenArchive.argtypes = [ctypes.c_wchar_p, ctypes.c_uint, ctypes.c_uint, ctypes.POINTER(Handle)]
        storm.SFileOpenArchive.restype = ctypes.c_bool
        storm.SFileOpenFileEx.argtypes = [Handle, ctypes.c_char_p, ctypes.c_uint, ctypes.POINTER(Handle)]
        storm.SFileOpenFileEx.restype = ctypes.c_bool
        storm.SFileGetFileSize.argtypes = [Handle, ctypes.POINTER(ctypes.c_uint)]
        storm.SFileGetFileSize.restype = ctypes.c_uint
        storm.SFileReadFile.argtypes = [Handle, ctypes.c_void_p, ctypes.c_uint, ctypes.POINTER(ctypes.c_uint), ctypes.c_void_p]
        storm.SFileReadFile.restype = ctypes.c_bool
        storm.SFileCloseFile.argtypes = [Handle]
        storm.SFileCloseArchive.argtypes = [Handle]
        storm.SFileCloseArchive.restype = ctypes.c_bool
        archive = Handle()
        if not storm.SFileOpenArchive(str(archive_path), 0, 0x100, ctypes.byref(archive)):
            return None, None
        try:
            file_handle = Handle()
            if not storm.SFileOpenFileEx(archive, member.encode("ascii"), 0, ctypes.byref(file_handle)):
                return None, None
            try:
                high = ctypes.c_uint()
                size = storm.SFileGetFileSize(file_handle, ctypes.byref(high))
                buffer = ctypes.create_string_buffer(size)
                read = ctypes.c_uint()
                if not storm.SFileReadFile(file_handle, buffer, size, ctypes.byref(read), None):
                    return None, None
                return buffer.raw[: read.value], f"{archive_path.relative_to(ROOT).as_posix()}::{member}"
            finally:
                storm.SFileCloseFile(file_handle)
        finally:
            storm.SFileCloseArchive(archive)
    except (OSError, AttributeError, ValueError):
        return None, None


def parse_misc(map_dir: Path) -> dict[str, Any]:
    """Capture map-level damage matrix literals from war3mapMisc.txt."""
    extracted = map_dir / "war3mapMisc.txt"
    if extracted.exists():
        payload, source = extracted.read_bytes(), extracted.relative_to(ROOT).as_posix()
    else:
        archive = ROOT / "references" / "maps" / "Risk_Reforged_v3.0_by_Saran_OPEN_SOURCE.w3x"
        payload, source = read_archive_file(archive, "war3mapMisc.txt")
    if payload is None:
        return {"status": "unavailable", "damage_bonus": {}}
    values: dict[str, Any] = {}
    for raw_line in payload.decode("utf-8", errors="replace").splitlines():
        if "=" not in raw_line:
            continue
        key, raw_value = raw_line.split("=", 1)
        key, raw_value = key.strip(), raw_value.strip()
        if not key.startswith("DamageBonus"):
            continue
        parts = [part.strip() for part in raw_value.split(",")]
        try:
            parsed: Any = [float(part) for part in parts]
        except ValueError:
            parsed = parts
        values[key] = {"literal": raw_value, "value": parsed, "source": source}
    return {"status": "parsed", "damage_bonus": values, "source": source}


def source_ref(path: Path, offset: int) -> str:
    try:
        display_path = path.resolve().relative_to(ROOT.resolve()).as_posix()
    except ValueError:
        display_path = path.as_posix()
    return f"{display_path}@0x{offset:x}"


def compact_stats(record: ObjectRecord, w3u_path: Path, strings: dict[int, str]) -> tuple[dict[str, Any], dict[str, Any]]:
    stats: dict[str, Any] = {}
    overrides: dict[str, Any] = {}
    for label, field in STAT_FIELDS.items():
        mod = record.modifications.get(field)
        if mod is None:
            stats[label] = INHERITED
            continue
        stats[label] = resolve_text(mod.value, strings)
        overrides[label] = {
            "field": field,
            "type": mod.type_id,
            "literal": mod.literal(),
            "value": resolve_text(mod.value, strings),
            "raw_value": mod.value,
            "resolved_literal": Modification(mod.field, mod.type_id, resolve_text(mod.value, strings), mod.offset, mod.sanity_check).literal(),
            "source": source_ref(w3u_path, mod.offset),
        }
    return stats, overrides


def config_source_refs(config: dict[str, Any], raw_id: str) -> list[str]:
    return [f"war3map.j:{line}" for line in config["literal_refs"].get(raw_id, [])]


def build_result(map_dir: Path) -> dict[str, Any]:
    w3u_path = map_dir / "war3map.w3u"
    wts_path = map_dir / "war3map.wts"
    jass_path = map_dir / "war3map.j"
    version, base, custom, parsed_bytes = parse_w3u(w3u_path)
    strings, names = parse_wts(wts_path)
    config = parse_jass(jass_path)

    custom_by_id = {record.new_id: record for record in custom}
    def effective_id(record: ObjectRecord) -> str:
        return record.old_id if record.new_id == "\0\0\0\0" else record.new_id

    all_by_id = {effective_id(record): record for record in (*base, *custom)}

    shops: dict[str, list[str]] = {}
    shop_sources: dict[str, str] = {}
    for record in custom:
        train = record.modifications.get("utra")
        if train and isinstance(train.value, str):
            units = [part.strip() for part in train.value.split(",") if re.fullmatch(r"[A-Za-z0-9]{4}", part.strip())]
            if units:
                shops[record.new_id] = units
                shop_sources[record.new_id] = source_ref(w3u_path, train.offset) + " utra=" + train.literal()

    # These are the production buildings actually placed/used by the JASS
    # flow. Their utra lists define the playable active unit set. Availability
    # calls are retained as source evidence and add any explicitly enabled
    # mode-specific units.
    production_buildings = [unit for unit in ("h00N", "h00O", "h004", "h007", "h01D") if unit in shops]
    playable_ids = set(unit for building in production_buildings for unit in shops[building])
    playable_ids.update(config["available_true"])
    playable_ids.intersection_update(custom_by_id)

    # Defense structures are evidenced by their object names/tooltips and by
    # the map's tech-limit call. The starting defender assignments are kept
    # separate from structures.
    tower_ids: list[str] = []
    for unit in ("h00K", "o000"):
        if unit in all_by_id:
            tower_ids.append(unit)
    support_structure_ids = [unit for unit in ("h01G",) if unit in all_by_id]
    defender_ids = sorted(
        {
            assignment["raw_id"]
            for key, values in config["assignments"].items()
            if key.lower() in {"startingdefendernormal", "startingdefendershipyard"}
            for assignment in values
            if assignment["raw_id"] in all_by_id
        }
    )

    def row(raw_id: str, role: str, shop_sources: list[str] | None = None) -> dict[str, Any]:
        record = custom_by_id.get(raw_id) or all_by_id.get(raw_id)
        if record is None:
            return {"raw_id": raw_id, "name": names.get(raw_id, raw_id), "role": role, "missing_object_data": True}
        stats, overrides = compact_stats(record, w3u_path, strings)
        return {
            "raw_id": raw_id,
            "name": names.get(raw_id, raw_id),
            "role": role,
            "old_id": record.old_id,
            "stats": stats,
            "overrides": overrides,
            "jass_refs": config_source_refs(config, raw_id),
            "shop_sources": shop_sources or [],
        }

    playable_rows = [row(raw_id, "playable_active", [shop for shop in production_buildings if raw_id in shops[shop]]) for raw_id in sorted(playable_ids)]
    tower_rows = [row(raw_id, "tower", []) for raw_id in tower_ids]
    support_rows = [row(raw_id, "support_structure", []) for raw_id in support_structure_ids]
    defender_rows = [row(raw_id, "defender", []) for raw_id in defender_ids]

    return {
        "source": {
            "w3u": str(w3u_path.relative_to(ROOT)).replace("\\", "/"),
            "wts": str(wts_path.relative_to(ROOT)).replace("\\", "/"),
            "jass": str(jass_path.relative_to(ROOT)).replace("\\", "/"),
            "w3u_format_version": version,
            "w3u_bytes_parsed": parsed_bytes,
            "base_object_count": len(base),
            "custom_object_count": len(custom),
            "format_reference": "https://github.com/Drake53/War3Net/blob/master/src/War3Net.Build.Core/Serialization/Binary/Object/SimpleObjectModification.cs",
        },
        "jass_config": {
            "production_buildings": production_buildings,
            "shops": shops,
            "shop_sources": shop_sources,
            "set_player_unit_available_true": config["available_true"],
            "set_player_unit_available_false": config["available_false"],
            "assignments": config["assignments"],
        },
        "misc_overrides": parse_misc(map_dir),
        "stat_field_references": {
            "damage_dice": {
                "field": "ua1d",
                "meaning": "UNIT_WEAPON_IF_ATTACK_DAMAGE_NUMBER_OF_DICE",
                "source": "https://github.com/Drake53/War3Net/blob/master/tests/War3Net.TestTools.UnitTesting/TestData/Jass/1.32.9/common.j#L1973",
            },
            "damage_sides": {
                "field": "ua1s",
                "meaning": "UNIT_WEAPON_IF_ATTACK_DAMAGE_SIDES_PER_DIE",
                "source": "https://github.com/Drake53/War3Net/blob/master/tests/War3Net.TestTools.UnitTesting/TestData/Jass/1.32.9/common.j#L1975",
            },
            "defense_type": {
                "field": "udty",
                "meaning": "UNIT_IF_DEFENSE_TYPE",
                "order": "0 Light, 1 Medium, 2 Large, 3 Fortified, 4 Normal, 5 Hero, 6 Divine, 7 None",
                "source": "https://github.com/Drake53/War3Net/blob/master/tests/War3Net.TestTools.UnitTesting/TestData/Jass/1.32.9/common.j#L2023-L2030",
            },
        },
        "tower_raw_ids": tower_ids,
        "support_structure_raw_ids": support_structure_ids,
        "defender_raw_ids": defender_ids,
        "spawn_raw_ids": sorted({assignment["raw_id"] for key, values in config["assignments"].items() if key.lower() == "recruitmentspawnunittype" for assignment in values}),
        "playable_active_unit_ids": sorted(playable_ids),
        "units": playable_rows + tower_rows + support_rows + defender_rows,
    }


def md_cell(value: Any) -> str:
    if isinstance(value, list):
        return ", ".join(str(item) for item in value) if value else ""
    if isinstance(value, dict):
        return json.dumps(value, ensure_ascii=False, separators=(",", ":"))
    return str(value)


def render_markdown(result: dict[str, Any]) -> str:
    source = result["source"]
    lines = [
        "# Reforged v3 unit and tower stats audit",
        "",
        "This report is generated by `scripts/audit_reforged_units.py` from the extracted `war3map.w3u`, `war3map.wts`, and `war3map.j`. It reports only compact combat/economy fields requested for the active JASS production flow. A missing field is written as `INHERITED`; it is never silently converted to zero.",
        "",
        f"The parser consumed format v{source['w3u_format_version']} through byte {source['w3u_bytes_parsed']} of {source['w3u_bytes_parsed']} ({source['base_object_count']} base objects, {source['custom_object_count']} custom objects). The v3 layout follows [War3Net's primary reader]({source['format_reference']}): old/new rawcodes, an unknown-count integer block, modification count, then field/type/value/sanity-check records.",
        "",
        "## JASS production configuration",
        "",
        f"Production buildings with `utra` lists: {', '.join(result['jass_config']['production_buildings'])}.",
        "",
        "| Shop raw ID | Trained raw IDs | Exact source |",
        "| --- | --- | --- |",
    ]
    for shop, units in result["jass_config"]["shops"].items():
        lines.append(f"| `{shop}` | {', '.join(f'`{unit}`' for unit in units)} | {result['jass_config']['shop_sources'].get(shop, '')} |")
    lines.extend(
        [
            "",
            "`SetPlayerUnitAvailableBJ` true/false calls are preserved in the JSON output with exact JASS line numbers. Starting assignments are also preserved: `StartingDefenderNormal`, `StartingDefenderShipyard`, and `RecruitmentSpawnUnitType`.",
            "",
            "The active ID set is the union of these shop lists and explicit `true` availability calls; therefore it includes the builder (`h01C`) and mode-specific entries such as `h00V`, even where an ID is not in a primary military shop list.",
            "",
            f"Tower raw IDs: {', '.join(f'`{x}` ({result['units'][next(i for i,u in enumerate(result['units']) if u['raw_id']==x)]['name']})' for x in result['tower_raw_ids']) or 'none'}.",
            f"Tower support structure raw IDs: {', '.join(f'`{x}` ({result['units'][next(i for i,u in enumerate(result['units']) if u['raw_id']==x)]['name']})' for x in result.get('support_structure_raw_ids', [])) or 'none'}.",
            f"Defender raw IDs: {', '.join(f'`{x}` ({result['units'][next(i for i,u in enumerate(result['units']) if u['raw_id']==x)]['name']})' for x in result['defender_raw_ids']) or 'none'}.",
            "",
            "## Active playable units",
            "",
            "Columns are raw override values. `damage_dice` is `ua1d` (number of dice) and `damage_sides` is `ua1s` (sides per die), verified against War3Net's checked JASS `common.j` fixture at lines 1973 and 1975. `defense_type` is `udty`; its verified enum order is `0 Light, 1 Medium, 2 Large, 3 Fortified, 4 Normal, 5 Hero, 6 Divine, 7 None` (the same fixture, lines 2023-2030). Costs are gold/lumber. `override source` lists exact binary field records; JASS references are shown in the JSON artifact.",
            "",
            "| Raw ID | Name | HP | Base | Dice | Sides | Cooldown | Range | Acquire | Armor | Attack | Defense | Speed | Gold | Lumber | Shop(s) | Override source |",
            "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | --- | --- |",
        ]
    )
    fields = ["hp", "damage_base", "damage_dice", "damage_sides", "cooldown", "range", "acquisition", "armor", "attack_type", "defense_type", "speed", "gold_cost", "lumber_cost"]
    for unit in result["units"]:
        if unit["role"] != "playable_active":
            continue
        stats = unit.get("stats", {})
        override_refs = [item["source"] + " " + item["field"] + "=" + item["literal"] for item in unit.get("overrides", {}).values()]
        lines.append("| " + " | ".join(
            [f"`{unit['raw_id']}`", unit["name"]] + [md_cell(stats.get(field, INHERITED)) for field in fields] + [", ".join(f"`{s}`" for s in unit.get("shop_sources", [])), "<br>".join(override_refs)]
        ) + " |")
    lines.extend(
        [
            "",
            "## Map-level damage matrix overrides",
            "",
            "The MPQ also contains `war3mapMisc.txt`; these are the exact eight-value literals from each `DamageBonus*` row. The tuple order is preserved from the file and is not silently relabeled by this unit-field audit.",
            "",
            "| Key | Exact literal | Source |",
            "| --- | --- | --- |",
        ]
    )
    for key, item in result.get("misc_overrides", {}).get("damage_bonus", {}).items():
        lines.append(f"| `{key}` | `{item['literal']}` | `{item['source']}` |")
    if not result.get("misc_overrides", {}).get("damage_bonus"):
        lines.append("| (unavailable) | `war3mapMisc.txt` was not readable | — |")
    lines.extend(
        [
            "",
            "## Tower and defender stat rows",
            "",
            "The same compact fields are available for tower/defender rows in the JSON artifact, including inherited markers and exact binary offsets. This avoids duplicating a second wide table while keeping every requested raw ID auditable.",
            "",
            "| Role | Raw ID | Name | HP | Base | Dice | Sides | Cooldown | Range | Acquire | Armor | Attack | Defense | Speed | Gold | Lumber |",
            "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: |",
        ]
    )
    for unit in result["units"]:
        if unit["role"] == "playable_active":
            continue
        stats = unit.get("stats", {})
        lines.append("| " + " | ".join([unit["role"], f"`{unit['raw_id']}`", unit["name"]] + [md_cell(stats.get(field, INHERITED)) for field in fields]) + " |")
    lines.extend(
        [
            "",
            "## Re-run",
            "",
            "```text",
            "python scripts/audit_reforged_units.py",
            "```",
            "",
            "The adjacent JSON file contains all parsed objects selected by the JASS flow, every requested field's type/literal/value/source offset, availability call line numbers, and assignment references.",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map-dir", type=Path, default=DEFAULT_MAP, help="exploded map directory")
    parser.add_argument("--json", type=Path, help="JSON output path")
    parser.add_argument("--report", type=Path, help="Markdown report path")
    args = parser.parse_args()
    result = build_result(args.map_dir)
    json_path = args.json or (args.map_dir / "reforged-unit-stats.json")
    report_path = args.report or (ROOT / "docs" / "REFORGED-UNIT-STATS.md")
    json_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    json_path.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    report_path.write_text(render_markdown(result), encoding="utf-8")
    print(f"parsed v{result['source']['w3u_format_version']}: {result['source']['base_object_count']} base, {result['source']['custom_object_count']} custom; {len(result['playable_active_unit_ids'])} active playable IDs")
    print(f"wrote {report_path}")
    print(f"wrote {json_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
