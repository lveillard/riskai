# Initial economy and garrison audit (v0.13)

This is a read-only source audit for the local Warcraft III **Risk Reforged v3.0 by Saran** extraction and the Unity v0.12/v0.13 prototype. It separates facts from `references/maps/reforged-v3-source/war3map.j` from local product choices. The source map is the Saran Europe/Mediterranean family: 69 countries/regions and 212 cities, of which 168 are `h00N` normal cities and 44 are `h00O` shipyard/port cities (`docs/RISK-MAPS-v0.10.md:1-3,16-18,34-44`).

## Confirmed source start state

The central correction is confirmed: the source does not begin with player armies. Setup creates one normal Rifleman defender (`h00B`) at every city, then transfers that defender to the city owner when ownership is assigned. `StartingDefenderNormal` and `StartingDefenderShipyard` are both `h00B` (`war3map.j:5454-5458`); the city setup creates one defender (`war3map.j:7062-7078`), and the assignment paths transfer the same defender (`war3map.j:8838-8846`, `8984-8993`). Therefore the source start roster is:

| Source object | Count / rule | Ownership at the end of setup |
| --- | --- | --- |
| City defender | **1 `h00B` Rifleman per city**; 212 total on this map | Same owner as its city, including neutral-aggressive cities |
| Normal city (`h00N`) | 168 city buildings | City owner |
| Port/shipyard city (`h00O`) | 44 city buildings; **still exactly 1 `h00B`**, not a second garrison | City owner |
| City circle (`n000`) | 1 circle per city; 212 total | Player owner for player cities; neutral-aggressive for neutral cities (`war3map.j:7062-7075`, `8838-8845`, `8984-8993`) |
| Country recruitment center (`h005`) | One dynamically created center for each configured country spawn region; source has 69 regions (`war3map.j:7032-7044`, `5454-5458`) | Neutral during setup; transferred only when that country is complete (`war3map.j:8897-8909`) |
| British Isles special spawner (`h00D`) | 1 static special-region spawner, fed by four country centers | Special-region route; it produces one `h00W` unit and gives +2 income when its grouped centers satisfy the special rule (`war3map.j:5424-5440`, `19271-19285`) |
| Initial boats (default Conquest) | **0** | None; port buildings do not grant boats. The separate Mode 5 special setup creates `h00U`/`h001` warships for its hard-coded factions (`war3map.j:4374-4379`, `4442-4453`). |

The source placement inventory contains only 212 `h00N`/`h00O` city buildings, 24 player start locations, four `n00A` scene units, and one `h00D`; it contains no ships (`docs/RISK-MAPS-v0.10.md:34-44`). `h00O` means the city has a shipyard production list; it is not an initial boat. The source default enables the full ship roster (`ModesShips=0`, `war3map.j:5563-5577`), but ships are subsequently trained/bought through the city/shipyard system. Mode 5 is an explicit exception with its own hard-coded warship grants; it is not the default Conquest start.

“Per country” therefore means one Rifleman for every city in that country, not one army or one Rifleman per country. Country totals follow that country's city count. The exact 69-country city counts and port counts are in `data/derived/risk-reforged-v3-placements.json`; the source totals are 212 defenders, 44 port-city defenders, and 168 normal-city defenders. A completed country receives its own `h005` recruitment center; its center is not a starting troop.

## Source ownership, randomization, and neutral circles

The loaded default is Conquest (`ModesMainGamemode=0`), 60% city victory, FFA, `ModesFillCities=false`, 60-second turns, and first/basic income 4 (`war3map.j:5563-5587`). During game start, the applicable city set is put into `CitiesRandomizingUnitGroup` (`war3map.j:8362-8406`). The later `Assign_bases` trigger computes integer division by active player count (`war3map.j:8408-8420`), repeatedly chooses a city with `GroupPickRandomUnit`, and transfers the city, circle, and defender to that player (`war3map.j:8935-8977`). The unassigned remainder is transferred to Neutral Aggressive, including its circle and defender (`war3map.j:9148-9158`, `8984-8993`).

For the default 2-player, 212-city case, `floor(212 / 2) = 106` cities per player and no remainder. For a city count or player count that does not divide evenly, the remainder is neutral-aggressive. With ten or more active players, the map sets `ModesFillCities=true` (`war3map.j:5604-5606`), which activates additional fill-city handling; it is still a source mode choice, not a universal “one country per player” rule.

The JASS source exposes no match seed or seed timing for this allocation. `GroupPickRandomUnit` delegates to Warcraft III's engine RNG; the extracted script does not set a numeric seed. Thus a Unity integer seed can provide reproducibility, but it is a deliberate adaptation and cannot be described as the original RNG.

Other source ownership modes are distinct:

* **Russia** (`ModesMainGamemode=1`) sets `CitiesSetupAmountPerPlayer=1` in the ordinary path (`war3map.j:8421-8424`), so each active player receives one random city and the rest remain neutral-aggressive.
* **Mode 5** assigns seven hard-coded `CountryGroupsMode5` groups to `AMode5Players`; this is country-group ownership, but it is not a seeded shuffle of arbitrary countries (`war3map.j:8996-9000`, `9119-9133`).
* **Capitals** are a separate mode/phase. Capital selection removes one random city per region later (`docs/REFORGED-ALLOCATION.md:15`; source capital trigger), so capital selection must not be folded into the default Conquest start.

## Source gold, income, bounty, and recruitment

At match start, `Post_Start_Init` sets `CurrentTurn=1` and immediately runs `Income_Give` (`war3map.j:4430-4488`). On turn 1 the income function sets the player's gold to `ModesFirstIncome`, which is 4 in the default Conquest preset (`war3map.j:19092-19097`, `19161-19170`, `5563-5587`). Each later 60-second turn uses basic income 4 plus the number of cities in fully controlled countries in normal Conquest; a fragmented country contributes no partial country income (`docs/RISK-RULES-v0.12.md:159-208`; source income paths `18768-18904`, `19161-19180`). Income is paid at turn end (`war3map.j:4768-4789`).

The source bounty divisor is 4. On an eligible unit death it adds `GetUnitPointValue(dying) / ModesBounty` to a fractional bank and pays whole gold while retaining the fraction (`war3map.j:18689-18703`). The source `h00B` point value is 1, so four Rifleman points yield one gold. No active source upkeep formula was found; a sound/reference to upkeep is not evidence of a cost (`docs/RISK-REFORGED-RULES.md:13-19`).

Normal country recruitment has three separate conditions: the country must be complete and its spawn center must be owned by a non-neutral player (`war3map.j:8897-8909`, `19354-19375`); recruitment begins at each turn end (`war3map.j:4768-4783`); and the live point-value cap is `RegionCityAmounts * ModesSpawnLimit`, with default `ModesSpawnLimit=5` (`war3map.j:19378-19414`). In default FFA, a completed country queues `ceil(cityCount / 2)` recruits at turn end (`war3map.j:19334-19407`); `Recruit_Step` emits one `h00B` Rifleman per step at the country spawn region until the queue or cap is exhausted (`war3map.j:19488-19524`). A Rifleman's point value is 1, so the default cap is also `5 * cityCount` living Riflemen for that country. Losing a city/country removes the country owner and stops that country's reinforcement stream (`war3map.j:18301-18310`).

The British Isles special region is an exception: its four grouped centers feed one `h00D` spawner, which creates one `h00W` and grants +2 income when the special condition is met (`war3map.j:5424-5440`, `19271-19285`). It is not an ordinary country bonus and should not be applied to every country.

## Unity v0.12 snapshot and v0.13 corrections

The old v0.12 setup and the current v0.13 setup must be kept separate:

* **v0.12 snapshot:** the earlier `RiskBootstrap` created two team-2 units at each neutral city and capital/non-capital army piles; the earlier `NavalWorld` granted one Transport and one Galley per team and moved existing units to ports. Those counts belong to the v0.12 validation snapshot, not to the current start rule.
* **v0.13 current start:** `RiskBootstrap.Awake` creates exactly **one Archer-equivalent defender per city**, for both player-owned and neutral cities, and binds it through `InitializeGarrison` (`RiskAI/Assets/RiskAI/Scripts/RiskBootstrap.cs:35-44`). `NavalWorld.Initialize` creates five mainland plus `MapLayout.Islands.Length` island posts (2 on Las Marcas, 3 on Cuatro Riberas), assigns their owners with the seeded local allocation, and creates exactly **one Archer per post**, using team 2 for neutral posts (`RiskAI/Assets/RiskAI/Scripts/NavalWorld.cs:24-43`). It creates **zero starting boats**. A mainland port does not receive a second guard; its post Archer is the one guard for that post.
* `BattleSession.StartingOwners` defaults to seeded `IndividualCities` for `RandomCities` and seeded `WholeCountries` for `RandomCountries` (`RiskAI/Assets/RiskAI/Scripts/BattleSession.cs:74-79`). `StartingAllocation.Generate(..., IndividualCities)` already shuffles, assigns `floor(cityCount/playerCount)` complete rounds, and leaves the remainder `NeutralOwner` (`RiskAI/Assets/RiskAI/Scripts/Core/StartingAllocation.cs:103-140`). This is deterministic Unity policy, not Saran's unseeded `GroupPickRandomUnit`; the implementation is present and should not be listed as missing.
* Runtime constants are `StartingGold=4`, `BaseIncome=4`, `TownIncome=1`, `RoundSeconds=60`, and bounty divisor 4 (`RiskAI/Assets/RiskAI/Scripts/Core/BattleRules.cs:8-19`). `Economy.Gold` is initialized to 4 immediately in its constructor (`BattleRules.cs:69-72`); the first 60-second `Advance` pays the first periodic income and is a separate event (`BattleRules.cs:157-177`). The retired `data/rules.json` is now a pointer manifest with `loadedByRuntime=false`; it does not duplicate current runtime values.
* v0.13 reinforcements use local per-country rosters with **Archer** as the intended Rifleman-equivalent kind and `PerTurn` values of 1 or 2 (`RiskAI/Assets/RiskAI/Scripts/MapLayout.cs:34-50`; `RiskAI/Assets/RiskAI/Scripts/BattleSession.cs:139-157`). Those values match `ceil(cityCount/2)` for the current group sizes; the five-point-per-city living cap and country camp spawn points are implemented. Remaining source-fidelity differences are the imported source XY/country topology and the source-style one-at-a-time timed stream rather than an immediate batch.

## Completed and remaining v0.13 corrections

1. **Complete — initial unit roster.** At world setup, v0.13 creates exactly one Archer/Rifleman-equivalent defender per city, including neutral cities and port cities. The capital/non-capital army piles and duplicate neutral defenders were removed; city binding follows the allocation.
2. **Complete — naval start.** v0.13 starts with zero boats. Ports remain production sites; the free transport/galley grant and automatic extra port placement were removed.
3. **Complete — circle ownership.** Player-owned circles follow their owner; remainder city circles and their single defender remain neutral-aggressive. No second neutral garrison is created.
4. **Complete — allocation provenance.** The Unity LCG and `IndividualCities` floor/remainder behavior are documented as deterministic product policy, with whole-country allocation as an optional mode rather than the original default.
5. **Complete — economy timing.** Runtime uses 4 starting gold and 4 basic income per 60-second turn; the constructor grant and later periodic `Advance` payout are documented separately.
6. **Partial — country recruitment.** The current prototype implements the `ceil(cityCount/2)` quantity for the current group sizes, the `5 * cityCount` living-point cap, and country camp spawn points with local Archer/Rifleman-equivalent rosters. It still emits the batch immediately rather than through the source-style one-at-a-time timed stream; importing the source XY/country topology is separate remaining work.
7. **Ongoing guidance — provenance in tests/docs.** Keep source assertions (1 defender/city, 0 boats, 4 first income, country cap) separate from product assertions (12/20-city layouts, LCG seed, harbors, naval shop, Archer post guards/reinforcements, AI grace timers). Do not use old v0.12 army snapshots as evidence of original Risk.

## Evidence index

* Source setup constants and garrison creation: `references/maps/reforged-v3-source/war3map.j:5454-5458`, `:7025-7078`.
* Source random allocation and neutral remainder: `war3map.j:8362-8420`, `:8838-8846`, `:8935-8977`, `:9148-9158`.
* Source modes and first/basic income: `war3map.j:5563-5587`, `:8421-8424`, `:9119-9133`, `:14453-14471`, `:19161-19180`.
* Source reinforcement cap and spawn: `war3map.j:19378-19524`; source death decrement: `war3map.j:18738-18754`.
* Source map inventory and city/port totals: `docs/RISK-MAPS-v0.10.md:34-50`, `data/derived/risk-reforged-v3-placements.json`.
* Unity setup/allocation/naval behavior: `RiskAI/Assets/RiskAI/Scripts/RiskBootstrap.cs:24-53`, `RiskAI/Assets/RiskAI/Scripts/BattleSession.cs:14-18,59-79,139-157`, `RiskAI/Assets/RiskAI/Scripts/NavalWorld.cs:21-55`, `RiskAI/Assets/RiskAI/Scripts/Core/StartingAllocation.cs:103-172`.
