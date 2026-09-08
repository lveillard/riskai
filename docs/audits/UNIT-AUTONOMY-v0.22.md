# Unit autonomy audit — v0.22

## Scope

This audit covers actor-level RTS behavior in `Soldier` and `Ship`: automatic acquisition, retaliation, explicit orders, target retention, pursuit, return, patrol, follow, and fixed garrisons. Strategic commanders, formation planning, economy, and UI command presentation are outside this review.

The classifications used below are:

- **Defect**: the runtime breaks an issued order or an established actor identity rule.
- **Adaptation**: an intentional RiskAI behavior with no claim of Warcraft III parity.
- **Parity pending**: a Warcraft III-like behavior for which the current source set is incomplete or policy has not been chosen.

## Current behavior

### Land units

`Soldier` has explicit Idle, Move, AttackMove, Attack, Hold, Patrol, and Follow states (`RiskAI/Assets/RiskAI/Scripts/Soldier.cs:11`). Orders replace the current state unless a supported movement or patrol order is appended to the bounded queue (`Soldier.cs:170-182`). Explicit Attack retains a valid target without the autonomous leash; Move and Follow suppress independent acquisition (`Soldier.cs:245-275`).

Idle, AttackMove, Patrol, and Hold can acquire nearby visible enemies. The source acquisition radius is used where one is available. Autonomous targets are retained against the same effective leash, while explicit Attack is unbounded by that leash (`Soldier.cs:245-275`). Target choice is distance plus a local pressure penalty, with deterministic entity-ID tie breaking. The pressure weights are a RiskAI adaptation rather than a source-derived Warcraft III priority rule.

After an autonomous target becomes invalid, AttackMove and Patrol resume their saved route. Idle units request a return to their anchor after moving more than 1.5 m from it (`Soldier.cs:297-305`). Hold can acquire and fire but drops a target that requires movement, so it remains anchored (`Soldier.cs:362-419`). A bound garrison uses Hold combat while its transform and NavMesh position are forced to one claim anchor (`Soldier.cs:103-137`).

Damage retaliation applies while Idle, AttackMove, Patrol, or autonomous combat is active. Move, Hold, and Follow suppress direct retaliation. A damaged unit also alerts idle allies within 5 m (`Soldier.cs:466-487`). Follow assists the followed ally against a target within 9 m; both distances and the assistance policy are local adaptations pending an explicit parity decision.

### Naval units

Move suppresses acquisition while a route is active. AttackMove acquires during travel, interrupts for combat, and now retains its original destination for resumption. Stop leaves a galley eligible for stationary automatic acquisition. Explicit Attack retains its chosen target and may build a pursuit route. Harbor garrisons can turn and fire but cannot pursue outside their fixed berth (`RiskAI/Assets/RiskAI/Scripts/Ship.cs:80-125`, `Ship.cs:180-224`, `Ship.cs:268-289`).

Transport orders and harbor binding are separate from combat autonomy. Embarkation stops and disables the soldier agent; unloading re-enables it and issues Stop, which restores ordinary idle acquisition (`Ship.cs:138-168`).

## Corrected defects

1. **Follow could transfer to a different pooled actor.** Follow stored a `Soldier` object reference. A dead actor remains a valid Unity object while retiring, and the pool can later initialize that same object with a new identity. The follower could therefore continue an old order against a new actor. Follow now stores the monotonic `EntityId` and resolves it through `BattleSession.FindTarget` each simulation step (`Soldier.cs:12`, `Soldier.cs:214-218`, `Soldier.cs:425-431`). It completes when the identity dies, leaves the battle, changes team, or is no longer a live soldier.

2. **Naval AttackMove discarded its destination during combat.** The firing and pursuit paths reused the route that represented the player's destination. Once combat ended, the galley had no route to resume. `Ship` now stores the AttackMove destination independently and rebuilds the sea route when the combat target becomes invalid (`Ship.cs:16-20`, `Ship.cs:80-88`, `Ship.cs:190-194`, `Ship.cs:256-267`). Destroyed Unity targets are also detected through reference identity so a destroyed ship cannot strand the order.

3. **Naval retaliation overwrote an explicit target.** `TakeDamage` assigned the attacker whenever the galley was eligible to react, including while it already had a player-selected target. Retaliation now runs only when no target is held, and explicit Attack is no longer marked as AttackMove (`Ship.cs:112-125`, `Ship.cs:300-304`).

## Open findings

### Parity and policy pending

- **Galley acquisition radius:** automatic naval acquisition currently queries `Profile.Range`, 20 m (`Ship.cs:215-224`). The source record for h00W has `uacq=650`, or 13 m after map scaling, but it is classified `tft_only_historical_candidate`, not map-explicit or classic consensus. Keep the current value until stronger 2.0.2 evidence or an explicit adaptation decision exists.
- **Galley attack point:** the ship currently releases an attack immediately when its cooldown is ready (`Ship.cs:199-203`). h00W `udp1=0.3` is also only a TFT historical candidate. Do not add a windup on that evidence alone.
- **Naval pursuit and return:** autonomous naval targets have no independent leash or return anchor. A galley may keep rebuilding pursuit paths until the target becomes invalid. Warcraft III parity needs verified acquisition/guard behavior; RiskAI policy also needs to decide whether free ships return to the interrupted position or only AttackMove ships resume a destination.
- **Hold with source acquisition greater than weapon range:** acquisition uses a source radius before Hold rejects targets that require movement. A ranged holder can briefly reacquire the same out-of-range target on later sense passes. This preserves stationary behavior but needs a parity decision and a focused state-retention test before changing either radius.

### Actionable coverage gaps

- Explicit land Attack retains a target without a leash, but an unreachable living target has no terminal failure transition. Add a blocked-path regression before deciding whether to stop, retain, or expose an order failure.
- The damage-reaction matrix is encoded directly in `Soldier.TakeDamage`; dedicated tests should cover Idle, Move, AttackMove, Hold, Patrol, and Follow so future changes cannot silently violate explicit orders.
- Patrol resumes and alternates endpoints after combat, but current coverage chiefly validates command rejection and land AttackMove resumption. Add a two-leg patrol interruption test.
- Target masks are enforced in source weapon impact handling, while automatic acquisition still accepts any enemy `CombatTarget`. A future source-fidelity pass should compare direct-attack target masks with runtime categories before narrowing acquisition.

## Regression coverage added

- `SimulationInfrastructureTests.FollowStopsWhenItsEntityDiesAndDoesNotAttachToThePooledReplacement` proves that Follow ends at entity death and remains ended when the same GameObject is rented with a new ID.
- `NavalGameplayTests.GalleyAttackMoveResumesItsOriginalDestinationAfterCombat` proves that combat interruption preserves and resumes the original sea destination.
- `NavalGameplayTests.GalleyDamageReactionKeepsAnExplicitAttackTarget` proves that damage retaliation cannot replace a living explicit target.

Existing coverage remains relevant: `BattleIntegrationTests.StopEngagesWhileHoldAndMoveRespectTheirOrders`, `BattleIntegrationTests.AttackMoveResumesAfterKillingItsTarget`, `NavalEmbarkCaptureTests.OccludedNavalGuardNeverPlansOrMovesOffItsBerth`, and command lifetime tests in `RtsSelectionLifetimeTests`.
