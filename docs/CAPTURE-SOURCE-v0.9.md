# Capture and tower source audit v0.9

Source audited: `references/maps/reforged-v3-source/war3map.j`, `war3map.w3u`,
`war3map.wts`, and `reforged-unit-stats.json`. Line references below are the
extracted JASS line numbers.

## City claim geometry and candidates

The map sets `CityCircleRange=155.00` and
`DetectAlliedDefenderCircleRadi=0.70` (`war3map.j:5455-5462`). For every city,
`RectFromCenterSizeBJ(center, 155, 155)` creates the capture rectangle and the
enter/leave triggers are registered on that rectangle
(`war3map.j:7126-7131`). This is the actual capture footprint: a square with
side 155, rather than an arbitrary building proximity test.

The enter trigger ignores only transport IDs `n007` and `n008`
(`war3map.j:17446-17456`). When the current city defender is the neutral
placeholder `n001`, is dead, or is at least 300 units from the circle center,
the entering unit is passed straight to `City Claim` and then stopped and
placed at the center (`war3map.j:17331-17341`, `17434-17443`). The source does
not make every unit near the building a claimant; the event is an actual entry
into the registered footprint.

When a defender leaves, the first replacement scan is
`155 / 0.80 = 193.75` units from the circle center. It filters to living,
non-structures and excludes `n008`, `n001`, and `n007`
(`war3map.j:17061-17096`, `17343-17352`). The owner-specific replacement scan
uses the same 193.75 range and additionally requires the circle owner's player
(`war3map.j:17102-17132`). Candidates can be moved to the center and selected
by point value or current health (`war3map.j:17134-17260`). These are defender
replacement paths, not a reason to contest a city merely by standing nearby.

The defender-death trigger is guarded so it runs only when the dying unit is
the indexed `CityDefenders[GetUnitUserData(dying)]`
(`war3map.j:17501-17507`). Its allied replacement scan uses
`155 / 0.70 = 221.4286` units from the center. It accepts living,
non-structures whose owners are allied to the circle owner and excludes only
`n008` and `n007` (`war3map.j:17508-17544`, `17802-17816`). There is no
registered-rectangle or distance-to-footprint check in this fallback. That is
the source of the “nearby unit contests” behavior: a unit outside the actual
155-side capture square can be selected after the defender dies.

If no allied unit is in that 221.43 radius, the source scans a 532-unit radius
around the dying unit for living, non-structure units owned by the killing
player, again excluding `n007` and `n008`; a living non-structure killing unit
is explicitly added (`war3map.j:17568-17596`, `17817-17838`). It ranks that
group by `GetUnitPointValue` and, depending on mode flags, current life
(`war3map.j:17605-17696`). The final takeover branch requires the selected
killer to be within 300 units of the city center and to be ground or flying
(`war3map.j:17702-17740`, `17839-17850`). `h00M` (Artillery) gets a new `n001`
claim unit; a city marker of type `h00O` has a special direct-claim path.

### Port rule

Use the registered city footprint as the only contest test: a living eligible
attacker must overlap the 155-side capture square (or enter it) before it can
contest or claim. Do not use the 193.75, 221.43, or 532 fallback scans as
generic “near building” contest radii. If defender-death replacement is
needed, keep the source's owner and living/non-structure/transport filters but
add the same footprint test. Structures, towers, transports, dead units, and
the neutral placeholder must never become ordinary contest candidates.

## Claim and defender conversion

`City Claim` is invoked with `ClaimDeadDefender` set to the city marker and
`ClaimKiller` set to the entering, surviving, or replacement unit. It requires
the city marker's user data to be positive (`war3map.j:17883-17888`) and starts
a 0.10-second per-city guard timer to prevent duplicate claims
(`war3map.j:18169-18185`). Ownership changes are only applied when the city
owner and claimant owner differ (`war3map.j:18162-18169`, `18197-18208`).
Allied units may replace a dead defender, but they should not change city
ownership.

The conversion itself is explicit (`war3map.j:18197-18207`):

1. Add the `UNIT_TYPE_ANCIENT` marker to the claimant.
2. Remove that marker and clear user data from the previous city defender.
3. Store the claimant in `CityDefenders[cityIndex]`, write the city index into
   claimant user data, and move it to the city center.
4. Kill the previous defender only when its type is `n001`
   (`war3map.j:17918-17924`, `18203-18206`).
5. For an enemy claimant, transfer the city marker and circle owner and update
   city/region ownership counters (`war3map.j:18207-18320`).

This is a unit identity conversion, not a timed occupancy meter. Preserve the
claimant unit, its current health, and its type when taking over; spawn a fresh
defender only on the source's explicit neutral/fallback branches.

## Towers and active state

The source has no `activeMode` field or JASS symbol. It also has no
`SetPlayerUnitAvailableBJ` call for `h00K` or `o000`. The only explicit tower
construction limits found in JASS are two Empire Flags (`h01G`) and twenty
Bunkers (`o000`) per player (`war3map.j:4167-4171`). Therefore an application
`activeMode` flag is an adaptation choice and must not be presented as a
source-map field. Keep construction availability separate from whether a
tower is currently allowed to fire.

Object data gives the following tower abilities:

| Raw ID | Base | Local object overrides | Abilities / source meaning |
| --- | --- | --- | --- |
| `h00K` Wall | `hwtw` | `uhpm=750`, `udef=4`, `udty=fort`, `ugol=2`, `ulum=0` | `uabi=A016` at `war3map.w3u@0x6024`; `A016` is **Open Wall**, which makes the wall passable or impassable (`war3map.wts:5678-5700`). |
| `o000` Bunker | `otrb` | `uhpm=550`, `ua1b=50`, `ua1c=1.5`, `ua1r=425`, `udef=3`, `udty=fort`, `ugol=3`, `ulum=0` | `uabi=A017,Astd,Abun,Abtl` at `war3map.w3u@0x688c`; `A017` self-destroys the building and refunds 1 gold (`war3map.wts:5922-5944`, `war3map.j:19907-19924`), while `Abtl` is Battle Stations (“nearby riflemen will come to defend this bunker”) (`war3map.wts:5642-5645`). |

The same object records also override Wall `ubld=5`, `upoi=2`, `usid=400`,
`usin=400`, `urtm=120`, and `upap=unwalkable`; Bunker `ubld=20`, `upoi=3`,
`usid=500`, `usin=500`, `urtm=120`, `upap=unwalkable`, and
`upat=PathTextures\\4x4SimpleSolid.tga`. These are object fields, not an
`activeMode` switch.

The Bunker is the only selected tower with a local attack override: 50 base
damage, 1.5-second cooldown, and 425 range. Dice count/sides, acquisition,
attack type, and speed are inherited from `otrb`; the extracted audit marks
them `INHERITED`. The Wall has no local damage, cooldown, range, dice, or
attack-type override; those values are inherited from `hwtw`. Do not fill these
unknowns with the Bunker values. The full raw rows and binary offsets are in
`docs/REFORGED-UNIT-STATS.md` and
`references/maps/reforged-v3-source/reforged-unit-stats.json`.

For the close combat adaptation, use the Bunker values above and resolve the
inherited base object only if the runtime has that Warcraft base data. Keep
Wall attack behavior separately configurable because this map object does not
provide a local attack stat. The source ability list and tech limits do not
justify turning every structure into an always-active attacker.

## Selected troop rawcodes and inheritance

These are exact local overrides from `war3map.w3u`; every field not listed is
`INHERITED` in the extracted audit. The old ID is the base object from which
those missing fields come.

| Raw ID | Name / base | Verified local overrides |
| --- | --- | --- |
| `h00B` | Rifleman / `hrif` | `uhpm=200`, `ua1b=15`, `ugol=1`, `ulum=0` |
| `h013` | Army Private / `hrif` | `uhpm=200`, `ua1b=15`, `ugol=1`, `ulum=0` |
| `h00G` | Knight / `hkni` | `uhpm=650`, `ua1b=37`, `udef=7`, `ugol=5`, `ulum=0` |
| `h00H` | Mortar / `hmtm` | `uhpm=350`, `ua1b=18`, `ua1r=900`, `uacq=900.0`, `umvs=230`, `ugol=3`, `ulum=0` |
| `h00E` | Medic / `hmpr` | `uhpm=250`, `ua1r=400`, `uacq=400.0`, `udef=1`, `ugol=2`, `ulum=0` |
| `h00F` | Elite Rifleman / `hrif` | `uhpm=450`, `ua1b=36`, `ua1c=1.0`, `ua1r=350`, `udef=1`, `ugol=6`, `ulum=0` |
| `h00J` | Army General / `hkni` | `ua1b=55`, `ua1c=1.4500000476837158`, `udef=10`, `ugol=10`, `ulum=0` |

For these rows, `ua1t` attack type and any field shown as `INHERITED` remain
unknown at the custom-object level. The source audit intentionally does not
claim a resolved armor, attack type, dice, range, or defense type when the
custom object does not override it. The corresponding exact override offsets
are listed in `docs/REFORGED-UNIT-STATS.md` (for example `h00B` at
`0x525/0x592`, `h00G` at `0x82c/0x89f/0x900`, and `h00J` at
`0xd44/0xe2d/0xdb7`).
