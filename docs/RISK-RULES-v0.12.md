# Warcraft Risk rules audit for v0.12

This is a source audit and an adaptation brief. It keeps the three Warcraft map
families separate: values under **Saran** come from the locally extracted
Reforged v3.0 map, **New World** comes from VoR's separate protected v3 map,
and **Rome** is an author page/visual reference without a local script or
object-data extraction. The checkout under `references/wc3-risk-system` is a
separate TypeScript implementation and is labelled as such below.

## Provenance and map scale

| Family | What is available | Source facts useful to v0.12 | Use in the prototype |
| --- | --- | --- | --- |
| Saran, Risk Reforged v3.0 | Full extracted `war3map.j`, `war3map.w3u`, WTS and placement audit. Archive SHA-256 is recorded in [`RISK-MAPS-v0.10.md`](RISK-MAPS-v0.10.md). | `AmountOfRegions=69` (`war3map.j`, lines 5454-5455); 212 city placements across those regions ([`RISK-MAPS-v0.10.md`](RISK-MAPS-v0.10.md), lines 16 and 44); Europe/Mediterranean geography (`war3map.j`, lines 6124-6193). | Primary rules and stat authority for capture, income, spawns, towers and the port roster. |
| VoR, Risk - New World v3.0 | Protected archive plus extracted JASS placement calls. | 293 assigned cities in 100 named regions, plus one `h00D` special spawn center; 298 placement calls total ([`RISK-MAPS-v0.10.md`](RISK-MAPS-v0.10.md), lines 19-23). The JASS hash is recorded with the derived placement JSON. | Separate World geometry. Do not mix its region count or mode defaults into Saran's map. |
| Reforged: Rome (RX3) | Public author page and supplied screenshots; no local `war3map.j`/`w3u`. | The author page describes victory at 65% (142/216 cities) and province adjacency/income. This is a variant reference, not an extracted rule. [Hive Workshop: Risk Reforged: Rome](https://www.hiveworkshop.com/threads/risk-reforged-rome.323779/) | Direction only. Do not use its 65% threshold, Galley numbers or roster as Saran facts. |
| WC3 Risk System checkout | TypeScript source and gameplay docs, independent of both Warcraft map archives. | Europe 81 countries/233 cities/46 ports, Asia 82/229/32, World 212/555/74 (local `references/wc3-risk-system/docs/gameplay/maps.md`, lines 25-44). Victory is `ceil(totalCities*0.6)` (`game-settings.ts`, `CITIES_TO_WIN_RATIO`). | A useful comparison for a no-Capitals prototype mode; it is not evidence about Saran's JASS. |

The public archive listings also keep the Saran and New World families
separate: [Risk Reforged archive](https://maps.w3reforged.com/maps/categories/risk/risk-reforged)
and [Risk - New World archive](https://maps.w3reforged.com/maps/categories/risk/Risk%20-%20New%20World).

### Owned-disc baseline data

On 2026-09-06 I inspected the user's Reign of Chaos ISO read-only. The bundle's
`ReadMe.nfo` identifies the included Frozen Throne patch as 1.21b. The Disk1
ISO SHA-256 is `08BEA4DCD81DFAFA5CE52C18063835448AE88D7F851981C3BACADF0C9C681BD1`;
Disk2 is `4FF413EDC38ABF2C473069F67600BFAAADCD0CDA8F07FEB27C5047DEEB300086`.
Disk1's CDFS volume is labelled `Warcraft III`; its data archive is `War3.mpq`,
and the relevant records are `Units\\UnitData.slk`, `Units\\UnitBalance.slk`, and
`Units\\UnitWeapons.slk`. Isolated copies are kept under the ignored
`references/owned-disc-data/` directory: `RoC-Units-UnitData.slk`
(`0699D0B04196E12EBAD2DB03FB0C0E2CE9C0430341D1395E5506BB5F374D17BC`),
`RoC-Units-UnitBalance.slk`
(`0DA90B14067E5B9F28FFCD58D8AB078B8D995BD5DBD9F9B04A4D18E3ED2006AB`), and
`RoC-Units-UnitWeapons.slk`
(`099E0A134D097F03469FA3A6A187FE3D4006D454FB22C8187BEAAD312C2D3B94`). No
installer, CD-key/license file, art, or audio was opened or copied. The Frozen
Throne ISO exposes `Setup.mpq`; its install archive did not provide directly
addressable SLK records without running an installer, so TFT-only transport
inheritance remains unresolved here. The extracted tables therefore document
the RoC base records bundled on Disk1; the included 1.21b patch was not run.

After the disk-read issue was resolved, StormLib extracted the 1.21b patch's
embedded `Patch_War3x.mpq` (executable offset 167936). Its root-level
`UnitData.slk`, `UnitBalance.slk` and `UnitWeapons.slk` contain delta payloads,
with a 29-byte wrapper followed by `BSDIFF40`, rather than complete SLK tables.
Standard BSDIFF decoding and a synthetic base/patch mount did not reconstruct
verified TFT records. No numeric runtime values were inferred from those bytes.
The remaining useful reference is an installed TFT base plus its applied patch.
The intermediate data is ignored under `references/owned-disc-data/`; no
installer or patch executable was executed.

The following are RoC base records. Damage is the base dice range after the
listed dice-plus value; map `war3map.w3u` overrides remain authoritative for
the custom `h00*` objects in the table below.

| Base ID | Role | HP | Armor/type | Weapon | Cooldown / range | Move speed | Gold / points |
| --- | --- | ---: | --- | --- | ---: | ---: | ---: |
| `hfoo` | Footman | 420 | 2 / Medium | Normal 12-13 | 1.35 s / 90 | 270 | 160 / 100 |
| `hrif` | Rifleman | 520 | 0 / Small | Pierce 18-24 | 1.60 s / 400 | 270 | 240 / 100 |
| `hkni` | Knight | 800 | 6 / Large | Normal 21-29 | 1.36 s / 100 | 350 | 290 / 100 |
| `hmtm` | Mortar Team | 380 | 0 / Medium | Siege 52-64 | 3.50 s / 1000 | 220 | 210 / 100 |
| `hmpr` | Priest/Medic base | 220 | 0 / Small | Pierce 8-9 | 2.00 s / 600 | 270 | 160 / 100 |
| `hgtw` | Guard Tower | 500 | 5 / Fortified | Pierce 23-27 | 0.90 s / 700 | structure | 140 / 100 |
| `hwtw` | Scout Tower | 500 | 5 / Large | no base weapon | — | structure | 80 / 100 |
| `otrb` | Orc Burrow | 600 | 2 / Medium | Pierce 34-41 | 4.00 s / 700 | structure | 170 / 100 |

The Saran map's custom IDs inherit selectively from these bases: `h00B`
(`hrif`) changes HP to 200, base damage to 15 and gold/point to 1/1;
`h00G` (`hkni`) changes HP to 650, damage to 37 and armor to 7;
`h00H` (`hmtm`) changes HP to 350, damage to 18 and range to 900; and
`h00E` (`hmpr`) changes HP to 250, range to 400 and armor to 1. Their
unlisted fields depend on the effective Warcraft installation and patch. These
RoC records are a historical comparison, not a verified resolution of TFT or
Reforged inheritance. The runtime retains the previously documented historical
SLK baseline and labels local tuning separately.

`h00W` and `n008` are TFT-era map objects, not present in the RoC SLKs above.
The local map overrides `h00W` to 400 HP, 30 base damage, 1000 range, 6 armor,
340 movement speed and gold/point 5/5; it overrides `n008` to 300 HP, 340
movement speed and gold/point 2/2. Their unlisted cooldown, weapon-type,
capacity and other fields remain inherited or product-defined; do not infer
them from the RoC infantry tables.

## Defender death and capture succession

The exact Saran v3 flow is in
`references/maps/reforged-v3-source/war3map.j`:

| Step | Extracted behavior | Evidence |
| --- | --- | --- |
| Trigger | The death trigger runs only when the dying unit is the registered `CityDefenders[index]`. | `war3map.j`, lines 17501-17505 |
| Nearby successor | Searches allies of the current circle owner inside `CityCircleRange / DetectAlliedDefenderCircleRadi = 155/.70 ≈ 221.43`; candidate must be alive, non-structure, and not `n007`/`n008`. | `17508-17541`, `17799-17815` |
| Candidate order | It starts with a random group member, then the configured “most valuable” point-value or “most damaged” health preference replaces that member. It is **not nearest-distance selection**. | `17664-17700`, `17799-17815` |
| Outside successor | If the nearby group is empty, it searches a 532-radius group around the dying defender, filtered to the killing unit's owner, alive/non-structure, excluding `n007`/`n008`; the killing unit is added only when alive, non-structure and within 300. | `17817-17849`, `17733-17737` |
| Empty result | It creates neutral dummy `n001` at the circle, owned by the dying defender. Special shipyard/ground branches can call `City Claim` for the selected unit. | `17827-17831`, `17839-17855` |
| Transfer | `City Claim` replaces the defender, moves the successor to the circle, transfers city and circle ownership, updates country counters, and only removes the old temporary `n001` where applicable. | `18169-18320` |

The v0.12 runtime policy is intentionally simpler and deterministic: retain a
living bound defender; when it is gone, select the nearest eligible owner-team
unit within the protection radius before considering the nearest eligible enemy
within takeover radius; use EntityId as the equal-distance tie-break; make the
post empty result neutral. This is a product adaptation of the source flow,
not a claim that Saran's group priority was nearest distance. Candidate
filters must continue to exclude structures, transports and garrisoned units.

Saran's setup values are `CityCircleRange=155` and detection ratio `.70`
(`war3map.j`, lines 5459-5461). The v0.12 Unity circle/protection radii are separate
prototype units and should not be presented as Warcraft map coordinates.

## Buildings, towers and attack values

The extracted object fields are compact raw overrides. `Gold` below is the
object's `ugol` gold-cost field; `Point` is the separate `upoi` point-value
field used by bounty, guard-priority, and reinforcement accounting. Neither
should be treated as a prototype shop rule unless a shop cost is separately
specified.

| Raw ID | Role/name | HP | Base damage | Cooldown | Range | Armor | Attack/defense | Gold (`ugol`) | Point (`upoi`) |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |
| `h00B` | Rifleman defender | 200 | 15 | inherited | inherited | inherited | inherited | 1 | 1 |
| `h00K` | Wall tower | 750 | inherited | inherited | inherited | 4 | inherited / Fortified | 2 | 2 |
| `o000` | Bunker tower | 550 | 50 | 1.5 | 425 | 3 | inherited / Fortified | 3 | 3 |
| `h01G` | Empire Flag support structure | 1000 | inherited | inherited | inherited | 1 | inherited / Fortified | 12 | 2 |
| `h00E` | Medic | 250 | inherited | inherited | 400 | 1 | inherited | 2 | 2 |
| `h00F` | Elite Rifleman | 450 | 36 | 1.0 | 350 | 1 | inherited | 6 | inherited |
| `h00G` | Knight | 650 | 37 | inherited | inherited | 7 | inherited | 5 | 5 |
| `h00H` | Mortar | 350 | 18 | inherited | 900 | inherited | inherited | 3 | 3 |
| `h00I` | Roarer | 400 | 29 (3 dice) | inherited | 500 | 1 | inherited | 4 | inherited |
| `h00J` | Army General | inherited | 55 | 1.45 | inherited | 10 | inherited | 10 | inherited |
| `h00M` | Artillery | 900 | 55 (13 dice) | 3.0 | 1000 | 3 | Pierce / None | 15 | inherited |
| `h00Y` | Tank A | 1500 | 75 | 1.8 | 500 | 14 | inherited | 25 | inherited |
| `h01A` | Tank | 1500 | 80 (11 dice) | 1.8 | 500 | 9 | Siege / Fortified | 25 | inherited |

All rows above are from [`REFORGED-UNIT-STATS.md`](REFORGED-UNIT-STATS.md),
lines 27-120; that report retains each `war3map.w3u` field and binary offset.
`INHERITED` means the
map did not override that field; it must not be interpreted as zero. The map's
damage multipliers are in the same report's matrix (`DamageBonusPierce`,
`DamageBonusSiege`, etc.).

Cities `h00N`/`h00O` configure 45 piercing damage, one die/five sides, `.9`
cooldown and 650 range, but retain `Avul`. The RoC archive has no `hbar` row in
`UnitWeapons.slk`, so the extracted map fields alone do not prove active
automatic fire; treat that behavior as unresolved until the TFT/map object or
ability data is available. They are invulnerable capture markers, and the
extracted capture paths transfer them after defender death or entry rather than
destroying them (`RISK-MAPS-v0.10.md`, lines 50-54). The Wall has no local
attack override. The Bunker has the 50/1.5/425 profile and its `A017`
self-destruct/refund action awards 1 gold in `war3map.j`, lines 19904-19918.

For the local prototype, keep these source profiles separate from balancing
tuning. In particular, the Bunker profile is a fortified 50-damage source
reference; a stronger three-to-four-hit guard tower can be a named local
profile around 80–90 damage after the project's damage multipliers, without
rewriting the extracted Saran numbers or inventing a source “three-hit” rule.

## Gold, costs and reinforcements

Saran v3 defaults are Conquest, 60% city victory, bounty divisor `4.00`, spawn
limit `5`, 60-second turns, first/basic income `4`, multiplier `1.00`, and no
city defeat at 100 (`war3map.j`, lines 5565-5594; summarized in
[`RISK-REFORGED-RULES.md`](RISK-REFORGED-RULES.md), lines 13-14). On an eligible enemy
death, the trigger adds `GetUnitPointValue(dying) / ModesBounty` to the killer
player's fractional bounty bank and pays the integer part, retaining the
fraction (`war3map.j:18556-18753`). Thus a 1-point Rifleman is a quarter gold
before carry; a 3-point Bunker is three quarters; a 4-point total pays one.
The source does not expose a separate naval kill bonus or upkeep formula.

`ugol` (gold cost) and `upoi` (point value used by bounty, guard priority and
reinforcement caps) are distinct Warcraft fields. The selected runtime IDs
happen to override both to the same values: `h00B=1/1`, `h00E=2/2`,
`h00G=5/5`, `h00H=3/3`, `h00W=5/5`, `n008/n009=2/2`, and `n007=6/6`
(each pair is `ugol/upoi`; `war3map.w3u` offsets are retained by the extracted
object parser). This coincidence must not become an API assumption: for
example `h01G` is `ugol=12` but `upoi=2`, and future roster entries can diverge.
Keep a separate point-value lookup when awarding bounty or counting live
reinforcements. The current prototype's Frigate/warship 5 and Transport 2 were
chosen from the corresponding map `ugol` values; they are product costs and do
not prove the original shop transaction or full roster pricing.

Country completion is the economic/control boundary in Saran: a country is
complete when `PlayerCitiesOwnedInRegions == RegionCityAmounts`; the region
spawn center is transferred to the completing owner
(`war3map.j:18081-18095`, `18243-18254`). Losing the required ownership
neutralizes the region owner and spawn center (`18301-18310`). In the loaded
normal Conquest branch, turn income is basic income 4 plus the number of cities
in each fully controlled country; a fragmented country does not contribute its
partial city count. The branch requires one owner for the country group and
adds `PlayerCitiesOwnedInRegions` (`war3map.j:18944-19004`), then applies basic
income and the multiplier (`19161-19197`). The source's comment saying
“Region Income (not implemented)” does not describe this active branch
(`RISK-REFORGED-RULES.md`, lines 38-42). The British Isles special region is a
separate example with one generated unit and +2 income
(`war3map.j:5414-5441`); it is not the ordinary country cap.

Regional reinforcement is at the configured country spawn center, using
`h005` as the recruitment/spawn structure and `h00B` as the normal defender
(`war3map.j:5455-5458`, `5424-5434`). The normal cap is
`RegionCityAmounts * ModesSpawnLimit` in **alive point value**, not five turns
or five unit objects; creation increments by `GetUnitPointValueByType` and
death decrements by `GetUnitPointValue`
`A_SPAWNS_ALIVE_VALUE[player*100+region]`
(`war3map.j:19374-19533`, death decrement `18753`).
For the default `h00B` Rifleman, the extracted `upoi` is 1, so a city-count
cap currently equals a unit-count cap; preserve the point-value calculation if
the spawn roster changes.

## Cities versus ports

Saran's exact production lists are in [`REFORGED-UNIT-STATS.md`](REFORGED-UNIT-STATS.md), lines 13-18:

* `h00N` normal city trains 20 entries: `h017,h016,h013,h011,h010,h00Z,h00F,h00Y,h01C,h018,h019,n003,h00J,h00B,h00G,h00H,h00I,h00E,h00M,h01A`.
* `h00O` shipyard/port trains 17 entries:
  `h015,h014,h012,h00X,h006,h00L,h00P,h00Q,n009,n008,n007,h00U,h00W,h001,h00R,h00T,h00S`.
* `h004` and `h007` country spawners train `h013,h00B`; `h01D` trains `n00B`.

The port list mixes marines, transports, warships and mode/building variants;
it should not be copied wholesale into the v0.12 shop. A bounded prototype
roster is **Frigate/warship profile plus Transport**, with one optional cheap
escort after combat balance is measured. Keep transport capacity at the
project's six-unit rule: Saran proves a cargo interface and a ten-iteration
load order, but no numeric capacity (`REFORGED-NAVAL.md`, “Carga, movimiento y
desembarco”). A port itself never earns a separate gold payout and a transport
never claims a city; a landed eligible troop does.

## Victory and “no capitals” mode

Saran's operational default computes the target from `ModesSubGamemode` and
`AmountOfCities` (`war3map.j`, lines 8348-8398); the loaded default is 60%, so
the target is the ceiling of 60% of all cities: 128 of 212 Saran cities. The
New World archive's city count is documented separately, but its victory mode
was not verified from an extracted JASS source in this audit. The 65%/“Classic”
text is a different preset and must not be combined with the loaded Conquest
default. For v0.12's no-Capitals mode, use the verified Saran city threshold
and do not add a capital selection phase or capital victory condition. The
independent WC3 Risk System reaches the same 60% city formula and treats
Capitals as a separate mode (`game-settings.ts`), which is useful corroboration
only.

No map assets are copied by this audit. Raw IDs, fields, JASS line references,
and adaptation recommendations above are sufficient for the current prototype
rules; Unity navigation and authoritative multiplayer determinism remain
separate implementation concerns.
