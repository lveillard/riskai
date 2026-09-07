# Grok Web v0.20 performance review

One Grok 4.6 review completed against the current WebGL performance packet. Its raw final response is stored at `.tools/grok-web-v20/round1/output.json` (exit 0, one turn, 349.0 seconds). The packet contained the probe’s aggregate result and relevant runtime diagnostics, tick, picking, Web template/bridge, and terrain/decor source. It did not include a frame timeline or a renderer trace.

## Observed measurement

The Europe WebGL probe at 1600×900 was valid: 293 to 321 units, 118 applied commands, no rejected or pending commands, and all six tracked units moved. Its 1,078 measured frames averaged 55.90 ms with a 741 ms maximum; 137 exceeded 50 ms, 108 exceeded 100 ms, and 70 exceeded 250 ms. `BattleWorld` averaged 6.47 ms and 7.28 ms in its two 30-second windows. The log does not attribute the long frames to one simulation phase. An unrelated `rustc` process consuming about 5 GB while only about 7 GB remained free is a host confounder, not evidence of a game-code cause.

## Triage

| Hypothesis | Disposition | Small discriminating measurement |
| --- | --- | --- |
| Imported terrain/decor rendering, first-draw GPU uploads, or WebGL shader/format fallback consumes the frame outside `BattleWorld.Tick`. | Plausible, not confirmed. Frame time is much larger than the timed world work; the first interval is worse; the browser log contains format and unsupported hidden-URP messages. Source shows imported terrain, vegetation and ground-cover render paths but does not prove which renderer stalled. | Same Europe probe with only imported tree/ground-cover renderer roots disabled, simulation unchanged; separately compare DPR 1.0. A large reduction in `avgMs` and `over250ms` supports the hypothesis. |
| The 30-second `RuntimeDiagnostics.Report` / `Debug.Log` contaminates headline maxima. | Plausible for the 9,824 ms and 741 ms maxima, not for all 70 long frames by itself. The report allocates a long string and logs it on the browser thread exactly twice in the run. | Drop the first five eligible frames and run one probe suppressing only `Debug.Log`, while retaining `LatestReport`. If maxima disappear but long-frame count remains, the report affected maxima only. |
| The existing telemetry cannot attribute off-phase stalls such as NavMesh, render, browser logging, particles or camera work. | Confirmed instrumentation gap. `BattleWorld` times only its simulation phases; `RuntimeDiagnostics` measures `unscaledDeltaTime`. The phase maxima cannot explain the reported frame maximum. | On frames over 100 ms, classify one bucket from stored world tick duration, a same-frame Gen0 delta, and sampled pending NavMesh paths: `slowTick`, `slowGc`, or `slowOutside`. Then distinguish high/zero pending paths inside `slowOutside`. |

`RtsPicking` performs several screen projections per candidate target, but no supplied caller proved a per-frame scan at the measured unit count. It should receive a temporary call counter only if the first split leaves a render-independent `slowOutside` bucket.

## Limits

This review did not establish a WebGL renderer cause, test touch hardware, or compare an identical native run. It recommends bounded A/B probes and attribution counters before a rendering or simulation rewrite.

The parent also checked the WebGL Player Settings batching flag. Its disabled
value does not establish that the existing runtime Combine calls fail:
[Unity 6.3 explicitly says runtime batching does not require that setting](https://docs.unity3d.com/6000.3/Documentation/Manual/static-batching-enable.html).
No batching switch was changed on that assumption.

## Bounded draw-call ablation

On the final v0.20 export, the normal Europe probe averaged 64.95 ms with
61 frames over 250 ms. A consecutive diagnostic run with the same map, seed,
viewport, DPR and recruit fixture suppressed 3,799,249 WebGL draw calls over
the whole browser run, while leaving Unity's simulation and scene processing
active. It still averaged 59.78 ms, with 47 frames over 250 ms. Both probes
passed movement/command checks. Evidence:
`Captures/web-v20-final-europe` and `Captures/web-v20-final-europe-no-draw`.

Skipping draws did not eliminate the problem. This does not exclude CPU
render preparation, culling, state changes or other work outside the tick:
the experiment skips the final draw methods, not those earlier stages.
An unrelated compiler was active in both runs and caches can differ, so
the small average difference is not an established optimization benefit.
The suppressed-draw run is deliberately not a playable performance result.
