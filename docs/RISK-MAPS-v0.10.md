# Risk map placement audit (v0.10)

This audit covers the locally available editable map `Risk_Reforged_v3.0_by_Saran_OPEN_SOURCE.w3x` (Saran, Reforged v3). The archive SHA-256 is `acfa7048a5c48d61cb80fb42222d87b0f0ce9204ff4517a256f8502a68907f26`. The map header/JASS identifies it as “Risk Reforged v3.0” by Saran (`war3map.j:1867-1871`). Its 69 named regions run from Germany, Poland, and the Balkans through the Mediterranean, North Africa, the British Isles, and Russian districts (`war3map.j:6124-6193`), so this source is the Europe/Mediterranean map family; it is not a World/New World map. The source map and its extracted binary members remain under ignored `references/maps/`; the derived placement tables are tracked in `data/derived/`.

## Reproducible extraction

```powershell
python scripts/extract_reforged_v3.py
python scripts/extract_risk_placements.py
```

`extract_risk_placements.py` parses `war3mapUnits.doo` as W3do version 8/subversion 11, retaining native Warcraft III `x`, `y`, `z`, owner, angle, skin, creation number, and variable sections. It joins city rows to the `Trig_Set_Bases_Actions` sequence in `war3map.j`, which is the map's authoritative city order and region grouping.

Outputs:

- `data/derived/risk-reforged-v3-placements.json` — all 241 records, with city/region fields on the 212 city records.
- `data/derived/risk-reforged-v3-placements.csv` — compact x/y/z/owner/angle table suitable for import.

## World/New World archive attempt

To answer the World comparison directly, I downloaded the public `Risk - New World v3.0` archive by VoR from [wc3maps](https://www.wc3maps.com/map/167651/Risk_-_New_World_v3.0) into ignored `references/maps/Risk-New-World-v3.0.w3x` (7,911,203 bytes, SHA-256 `e653d6993bf8d7091f50aad0dd61e9bc16c0b0cadaaa4d39344aaa74cef700c2`). The map's extracted `war3mapUnits.doo` is W3do version 7/subversion 9 with a single `sloc` record. It is therefore a protected/stripped archive for static placements: the `.doo` does not contain its city/building table.

The map does keep literal runtime placement calls in `war3map.j`. Running the bounded fallback parser gives 298 `BlzCreateUnitWithSkin` placements: 173 `h00N`, 59 `h00O`, 61 `h00T`, 4 `n00A`, and 1 `h00D`. The JASS city sequence assigns 293 of those to 100 named regions; the five unassigned calls are four `n00A` scene units and one `h00D` special spawn center. The labels extend the Europe/Mediterranean set into Caribbean and eastern/central North American regions and Greenland (for example Haiti, Dominican Republic, Belize, Yucatan, Jamaica, Cuba, Florida through Nunavut and West Greenland). The raw x/y/angle and region assignment data are in `data/derived/risk-new-world-v3-jass-placements.json` and `.csv`, sourced from `references/maps/risk-new-world-v3-source/war3map.j` (JASS SHA-256 `2e038c52062fc0198a465023303c38db19226d905cf774c3272f75f5c33a3d38`).

Reproduction after obtaining the archive is:

```powershell
python scripts/extract_map_members.py references/maps/Risk-New-World-v3.0.w3x references/maps/risk-new-world-v3-source
python scripts/extract_world_jass_placements.py
```

This JASS result is useful for coordinates and geography, but it is not an editable `.doo` source and does not include units created dynamically after map initialization. It should therefore be treated as a faithful audit of the public archive's literal placements, not as proof that every runtime property is statically placed.

## Placement inventory

| raw ID | role in map | count | placed owner |
| --- | --- | ---: | ---: |
| `h00N` | normal city production building / city marker | 168 | 24 (neutral aggressive) |
| `h00O` | shipyard city production building / city marker | 44 | 24 (neutral aggressive) |
| `sloc` | player start locations | 24 | 0–23 |
| `n00A` | map decoration/scene unit created by JASS | 4 | 0 |
| `h00D` | special regional spawn center | 1 | 27 (neutral victim) |

The 212 city placements cover 69 regions. Every city is explicitly assigned in JASS; `city_index` is 1–212 and `region_id` is 1–69. The CSV and JSON preserve the unrounded source coordinates to five decimal places (the source placements are mostly integer x/y values).

## Region names and defense interpretation

The export now includes `region_name` for every city assignment. Names are copied verbatim from the map's `udg_CountryTexts` table; spelling is therefore source-faithful, including `Lybia` and `Moscov (Russia)`. This is a gameplay region label, not a claim that every city coordinate follows modern political boundaries.

There are no placed `o000` bunkers and no placed `h00K` walls in `war3mapUnits.doo`; those are buildable tower types, not innate per-city placements. The city structures themselves (`h00N` and `h00O`) have these configured local combat fields in `war3map.w3u`: 45 piercing base attack, one damage die with five sides, 0.9 cooldown, 650 range, projectile speed 1,600 (`ua1z`), target filter `ground,structure,debris,air,item,ward`, and Divine defense. Their hit points are inherited from the base `hbar` object rather than overridden by these custom rows. The explicit ability list is `A011,A001,Avul`: alternative-income UI, defender selection, and the base Invulnerable ability. No JASS path removes `Avul` from city buildings.

The custom rows do not override `uaen` (the attacks-enabled boolean), and the local JASS contains no `BlzSetUnitIntegerField` or equivalent runtime weapon-enable call. A historical base-data fixture's `hbar` row identifies Human Barracks and has `weapsOn=0` ([wc3libs `UnitWeapons.slk`](https://raw.githubusercontent.com/inwc3/wc3libs/master/src/test/resources/slks/UnitWeapons.slk), row `hbar`). That makes the effective attack state a base-data dependency and potentially disabled; this audit does not claim that the configured 45/.9 fields produce live city DPS. If the inherited weapon is enabled, `ua1g` includes both `ground` and `air`, so the configured filter is not anti-air-only. `utc1=1` is the maximum-targets field, not the attacks-enabled switch.

The source therefore proves configured city combat fields but not active automatic fire, and those buildings are not intended to be destroyed to capture a city. The capture paths are defender death, unit entry, or defender swap; the claim routine transfers the city structure and circle with `SetUnitOwner` (`war3map.j:17872-17875`, `18169-18320`). `h00B` defenders are created dynamically at setup, one per city (`war3map.j:5455-5464`, `5339-5351`), and the defender death trigger—not a city-building death trigger—drives replacement/claim logic.

City defenders are not static `.doo` placements. At game setup, JASS creates one `h00B` (`StartingDefenderNormal` and `StartingDefenderShipyard`) at each city circle and stores it in `CityDefenders`; restart logic removes and recreates these defenders. Therefore the source-supported defense model is one dynamically created `h00B` defender per city, plus optional player-built `h00K`/`o000` towers; `h00N`/`h00O` weapon behavior remains dependent on the inherited base weapon-enable data above.

## Public archive comparison (snapshot research)

The public archive listing identifies the exact Saran v3.0 release as a 24-player Risk Reforged version released 2 October 2020, with a 5.00 listing rating; it also lists later VoR continuations in the same version family ([maps.w3reforged.com Risk Reforged archive](https://maps.w3reforged.com/maps/categories/risk/risk-reforged)). For comparable archive download signals, wc3maps currently shows Risk Reforged Beta 1.46b by Saran at 4,357 downloads ([wc3maps](https://www.wc3maps.com/map/34199)), Risk Europe 1.37 at 4,063 ([wc3maps](https://www.wc3maps.com/map/246623)), Risk Europe 3.08 at 3,123 ([wc3maps](https://www.wc3maps.com/map/404434)), and Risk - New World v3.0 at 21 ([wc3maps](https://www.wc3maps.com/map/167651)). These are different versions, authors, release dates, and archive records. They support discoverability only; they do not justify calling Saran, Europe, or World “most popular.” The archive category itself exposes separate rating, download-count, and hosted-time sort modes, rather than one normalized popularity measure ([risk category](https://maps.w3reforged.com/maps/categories/risk)).

The available World comparison is `Risk - New World` (VoR), a distinct 24-player 384x384 map family listed separately from the Saran Reforged source ([New World archive](https://maps.w3reforged.com/maps/categories/risk/Risk%20-%20New%20World)). The public v3.0 archive is protected for editor placement data, but its JASS still exposes the 293-city, 100-region geography summarized above; this is the concrete World evidence used in this audit.
