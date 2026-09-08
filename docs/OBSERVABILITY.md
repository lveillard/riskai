# Observing a live RiskAI runtime log

The v0.19 player keeps this passive reader compatible. Diagnostics also report
`unityAllocatedB` (Unity's tracked allocated memory, not total process/WASM
memory). Startup emits `RISKAI_STARTUP` phase timings with allocated, reserved
and managed memory; these separate map generation, navigation and UI startup
from steady gameplay.

Add `--riskai-frame-trace` alongside `--riskai-probe` for opt-in hitch
correlation. It logs at most 64 frames above 50 ms, with available main/render
thread and GC allocation recorders. Unsupported counters are reported as -1;
FrameTiming CPU/GPU data requires the engine feature to be enabled. Samples
can be asynchronous and logging itself adds overhead: use this to investigate
correlation, not to assert a cause or replace a normal benchmark.

The opt-in native command probe accepts `--riskai-probe --riskai-map europe
--riskai-players 16 --riskai-seed 160212 --riskai-probe-warmup 900
--riskai-probe-warmup-commander --riskai-probe-seconds 90`. It runs accelerated
warmup, restores 1x, stabilizes, and records synthetic order and movement
latencies. Do not treat startup/warmup diagnostic windows as 1x gameplay.

`--riskai-restart-probe --riskai-restart-cycles 3 --riskai-map europe`
loads the map and returns to the front end repeatedly. Only this diagnostic
mode explicitly requests unused-asset collection and GC between checkpoints.
Compare the first and final **front-end-after-cleanup** samples, not a loaded
battle against an empty menu. Counts include meshes, materials, renderers and
NavMeshData. Normal play never starts this probe implicitly.

`scripts/observe_runtime.py` is a passive Python standard-library reader for the
`RuntimeDiagnostics`, `RISKAI_ORDER_REJECTED`, and `RISKAI_ROUTE_BLOCKED`
lines written by RiskAI. It does not start Unity, send input, or inspect/control
processes.

From the repository root:

```powershell
python scripts/observe_runtime.py
python scripts/observe_runtime.py RiskAI/Logs/v17-opened.log --last 5
python scripts/observe_runtime.py --follow --duration 90
python scripts/observe_runtime.py --json
```

The positional path is optional and defaults to
`RiskAI/Logs/v17-opened.log`, written by `Play-RiskAI.cmd`. `--follow` polls once per second; `--duration`
bounds it for scripts or a short observation. With `--json --follow`, each
new record is emitted as one JSON Lines object. Snapshot JSON includes the latest
diagnostic windows and recent rejection/route events.

The reader keeps only a bounded tail for a snapshot and a bounded append per
poll. It ignores a line until its newline arrives, but discards an unterminated
line once it exceeds 64 KiB and skips its remainder through the next newline.
It resumes from the beginning if a live writer truncates or rotates the file,
including a replacement whose size is larger than the previous read offset
(detected from device/inode identity). A very large single append is reported
as capped rather than loaded without limit.

The parser requires the original v16 fields, regardless of their order, and
retains later whitespace-separated `key=value` fields in JSON output and
snapshots. The compact text view intentionally shows only the stable base
fields; use `--json` to inspect v17 timing fields. `--duration` accepts only
finite non-negative seconds.

## RuntimeDiagnostics v17 fields

Each row is a 30-second aggregate. The original `avgMs`, `maxMs`, heap, GC,
unit, simulation and command totals remain compatible with v16. The row can
also include:

- `frames`, `over50ms`, `over100ms`, `over250ms`: gameplay frames included in
  the window and threshold counts. Frames while paused or unfocused are not
  included. `focusLost`, `focusRecovered`, `pauseEntered`, and `pauseResumed`
  record state changes; `focused` and `paused` are the state at report time.
  The first eligible frame after a focus or pause transition is discarded, so a
  resume hitch is not reported as a gameplay frame.
- `world*AvgMs` and `world*MaxMs`: average per world tick and largest observed
  invocation for commands, spatial rebuilds, soldiers, towers, ships, combat,
  claims, rules, AI, and pool cleanup. AI is also split into `worldLandAi*`
  and `worldNavalAi*`; the original `worldAi*` remains their combined phase.
  `worldSpatialAvgMs` combines the two
  rebuilds in a tick, while its maximum is one rebuild invocation.
- `seaSearches`, `seaSearchAvgMs`, `seaSearchMaxMs`, `seaExpandedMax`,
  `seaDirect`, and `seaDisconnected`: aggregate sea-path work in the reporting
  window. `seaGridBuildMs` is the last static graph preparation, normally during
  loading; it is not part of every search. Smoothing is included in search time.
- `commandSubmitHuman`/`Ai`, `commandApplyHuman`/`Ai`,
  `commandRejectHuman`/`Ai`, and `commandQueueMax`: accepted submissions,
  applied/rejected commands, and the greatest queued depth in the window.
  Player 0 is `Human`; all other current match players are reported as `Ai`.
- `submitApply*Active*Ms`: latency from accepted `BattleCommands.Submit` to
  successful application, rather than input-click latency. It excludes pauses
  observed by `RuntimeDiagnostics`; `commandObservedPauseMs` reports that
  observed time. If the diagnostics component was absent or unable to update,
  pause exclusion is necessarily incomplete.
- `firstMoveHumanEligible`, `firstMoveHumanCancelled`, and
  `firstMoveHuman*Active*Ms`: a stricter movement sample for a direct player-0
  Move or AttackMove. It is eligible only when its agent initially has no path,
  no pending path, and negligible velocity. It completes only after the new
  route has resolved and a velocity toward the new destination is observed.
  A cancelled or absent sample is not proof that an order did not apply.
- `pathPending`, `pathPendingAvgAgeMs`, and `pathPendingMaxAgeMs`: a single
  per-unit snapshot at report time, with age measured in simulation time. They
  do not count all pending routes that occurred during the 30-second window.
  `navIterationsPerFrame` reports the active Unity asynchronous path budget.

## Reading limits

A diagnostic row covers a 30-second window. Its averages and maxima describe
only that window; the tool does not calculate percentiles from them. Likewise,
`commandsPending=0` or `pathPending=0` in a sampled row does not establish that
no command or route waited between rows. Submit-to-apply and first-move values
are active-time telemetry, not end-to-end pointer-input latency.

Focus and pause counters describe transitions observed by the diagnostics
component. They are useful context for excluding resume frames and pause time,
but cannot reconstruct a state change that occurred before the component
started or between unavailable callbacks. File age is based on the log's
modification time.

## Advanced player probe

With no other test/player running:

```powershell
& ./Builds/Windows-v0.17/RiskAI.exe -screen-width 1600 -screen-height 900 -screen-fullscreen 0 --riskai-map europe --riskai-players 16 --riskai-seed 160212 --riskai-probe --riskai-probe-warmup 1200 --riskai-probe-warmup-commander --riskai-probe-seconds 90 -logFile ./RiskAI/Logs/v17-probe.log
```

This test window disables the physical controller, enables background updates,
and exits when finished. A temporary player-zero commander participates only
during warmup. Warmup advances 1,200 simulation seconds at 8x; after restoring
1x and a two-second stabilization, the probe adds up to six archers per player
and measures 90 real seconds. It submits hold/move cycles to the tracked human
units and exercises picking with a fixed screen-center pointer each frame.
Tracked identities are immutable: a pooled replacement is not the same unit.
The measurement frame sampler includes background frames, unlike the normal
focused-gameplay diagnostic sampler. Logs distinguish those phases explicitly.

Use final measurement records to assess normal-speed response. Accelerated
warmup requests more NavMesh work per rendered frame and must not be presented
as normal-speed pointer latency. The same seed/configuration does not guarantee
identical battles: Unity movement is still frame-dependent. This is a controlled
load probe, not a replay or a substitute for testing real mouse/touch input.

## NavMesh path-budget A/B

The desktop player accepts `--riskai-path-budget`, clamped to `100..2000`, and
reports the applied value as `RISKAI_NAV_BUDGET` and
`navIterationsPerFrame`. To compare the current budget with a larger budget,
run the same controlled probe twice and change only the final value. Keep the
same seed, map, players, warmup, duration, and recruit fixture. Record
`unitsInitial` from `RISKAI_PROBE_PHASE phase=measurement`, the final unit
count, and external load. Movement is not deterministic, so unequal battles
make this an exploratory comparison; do not discard inconvenient runs or
attribute every difference to the budget (fixture target: roughly 600 units):

```powershell
& ./Builds/Windows-v0.21/RiskAI.exe -screen-width 1600 -screen-height 900 -screen-fullscreen 0 --riskai-map europe --riskai-players 16 --riskai-seed 160212 --riskai-probe --riskai-probe-warmup 900 --riskai-probe-warmup-commander --riskai-probe-recruits 24 --riskai-probe-seconds 90 --riskai-path-budget 500 -logFile ./RiskAI/Logs/path-budget-500.log
& ./Builds/Windows-v0.21/RiskAI.exe -screen-width 1600 -screen-height 900 -screen-fullscreen 0 --riskai-map europe --riskai-players 16 --riskai-seed 160212 --riskai-probe --riskai-probe-warmup 900 --riskai-probe-warmup-commander --riskai-probe-recruits 24 --riskai-probe-seconds 90 --riskai-path-budget 1000 -logFile ./RiskAI/Logs/path-budget-1000.log
```

Compare `firstMoveHumanActive*Ms`, `pathPending*`, frame thresholds, and the
world phase timings. The first exploratory Windows runs are documented in
[VALIDATION-v0.21](VALIDATION-v0.21.md); unequal battles and external load
prevent attributing their differences solely to this budget. The default
remains 500. The browser probe exposes the
same switch through `scripts/check_web_player.py --path-budget 500` or
`--path-budget 1000`; keep its other options identical for that A/B.
