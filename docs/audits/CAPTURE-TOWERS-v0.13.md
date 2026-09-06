# Capture and tower divergence audit v0.13

This is a bounded comparison of the extracted Saran Reforged v3 source with the current Unity prototype. Source behavior is cited from the local `war3map.j`, `war3map.w3u` audit, and decoded placement data. A value described as inherited or unresolved is not treated as a confirmed Warcraft runtime rule.

## Starting defenders and city geometry

Saran configures both normal and shipyard starting defenders as `h00B` (`war3map.j:5454-5458`). During setup, each `B00R` circle destructable is replaced by an `n000` circle unit and one dynamically created `h00B` at the circle position; that unit is stored in `CityDefenders` (`7062-7078`). The circle is therefore a registered garrison post with one source Rifleman per city, including neutral cities. Restart logic recreates the configured defender at the circle (`5339-5351`).

The circle is not placed by an offset from the city building in JASS. Setup associates the new circle to a nearby city marker (`GetUnitsInRangeOfLocMatching(512, ...)`, `7068-7074`). Decoding `war3map.doo` gives 212 `B00R` records, matching the 212 static `h00N`/`h00O` city placements. Nearest city-to-circle XY separations are native Warcraft units: minimum 243.7047, median 286.2167, mean 288.7579, maximum 320.0. With the current adaptation scale of 50 Warcraft units per Unity metre, the median is 5.72 m, close to the measured runtime town-circle separation of 5.66 m. These placement distances are measurements, not capture rules.

Saran sets `CityCircleRange=155` and creates a `RectFromCenterSizeBJ(center,155,155)` for each circle (`5459-5461`, `7125-7131`). The registered capture footprint is therefore a square 155 units on a side. The source enter trigger ignores only `n007` and `n008`, and invokes `City Claim` when the current defender is `n001`, dead, or at least 300 units from the circle center (`17438-17484`).

The prototype uses the same visual half extent after scaling (`ClaimRules.CircleRadius=1.55`, `ClaimZone.cs:9-18`), but `Step` queries a radial 6 m takeover area (`CityClaimZone.cs:19-23`; `ClaimRules.cs:6-8`) and does not test membership in the square footprint. The ring is consequently a visual 1.55 m radius while contest/capture can occur much farther away. This is a deliberate adaptation only if documented as such; it is not the source geometry.

## Reproducible circle extraction

The checked-in extractor is `scripts/extract_risk_circles.py`. From the repository root, reproduce the derived numeric report with:

```text
python scripts/extract_risk_circles.py
```

It reads only `references/maps/reforged-v3-source/war3map.doo` and `data/derived/risk-reforged-v3-placements.json`, then writes `data/derived/risk-reforged-v3-circles.json`. The parser verifies `W3do`, version 8/subversion 11, 4,925 records, 54-byte fixed records, and eight zero trailing bytes. It rejects a different layout or any non-zero variable item-set section rather than guessing offsets. The current source SHA-256 is `011815edb761db57e884bfadf8864f855d655ce4d54789ed1369b880438e8961`.

The extractor finds exactly 212 `B00R` records and exactly 212 city rows (`h00N`/`h00O`) in the existing placements JSON. Each circle is matched to the unique nearest city center in native XY coordinates; the output retains circle and city native X/Y/Z, source record/byte offset, offsets, XY distance, 3D distance, and the source hashes. The run verified 212 unique city matches, zero duplicate matches, and zero nearest-distance ties.

Verified output metrics in `data/derived/risk-reforged-v3-circles.json`: XY minimum `243.70473938764508`, median `286.2167011199731`, mean `288.75793211878874`, maximum `320.0` native Warcraft units. At the prototype's 50 native units per Unity metre, those are `4.8740947877529015`, `5.724334022399462`, `5.775158642375775`, and `6.4` m. The closest verified pairs are city 104 (`h00O`) at `243.70473938764508`, city 113 (`h00N`) at `263.8787600395303`, and city 122 (`h00N`) at `263.8787600395303` native XY units.

This is a coordinate association for audit and geometry work. It does not claim that nearest-city matching is the JASS association algorithm, and it does not infer attack, capture, or tower behavior. The extractor is intentionally bounded to this verified `war3map.doo` layout and will fail on other versions, record lengths, trailing payloads, or variable sections.

## Defender retention, replacement, and capture

The source death trigger runs only when the dying unit is the indexed `CityDefenders[GetUnitUserData(dying)]` (`war3map.j:17501-17505`). It first searches living, non-structure allied units, excluding transports `n007`/`n008`, within `155/.70 = 221.43` units of the circle (`17508-17541`, `17802-17815`). If that group is empty, it scans 532 units around the dying defender for living, non-structure units owned by the killing player (`17544-17585`, `17817-17823`); the killer is added only if alive and non-structure. The selected takeover unit must be within 300 units of the circle and ground or flying (`17702-17737`). If no successor is available, the source creates neutral dummy `n001` at the circle (`17827-17831`, `17851-17855`).

`City Claim` stores the claimant as the new `CityDefenders` entry, writes city user data, moves it to the circle, and transfers city/circle ownership only when claimant and city owners differ (`18169-18320`). It kills the old defender only on the explicit temporary-`n001` path (`18203-18206`). The source is a bound-defender identity conversion, not a generic “enemy nearby captures” meter.

The prototype does not spawn a dedicated Rifleman per city. `RiskBootstrap` spawns Footmen/Archers near towns and then assigns one existing nearby unit to each town (`RiskBootstrap.cs:38-52`; `Settlement.cs:110-126`). A garrison may therefore be an Archer or Footman, and neutral/owned starting armies are larger than the one-source-`h00B` model. The prototype retains a living bound defender while its `Garrison` remains this zone (`CityClaimZone.cs:31-35`, `51-57`), but does not check that the defender remains inside the source circle. Once absent, it picks the nearest eligible owner unit inside 4.43 m, otherwise the nearest enemy inside 6 m (`39-57`). Ownership changes immediately in `Settlement.SimTick` when `Step` returns a different owner (`Settlement.cs:151-159`); `Progress` is currently always zero (`CityClaimZone.cs:16-17`).

The current replacement ordering is a product rule: owner-team candidate first, then enemy, then nearest distance and `EntityId` tie-break. Saran's source begins from a random group member and can replace it by point value or health preference (`war3map.j:17664-17700`, `17775-17790`), so nearest-distance selection is not source behavior.

## Invulnerability and tower fireability

The city marker objects `h00N`/`h00O` have local configured attack fields (45 Pierce, one die/five sides, 0.9 s, 650 range) but retain `Avul`; their automatic weapon activation is unresolved because the available RoC weapon table has no `hbar` row. The local audit therefore proves configured fields, not active city fire (`docs/RISK-MAPS-v0.10.md:50-56`; `docs/RISK-RULES-v0.12.md:144-150`). Ownership is transferred by JASS; the city marker is not intended to be destroyed as the capture condition (`war3map.j:18169-18320`).

The selected source Bunker `o000` has explicit local fields `550 HP, 50 base damage, 1.5 s, 425 range, Fortified/3 armor`; dice, acquisition, and other omitted fields are inherited (`war3map.w3u` audit; `docs/REFORGED-UNIT-STATS.md:94-97`). Its abilities include `A017`, `Astd`, `Abun`, and `Abtl`; `Abtl` is documented as Battle Stations, while `A017` is the self-destruct/refund action (`docs/CAPTURE-SOURCE-v0.9.md:99-110`). These records do not establish a universal always-active tower mode. The map also explicitly limits `o000` to 20 per player (`war3map.j:4168-4171`).

The prototype makes `DefenseTower.CanBeAttacked` false and its `TakeDamage` method intentionally does nothing (`DefenseTower.cs:13-19`, `123-126`), which matches the source city's invulnerable-marker role at a high level. Its tower is nevertheless an active local combatant: it fires whenever built, alive, and a defender exists (`89-97`), scans a local 13 m range (`29-30`, `99-116`), uses local 80-plus-d8 damage, and stops firing when no defender exists. This is a product implementation, not evidence that Saran city markers or Bunkers fire in exactly this state. Keep a separate fireability flag/profile until matching TFT/base weapon-enable data is available.

## Safe ranged geometry

The prototype Archer/Rifleman adaptation uses range 8 m (approximately 400 Warcraft units at the current scale), while the capturable tower uses range 13 m (650 units). The source placement median city-to-circle separation is 286.22/50 = 5.72 m; the current runtime measurement is 5.66 m. `Settlement.Initialize` places the circle at town Z offset -4.2 m and the tower laterally at ±3.8 m (`Settlement.cs:40-54`), producing the measured 5.66 m separation.

For a lone Archer to kill the circle defender without tower fire, approach from the side opposite the tower and stop at the Archer attack range. At the measured separation, the far-side tower distance is approximately 8 + 5.66 = 13.66 m, leaving only 0.66 m beyond the tower's 13 m range. This is a narrow geometric margin; NavMesh stopping distance, collider extents, line-of-sight ray origin, and movement overshoot can erase it. The source placement range is a useful sanity check, not proof of an angle that succeeds in Unity.

## Prioritized recommendations

1. Preserve the current source-aligned capture identity contract: one dedicated defender slot per city, retained while alive, and replacement only after the registered defender dies or is explicitly removed. If using existing Unity soldiers remains desirable, expose it as an adaptation and ensure the initial unit is intentionally the Archer/Rifleman profile rather than an arbitrary nearby Footman.
2. Decide and document the geometry contract before changing combat tuning. Either enforce a scaled square 155-side capture footprint, or label the current 1.55/4.43/6 m radial model as product adaptation. Keep the ring radius, claim eligibility, and ranged positioning test consistent.
3. Add a deterministic ranged-geometry regression for the measured 5.66 m arrangement: Archer at a far-side firing point must damage the defender while the tower has no valid target; move it inward until the tower range legitimately acquires it. Include NavMesh sampling and terrain LOS in the test.
4. Keep tower fireability separate from tower stats. The source confirms Bunker values and city `Avul`, but does not resolve automatic attack-enable behavior. Do not infer that every source structure fires continuously from its configured range.
5. If source succession is the goal, add the explicit source filters and ranges as named policy values (221.43 allied fallback, 532 outer scan, 300 takeover distance, ground/flying requirement) instead of reusing attack range or the visual circle radius.
