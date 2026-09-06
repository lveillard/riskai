# Grok 4.6 review — v0.17

## Preliminary attempt

An initial large v17 packet was invoked with `grok-4.6`, tools/subagents/web/plan
disabled, and one turn. It produced no stdout, JSON, or `stopReason` after more
than seven minutes and was stopped. Its output is empty and exit code is `-1`.
It is not a completed review and yielded no findings. The ignored request and
status are in `.tools/grok-v17/round1/`.

## Round 1: controls, telemetry, and pre-final sea navigation

**Completed:** 6 September 2026. A new, single `grok-4.6` call used the current
reduced source packet with one turn, `--tools none`, `--no-subagents`,
`--disable-web-search`, `--no-plan`, `--verbatim`, and JSON output. It ended
with `stopReason: end_turn` after about eight minutes. The ignored request,
response, stderr, and PID are in `.tools/grok-v17/round1-retry/`.

The packet preceded the final navigation cache/direct-route/stagger changes, so
claims about sea routing require comparison with final source before acting.
The model supplied four claims and one performance risk:

| Claim | Disposition before Round 2 |
| --- | --- |
| A same-grid-cell sea route with a blocked direct segment has no graph detour. | Confirmed in the algorithm and fixed by allowing the common cell as a waypoint. An initial proposed Europe witness did not reproduce under Unity's sampler and was discarded; the regression uses an isolated numeric terrain fixture. |
| First-move telemetry is cancelled when an already-near command completes without velocity. | Rejected as intended. This is explicitly a first-*movement* metric: no observed movement must remain cancelled, rather than confirming an order that did not move. |
| A probe destination at ±20 m can have no land/NavMesh sample. | QA validity/reporting issue, not a gameplay command defect. A probe must report an invalid/unavailable destination clearly; it must not manufacture a passing movement sample. |
| Player 0 can own no town after a long warmup, leaving no tracked probe units. | QA validity/reporting issue, not a gameplay command defect. An empty tracked cohort must fail or be marked invalid rather than claim command health. |
| Repeated A* could account for high ships/AI phase maxima. | Risk-to-measure only. The model did not establish a correctness fault. Final navigation telemetry and the later controlled measurement are the appropriate evidence. |

The confirmed same-cell navigation defect was fixed and regression-covered after
this review. The remaining QA validity items remain intentionally distinct from
gameplay command defects. Round 2 is a separate final-source re-review of
navigation, naval AI, timing, and probe semantics.

## Round 2: final-source sea routes, naval orders, and probe semantics

**Completed:** 6 September 2026. A separate single `grok-4.6` call reviewed a
focused packet (about 152 KB) containing sea navigation, ships, naval AI, the command
probe, and v17 timing/diagnostics. The same restricted one-turn invocation ended
with `stopReason: end_turn`. Its request, JSON response, stderr, and PID are in
`.tools/grok-v17/round2/`. The model's response text, rather than its internal
reasoning trace, is the source for the dispositions below.

| Claim | Disposition after source comparison |
| --- | --- |
| A same-grid-cell sea route with a blocked direct segment can be rejected despite a valid via-cell path. | Duplicate of Round 1; its packet preceded the fix. See the numeric fixture above, not the discarded Europe coordinates. |
| A shore berth selected at `LoadRadius` can remain just outside the unload radius because ships stop 0.4 m short of their waypoint. | Fixed: reserve the arrival tolerance plus 0.05 m inside the berth search radius. The queued-unload test checks this margin and actual unloading. |
| A command probe with no Player 0 spawn or no sampled land destination runs a full measurement and labels it `valid=true`, although it later fails. | Fixed in QA: no cohort ends immediately with `valid=false`; unavailable destinations are counted, and two directions with no accepted move end the probe as invalid. No fabricated motion samples. |
| `Ship.IsAtOrRoutingTo` can keep a ship with a blocked, unchanged route goal from being replanned on later AI decisions. | Fixed: after three simulation seconds without waypoint progress, the AI may request a new route. A stalled-route regression accompanies the existing active-route retention test. |
| `OrderEmbark` can return a success string and send the soldier after `Ship.MoveTo` has set a route error. | Fixed in the public helper and `OrderDisembark`: propagate the route error before ground movement or a success message. The controller's main boarding flow already checked this. |

The review also listed cache-key identity, the eight-cell `NearestOcean` search
limit, single-rebuild spatial timing maxima, AI catch-up cadence, and the
probe's non-Player-0 synthetic units as **risks to measure**. They are not
confirmed defects from source inspection and should not be treated as such.

Two completed Grok reviews were obtained for v0.17. The earlier preliminary
attempt timed out without output and is deliberately excluded from that count.
