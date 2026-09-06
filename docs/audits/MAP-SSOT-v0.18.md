# MAP SSOT audit · v0.18

**Scope.** Read-only review of runtime map selection, ports, camera, economy,
combat and controls. The intended model is that a map supplies World-Editor-like
content (terrain, water, positions, cities, countries and ports), while the
simulation rules remain shared.

## Result

No scenario-specific branch was found in the unit combat profiles, combat
commands, population rule, income rule, country-recruitment tick, ship combat or
tower combat.

- `Core/BattleRules.cs`, `Core/ReforgedProfiles.cs` and
  `Core/NavalProfiles.cs` expose shared values; they do not read
  `MapLayout.IsImported`, `MapLayout.IsExpanded` or `Scenario`.
- `Soldier.cs`, `Ship.cs`, `DefenseTower.cs` and `BattleCommands.cs` likewise
  contain no scenario selector. Damage, cooldown and movement operate through
  the shared profiles.
- `BattleSession.cs:112-121` calculates the recruitment population and its
  reservations once for all layouts. It excludes garrisoned soldiers uniformly.
- `MapLayout.cs:125-145` applies
  `ApplySharedReinforcementFormula` to both imported and authored country
  rosters. `CountryRecruitment.cs` consumes that common `PerTurn` data rather
  than selecting a map economy.
- `BattleSession.cs:95-103` uses the same allocation engine for normal
  `RandomCities` and `RandomCountries` starts. Its Fixed two-player branch
  reads initial owners from map content.

`IsCapital` is not a gameplay rule: it selects the initial/home camera target
in `RtsController.cs:44,216`; `Settlement.cs:57` uses it only for city
presentation.

## Valid content and topology adapters

These branches are necessary because the imported maps have source terrain and
port cities, while the authored maps contain hand-authored coasts, islands and
independent piers.

| Area | Evidence | Classification |
| --- | --- | --- |
| Country camp placement | `CountryCamp.cs:30` selects imported `CampPoint`, otherwise a point derived from the authored town. | Source position / navigation adapter. |
| Camera framing | `RtsCameraRig.cs:17,62` raises only the focus speed cap by imported map depth; clamping and zoom use `PlayableMin`, `PlayableMax` and common formulas. | Control scaling for world size, not a different command rule. |
| Sea path grid | `SeaNavigation.cs:129-136,176-179` selects source dimensions/origin and a cached-grid implementation for imported W3E data. | Geography and bounded pathfinding implementation. It does not select a ship profile or combat value. |
| Harbor construction | `NavalWorld.cs:30-34,88` builds a harbor for each imported `Settlement.IsPort`; authored maps create their independent harbor roster from coast/island content. | Map topology. |
| Linked imported harbor | `Harbor.cs:82-85` takes the town's `TownState`, claim zone and tower. Delegation at `133-153`, `236-257`, `271`, and shared-tick guards at `289-306,333-340` prevent a second queue, capture clock, tower or economy entry. | Required adapter: a source port city is one city, not a second settlement. |
| Independent authored harbor | `Harbor.cs:64-67,296-340` retains its own state because it has no corresponding town in the authored map data. | Authored content topology, including a distinct capturable post. |

Consequently, the different accounting of an imported linked port and an
authored independent pier is intentional: it preserves the number of actual
towns rather than applying a map-specific economy or victory rule.

## Open catalog issue: AI-owned port production

`SkirmishCommander.cs:235-246` excludes `town.IsPort` from the normal
`RecruitmentSite` list. This is **not** a branch on `Scenario`; it is a
production-catalog decision by building type.

It cannot safely be removed as a one-line DRY cleanup. The human harbor UI
currently exposes the Marine catalog through `Harbor.RecruitLand`
(`Harbor.cs:234-236`), whereas placing a port town in the existing commander
cycle would make the AI buy ordinary units there. That would create a
human/AI catalog asymmetry.

The future fix is to define one shared production-catalog API for each
settlement/harbor type, and have both HUD and commander query it. It should
then choose which port units the AI may purchase deliberately, with tests for
the same catalog visible to both sides. No runtime behavior was changed by
this audit.

## Fixed-start data caveat

`BattleSession.cs:98` uses map-owned initial state only for `Fixed` with two
players. Imported `MapLayout.City` currently derives that field from
`country % 2` (`MapLayout.cs:50`); random layouts recompute ownership via the
common allocator. If Fixed must reproduce an original map's ownership exactly,
that owner needs to be recorded in imported source data. There is no evidence
in the reviewed runtime API that such a source field already exists, so this
is documented as a content-data decision rather than silently changing
allocation rules.
