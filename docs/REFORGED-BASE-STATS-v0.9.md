# Risk Reforged inherited base stats v0.9

This is a read-only inheritance audit for the Saran v3 source extracted at
`references/maps/reforged-v3-source/`. The custom-object JSON deliberately marks
unspecified combat fields as `INHERITED`; this note resolves those fields only
against the historical Blizzard reference tables. It does not assert that the
tables match any current Warcraft III patch.

## Field and unit rules

The relevant object fields are `ua1b` (damage base), `ua1d` (number of dice),
`ua1s` (sides per die), `ua1c` (cooldown), `ua1r` (attack range), `ua1t`
(attack type), `udef` (numeric armor), and `udty` (defense/armor type). The
extracted JSON is the authority for which fields the map overrides; see
[`CAPTURE-SOURCE-v0.9.md`](CAPTURE-SOURCE-v0.9.md) for the exact custom values.

Blizzard's [Human Unit Stats](https://classic.battle.net/war3/human/unitstats.shtml)
and individual [Rifleman](https://classic.battle.net/war3/human/units/rifleman.shtml),
[Knight](https://classic.battle.net/war3/human/units/knight.shtml), and
[Priest](https://classic.battle.net/war3/human/units/priest.shtml) pages show
range in the compact UI scale (`40`, `60`, `Melee`). The [Orc Burrow page](https://classic.battle.net/war3/orc/buildings/orcburrow.shtml)
shows `70`. Warcraft object data uses engine distance values such as the map's
explicit `350`, `400`, `425`, and `900`; for ordinary ranged units the practical
conversion is approximately page value × 10 (40 → 400). Treat the conversion as
an implementation convention, not a claim that the web pages document the raw
`ua1r` number. The map's explicit range always wins.

The Blizzard pages do not expose `ua1d` and `ua1s`. A public `wc3libs` test
fixture contains the extracted
[`UnitWeapons.slk`](https://github.com/inwc3/wc3libs/blob/master/src/test/resources/slks/UnitWeapons.slk)
(file commit `bf228c9`, June 2018). Parsing its named rows gives the useful raw
dice values below: `hrif=2d4`, `hkni=2d5`, `hmtm=1d13`, `hmpr=1d2`, and
`otrb=1d8`. This is a legitimate public data extraction, but its game build is
not identified as the Saran map's base build. Use these dice as **medium
confidence inheritance candidates**, not as proof of a current patch match.

The same fixture confirms the SLK column meanings documented by
[OpenWarcraft3](https://github.com/Lyraedan/openwarcraft3/blob/main/games/warcraft-3/docs/file-formats/slk.md),
but its old cooldown/range/defense rows conflict with some newer Blizzard HTML
pages. For those fields this note keeps the primary page value when it is
available and keeps an explicit Risk map override authoritative.

For traceability, the fixture rows themselves parse as follows: `hrif` range
400, cooldown 1.5, 2d4, 18–24; `hkni` range 100, cooldown 1.5, 2d5,
27–35; `hmtm` minimum range 250, range 1150, cooldown 3.5, 1d13, 52–64;
`hmpr` range 600, cooldown 2, 1d2, 8–9; and `otrb` range 700, cooldown 4,
1d8, 34–41. These are fixture-version values, not replacements for the
primary-page row when the two disagree.

## Historical base rows

The values below are the unupgraded values printed by the Blizzard pages; an
asterisk on those pages denotes an upgrade value. “Known” means printed
directly on the primary page. “Inherited” means the map did not override it;
it is not a claim that the extracted map contains the base row.

| Base rawcode | Blizzard page values relevant to this port | Confidence |
|---|---|---|
| `hrif` Rifleman | attack **Pierce**, weapon **Instant**, defense type **Medium**, numeric armor **0**, ground/air **21 average**, cooldown **1.5**, page range **40** (60 with Long Rifles); fixture dice **2d4** | High for page fields; medium for fixture dice |
| `hkni` Knight | attack **Normal**, weapon **Normal**, defense type **Heavy**, numeric armor **5**, ground **34 average**, cooldown **1.4**, range **Melee**; fixture dice **2d5** | High for page fields; medium for fixture dice |
| `hmtm` Mortar Team | attack **Siege**, weapon **Artillery**, defense type **Heavy**, numeric armor **0**, ground **58 average**, cooldown **3.5**, page range **25 minimum / 115 maximum**; fixture dice **1d13** | High for page fields/dice family; raw min-range behavior remains version-sensitive |
| `hmpr` Priest | attack **Magic**, weapon **Missile**, defense type **Unarmored**, numeric armor **0**, ground/air **8.5 average**, cooldown **2**, page range **60**; fixture dice **1d2** | High |
| `otrb` Orc Burrow | attack **Piercing**, defense type **Heavy/Fort**, numeric armor **5**, page damage **23–27**, cooldown **2** or **0.8** with garrison speed-up, page range **70**; fixture dice **1d8** | High for page fields; medium for fixture dice and raw building cooldown |

The same Blizzard pages report historical HP values (Rifleman 535, Knight 835,
Mortar 360, Priest 290, Burrow 600), but those are included only as a sanity
check. The Risk map overrides or inherits HP per unit independently; they are
not a request to replace the map's custom HP.

## What the selected map units actually inherit

This table combines the JSON's `INHERITED` markers with the base rows above.
Values in the “map override” column are already present in the extracted map
and must take precedence over the historical page value.

| Map rawcode → base | Map overrides | Inherited result usable for a port | Confidence |
|---|---|---|---|
| `h00B` / `h013` → `hrif` | `uhpm=200`, `ua1b=15` | inherited **2d4**; `ua1c=1.5`; range base 40 page-scale (roughly 400 engine units unless an upgrade applies); `udef=0`; `ua1t=Pierce`; `udty=Medium` | High for type/cooldown/armor; medium for fixture dice and converted range |
| `h00G` → `hkni` | `uhpm=650`, `ua1b=37`, `udef=7` | inherited **2d5**; `ua1c=1.4`; `ua1r=Melee`; `ua1t=Normal`; `udty=Heavy` | High for page fields; medium for fixture dice |
| `h00H` → `hmtm` | `uhpm=350`, `ua1b=18`, `ua1r=900`, `uacq=900`, `umvs=230` | inherited **1d13**; `ua1c=3.5`; base attack `Siege`; base defense `Heavy`; active map range explicitly **900** | High for type/cooldown/override; medium for dice |
| `h00E` → `hmpr` | `uhpm=250`, `ua1r=400`, `uacq=400`, `udef=1` | inherited **1d2**; `ua1c=2`; base attack `Magic`; base defense `Unarmored`; active map range **400** | High |
| `h00F` → `hrif` | `uhpm=450`, `ua1b=36`, `ua1c=1`, `ua1r=350`, `udef=1` | inherited **2d4**; attack `Pierce`; defense type `Medium` | High for type; medium for fixture dice |
| `h00J` → `hkni` | `ua1b=55`, `ua1c=1.4500000477`, `udef=10` | HP inherited from base row (page 835); inherited **2d5**; range `Melee`; attack `Normal`; defense type `Heavy` | High for page fields; medium for fixture dice and exact inherited HP version |
| `o000` → `otrb` | `uhpm=550`, `ua1b=50`, `ua1c=1.5`, `ua1r=425`, `udef=3`, `udty=fort` | inherited **1d8**; attack `Piercing`; active map cooldown **1.5** and range **425**; map defense type **Fort** overrides the base label | High for explicit map row and attack type; medium for fixture dice |

`h00B` and `h013` therefore inherit the same `2d4` weapon dice as `hrif`;
changing `ua1b` from the base to 15 does not change their inherited dice. The
same rule applies to every other row: a custom `ua1b` changes only the base
component unless `ua1d` or `ua1s` is also written.

## Source and version limits

The Blizzard HTML pages are authoritative for the values printed on those
pages, but they are historical reference material. The older
[Human-units PDF](https://classic.battle.net/war3/pdf/humanunits.pdf) contains
different values for some rows, so it was not used to manufacture a “current
patch” claim. No downloadable Risk Reforged: Rome `.w3x` with verified object
data was available in this bounded pass; the public [RX3 thread](https://www.hiveworkshop.com/threads/risk-reforged-rome.323779/)
is a description/screenshots page and does not supply the map attachment.

For a final exact combat port, obtain the matching game's
`Units\\UnitWeapons.slk` (or inspect the same base rawcodes in the World
Editor) to settle the version-sensitive raw range/min-range and building
cooldown rows. The fixture dice are the best public inheritance evidence found
in this pass; the explicit Risk map overrides remain authoritative.
