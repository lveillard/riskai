#!/usr/bin/env python3
"""Resolve Saran W3U combat fields against owned classic Warcraft data.

The map declares a Reforged 2.0.2 editor version, but no matching 2.0.2 game
data is present in this repository. Consequently inherited values from the
owned TFT archive are historical candidates, never verified Reforged defaults.
"""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path
from typing import Any

from audit_reforged_units import (
    DEFAULT_MAP, ROOT, Reader, parse_w3u, parse_wts, read_archive_file, resolve_text,
)
from audit_combat_parity import slk


PRIVATE = ROOT / "references/owned-disc-data/resolver"
MAP_W3U = DEFAULT_MAP / "war3map.w3u"
MAP_JASS = DEFAULT_MAP / "war3map.j"

# Combat and transport fields required by the runtime catalog. Metadata is
# authoritative for the backing SLK/profile column. udu*, uhd*, uqd*, udp*
# and ubs* are real object field IDs (not invented slk.* IDs).
UNIT_FIELDS = [
    "uhpm", "uhpr", "uhrt", "udef", "udty", "umvs", "umvt", "utyp",
    "utar", "ucar", "udtm", "uprw", "upgr", "uacq", "uamn", "uaen",
]
for weapon in (1, 2):
    UNIT_FIELDS.extend([
        f"ua{weapon}b", f"ua{weapon}d", f"ua{weapon}s", f"ua{weapon}c",
        f"ua{weapon}r", f"ua{weapon}t", f"ua{weapon}w", f"ua{weapon}g",
        f"ua{weapon}f", f"ua{weapon}h", f"ua{weapon}q", f"ua{weapon}p",
        f"ua{weapon}m", f"ua{weapon}z", f"ubs{weapon}", f"udp{weapon}",
        f"uhd{weapon}", f"uqd{weapon}", f"udu{weapon}", f"utc{weapon}",
        f"ucs{weapon}", f"usr{weapon}", f"usd{weapon}", f"urb{weapon}",
        f"uma{weapon}", f"umh{weapon}",
    ])


def extract() -> dict[str, Any]:
    """Extract only source tables used by this audit from owned archives."""
    PRIVATE.mkdir(parents=True, exist_ok=True)
    members = [
        "UnitWeapons.slk", "UnitBalance.slk", "UnitData.slk", "UnitMetaData.slk",
        "UnitUI.slk", "UpgradeData.slk", "UpgradeMetaData.slk", "MiscData.txt",
        "AbilityData.slk", "AbilityMetaData.slk",
        "HumanUnitFunc.txt", "OrcUnitFunc.txt", "NeutralUnitFunc.txt",
        "UndeadUnitFunc.txt", "NightElfUnitFunc.txt",
        "HumanAbilityFunc.txt", "OrcAbilityFunc.txt", "NeutralAbilityFunc.txt",
        "CampaignAbilityFunc.txt", "ItemAbilityFunc.txt", "CommonAbilityFunc.txt",
    ]
    provenance: dict[str, Any] = {}
    archives = [
        ("RoC", ROOT / "references/owned-disc-data/_tmp/WAR3.MPQ"),
        ("TFTinner155", ROOT / "references/owned-disc-data/_tmp/tft-inner155.mpq"),
    ]
    for label, archive in archives:
        for member in members:
            data, source = read_archive_file(archive, "Units\\" + member)
            if data is None:
                continue
            output = PRIVATE / f"{label}-{member}"
            output.write_bytes(data)
            provenance[output.name] = {
                "source": source,
                "bytes": len(data),
                "sha256": hashlib.sha256(data).hexdigest(),
            }
    (PRIVATE / "provenance.json").write_text(
        json.dumps(provenance, indent=2) + "\n", encoding="utf-8"
    )
    return provenance


def map_info(path: Path) -> dict[str, Any]:
    reader = Reader(path.read_bytes())
    result = {
        "format_version": reader.int32(),
        "map_version": reader.int32(),
        "editor_version": reader.int32(),
    }
    if result["format_version"] >= 28:
        result["game_version"] = [reader.int32() for _ in range(4)]
    result["strings"] = [reader.cstring() for _ in range(4)]
    reader.pos += 32 + 16
    result["playable_size"] = [reader.int32(), reader.int32()]
    result["flags"] = reader.int32()
    result["tileset"] = chr(reader.data[reader.pos])
    reader.pos += 1
    result["loading_background"] = reader.int32()
    if result["format_version"] >= 25:
        result["loading_model"] = reader.cstring()
    result["loading_strings"] = [reader.cstring() for _ in range(3)]
    result["game_data_set_offset"] = hex(reader.pos)
    result["game_data_set"] = reader.int32()
    return result


def parse_profiles(prefix: str) -> dict[str, dict[str, dict[str, Any]]]:
    """Merge race UnitFunc profiles, retaining the exact source line."""
    profiles: dict[str, dict[str, dict[str, Any]]] = {}
    for path in sorted(PRIVATE.glob(f"{prefix}-*UnitFunc.txt")):
        section: str | None = None
        for number, raw_line in enumerate(path.read_text(encoding="latin1").splitlines(), 1):
            line = raw_line.strip()
            match = re.fullmatch(r"\[([^]]+)]", line)
            if match:
                section = match.group(1).strip()
                profiles.setdefault(section, {})
                continue
            if section and "=" in line and not line.startswith("//"):
                key, value = line.split("=", 1)
                profiles[section][key.strip().lower()] = {
                    "raw_value": value.strip(),
                    "source": f"{path.relative_to(ROOT).as_posix()}:{number}",
                }
    return profiles


def scalar(raw: Any) -> Any:
    if not isinstance(raw, str):
        return raw
    value = raw.strip()
    if value in {"", "-", "_"}:
        return None
    if re.fullmatch(r"[-+]?\d+", value):
        return int(value)
    try:
        return float(value)
    except ValueError:
        return value


def profile_value(item: dict[str, Any], index: int) -> tuple[Any, Any]:
    raw = item["raw_value"]
    if index >= 0:
        parts = [part.strip() for part in raw.split(",")]
        if index >= len(parts):
            return None, raw
        return scalar(parts[index]), raw
    return scalar(raw), raw


def load_unit_source(prefix: str) -> tuple[dict[str, Any], dict[str, Any]]:
    tables: dict[str, Any] = {}
    for table in ("UnitWeapons", "UnitBalance", "UnitData", "UnitUI"):
        path = PRIVATE / f"{prefix}-{table}.slk"
        rows, status = slk(path)
        if status == "not_text_slk":
            raise ValueError(f"expected text SLK: {path}")
        tables[table] = rows
    metadata_path = PRIVATE / f"{prefix}-UnitMetaData.slk"
    metadata, status = slk(metadata_path)
    if status == "not_text_slk":
        raise ValueError(f"expected text SLK: {metadata_path}")
    return tables, {"metadata": metadata, "profiles": parse_profiles(prefix)}


def source_field(
    raw_id: str,
    field_id: str,
    tables: dict[str, Any],
    support: dict[str, Any],
) -> dict[str, Any]:
    meta = support["metadata"].get(field_id)
    if not meta:
        return {"value": None, "raw_value": None, "source": None, "metadata_source": None}
    table = meta.get("slk", {}).get("value")
    column = meta.get("field", {}).get("value")
    index = int(meta.get("index", {}).get("value", -1))
    metadata_source = meta.get("field", {}).get("source")
    if table == "Profile":
        item = support["profiles"].get(raw_id, {}).get(str(column).lower())
        if not item:
            return {"value": None, "raw_value": None, "source": None,
                    "metadata_source": metadata_source, "table": table, "column": column, "index": index}
        value, raw_value = profile_value(item, index)
        return {"value": value, "raw_value": raw_value, "source": item["source"],
                "metadata_source": metadata_source, "table": table, "column": column, "index": index}
    item = tables.get(str(table), {}).get(raw_id, {}).get(column)
    if not item:
        return {"value": None, "raw_value": None, "source": None,
                "metadata_source": metadata_source, "table": table, "column": column, "index": index}
    return {"value": scalar(item["value"]), "raw_value": item["value"], "source": item["source"],
            "metadata_source": metadata_source, "table": table, "column": column, "index": index}


def parse_object_file(path: Path) -> tuple[int, list[dict[str, Any]], int]:
    """Parse leveled W3A/W3Q object modifications through EOF."""
    reader = Reader(path.read_bytes())
    version = reader.int32()
    records: list[dict[str, Any]] = []
    for table in ("base", "custom"):
        for _ in range(reader.int32()):
            offset = reader.pos
            old_id, new_id = reader.tag(), reader.tag()
            unknown = [reader.int32() for _ in range(reader.int32())] if version >= 3 else []
            modifications = []
            for _ in range(reader.int32()):
                mod_offset = reader.pos
                field, type_id = reader.tag(), reader.int32()
                level, pointer = reader.int32(), reader.int32()
                value = reader.value(type_id)
                check = reader.tag()
                modifications.append({
                    "field": field, "level": level, "pointer": pointer, "value": value,
                    "sanity_check": check,
                    "source": f"{path.relative_to(ROOT).as_posix()}@0x{mod_offset:x}",
                })
            records.append({
                "id": new_id if new_id.strip("\0") else old_id,
                "base": old_id, "table": table, "unknown": unknown,
                "source": f"{path.relative_to(ROOT).as_posix()}@0x{offset:x}",
                "modifications": modifications,
            })
    if reader.pos != len(reader.data):
        raise ValueError(f"{path.name}: {len(reader.data)-reader.pos} trailing bytes")
    return version, records, reader.pos


def parse_roster(custom: list[Any]) -> dict[str, Any]:
    shops: dict[str, list[str]] = {}
    shop_sources: dict[str, str] = {}
    for record in custom:
        train = record.modifications.get("utra")
        if train and isinstance(train.value, str):
            shops[record.new_id] = [
                x.strip() for x in train.value.split(",")
                if re.fullmatch(r"[A-Za-z0-9]{4}", x.strip())
            ]
            shop_sources[record.new_id] = f"{MAP_W3U.relative_to(ROOT).as_posix()}@0x{train.offset:x}"

    lines = MAP_JASS.read_text(encoding="utf-8").splitlines()
    calls: list[dict[str, Any]] = []
    for number, line in enumerate(lines, 1):
        match = re.search(r"SetPlayerUnitAvailableBJ\('([A-Za-z0-9]{4})', (true|false),", line)
        if match:
            calls.append({"raw_id": match.group(1), "available": match.group(2) == "true", "line": number})

    initial = {c["raw_id"] for c in calls if 5002 <= c["line"] <= 5041 and c["available"]}
    classic_removed = {c["raw_id"] for c in calls if 8171 <= c["line"] <= 8188 and not c["available"]}
    default_available = sorted(initial - classic_removed)
    recruitable = set(shops.get("h00N", [])) | set(shops.get("h00O", []))
    return {
        "defaults": {
            "unit_set": 0, "ships": 0,
            "sources": ["references/maps/reforged-v3-source/war3map.j:2824",
                        "references/maps/reforged-v3-source/war3map.j:2180",
                        "references/maps/reforged-v3-source/war3map.j:5574"],
        },
        "shops": shops, "shop_sources": shop_sources, "availability_calls": calls,
        "restart_enabled_superset": sorted(initial),
        "unit_set_0_removed_classic": sorted(classic_removed),
        "default_available": default_available,
        "default_recruitable_from_city_or_shipyard": sorted(recruitable & set(default_available)),
        "notes": [
            "h00V and h000 are enabled by JASS but absent from h00O utra, so they are not normally recruitable.",
            "n00B is only trained by h01D (Blacksmith) and is excluded from the city/shipyard combat roster.",
            "ModesUnitSet=1 and ModesShips=2 are separate branches; do not flatten all 40 referenced IDs into one roster.",
        ],
    }


def ability_evidence(
    unit_rows: list[dict[str, Any]], strings: dict[int, str], combat_ids: set[str]
) -> dict[str, Any]:
    version, records, parsed = parse_object_file(DEFAULT_MAP / "war3map.w3a")
    attached: dict[str, list[str]] = {}
    for row in unit_rows:
        if row["id"] not in combat_ids:
            continue
        abilities = row["all_map_overrides"].get("uabi", {}).get("value")
        if isinstance(abilities, str) and abilities:
            attached[row["id"]] = [x for x in abilities.split(",") if re.fullmatch(r"[A-Za-z0-9]{4}", x)]
    relevant = {ability for values in attached.values() for ability in values}
    relevant.update({"Ahea", "Aroa", "Adis", "A00W"})
    selected = []
    for record in records:
        if record["id"] not in relevant:
            continue
        copy = dict(record)
        copy["modifications"] = [
            {**mod, "value": resolve_text(mod["value"], strings)} for mod in record["modifications"]
        ]
        selected.append(copy)

    inherited: dict[str, Any] = {}
    for prefix in ("RoC", "TFTinner155"):
        ability_rows, _ = slk(PRIVATE / f"{prefix}-AbilityData.slk")
        inherited[prefix] = {}
        for ability_id in sorted(relevant):
            row = ability_rows.get(ability_id)
            if row:
                inherited[prefix][ability_id] = {
                    key: {"value": scalar(item["value"]), "raw_value": item["value"], "source": item["source"]}
                    for key, item in row.items()
                }
    return {
        "w3a_format_version": version, "w3a_bytes_parsed": parsed,
        "attached_explicitly_by_unit": attached,
        "map_records_relevant_to_attached_combat_abilities": selected,
        "historical_candidate_rows": inherited,
        "interpretation": {
            "Ahea": "Map changes valid targets only. The TFT candidate row has DataA1=20 heal, Cast1=0, Cool1=1, Cost1=4 and Rng1=250. It does not support a 15-HP heal; Reforged 2.0.2 remains unverified.",
            "Aroa": "Map explicitly sets area 700, targets, and hero duration 1.0. Other values remain historical inheritance candidates.",
            "A00W": "Quick Roar is a custom Aroa derivative with normal duration 15 seconds; it is not explicitly attached by the audited unit uabi fields.",
            "mortar": "Mortar splash is weapon data in W3U/UnitWeapons, not an ability record.",
        },
        "transport": {
            "ability_units": [raw_id for raw_id in ("n007", "n008") if "Sch3" in attached.get(raw_id, [])],
            "cargo_ability": "Sch3",
            "cargo_capacity": 10,
            "capacity_source": "references/maps/reforged-v3-source/war3map.w3a@0x395",
            "n009_has_explicit_transport_abilities": bool(attached.get("n009")),
            "interpretation": "n007/n008 explicitly attach the custom load/unload set and Sch3 sets Car1=10. n009 has no explicit uabi list, so its display name alone is not evidence of equivalent transport behavior.",
        },
    }


def upgrade_evidence(strings: dict[int, str]) -> dict[str, Any]:
    version, records, parsed = parse_object_file(DEFAULT_MAP / "war3map.w3q")
    for record in records:
        for mod in record["modifications"]:
            mod["raw_value"] = mod["value"]
            mod["value"] = resolve_text(mod["value"], strings)
    rows_by_source: dict[str, Any] = {}
    for prefix in ("RoC", "TFTinner155"):
        rows, _ = slk(PRIVATE / f"{prefix}-UpgradeData.slk")
        wanted = {record["base"] for record in records} | {record["id"] for record in records}
        rows_by_source[prefix] = {
            raw_id: {
                key: {"value": scalar(item["value"]), "raw_value": item["value"], "source": item["source"]}
                for key, item in rows[raw_id].items()
            }
            for raw_id in sorted(wanted & set(rows))
        }
    lines = MAP_JASS.read_text(encoding="utf-8").splitlines()
    calls = [
        {"line": number, "text": line.strip()}
        for number, line in enumerate(lines, 1)
        if re.search(r"SetPlayerTechResearchedSwap\('(Rhri|R00[0-4])'", line)
    ]
    return {
        "w3q_format_version": version,
        "w3q_bytes_parsed": parsed,
        "map_records": records,
        "historical_candidate_rows": rows_by_source,
        "jass_research_calls": calls,
        "interpretation": "Rhri map overrides gef1 to renw (Enable Weapon) and gba1 to 1.0, replacing stock ratr/+200. Value 1 enables attack 1. h00N/h00O list Rhri in upgr, and the default mode researches it for players and neutral, so their first weapon becomes active; +200 range must not be applied.",
    }


def capture_and_structure_evidence(objects: list[dict[str, Any]]) -> dict[str, Any]:
    by_id = {row["id"]: row for row in objects}
    def values(raw_id: str, fields: list[str]) -> dict[str, Any]:
        return {field: by_id[raw_id]["fields"][field] for field in fields}
    return {
        "city_marker_units": {
            "raw_ids": ["h00N", "h00O"],
            "fields": {raw_id: values(raw_id, ["uaen", "ua1r", "ua1b", "ua1d", "ua1s", "ua1c", "udty"])
                       for raw_id in ("h00N", "h00O")},
            "static_attack_enable": 0,
            "default_runtime_attack_enable": 1,
            "runtime_enable_sources": [
                "references/maps/reforged-v3-source/war3map.w3u@0x1202",
                "references/maps/reforged-v3-source/war3map.w3u@0x14b5",
                "references/maps/reforged-v3-source/war3map.w3q@0x1c",
                "references/maps/reforged-v3-source/war3map.w3q@0x3d",
                "references/maps/reforged-v3-source/war3map.j:7970",
                "references/maps/reforged-v3-source/war3map.j:8529",
                "references/maps/reforged-v3-source/war3map.j:8530",
            ],
            "interpretation": "Both markers statically inherit uaen=0 from hbar, but list Rhri in upgr. The map changes Rhri to renw/1 (Enable Weapon 1) and researches it for every player and neutral in the default mode, activating their explicit 650-range first weapon.",
        },
        "starting_city_defender": {
            "raw_id": "h00B",
            "jass_sources": ["references/maps/reforged-v3-source/war3map.j:5456",
                             "references/maps/reforged-v3-source/war3map.j:5457",
                             "references/maps/reforged-v3-source/war3map.j:7075"],
            "fields": values("h00B", ["uaen", "uacq", "ua1r", "ua1w", "ua1z", "ua1b", "ua1d", "ua1s"]),
            "interpretation": "The actual starting city/shipyard defender is h00B. Its range 400 and acquisition 600 are TFT historical candidates; its map-explicit base damage is 15 and the inherited weapon type is instant.",
        },
        "buildable_bunker": {
            "raw_id": "o000", "jass_source": "references/maps/reforged-v3-source/war3map.j:4170",
            "fields": values("o000", ["uaen", "uacq", "ua1r", "ua1c", "ua1b", "ua1d", "ua1s"]),
        },
        "wall": {
            "raw_id": "h00K", "fields": values("h00K", ["uaen", "ua1r", "uhpm", "udef", "udty"]),
            "interpretation": "h00K is named Wall and inherits attacks disabled; it is not an archer tower.",
        },
        "capture": {
            "city_circle_size": 155.0,
            "sources": ["references/maps/reforged-v3-source/war3map.j:5459",
                        "references/maps/reforged-v3-source/war3map.j:7126"],
            "transport_entry_exclusions": ["n007", "n008"],
            "transport_not_excluded": ["n009"],
            "transport_filter_source": "references/maps/reforged-v3-source/war3map.j:17438",
        },
        "mode_damage_mutation": {
            "sources": ["references/maps/reforged-v3-source/war3map.j:7813",
                        "references/maps/reforged-v3-source/war3map.j:7814",
                        "references/maps/reforged-v3-source/war3map.j:7815",
                        "references/maps/reforged-v3-source/war3map.j:7955",
                        "references/maps/reforged-v3-source/war3map.j:7956",
                        "references/maps/reforged-v3-source/war3map.j:7957"],
            "interpretation": "These natives pass weaponIndex=1, the zero-based second weapon. h00N/h00O have no second weapon defined; the calls do not replace their first 45+1d5 weapon.",
        },
    }


def field_confidence(map_override: bool, tft: dict[str, Any], roc: dict[str, Any]) -> str:
    if map_override:
        return "map_explicit"
    tft_value, roc_value = tft["value"], roc["value"]
    if tft_value is not None and roc_value is not None and tft_value == roc_value:
        return "classic_consensus"
    if tft_value is not None and roc_value is not None and tft_value != roc_value:
        return "classic_conflict_do_not_replace_runtime"
    if tft_value is not None:
        return "tft_only_historical_candidate"
    if roc_value is not None:
        return "classic_conflict_do_not_replace_runtime"
    return "unresolved"


def build() -> dict[str, Any]:
    provenance = extract()
    tft_tables, tft_support = load_unit_source("TFTinner155")
    roc_tables, roc_support = load_unit_source("RoC")
    # RoC metadata predates several editor declarations even though the RoC
    # tables already contain those columns. Use TFT metadata only as a schema
    # fallback; candidate values and their sources still come from RoC.
    roc_support["metadata"] = {**tft_support["metadata"], **roc_support["metadata"]}
    _, base, custom, parsed = parse_w3u(MAP_W3U)
    strings, names = parse_wts(DEFAULT_MAP / "war3map.wts")
    base_mods = {record.old_id: record.modifications for record in base}

    objects: list[dict[str, Any]] = []
    for record in [*base, *custom]:
        raw_id = record.new_id if record.new_id.strip("\0") else record.old_id
        inherited_overrides = base_mods.get(record.old_id, {}) if record.table == "custom" else {}
        overrides = {**inherited_overrides, **record.modifications}
        fields: dict[str, Any] = {}
        for field_id in UNIT_FIELDS:
            tft = source_field(record.old_id, field_id, tft_tables, tft_support)
            roc = source_field(record.old_id, field_id, roc_tables, roc_support)
            mod = overrides.get(field_id)
            if mod:
                effective = {
                    "value": resolve_text(mod.value, strings), "raw_value": mod.value,
                    "status": "map_explicit",
                    "source": f"{MAP_W3U.relative_to(ROOT).as_posix()}@0x{mod.offset:x}",
                    "inherited_from_base_object_modification": field_id in inherited_overrides and field_id not in record.modifications,
                }
            else:
                effective = {**tft, "status": "tft_archive_historical_candidate" if tft["source"] else "unresolved"}
            fields[field_id] = {
                **effective, "tft_candidate": tft, "roc_candidate": roc,
                "roc_tft_different": tft["value"] != roc["value"],
                "implementation_confidence": field_confidence(bool(mod), tft, roc),
            }
        objects.append({
            "id": raw_id, "base": record.old_id, "name": names.get(raw_id, raw_id),
            "table": record.table, "fields": fields,
            "all_map_overrides": {
                key: {"value": resolve_text(mod.value, strings),
                      "source": f"{MAP_W3U.relative_to(ROOT).as_posix()}@0x{mod.offset:x}"}
                for key, mod in record.modifications.items()
            },
        })

    roster = parse_roster(custom)
    combat_ability_ids = set(roster["restart_enabled_superset"]) | {
        "h00N", "h00O", "h00K", "h01D", "o000", "n009",
    }

    return {
        "scope": "69 W3U records; combat, movement, targeting, transport, upgrades, relevant W3A abilities, and mode-specific roster",
        "source_limit": "Inherited values use an owned TFT archive historical candidate. The map declares game version 2.0.2.22796; no matching Reforged data installation was available, so inherited values are not certified for patch 2.0.2.",
        "map_info": map_info(DEFAULT_MAP / "war3map.w3i"),
        "w3u_bytes_parsed": parsed, "archive_provenance": provenance,
        "field_definitions": {
            field_id: {
                "table": tft_support["metadata"].get(field_id, {}).get("slk", {}).get("value"),
                "column": tft_support["metadata"].get(field_id, {}).get("field", {}).get("value"),
                "index": tft_support["metadata"].get(field_id, {}).get("index", {}).get("value"),
                "source": tft_support["metadata"].get(field_id, {}).get("field", {}).get("source"),
            }
            for field_id in UNIT_FIELDS
        },
        "roster": roster, "objects": objects,
        "abilities": ability_evidence(objects, strings, combat_ability_ids),
        "upgrades": upgrade_evidence(strings),
        "capture_and_structures": capture_and_structure_evidence(objects),
        "weapon_target_interpretation": {
            "direct_field": "ua1g/ua2g",
            "splash_field": "ua1p/ua2p",
            "relation_group": "If enemy/neutral/friend tokens are all absent, the target-data relation group is unrestricted; it is not implicitly enemy-only.",
            "self": "Self is an independent permission. Without the self token, the attacking unit is excluded.",
            "h00H_effect": "tree/ground/structure splash is relation-unrestricted but excludes the attacker itself.",
            "h00W_effect": "enemy/neutral plus ground/structure/debris/wall explicitly excludes friend and self targets.",
        },
        "runtime_recommendation": {
            "safe_without_reforged_install": ["map_explicit", "classic_consensus"],
            "hold_current_behavior": ["classic_conflict_do_not_replace_runtime", "tft_only_historical_candidate", "unresolved"],
            "reason": "W3I game_data_set=0 means default game data; it does not select or identify RoC, TFT, or a balance patch. The declared engine version is 2.0.2.22796.",
        },
    }


def write_docs(result: dict[str, Any]) -> None:
    by_id = {row["id"]: row for row in result["objects"]}
    roster = result["roster"]
    fields = ["uhpm", "udef", "udty", "umvs", "uaen", "uacq", "uamn",
              "ua1b", "ua1d", "ua1s", "ua1c", "ua1r", "ua1t", "ua1w",
              "udp1", "ubs1", "ua1z", "ua1g", "ua1f", "ua1h", "ua1q", "uhd1", "uqd1"]
    lines = [
        "# Saran combat source resolution", "",
        "Generated by `python scripts/resolve_source_combat.py`.", "",
        "This report preserves the original historical-source audit. A later comparison",
        "against live Reforged 2.0.4.23745 and its three balance overlays is available in",
        "[the current inheritance audit](REFORGED-LATEST-INHERITANCE.md). It resolves",
        "some inherited values while keeping engine layer selection explicitly pending.", "",
        "The map declares `2.0.2.22796` in W3I and uses the default game-data set (`0`). The inherited tables available locally come from an owned classic TFT archive and are historical candidates, not proof of Reforged 2.0.2 defaults. Map-explicit W3U/W3A values remain authoritative. Empty SLK/profile cells stay unresolved rather than becoming guessed zeroes.", "",
        "`game_data_set=0` means default game data; it does not choose or identify RoC, TFT, or a balance patch. Runtime changes are safe from this evidence only for map-explicit fields and values where the owned RoC and TFT sources agree. Where they differ (for example Rifleman cooldown 1.6 vs 1.5 and Light vs Medium armor), keep the current behavior until 2.0.2 data or an engine observation resolves it.", "",
        "## Default effective roster", "",
        f"The initial reset enables {len(roster['restart_enabled_superset'])} IDs. With the declared defaults `ModesUnitSet=0` and `ModesShips=0`, the UnitSet=0 branch disables the 18 Classic variants, leaving {len(roster['default_available'])} available IDs; {len(roster['default_recruitable_from_city_or_shipyard'])} of those occur in Military Base or Shipyard `utra` lists.", "",
        "Default recruitable IDs: `" + "`, `".join(roster["default_recruitable_from_city_or_shipyard"]) + "`.", "",
        "`h00V` and `h000` are enabled by JASS but absent from the Shipyard `utra` list. `n00B` belongs only to Blacksmith `h01D`. The 40-ID audit union is therefore not one simultaneous roster.", "",
        "## Field provenance", "",
        "Each JSON field contains its effective value, status and direct source plus both TFT and RoC candidates. Metadata sources record how the four-character object field maps to an SLK/profile column. Real fields `udp1`/`ubs1`/`uhd1`/`uqd1` represent damage point, backswing and splash factors.", "",
        "| ID / name | " + " | ".join(fields) + " |", "|---|" + "---|" * len(fields),
    ]
    reported = sorted(set(roster["restart_enabled_superset"]) | {"h00N", "h00O", "h00K", "o000", "n007", "n008", "n009"})
    for raw_id in reported:
        row = by_id.get(raw_id)
        if not row:
            continue
        values = []
        for field_id in fields:
            item = row["fields"][field_id]
            value = item["value"]
            text = "?" if value is None else str(value).replace("|", "/")
            if item["status"] == "tft_archive_historical_candidate":
                text += " T"
            values.append(text)
        lines.append(f"| {raw_id} / {row['name'].replace('|', '/')} | " + " | ".join(values) + " |")
    lines.extend([
        "", "`T` marks TFT historical inheritance; unmarked values are explicit map overrides.", "",
        "Target masks keep WC3's category groups separate. `ua1g` is the direct-target mask and `ua1p` is the splash-target mask. With no enemy/neutral/friend token the relation group is unrestricted, while `self` remains an independent permission. Thus h00H's explicit `tree,ground,structure` splash may affect friendly and hostile qualifying targets but excludes the attacker; h00W explicitly limits its splash relationship group to `enemies,neutral`.", "",
        "## Combat abilities", "",
        f"The W3A audit is limited to abilities attached to {len(result['abilities']['attached_explicitly_by_unit'])} combat-roster units/structures plus the relevant stock bases and Quick Roar; UI and cosmetic W3U objects are excluded. The Medic units explicitly carry stock `Ahea`. W3A changes only its target mask. The TFT historical row says `DataA1=20`, `Cast1=0`, `Cool1=1`, `Cost1=4`, and `Rng1=250`; it does not establish the prototype's 15-HP heal, and Reforged 2.0.2 remains unresolved. `Aroa` is map-modified to area 700, explicit `nonhero,air,friend,self,ground,alive` targets, and one-second hero duration. Quick Roar `A00W` lasts 15 seconds but is not directly present in the audited unit ability lists. Mortar splash comes from weapon fields, not W3A. Transports `n007`/`n008` explicitly attach the custom load/unload set and `Sch3` sets `Car1=10`; `n009` has no explicit `uabi`, so its name alone does not prove transport behavior.", "",
        "## City defender, structures and capture", "",
        "`h00N` and `h00O` statically inherit `uaen=0`, but both list `Rhri`; the map replaces that upgrade with `renw/1` (Enable Weapon 1) and researches it for all players plus neutral in the default mode. Their effective default weapon is therefore the explicit 45+1d5, range 650, cooldown .9, missile speed 1600 profile. The native mutations at lines 7813-7815 and 7955-7957 target zero-based weapon index 1 (attack 2), which is undefined for these markers, and do not replace attack 1.", "",
        "A separate Rifleman `h00B` is created as the starting defender (`war3map.j:5456-5457,7075`): map damage 15 + 2d4; range 400, acquisition 600, damage point .17 and backswing .7 agree between the classic sources. Buildable Bunker `o000` is enabled and has explicit range 425/cooldown 1.5/base damage 50. `h00K` is a non-attacking Wall.", "",
        "Capture uses `CityCircleRange=155` to build the registration rectangle. The unit-entry filter excludes transports `n007` and `n008`; it does not exclude `n009`.", "",
        "## Upgrade boundary", "",
        "The companion combat JSON retains W3Q records. `Rhri` replaces effect 1 with `renw` and base 1 with `1.0`; this source does not establish the stock Long Rifles +200 range effect. Runtime research calls must apply the decoded custom effect through upgrade metadata before any numeric range transition is claimed.", "",
    ])
    (ROOT / "docs/audits/reforged-source-resolution.md").write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    result = build()
    output = ROOT / "data/derived/reforged-source-combat.json"
    output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    write_docs(result)
    count = len(result["roster"]["default_recruitable_from_city_or_shipyard"])
    print(f"{len(result['objects'])} objects; {count} default recruitable; wrote {output}")


if __name__ == "__main__":
    main()
