# Mobile UI and Azure update — 2026-09-09

This revision applies the same interface, input, elimination and AI rules to all
four scenarios. The global player ceiling remains 16. Capacity is additionally
limited to `floor(cityCount / 3)`: Classic 11, Riverlands 14, Europe 16 and New
World 16. Setup defaults to the scenario maximum; after manually adjusting the
count it preserves that choice, clamped when switching to a smaller map.

## Behavior

- Europe is first in setup and its visible name omits Reforged. Loading copy
  wraps within the available width. The browser loader omits the device slogan.
- Buildings expose their available production directly, and enemy buildings
  expose no production. Units expose six vector command buttons directly.
  Selection changes and ownership changes update the existing shared UI.
- Portrait HUD height is 64 instead of 96 logical pixels; its footer is 224
  instead of 260. Resource and ranking counters use city, sword and shield marks.
  Ranking rows carry player colors and an eliminated status.
- Colors use Warcraft III's extended palette (patch 1.29), retaining the existing
  blue human / red first rival assignment.
- Middle drag and three-contact drag rotate yaw and pitch. Right drag still pans.
  Pitch stays between 35 and 80 degrees; reset restores 55 degrees and zero yaw.
  Releasing part of a three-finger gesture cannot turn the remainder into orders.
- Strategic mode enters at zoom 120 and exits at 105.6. Overview symbols and
  picking share terrain anchors, cached per match to avoid per-frame raycasts.
  Diamond rotation is composed in logical coordinates before the display-density
  matrix. The previous `GUIUtility.RotateAroundPivot` order displaced their
  centers on high-density displays; actual DPR-2 browser captures exposed this.
- Browser canvas density is capped at 2 instead of 1.5. The Mobile URP profile,
  also used by WebGL, renders at scale 1 instead of 0.8 and uses 2x MSAA.
- AI recruits immediately after the countdown. Its first ready recruit can
  depart promptly in either difficulty; fast offensive searches stop after the
  first dispatch or five seconds. Subsequent decisions retain their normal
  difficulty cadence, and threatened posts keep their reserves.
- A player with no buildings, soldiers or ships is eliminated once and cannot
  respawn. Loaded transports keep their owner alive. Elimination announcements
  are queued; local defeat remains visible while surviving opponents play on.

## Validation and publication

The latest results contain 96 distinct passing Unity cases, retaining the first
failed run and the successful reruns in `audits/v0.22-mobile-azure/`. This includes
player limits, early AI dispatch and subsequent cadence, defensive reserves,
paid naval expeditions, loaded-transport survival, elimination, camera gestures,
country-marker geometry at DPR 1/2/3, contextual production and six-button layout.

The first PlayMode run passed 48/51. Two naval fixtures depended on the removed
grace period, and a real early-offense regression could consume a defensive
reserve. The fixtures were updated and offense now defers while posts are under
threat; all affected suites passed again. Browser inspection subsequently exposed
the diamond transform order and sixth-button wrapping, both corrected and tested.

Hidden-window native capture could report layout dimensions but produced no
usable images (D3D12 failed; D3D11 returned black frames). Those images were
explicitly rejected. Actual rendered visual checks use Edge WebGL. Browser
viewport and touch emulation remain desktop tests; they do not establish
performance on a physical phone.

The existing nginx VM is updated using `scripts/deploy_azure_vm.py`. It verifies
the uploaded archive and each staged file, retains previous asset URLs, and
switches the release symlink atomically. Updated index references use a unique
release directory so the one-hour asset cache cannot serve the previous build.
The deployment receipt includes the previous release and rollback command.

Published URL: **https://riskai-demo.spaincentral.cloudapp.azure.com/**.
Active release: `20260908T225132Z-413c435`, built from runtime commit `413c435`.
Previous release `20260908T201807Z` remains available for rollback. All ten
published files matched their SHA256 values on a public HTTPS readback; gzip
encoding and the WebAssembly MIME type were verified.

Both final builds succeeded: Windows `206,950,958` bytes and Web `57,506,532`
bytes reported by Unity. The manifest records all 194 exported files. The final
browser checks covered 390×844 at device DPR 3 (canvas capped at 2), and 844×390
at DPR 2, including direct production, recruitment, six actions, ranking, middle
drag and emulated three-finger orbit. A browser screencast captured the actual
loading panel within its narrow bounds. Review images are stored with the audit.

The public Azure command probe ran for 60.01 real seconds / 60.05 simulation
seconds at 1280×800: 175 commands applied, zero rejected, all six tracked units
moved, zero unmoved survivors and no pending commands. Frame samples averaged
29.01 ms, maximum 59 ms, with zero frames over 100 ms. This was an uncontrolled
desktop Edge / RTX 5080 Laptop GPU sample with 293→232 units, not a physical
mobile measurement or a controlled comparison with earlier builds. Five probe
destinations became unavailable as the battle evolved; only one tracked mobile
survived, so this is a live-match smoke check rather than a sustained army test.
No runtime/page errors occurred; the only browser console error was the missing
favicon. It does not close the previously documented large-army performance work.

The broader WC3 inheritance audit and pending roster decisions remain documented
in `audits/REFORGED-LATEST-INHERITANCE.md` and `audits/SOURCE-ROSTER-NEXT.md`.
This revision does not add the proposed eight source units or claim exact
inherited Reforged balance where the effective data layer is still unresolved.
