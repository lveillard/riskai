# Counters v0.17: source facts and current runtime

## Scope and evidence

This is a data audit, not a balance verdict. It compares the current runtime
profiles with the two locally extracted maps:

| Map | Object source | Parsed form |
| --- | --- | --- |
| Risk Reforged Europe by Saran | `references/maps/reforged-v3-source/war3map.w3u` | W3U v3, 66 custom records |
| Risk New World | `references/maps/risk-new-world-v3-source/war3map.w3u` | W3U v2, 73 custom records |

The reproducible local audits are
`references/owned-disc-data/audits-v016/v016-europe-unit-audit.json` and
`v016-world-unit-audit.json`. Both give the same overrides for the playable
rows below. A W3U only stores changed fields; values marked *inherited* were
resolved by the prior audit against the local RoC SLKs. They are not claims
about an unextracted TFT patch table. Native distance and movement values are
shown in Unity as native/50.

This document concerns Saran Europe and Risk New World only. It does not use
Rome data, and it does not treat a rawcode as variant-independent: in
particular, `h00T` has different roles in Europe and New World and is excluded.

## Damage and armour matrix now used

The map members are the source for this matrix, not a runtime-code ordering.
For this audit, `war3mapMisc.txt` was directly read from both local archives:
`references/maps/Risk_Reforged_v3.0_by_Saran_OPEN_SOURCE.w3x::war3mapMisc.txt`
and `references/maps/Risk-New-World-v3.0.w3x::war3mapMisc.txt`. Both members
are 820 bytes with SHA-256
`acfc8258a69e588c925ac4605e176d48f42df45c55c03cfd49650686285592e1`.
The earlier New World audit referenced the Europe member path only; it is not
an evidence gap after this direct member comparison. No archive content is
committed.

Warcraft's eight literal columns are **Light, Medium, Large, Fortified,
Normal, Hero, Divine, None**. These are the source rows used by the maps:

| Attack | Light | Medium | Large | Fortified | Normal | Hero | Divine | None |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Normal | 1.00 | 1.50 | 1.00 | 0.70 | 0.70 | 1.00 | 0.05 | 1.00 |
| Piercing | 2.00 | 0.75 | 1.00 | 0.35 | 0.35 | 0.50 | 0.05 | 1.50 |
| Siege | 1.00 | 0.50 | 1.00 | 1.50 | 1.50 | 0.50 | 0.05 | 1.50 |
| Magic | 1.50 | 0.75 | 2.00 | 0.35 | 0.35 | 0.50 | 0.05 | 1.00 |

`ArmorKind` has five categories, so `Core/CombatRules.cs` deliberately
projects source indices `[0, 1, 2, 3, 7]`, rather than taking the first five:
Runtime **Light** maps to source Light, **Medium** to Medium, **Heavy** to
Large, **Fortified** to Fortified, and **Unarmored** to None. The resulting
runtime table is:

| Attack \ runtime defence | Light (Light) | Medium (Medium) | Heavy (Large) | Fortified (Fortified) | Unarmored (None) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Normal | 1.00 | 1.50 | 1.00 | 0.70 | 1.00 |
| Piercing | 2.00 | 0.75 | 1.00 | 0.35 | 1.50 |
| Siege | 1.00 | 0.50 | 1.00 | 1.50 | 1.50 |
| Magic | 1.50 | 0.75 | 2.00 | 0.35 | 1.00 |

`CombatTarget.ReceiveAttack` applies that projected table and then
`1/(1 + 0.06 × armour)`. The runtime has no separate Normal, Hero, Divine, or
Warcraft `Large` category; this adapter is therefore intentionally lossy, but
its five values correspond to the stated source columns.

## Current profile matrix

Damage is the rolled range before the matrix and numeric armour. `*` means a
field inherited by the W3U and resolved from the local RoC baseline in the
v0.16 audit. “Local” is intentionally not presented as a map stat.

| Runtime unit | Source evidence | HP; damage; attack/defence | Range (minimum); cooldown; speed | Counter-relevant reading |
| --- | --- | --- | --- | --- |
| Espadachín | Local adaptation | 200; 18–21; Normal/Heavy | 0.9 (0); 1.35; 5.4 | Normal gains 1.5× against Medium; it is not a Saran rawcode profile. |
| Ballestero | `h00B` ← `hrif`; W3U verifies HP 200, base 15, cost 1 | 200; 17–23*; Piercing/Light* | 8 (0); 1.60*; 5.4* | 2× versus Light and 1.5× versus Unarmored; only 0.35× versus Fortified. |
| Caballero | `h00G` ← `hkni`; W3U verifies HP 650, base 37, armour 7, cost 5 | 650; 39–47*; Normal/Heavy* | 2* (0); 1.36*; 7* | Fast Heavy body; Normal is 1.5× versus Mortar's Medium. |
| Mago | Local adaptation | 250; 30–32; Magic/Unarmored | 10 (0); 1.60; 5.4 | Magic is 2× versus Heavy; this is runtime design, not a source mage. |
| Mortero | `h00H` ← `hmtm`; W3U verifies HP 350, base 18, range 900, speed 230, cost 3 | 350; 19–31*; Siege/Medium* | 18 (5); 3.50*; 4.6 | Siege is 1.5× versus Fortified/Unarmored, but slow and unable to fire inside 5. |
| Sanador | `h00E` ← `hmpr`; W3U verifies HP 250, range 400, armour 1, cost 2 | 250; 8–9*; Piercing/Light* | 8 (0); 2.00*; 5.4* | The basic attack has the same Light/Unarmored bias; allied healing is an adaptation. |
| Marine Private | `h012` ← `hrif`; W3U verifies HP 200, range 300, armour 1, cost 1 | 200; 18–24*; Piercing/Light* | 6 (0); 1.60*; 5.4* | Same attack family as Ballestero, with two less range. |
| Marine Major | `h014` ← `hkni`; W3U verifies HP 650, base 37, armour 6, speed 280, cost 5 | 650; 39–47*; Normal/Heavy* | 2* (0); 1.36*; 5.6 | Heavy melee body, slower and one armour below Caballero. |
| Marine General | `h015` ← `hkni`; W3U verifies base 64, cooldown 1.45, armour 8, speed 280, cost 10; HP inherited | 800; 66–74*; Normal/Heavy* | 2* (0); 1.45; 5.6 | Stronger Heavy melee tier; no extra source counter category is exposed. |
| Capturable city post | `h00N`/`h00O`; audited in `RISK-RULES-v0.16.md` | 550 local shell; 46–50; Piercing/Fortified | 13 (0); 0.9; 0 | Source verifies damage, dice, cadence, and 650/50 range. Runtime health/armour shell remains local. |
| Fragata | `h00W` ← `hdes`; W3U verifies HP 400, base 30, range 1000, armour 6, speed 340, cost 5; local owned weapon row resolves Normal, 1d15, 1.5 s | 400; 31–45; Normal/Heavy runtime | 20 (0); 1.5; 6.8 | A Normal ship has no type bonus against Heavy ships. |
| Transporte | `n008` ← `nzep`; W3U verifies HP 300, speed 340, cost 2, `uacq=0`, Large defence | 300; no attack; Heavy runtime | 0; 0; 6.8 | Source has no enabled weapon. Runtime capacity 6 and Heavy mapping are local. |

`ReforgedProfiles.cs`, `NavalProfiles.cs`, and `BattleRules.cs` are the current
runtime contract. The W3U audit proves the explicitly listed HP/base/range/
armour/speed/cost values; inherited dice, type, defence, cooldown, and some HP
remain baseline-dependent as marked.

## Counters that actually emerge

These follow from the runtime matrix plus the current profiles; they are not a
claim that the roster is balanced in a full match.

- Piercing units have a real damage-type advantage into Light units: Ballestero,
  Medic, Marine Private, and city post use Piercing, while Ballestero, Medic,
  and Private are Light. The post is defensive rather than a recruitable
  counter.
- Caballero and both higher port marines use Normal; Normal's concrete matchup
  bonus is against Medium. Mortar is the present Medium target, while its
  minimum range and lower speed create the positional part of that interaction.
- The runtime Mage is Magic/Unarmored and gets 2× into Heavy. This is a current
  adaptation-only counter, since no corresponding Saran W3U mage was used.
- Mortar is Siege/Medium and gets 1.5× into Fortified. It cannot damage the
  permanent city tower in this prototype because `DefenseTower.TakeDamage` is
  intentionally empty; therefore that matrix entry is not presently a playable
  tower-killing counter.
- Numeric armour still matters after the type multiplier: armour 1, 3, 6, 7,
  and 8 multiply by approximately 0.943, 0.847, 0.735, 0.704, and 0.676.
  This reinforces Heavy tier durability but does not replace testable time-to-
  kill measurements.

There are also current, deliberately non-source combat rules: Piercing shots
have 25% uphill miss at a height gain of at least 2.5 (`CombatRules.cs:20`),
and `CombatWorld.cs:80-92` gives Magic a 2.4-radius 50% soldier splash and
Siege a 1.5-radius 35% splash. They can change practical counters beyond the
matrix and must be measured before any balance claim.

## Verified gaps and next measurements

- Imported source trees use own meshes with `solid=false`
  (`ImportedTerrain.cs:69-97`, `BiomeVegetation.cs:21-25`); `GroundCover` has
  no colliders. Forest currently neither slows nor blocks. A future woodland
  slow should use a NavMesh area/cost or movement multiplier that preserves a
  path; it needs a route and time-to-target test before implementation.
- Coast and landing are passability rules, not an attack/armour counter. Ships
  require water clearance; landing requires a land NavMesh sample and gentle
  shore (`Ship.cs:120-133`). Measure embarked, shore-unload, and coastal target
  behaviour before adding coastal modifiers.
- Projectile splash is runtime-defined as above. Test simultaneous targets,
  friendly exclusion, structures, and armour/type order before retuning radius
  or percentage.
- Warcraft `Large`, Hero, Divine, Chaos, exact acquisition rules, and remaining
  inherited TFT fields lack a lossless current mapping. Do not invent a
  multiplier for them from these maps.

No Unity test or runtime change was performed for this audit.
