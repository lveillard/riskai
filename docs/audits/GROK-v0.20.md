# Grok v0.20 UI restoration review

One Grok 4.6 review completed against the then-current UI restoration snapshot. The raw response is retained at `.tools/grok-v20/ui-review/output.json` (exit 0, one turn, 921.7 seconds). The packet covered the retained HUD, UI input/runtime, ornaments, front end, viewport/platform presentation, and UI capture harness. It requested only reproducible layout, input ownership, stale-panel, callback, and resource-lifetime failures.

## Disposition after source review

| Review claim | Disposition | Evidence and follow-up |
| --- | --- | --- |
| Ranking `VOLVER` cannot close because `ScoreboardVisible` remains true | Rejected | `RtsController.ScoreboardVisible` is a readonly hold state for Tab (`!HelpVisible && tabKey.isPressed`), not a modal flag. `BattleHud.ShowPlayers` opens ranking by setting `HelpVisible` and `menuTab = 2`; `CloseModal` correctly clears `HelpVisible`. Holding Tab continues to show ranking until Tab is released, by design. |
| A retained modal permits world input because it does not call `RtsUiInput.Block` | Rejected | `RtsController.OverHud` independently rejects `HelpVisible`, `ScoreboardVisible`, and a winner. `AcceptsDirectPointerInput` has the same modal gates, and `Update` releases/cancels input while either UI flag is active. The lower-level `BlocksWorld` helper alone is not the command authority. |
| Compact context ScrollView can grow outside its footer instead of scrolling | Confirmed | The compact branch gives the context `flexGrow` but no `minHeight = 0`, while the wide branch supplies it. This is a bounded layout fix. Regression: in compact viewport, select harbor production with both queues and ships; assert scroll viewport height is bounded by the footer remainder, content height exceeds it, and the final production control becomes reachable after scrolling. |
| Destroyed selected building causes stale queue UI / null reference | Rejected for shipped lifecycle | The code would be fragile if external code destroys a selected `Settlement` or `Harbor`, but authored gameplay transfers ownership and preserves buildings until scene teardown. Controller cleanup only needs mobile and fleet lifetimes because those are runtime-despawned. No normal command, capture, queue, or scene lifecycle removes one selected building while the HUD survives. Do not add an artificial destruction test as a gameplay contract. |
| Help sheet remains after victory or ranking changes while it is already open | Confirmed | `RefreshRetainedUi` compares only the aggregate modal-open boolean. Help open followed by `Winner >= 0` keeps that boolean true, so it does not rebuild the modal even though `BuildModal` prioritizes the result sheet. The same aggregate-state issue applies to an active held Tab. Track modal kind or the individual source flags. A staging PlayMode regression is at `.tools/ui-restoration/BattleHudTests-v20.cs`: it opens help, earns an actual timed conquest through `BattleSession.TickRules`, then asserts the retained tree contains `VICTORIA`. |

No duplicate-controller-callback defect was confirmed: the rebuilt retained tree is replaced through `SetContent`, and each ornament button owns one callback.

## Validation limits

This review did not validate WebGL, ARM, physical touch hardware, or visual frame captures. It records source-level dispositions and a staged regression only; production test/build outcomes belong to the final Unity validation run.
