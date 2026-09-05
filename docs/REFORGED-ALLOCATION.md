# Reforged starting allocation audit

This note records the local map source and the small deterministic allocation used by the Unity prototype. The C# result is an adaptation; it does not reproduce Warcraft III's runtime RNG or its 69-region map.

## Evidence from `war3map.j`

The source map declares 69 regions and uses `h00B` as both normal and shipyard starting defender, with `h005` as the recruitment spawn unit (`references/maps/reforged-v3-source/war3map.j:5455-5458`). The original setup therefore starts from many map cities, not a 12-city board.

The mode presets are explicit: Conquest is mode `0`, 60% city submode, FFA, and `ModesFillCities=false` (`war3map.j:14453-14471`); Timed is mode `2` with a 30-turn submode (`war3map.j:14508-14526`); Russia is mode `1`, submode `10`, with one starting city per player in the later assignment logic (`war3map.j:14561-14568`, `war3map.j:8421-8424`).

For ordinary modes, the map computes `CitiesSetupAmountPerPlayer` as integer division of the randomizing city group by the active player count (`war3map.j:8418-8420`, `war3map.j:9110-9113`). The assignment loop repeatedly calls `GroupPickRandomUnit`, writes the city owner, and removes that city (`war3map.j:8935-8955`); after player picks, the remaining cities go to the neutral aggressive player (`war3map.j:9150-9157`). This is the source basis for the optional `IndividualCities` mode.

Mode 5 is different: it iterates seven prebuilt `CountryGroupsMode5` and assigns each group to `AMode5Players` (`war3map.j:9119-9133`). That is country-group ownership, but the player slots and groups are hardcoded by the map. The prototype uses the same whole-country idea while choosing two distinct countries from a fixed seed.

Capital selection is separate from initial ownership. `Trig_Build_Capitals_Actions` iterates each region and picks a random city from the region group (`war3map.j:11669-11686`), then removes the selected capitals from later choices (`war3map.j:11685-11691`). The prototype allocator does not select capitals; the caller should perform that policy after ownership is assigned.

## Prototype adaptation

`RiskAI/Assets/RiskAI/Scripts/Core/StartingAllocation.cs` exposes a pure `StartingAllocation.Generate` function and a `GeneratePrototype` convenience entry point. The prototype topology is 12 zero-based city indices, two cities per each of six countries, and two players. `WholeCountries` shuffles country indices with a fixed unsigned LCG, gives one complete country to each player, and leaves the other four countries and their eight cities neutral. Identical seeds produce identical arrays without depending on `System.Random` implementation details.

`IndividualCities` is available for a caller that needs the map's generic city-count behavior. It shuffles city indices and assigns only `floor(cityCount / playerCount) * playerCount` entries round-robin; any remainder stays neutral, matching the source's integer division. Thus 12 cities produce six per player, while 13 cities produce six per player plus one neutral city. A country owner is reported as `MixedOwner` when its cities have differing owner values, including an assigned city alongside a neutral remainder; this mode is not the preferred default because it does not preserve country boundaries.

Owner values are player indices `0..playerCount-1`; `NeutralOwner` is `-1`; `MixedOwner` is `-2` and is meaningful only in the country summary for `IndividualCities`.

## Damage bonus constants (audit only)

The primary local archive member `references/maps/Risk_Reforged_v3.0_by_Saran_OPEN_SOURCE.w3x::war3mapMisc.txt` contains seven `DamageBonus*` rows, each with eight comma-separated values. I do not relabel those tuples without a source. War3Net's runtime enum, which mirrors the Blizzard/JASS `ConvertDefenseType` constants, explicitly gives the index order as `0 Light, 1 Medium, 2 Large, 3 Fortified, 4 Normal, 5 Hero, 6 Divine, 7 None` ([War3Net `DefenseType.cs`](https://github.com/Drake53/War3Net/blob/master/src/War3Net.Runtime.Core/Enums/DefenseType.cs), [War3Net `DefenseTypeApi.cs`](https://github.com/Drake53/War3Net/blob/master/src/War3Net.Runtime.Api.Common/Enums/DefenseTypeApi.cs); the checked JASS `common.j` fixture lists the eight constants at lines 2139-2146). Therefore the archive tuple columns are documented in that verified order:

| Row | Eight values |
|---|---|
| DamageBonusNormal | `1.00,1.50,1.00,0.70,0.70,1.00,0.05,1.00` |
| DamageBonusPierce | `2.00,0.75,1.00,0.35,0.35,0.50,0.05,1.50` |
| DamageBonusSiege | `1.00,0.50,1.00,1.50,1.50,0.50,0.05,1.50` |
| DamageBonusMagic | `1.50,0.75,2.00,0.35,0.35,0.50,0.05,1.00` |
| DamageBonusChaos | `1.75,0.75,1.00,0.35,0.35,0.50,0.05,1.50` |
| DamageBonusHero | `1.00,1.00,1.00,0.50,0.50,1.00,0.05,1.00` |
| DamageBonusSpells | `1.00,1.00,1.00,1.00,1.00,0.75,0.05,1.00` |

These values are documented for combat-audit consumers only; this subtask adds no combat or damage implementation.
