# Unit types and distances audit v0.13

Batch 1, topic 2. This is a source-to-Unity audit of combat units, naval
units, ranges, movement, acquisition, minimum range, and retaliation. It does
not repeat the active single-Archer flank/capture test or the separate capture
and placement audits. No Unity code or extracted source data was changed.

## Evidence boundary and unit scale

The primary source is the locally extracted Saran Risk Reforged v3 map:
`references/maps/reforged-v3-source/war3map.w3u`,
`reforged-unit-stats.json`, and `war3map.j`. `REFORGED-UNIT-STATS.md` reports
the exact override offsets; `INHERITED` means that the map did not write the
field. `REFORGED-BASE-STATS-v0.9.md` resolves some inherited values from a
historical Blizzard/wc3libs fixture, with medium confidence where the build
is not identified. The owned-disc extracts under
`references/owned-disc-data/` are RoC base records. They do not provide a
complete verified TFT runtime table, so they cannot settle every inherited
field for this Reforged map.

The prototype's world values consistently use a practical `raw Warcraft
distance / 50` convention. This is an implementation conversion, not a
claim about the map's coordinate system:

| Source value | Converted value | Unity check |
| ---: | ---: | --- |
| Rifleman inherited range 400 | 8.0 | `h00B` Archer profile range 8 |
| `h00E` range/acquisition 400 | 8.0 | Medic range 8 |
| `h00H` range/acquisition 900 | 18.0 | Mortar range 18 |
| `h00W` range 1000 | 20.0 | Current Galley/Frigate range 20 |
| Rifleman speed 270 | 5.4 | Archer speed 5.4 |
| Knight speed 350 | 7.0 | Guard speed 7 |
| `h00H` speed 230 | 4.6 | Mortar speed 4.6 |
| `h00W` speed 340 | 6.8 | Current Galley/Frigate speed 6.8 |

The current capture circle distance (`5.66`) is product geometry and remains
outside this unit-stat comparison. `CityCircleRange=155` is the source map
value; it should not be presented as the same Unity distance without an
explicit conversion decision.

## Source roster versus current Unity roster

The source city `h00N` production list contains 20 entries and the shipyard
`h00O` list contains 17 entries (`REFORGED-UNIT-STATS.md`, JASS object field
`utra` at `war3map.w3u@0x1213` and `@0x14c6`). The active Unity prototype
exposes six land kinds (`Footman`, `Archer`, `Guard`, `Mage`, `Mortar`,
`Medic`) and two naval kinds (`Galley`, `Transport`). Footman and Mage are
explicit local fantasy units; they are not asserted to be Saran raw IDs.

| Unity kind/profile | Intended source identity | Source evidence | Current result |
| --- | --- | --- | --- |
| Archer, `ReforgedProfiles.Units[1]` | `h00B` Rifleman | `war3map.w3u@0x525` damage base 15; `@0x592` HP 200; `@0x572` gold 1. Dice/range/cooldown/type fields are inherited. | Good adapted match: 200 HP, 15+2d4, range 8, 1.5 s, speed 5.4, armor 0, Pierce/Medium. Inherited dice/cooldown/range/type remain version-limited evidence. |
| Guard, `Units[2]` | `h00G` Knight | `@0x82c` damage 37; `@0x89f` HP 650; `@0x900` armor 7; inherited melee/range and weapon fields. | HP, base damage, armor, speed 7, 1.4 s and 2d5 are a reasonable inherited adaptation. Unity range 1.05 is deliberately close combat; fixture raw range 100 would convert to about 2.0, while the primary page says Melee. Keep 1.05 as product tuning until the matching TFT object data is verified. |
| Mortar, `Units[4]` | `h00H` Mortar Team | `@0x9d3` damage 18; `@0xa49` HP 350; `@0x9e3` range 900; `@0xb12` acquisition 900; `@0xae2` speed 230. | Strong match: 350 HP, 18+1d13, range/acquisition 18, speed 4.6, cooldown 3.5, Siege/Heavy. Unity's minimum range 5 matches the historical 250 raw minimum divided by 50, but that raw inherited value is version-sensitive. |
| Medic, `Units[5]` | `h00E` Priest | `@0x690` range 400; `@0x6f6` HP 250; `@0x7f8` acquisition 400; `@0x798` armor 1. | Range 8, acquisition 8, speed 5.4, cooldown 2 and armor 1 are consistent with the inherited Priest row. Unity stores 7+1d2 to display 8–9; the source fixture reports the same 8–9 result, while attack/defense are inherited Magic/Unarmored. |
| Footman, `Units[0]` | None asserted | No corresponding active source ID is claimed. | Local 200 HP, 17+1d4, range .9, cooldown 1.35, speed 5.4. Keep clearly labelled local. |
| Mage, `Units[3]` | None asserted | No corresponding active source ID is claimed. | Local 250 HP, 29+1d3, range 10, cooldown 1.6, speed 5.4. Keep clearly labelled local. |

Not represented in the current shop but present in the source list are the
elite and support infantry (`h00F`, `h00J`, `h00M`, `h010`, `h011`, `h016`,
`h017`), tanks (`h00Y`, `h018`, `h019`, `h01A`), builders/sappers, and the
marine equivalents (`h00R`, `h00S`, `h00T`, `h012`, `h014`, `h015`). Their
map overrides are real source evidence, but inherited weapon dice, attack
types, defense types, and several cooldowns/ranges should not be promoted to
playable Unity stats until a matching TFT/base object table is verified.

## Damage, armor, cooldown, range, and movement differences

`ReforgedProfiles.cs` already carries the source damage dice shape for the
four source-aligned land profiles. The current damage matrix in
`CombatRules.cs` reproduces the extracted `DamageBonusNormal`,
`DamageBonusPierce`, `DamageBonusSiege`, and `DamageBonusMagic` values for the
first four physical columns and deliberately uses the source tuple's final
`None`-style value for Unity `Unarmored`. The report's exact eight-value
literals remain the authority; this five-column projection is an adapter, not
a claim that every source column has been resolved. The remaining fidelity
issue is type vocabulary: Warcraft's verified defense enum is `Light, Medium,
Large, Fortified, Normal, Hero, Divine, None`, while the Unity enum is
`Unarmored, Light, Medium, Heavy, Fortified`. `Heavy` is being used as a
practical stand-in for source Large/Heavy and `Unarmored` for the source
None-style column. That is serviceable for the prototype but loses source
identity and will become visible when tanks, transports, or artillery are
added.

The most material current differences are:

| Area | Exact source fact | Current Unity behavior | Assessment |
| --- | --- | --- | --- |
| Acquisition | Source has a distinct `uacq` field. `h00H=900` and `h00E=400` are explicit; other rows may be inherited. | `UnitProfile` has only `Range`. Soldier acquisition uses `Range+1` for ranged units and mode/team leash rules; ships and towers also query attack range. | P0 model gap. Range and acquisition should be separate values even if most initial profiles set them equal. |
| Minimum range | Historical base `hmtm` minimum is 250 raw, about 5; other inherited minimums are unresolved. | `BattleRules.MinimumRange` hard-codes 5 only for Mortar. | Behavior is currently correct for Mortar, but the value is in rules code rather than the profile and cannot represent future artillery variants. |
| Guard melee range | Source primary page says Melee; fixture raw range is 100 and is version-sensitive. | Guard uses 1.05. | Acceptable intuitive RTS tuning. Do not silently call 1.05 the verified source range. |
| City/capture tower | `h00N/h00O` source fields are 45 Pierce, 1d5, .9 s, 650 range; HP is inherited and city objects retain `Avul` (`RISK-RULES-v0.12.md`, buildings section). | Live `DefenseTower` uses local `CapturableTower`: 550 HP, 80+1d8, .9 s, range 13, armor 3. | Range and cooldown preserve the source scale; damage/HP/dice are explicitly local balance. This is appropriate for the current capture prototype. |
| Bunker | `o000` explicitly has 550 HP, 50 base, 1.5 s, 425 range, armor 3, Fortified (`war3map.w3u@0x684c` HP, `@0x685c` damage, `@0x686c` cooldown, `@0x687c` range, `@0x68bd` armor, `@0x68ac` defense). | `ReforgedProfiles.Tower` stores 550, 50+1d8, 1.5 s, 8.5, armor 3, but `DefenseTower` fires the `CapturableTower` profile at runtime. | Profile exists but is not the live tower firing profile. Decide later whether buildable Bunker and invulnerable city marker need separate runtime structure kinds. |
| Movement | Source base speeds are Rifleman 270, Knight 350, Mortar 220; map Mortar override is 230. Priest is 270. | Archer 5.4, Guard 7, Mortar 4.6, Medic 5.4. | Conversion is consistent: 270/50=5.4, 350/50=7, 230/50=4.6. Mortar's 220 base is superseded by explicit 230. |
| Retaliation source | Extracted JASS issues attack orders for recruited units at rally points (`war3map.j:19515-19523`) and has capture/transport orders, but no custom per-unit damage retaliation routine. Automatic acquisition/retaliation is therefore inherited Warcraft behavior and remains unverified from this map source. | Soldier damage sets its attacker as a target unless in Move/Hold/Follow, then alerts idle allies within 5 Unity units (`Soldier.cs:282-303`). | Modern RTS behavior is deterministic and readable, but it is a product adaptation, not a source claim. |

The source `uacq` field should not be confused with `ua1r` attack range. The
current implementation does exactly that for all units except the hard-coded
Mortar minimum. That can produce premature or late acquisition when a unit's
desired sense radius differs from its firing radius, especially for tanks,
ships, or a later long-rifle upgrade.

## Naval audit

The source shipyard catalog is the exact `h00O` `utra` list above. The map
proves transport load/unload behavior but does not prove a numeric capacity:
the load trigger scans 512 raw range, chooses nearest candidates, and loops
at most ten times (`war3map.j:20070-20180`). The current six-soldier capacity
is therefore a deliberate prototype rule, not extracted Saran data.

| Unity profile | Closest source row | Source fields | Current fields/diff |
| --- | --- | --- | --- |
| Galley/Frigate | `h00W` Warship B is the documented adaptation | HP 400, damage 30, range 1000, armor 6, speed 340, gold 5 (`reforged-unit-stats.json`, overrides around `@0x1fcb-@0x20e4`). | Current 400 HP, 30 damage, range 20, armor 6, speed 6.8, cost 5: exact after `/50` for the explicit fields. Cooldown 1.5 and Normal attack are local choices because source values are inherited. Only Galley attacks or auto-acquires in `Ship.cs`. |
| Transport | `n008` or `n009` Transport Ship | HP 300, speed 340, gold 2, `uacq=0`, Large defense type; damage/range/cooldown/armor inherited (`n008` offsets `@0x338d`, `@0x3462`, `@0x34ed`; `n009` has the parallel row). | Current 300 HP and cost 2; speed 5.0 versus source-converted 6.8; armor 1 and six capacity are local; range/damage/cooldown 0 disable attack. Recommend retaining capacity 6 as product UX, but document the speed and armor choices. |
| Armoured Transport | `n007` | HP 300, speed 370 (7.4 converted), armor 30, Large defense, cost 6, `uacq=0`. | No Unity kind. A future armored transport should not be folded into normal Transport: its 30 armor and 7.4 speed materially change survivability and movement. |
| Other warships | `h00X/h001/h006/h00L/h00P/h00Q/h00U` | Explicit source ranges 1000–1500, HP 400–2500, base damage 30–130, armor 4–20, and several speed/cost values; many attack/cooldown fields are inherited. | Not exposed by the prototype. Keep source rows as a data backlog, not guessed Unity profiles. |

`Ship` currently reports every ship as `ArmorKind.Heavy`; the source transport
rows explicitly say Large, and source attack/defense types are frequently
inherited. A separate source defense/type mapping is preferable before adding
the rest of the fleet. Galley retaliation is also narrower than Soldier
retaliation: `Ship.TakeDamage` retargets only when the Galley is attack-moving
or has no remaining route (`Ship.cs:147-151`); a transport never retaliates.

## Prioritized implementable changes

### P0 — preserve the current prototype while removing ambiguous semantics

1. Add an explicit audit/data contract for `AttackRange`, `AcquisitionRange`,
   and `MinimumRange` in both land and naval profile records. Initialize
   source-aligned values as: Archer 8/8/0, Guard 1.05/2-or-1.05/0 pending the
   melee decision, Mortar 18/18/5, Medic 8/8/0, Galley 20/20/0, Transport
   0/0/0. This can be implemented behind the existing profile APIs; it does
   not require an ECS rewrite.
2. Keep inherited values labelled with confidence. Use “map override”,
   “historical inheritance”, or “local tuning” in profile source strings and
   UI/debug output. Never treat `INHERITED` as zero or as verified TFT data.
3. Add deterministic distance acceptance checks: ranged units acquire only
   within acquisition range, fire only within attack range, Mortar cannot fire
   inside minimum range, and equal-distance targets use EntityId as the tie
   break. These checks should exercise existing `Spatial.Query` and NavMesh
   adapters rather than introduce a new simulation architecture.

### P1 — close the most visible fidelity gaps

1. Decide whether the normal Transport should move at source-converted 6.8 or
   retain the current 5.0 for readability. If fidelity wins, use 6.8 and keep
   six capacity explicitly marked as product tuning. Add a separate Armoured
   Transport profile only after naval combat balance is measured.
2. Split invulnerable city markers from buildable Bunkers in the profile/data
   contract. Preserve the current local capturable tower's 80+1d8 tuning for
   the capture test, while retaining the source city 45+1d5 and Bunker 50+1d8
   rows as named reference profiles.
3. Define a compact retaliation policy in tests: Idle and AttackMove units may
   retarget their attacker; Move/Hold/Follow preserve their order; nearby idle
   allies may be alerted within a fixed radius; transports never attack. This
   records the modern RTS choice without implying that it came from JASS.

### P2 — only when the roster expands

1. Add source `DefenseType` values Large, None, Hero, and Divine (or a lossless
   adapter) instead of extending the current Heavy/Unarmored approximation.
2. Add the source-aligned elite infantry, tanks, and warships from the two
   production lists. For each new row, resolve the matching TFT/base object
   data first; otherwise expose it as an inherited-uncertain profile and keep
   it out of competitive balancing.
3. Verify the map's inherited cooldowns, dice, attack types, and artillery
   minimum ranges from a matching installed TFT/World Editor dataset. The
   current RoC extracts and patch delta do not establish those values.

## Audit conclusion

The four source-aligned land units are internally coherent after the existing
`/50` distance conversion. Archer, Mortar, and Medic have strong evidence for
their current Unity values; Guard range is a defensible melee adaptation. The
largest implementation risk is the conflation of attack range and acquisition
range, followed by the lack of a lossless Warcraft defense-type vocabulary.
The Galley profile is close to the explicit `h00W` source row, while normal
Transport speed, armor, capacity, and all naval cooldown/type fields are
product choices or unresolved inheritance. The next safe implementation step
is a profile-contract and acceptance-test pass, followed by an explicit
Transport speed decision; no source or installer data should be inferred to
fill the remaining TFT gaps.
