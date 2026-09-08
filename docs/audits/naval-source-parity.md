# Naval source parity audit and remaining implementation

Reproduce with `python .tools/naval-parity/build_proposal.py`. `naval-source-profiles.json` contains 13 ship objects, both weapons, economy/movement/abilities/upgrades, and every map override. `naval-table.md` is the compact comparison. Each field records a W3U offset or historical TFT SLK/profile line. Metadata maps `udp1/ubs1/uhd1/uqd1`, not invented `slk.*` pseudo-fields, and `upgr` is the actual upgrades field. Historical TFTinner155 is not proof of Reforged 2.0.2.22796 defaults; retain that qualification in implementation/docs. SLK `-`, `_`, missing/null are not numeric zero.

## Roster and modes

- Custom naval combat objects: h00U, h00V, h00W, h000, h001; Classic objects h006, h00L, h00P, h00Q, h00X. Transport objects n007 (armoured), n008, n009 (Classic).
- Shipyard h00O `utra` W3U@0x14c6 lists `h015,h014,h012,h00X,h006,h00L,h00P,h00Q,n009,n008,n007,h00U,h00W,h001,h00R,h00T,h00S`. This mixes land and naval units. h000/h00V are objects/availability candidates, not directly trainable from this list. Do not silently add them to the production menu.
- UnitSet0 (default initialization, JASS444/2824) disables Classic h00X,h006,h00L,h00P,h00Q,n009 (plus land units) at8171–8188. Shop naval intersection: h00U,h00W,h001,n007,n008.
- UnitSet1 (Classic/Devolution UI10063) disables h001,h000,h00U,n007,n008 but explicitly enables h00W at8208. Shop naval intersection: h00X,h006,h00L,h00P,h00Q,n009,h00W. Preserve the surprising h00W overlap; suffix alone cannot choose the roster.
- ShipsMode2 further disables warships and n007/n008, then enables n008 (8227–8238). n009 is absent from this disabling list and remains possible in Classic mode. Mode application order matters. Implement replay of explicit availability changes, intersected with shipyard production, rather than mutually exclusive hand-authored class lists.
- n006 is a spy balloon, explicitly disabled at5021; do not present as a transport/ship. h00O is a structure, not a combat ship.

## Capture and runtime trigger rules

Entry condition JASS17438–17445 excludes **n007 and n008**, not n009. Entry actions17477–17487 claim, stop, and move entering unit to circle center; CityClaim18202 also moves claimant. Defender death successor filtering17525–17585 makes the same exclusions. Thus all ten listed warships and Classic n009 pass the ID filter, subject to ownership/alliance/live defender/circle conditions. Existing `ShipKind.Transport` must not become the source of capture eligibility once it represents more than n008.

JASS19614/19704–19706 applies `(default movement speed - 50) * 2` to trained units in MainGamemode1. JASS19635–19650/19714–19716 makes WeakShipsMode1 affect only h001 and h00U, setting max HP to 80% of current life. JASS19625–19632/19719–19720 gives mode2 n008 regeneration -1 and A013. Default static values alone do not implement these modes. Recruitment also swaps an existing ship defender under userdata comparison (19655–19685,19723–19735); do not conflate this special replacement with unguarded entry capture.

## Concrete runtime changes proposed

1. Keep `NavalUnitKind.Galley=0` and `Transport=1`, `ShipKind`, `NavalProfiles.Galley/Frigate/Transport`, and existing public order APIs compatible. Add immutable rawID-keyed naval definitions and a selected rawID on Ship. Existing Galley resolves to h00W; existing Transport resolves to n008. `TryGetSourceProfile` rejects unknown IDs instead of falling back to Galley. Keep role (presentation/loading) separate from source identity and capture permission.
2. Extend shared weapon definition with enabled mask, damage base/dice/sides, attack type, weapon type, target masks, range/min range/acquisition, attack point, backswing, cooldown, projectile speed, splash radii/factors/target masks; retain raw metadata/provenance in generated source data. Use root's land combat weapon implementation rather than a second naval damage pipeline. All ten hdes descendants inherit historical candidate msplash, 1d15, attackpoint .3, backswing .3, splash25/35/50 with fractions1/.3/.1. Map changes damage/range and some cooldowns. Disable both weapons for nzep descendants through `weapsOn=0`, without coercing absent damage dice into authoritative zero stats.
3. Existing h00W matches HP400, armor6, speed340/50, damage30+1d15, range1000/50 and cooldown1.5 candidate. Missing current behavior: delayed attack impact, projectile travel1100/50, splash .5/.7/1 world units, and target masks. Train duration is explicitly 1 second for both h00W and n008; the current profile change corrects the former local values 4 and 6. Armor classification remains unresolved for the current compatibility profile: historical TFT hdes has defType small (Light), whereas Ship currently uses Heavy; no matching Reforged base data proves which inheritance applies. Do not interpret large on the explicit transport overrides as hdes armor. n007 requires armor30/speed7.4; h00Q has armor4 (not h00W6). Do not merely rename variants while sharing a single profile.
4. Add `CanCapture` from rawID and replace Galley-kind eligibility guards in Ship/Harbor/claim acquisition with it. Keep n007/n008 excluded. For n009 allow actual SailToHarbor claim and existing guarded movement/selection/snap behavior, while retaining transport loading role. Do not remove living defenders or snap through land. Root should explicitly stage this only when n009 can actually be selected/produced; current n008 transport behavior stays valid.
5. Add mode-aware shipyard production catalog using source availability operations above. Existing UI/order Galley calls remain alias-compatible. Use map point-value for bank debit (JASS19702), not guessed gold cost equality. Cargo capacity is explicitly 10 for n007/n008: their W3U uabi attaches Sch3 (n008@0x3588), whose W3A Car1@0x395 integer override is 10. Current n008 capacity is corrected from local 6 to 10. This evidence comes from the attached cargo ability, not the trigger enumeration cap or unit cargoSize. n009 has no explicit uabi list, so equivalent loading behavior is not established. See data/derived/reforged-source-combat.json abilities.transport.
6. Stage source catalog, mode/production, and weapon behavior separately so tests expose which parity dimension changed. Exact modern base inheritance remains a documented gap until matched source is obtained; do not label this complete Warcraft computational parity.

## Meaningful regressions for root to implement/run after source freeze

- Data-driven 13-ID fixture: exact explicit map overrides and distinct h00Q/h00W armor; h0062500 vs h0012350 HP; n007 armor30/speed370; train1 for all; unknown ID fails. Provenance classifications remain attached to inherited values.
- UnitSet0/1 crossed with ShipsMode0/1/2: replay availability, compare complete shipyard intersection including Classic h00W and mode2 n009. Assert h000/h00V not added to shop merely because their objects exist.
- Shared combat deterministic RNG boundaries: h00W31–45 damage before armor, attackpoint .3 elapsed before launch,1100-source-units/s flight, exact splash boundary/factor tests at25/35/50 and beyond; target filters and friendly splash exclusions. Both disabled transport weapons produce no attack even with inherited normal attack token.
- Actual `SailToHarbor` flow for h00W and n009 both local7.5m and approach from far; n007/n008 cannot claim; living defender cannot be replaced; island/istmus clear-segment blocks local snap; losing allied candidate stays unmoved. Keep existing `NavalDockingOrderTests`, `NavalEmbarkCaptureTests`, `SharedHarborGarrisonTests` and parameterize source IDs without globally changing the Transport rule.
- Mode trigger tests: WeakShips changes h001/h00U only; MainGamemode1 applies source speed transform once at spawn; mode2 n008 regeneration/ability modeled only if implemented, otherwise explicit unsupported feature. Capture replacement and regular vacant entry tested separately.

Implemented bounded profile changes: h00W/n008 rawID, capture permission and explicit one-second training, plus n008 capacity 10 from Sch3. The full 13-ID roster remains an audit, not a production/UI expansion. No inherited armor classifications were changed. NavalSourceProfileTests covers the existing aliases and explicit fields; Unity validation is run separately by the integration owner.

## All 13 static profiles

T = historical TFT candidate, not exact map patch proof.

| ID | uhpm | udef | umvs | uaen | ua1b | ua1d | ua1s | ua1c | ua1r | ua1t | ua1w | udp1 | ubs1 | ua1f | ua1h | ua1q | uhd1 | uqd1 | ubld |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| h00U | 1250 | 10 | 450 | 1 T | 90 | 1 T | 15 T | 1.5 T | 1500 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| h00V | 550 | 6 | 350 T | 1 T | 50 | 1 T | 15 T | 1.5 T | 1200 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| h00W | 400 | 6 | 340 | 1 T | 30 | 1 T | 15 T | 1.5 T | 1000 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| h000 | 2000 | 15 | 350 T | 1 T | 90 | 1 T | 15 T | 1.399999976158142 | 1500 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| h001 | 2350 | 20 | 330 | 1 T | 130 | 1 T | 15 T | 1.399999976158142 | 1500 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| n007 | 300 | 30 | 370 | 0 T | - T | - T | - T | 0 T | - T | normal T | _ T | - T | - T |  -  T |  -  T |  -  T | - T | - T | 1 |
| n008 | 300 | 0 T | 340 | 0 T | - T | - T | - T | 0 T | - T | normal T | _ T | - T | - T |  -  T |  -  T |  -  T | - T | - T | 1 |
| h006 | 2500 | 20 | 330 | 1 T | 130 | 1 T | 15 T | 1.399999976158142 | 1500 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| h00L | 1000 | 10 | 420 | 1 T | 70 | 1 T | 15 T | 1.5 T | 1500 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| h00P | 550 | 6 | 350 T | 1 T | 50 | 1 T | 15 T | 1.5 T | 1200 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| h00Q | 400 | 4 | 340 | 1 T | 30 | 1 T | 15 T | 1.5 T | 1000 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| h00X | 2000 | 15 | 350 T | 1 T | 90 | 1 T | 15 T | 1.399999976158142 | 1500 | normal T | msplash T | 0.3 T | 0.3 T | 25 T | 35 T | 50 T | 0.3 T | 0.1 T | 1 |
| n009 | 300 | 0 T | 340 | 0 T | - T | - T | - T | 0 T | - T | normal T | _ T | - T | - T |  -  T |  -  T |  -  T | - T | - T | 1 |

Cargo regression: NavalSourceProfileTests.TransportCapacityMatchesAttachedSch3AbilityBinaryOverride parses the exact 24-byte W3A modification fixture (source offset 0x395 through 0x3AC), checks integer type, level, data pointer and Sch3 check ID, and compares its capacity against runtime. n008 remains unable to attack or capture.
