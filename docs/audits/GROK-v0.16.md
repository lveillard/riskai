# Grok 4.6 review — v0.16

## Round 1: country reinforcement and camp ownership

**Completed:** 6 September 2026. The call used the exact CLI model alias `grok-4.6`, one turn, with `--tools none --no-subagents --disable-web-search --no-plan --verbatim`. It ended normally with `stopReason: end_turn`; it was not a timeout. The complete input, JSON response, stderr, and exit status are ignored under `.tools/grok-v16/round1/`.

The supplied packet contained complete country recruitment, camp, session, settlement files and their focused tests. It requested no architecture rewrite and a maximum of five concrete reproductions. Grok returned five claims; the integrator compared them against source-rule documentation before accepting anything.

| Claim | Triage | Evidence and disposition |
| --- | --- | --- |
| Credit is decremented before the country point-cap check. | Rejected — intended source behavior. | `CountryRecruitment.Tick` deliberately mirrors JASS `RecruitStep500`: it spends the scheduled credit before the cap gate. `docs/RISK-RULES-v0.16.md` records this exception. |
| Credits persist when a country changes owner. | Rejected for credits; accepted for camp rally UX. | Country credit belongs to the country under the source rule and persists across owner changes. A previous owner's local rally must be cleared on owner change so it does not direct the new owner's reinforcements. |
| A neutral/mixed country emits a false “country complete” notification. | Accepted. | `Settlement.Captured` compared `CountryOwner == State.Owner` without requiring a non-neutral owner; both can be `-1`. Guarding `State.Owner >= 0` fixes the message only. |
| Country reinforcement can overrun the 100-mobile cap when a town queue becomes ready later. | Accepted. | `CountryRecruitment.Tick` counted only live mobiles, while a completed town queue can later spawn without a cap check. Count pending owned queues when country recruitment decides and defer a completed town queue at the cap. |
| A failed camp spawn burns a credit. | Not accepted as a current defect. | The ordering is intentionally source-aligned. No reproduced authored camp/NavMesh failure was supplied. |

The second round received the source-rule exceptions above and targeted command/boarding queue state plus simulation hot paths.

## Round 2: command, movement, AI, and boarding state

**Completed:** 6 September 2026. This was the second requested actual Grok response, again using `grok-4.6` with tools, subagents, web search, and plan mode disabled. It ended normally with `stopReason: end_turn` after one turn. The input packet had 2,384 numbered lines and declared the round-one source-rule exceptions. Raw artifacts are ignored under `.tools/grok-v16/round2/`.

| Claim | Triage | Evidence and disposition |
| --- | --- | --- |
| A partial NavMesh path can complete as though it reached the requested destination. | Implemented and regression-tested against an island destination. | `Soldier.Travel` reports arrival when `remainingDistance < .4f` without also requiring `Agent.pathStatus == PathComplete`. The prior block reports an incomplete path only after a 1.2-second stall. A partial path that ends cleanly can take the arrival branch first. |
| Pending boarding/unload is not consistently cancelled by later orders. | Partially accepted; most of the reported stale paths were fixed during integration. | Current controller source processes boarding before its focus early return and clears it for Stop, Hold, attack, movement, and harbor movement. The remaining right-click Follow branch did not clear the pending boarders. The report's old controller-owned unload finding does not apply after its removal. |
| AI country reinforcements at a camp never join offense. | Product decision. | Camps intentionally use Hold with no explicit rally. `SkirmishCommander.IssueOffensiveOrders` accepts only Idle units. This is correct for the player-facing default but leaves AI camp reinforcements defensive-only. Decide explicitly whether AI commanders should treat Hold as available after the opening. |
| Patrol failures still increment `AppliedCount`. | Accepted. | `BattleCommands.Tick` assigns the boolean outcome only for Move and AttackMove. `Soldier.Patrol` invokes the same fallible `Issue` path but returns void, so an off-mesh or full-queue Patrol is counted applied and cannot surface its rejection. |

## Integrated outcome

The accepted fixes are implemented. Patrol propagates its failure to the command inbox; the island regression additionally exposed a retry loop where an exhausted partial path repeatedly entered `pathPending`. The unit now reports the blocked route once, traverses the reachable segment once and finishes without restarting a failed patrol. Follow and Unload cancel selected boarding work; selection/focus changes preserve an issued boarding mission, and pause suspends it. Unload destinations belong to each ship.

The commander explicitly treats its own waiting Hold units as available for an offensive order, while a player's camp still defaults to Hold. Dedicated tests cover these choices, shared population reservations, and camp ownership changes. Final run results and build evidence are recorded in [validation](../VALIDATION-v0.16.md). Neither model response is treated as proof of correctness or as a performance benchmark.
