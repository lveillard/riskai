# Risk Europe rules audit

This is a source audit of the local `references/wc3-risk-system` checkout. It records behavior that is present in code and tests, plus configuration that is documented but not necessarily exercised. “Risk Europe” here means the local project/map configuration; this document makes no claim that it is a Warcraft III Reforged rule set.

## Verified active rules

### Cities, guards, and capture

- A city starts hostile-neutral. Its barracks, guard, and Circle of Power are the owned objects; `changeOwner` also resets the barracks rally point. (`src/app/city/city.ts:L32-L42`, `L83-L100`.)
- City influence is a 186 world-coordinate square region. The builder registers enter/leave triggers using half-size offsets. (`src/configs/city-settings.ts:L3-L6`; `src/app/city/concrete-city-builder.ts:L155-L168`.)
- On entry, the active guard is retained if valid. Otherwise the trigger searches owned units, then allies, then enemies inside the 186 radius; an enemy candidate changes city owner and becomes guard. (`src/app/triggers/enter-region-event.ts:L17-L61`.)
- On guard departure, the replacement search is owned units first, then allies except for an un-captured capital. With no valid candidate, a dummy guard is created for the city owner. (`src/app/triggers/leave-region-event.ts:L16-L42`.)
- Guard death/replacement search uses a small radius of 235 and a large radius of 550. (`src/app/triggers/unit_death/search-radii.ts:L1-L2`; `docs/gameplay/units.md:L187-L228`.)
- Guard choice is configurable by point value and then health, each ascending or descending. Ties keep the first candidate. (`src/app/utils/guard-priority-logic.ts:L30-L75`, `L88-L95`; tests: `tests/guard-priority-logic.test.ts`.)
- Land cities reject ships; port cities have special ship/melee replacement handling. (`src/app/city/land-city.ts:L34-L41`, `L48-L60`; `src/app/city/port-city.ts:L25-L52`.)

The gameplay documentation describes the capture sequence as guard killed, attacker entering the 186-radius region, barracks/COP ownership and color update, rally adjustment, then country/income recalculation. (`docs/gameplay/cities-countries.md:L261-L291`.) The trigger implementation confirms the guard/owner half of this sequence; country aggregation is in the country/region services rather than in `City.changeOwner`.

### Countries, regions, and income

- A country owns an array of cities and one spawner. Setting country owner also sets the spawner owner and emits conquered/lost messages; resetting a country resets its spawner and owner but deliberately does not reset cities in that call. (`src/app/country/country.ts:L12-L36`, `L59-L94`.)
- Country income is the number of cities owned. Region income is a separate configured bonus applied when all countries/cities in that region are owned. (`src/app/managers/income-logic.ts:L23-L34`, `L68-L100`; `docs/gameplay/economy.md:L48-L84`.)
- Standard starting income is 4; Chaos Promode starting income is 25. A dead player is assigned income 1, a leaver 0, and a nomad 4. (`src/configs/game-settings.ts:L8-L12`; `src/app/game/game-mode/utillity/on-player-status.ts:L21-L27`, `L31-L55`, `L102-L110`, `L153-L159`.)
- Regions must contain at least one country; the builder throws otherwise. (`src/app/region/concrete-region-builder.ts:L15-L27`.)

### Rounds, spawning, and population

- Default turn duration is 60 seconds, tick duration is 1 second, and the starting countdown is 10 seconds. (`src/configs/game-settings.ts:L14-L24`.)
- Country spawners cap a player at `spawnsPerStep * SpawnTurnLimit`; `SpawnTurnLimit` is 5. Each step creates at most the remaining amount and sends new units to the spawner rally point. (`src/configs/country-settings.ts:L3-L6`; `src/app/spawner/concrete-spawn-builder.ts:L50-L65`; `src/app/spawner/spawner.ts:L67-L119`; `src/app/spawner/spawner-logic.ts:L16-L19`.)
- In FFA, an eliminated player is prevented from spawning even if the WC3 slot remains playing. Team games intentionally allow eliminated teammates’ spawners to continue. (`src/app/spawner/spawner.ts:L72-L86`.)
- The documented per-player starting-city distribution upper bound is 22. (`src/configs/game-settings.ts:L32-L34`; `src/app/game/services/distribution-service/standard-distribution-service.ts:L8-L20`.)

### Desertion, elimination, and rebellion

- A player with units but no cities enters `NOMAD`, receives income 4, and has a 60-second timer; recovery occurs if a city is retaken before the timer expires, while loss of all units or timeout becomes `DEAD`. (`src/configs/game-settings.ts:L23-L24`; `src/app/game/game-mode/utillity/on-player-status.ts:L102-L150`.)
- Death sets income to 1 and disables controls; leaving sets income to 0 and marks `LEFT`. (`src/app/game/game-mode/utillity/on-player-status.ts:L31-L55`, `L153-L195`.) Forfeit is an explicit event/command, not a random desertion rule. (`src/app/commands/forfeit.ts:L4-L12`.)
- No active `rebellion` rule was found in the audited `src` or gameplay documentation. Do not treat comments or the nominal `NOMAD` state as a rebellion mechanic.

### Diplomacy and modes

- Alliance initialization groups playing, non-observer slots by WC3 team, and locks map alliance changes after setup. A co-allied check is reciprocal; `isAllied` itself is one-way. (`src/app/managers/alliances/alliance-manager.ts:L11-L37`, `L48-L74`.)
- Creating an alliance sets passive, help, shared XP/spells/vision/control flags in both directions. (`src/app/managers/alliances/alliance.ts:L39-L45`, `L67-L93`.) The README lists host-selectable diplomacy and team/FFA play, but the source does not implement a separate treaty timer. (`README.md:L29-L35`.)
- Active mode names are Standard, Promode, Equalized Promode, W3C, and Capitals; mode resolution gives Capitals priority, then W3C, Equalized, Promode/Chaos, Standard. (`src/app/utils/game-mode-logic.ts:L11-L25`, `L47-L65`.) Capitals has a 30-second selection phase. (`src/configs/game-settings.ts:L50-L54`.)
- Equalized uses a two-round result: the first round advances, a same winner in round two records a win, and different winners produce no win. (`src/app/utils/game-mode-logic.ts:L311-L344`; tests: `tests/game-mode-logic.test.ts`.)

### Victory

- Normal victory threshold is `ceil(totalCities * 0.6)`. Overtime reduces the threshold by 1 per overtime turn by default, never below 1. (`src/configs/game-settings.ts:L4-L6`, `L26-L30`; `src/app/managers/victory-logic.ts:L17-L31`; tests: `tests/victory-logic.test.ts:L10-L54`.)
- Candidates at the threshold are included, eliminated players are excluded, and equal highest counts return a tie list. (`src/app/managers/victory-logic.ts:L54-L81`; `tests/victory-logic.test.ts:L71-L169`.)
- In Capitals, capturing an owned capital eliminates the previous owner only when the capital was active; neutral, same-owner, or ordinary-city captures do not trigger this elimination. (`src/app/utils/game-mode-logic.ts:L469-L491`; `tests/game-simulation/capitals.test.ts`.)
- W3C mode can terminate when fewer than two human players remain if its setting is enabled. (`src/configs/game-settings.ts:L53-L57`; `src/app/utils/game-mode-logic.ts:L280-L282`.)

## Configuration and comments that must not be conflated

The numeric comments in `src/configs/game-settings.ts` are active exported constants when imported. Comments describing intended defaults are not separate runtime rules. `src/configs/terrains/cities.txt` is an older/alternate terrain source and contains commented-out guard-reset code (for example around its guard reset block); it should not override the active `src/app/city` implementation. `README.md:L77` mentions a generated “Risk Europe 2.7.4-preview” filename and `L90-L103` documents launch/build commands; these identify the local project workflow, not authoritative gameplay values. The reference repository is MIT licensed; retain its copyright and license notice when reusing substantial code. (`LICENSE:L1-L21`.)

## Brief comparison with current RiskAI

RiskAI’s current prototype has a two-team, 12-town economy with 60-second rounds, 12 base income, 8 per-town income, region bonuses `{8,12,8}`, 100 population, 7-second capture, 9-town victory, towers, and four unit kinds. (`RiskAI/Assets/RiskAI/Scripts/Core/BattleRules.cs:L6-L27`, `L88-L127`.) Its capture is a timed occupancy model with contested neutral defenders and a capped multi-unit speed bonus (`L58-L84`), whereas the reference uses WC3 region triggers, explicit guard replacement, country ownership, nomad status, and configurable modes. RiskAI currently has no reference-equivalent nomad timer, capital-elimination mode, diplomacy layer, or 5-turn per-country spawner cap in this rules file.

## Four portable pure functions

These are small, engine-independent functions already separated and tested in the reference; they are candidates for reuse without porting WC3 objects:

1. `computeSpawnAmount(currentCount, maxPerPlayer, perStep)` — clamps one spawn step to the remaining cap. (`src/app/spawner/spawner-logic.ts:L16-L19`; `tests/spawner-logic.test.ts`.)
2. `selectBestGuard(candidates, settings)` — deterministic value/health priority selection. (`src/app/utils/guard-priority-logic.ts:L30-L95`; `tests/guard-priority-logic.test.ts`.)
3. `updateIncomeForCountryChange` / `updateIncomeForRegionChange` — pure delta updates for city and complete-region ownership changes. (`src/app/managers/income-logic.ts:L68-L100`; `tests/income-logic.test.ts`.)
4. `calculateCityCountWin` plus `findVictors` — threshold calculation, overtime floor, eliminated-player filtering, and ties. (`src/app/managers/victory-logic.ts:L17-L31`, `L54-L81`; `tests/victory-logic.test.ts`.)

These functions carry no Warcraft handles. Guard replacement, capture triggers, alliances, timers, and player status transitions do carry engine state and should remain behavioral references rather than direct pure-function ports.
