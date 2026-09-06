# Visual/readability audit v0.13

Scope: bounded review of the current Unity UI/terrain code and the supplied v12 screenshots. This is a visual audit; it does not change C# or extract/copy Blizzard assets. The WC3 comparison is limited to the supplied references (`references/visual/BetaTest3_SS_3.png`, `references/visual/BetaTest3_SS_2.png`, `references/visual/i00W03C.png`, `references/visual-v07/sc2-cliff-reference.jpg`, and `references/warcraft-ui-day.jpg`). It is not a review of every WC3 asset or map.

## Evidence reviewed

- Runtime captures: `RiskAI/Screenshots/v12-clearings-expanded/v12-player-overview.png`, `v12-player-city.png`, `v12-player-highlands.png`, `v12-player-river.png`, and `v12-player-zoomout.png`; the matching `v12-clearings-classic` overview, city, highlands, river, and zoomout captures.
- HUD geometry: `RiskAI/Assets/RiskAI/Scripts/BattleHud.cs:10`, `:68-80`, `:317-380`. At the 1600x900 capture size, the top frame is 48 px and the bottom frame is 208 px. The minimap is drawn at 208x156 from a 48x37 point-filtered texture.
- Camera: `RiskAI/Assets/RiskAI/Scripts/RiskBootstrap.cs:55-56` uses a perspective camera, FOV 44, and a 49 degree downward / 30 degree yaw rotation. `RtsCameraRig.cs:7-8`, `:47-75` uses zoom 17-44 (60 on expanded maps) and positions that perspective camera from `orthographicSize`.
- World labels and markers: `BattleHud.cs:334-357` draws town labels only when selected, hovered, or health bars are requested; it skips tower bars at `:351`. Minimap town squares are 8 px and unit dots are 2.5 px at `:365-374`.
- Team color: `VisualFactory.cs:127` defines blue, red, and neutral colors; `VisualFactory.cs:142-154` draws the unit rings.
- Terrain: `StrategicTerrain.cs:28`, `:35`, and `:92-131` use the `Meadow` material for land/cliff mesh and `RiverWater` for water. Procedural tree/clearing generation is present in the same file. Existing forest canopy/clearing work is treated as complete for this audit.
- Current active tower profile: `Core/ReforgedProfiles.cs:39` is 550 HP, 80 base damage plus 1d8, range 13, cooldown 0.9 s. The v0.12 inspector previously hardcoded 51-58 damage, 1.5 s, and range 8.5; in v0.13 `BattleHud.cs:250-255` reads the active profile, matching the shop panel. The earlier contradiction was between two UI panels in the prior source, not evidence of a different build.

## Ranked findings and bounded fixes

### Resolved in v0.13 — stale tower inspection values (historical v0.12 bug)

The prior `BattleHud.cs:253-255` implementation hardcoded stale tower values instead of reading `ReforgedProfiles.CapturableTower`, while the shop panel already read that profile. The v0.13 implementation now uses the shared profile, so the active inspector reports 81-88 damage, 0.9 s, and range 13 (`Core/ReforgedProfiles.cs:39`).

The bounded fix was applied in v0.13: the inspect panel uses the same profile fields as the shop panel. A new capture can verify the updated presentation, but the source contradiction is resolved.

### P1 — Give cities and garrisons a persistent strategic marker (factual behavior, readability impact)

In the current code, a town's name is hidden unless the town is selected, hovered, or `ShowHealthBars` is enabled (`BattleHud.cs:334-341`). Tower health bars are deliberately skipped (`:349-357`). At the supplied zoomout captures, this leaves most cities as small building silhouettes and most units as tiny blue/red points; the minimap only supplies 8 px town squares and 2.5 px unit dots (`:365-370`). The close city capture is legible, but the overview/zoomout capture makes it hard to tell which settlement has a garrison, a tower, or an active capture state without selecting it.

Bounded fix: add a compact always-on world icon or flag for each city, with a separate garrison/tower glyph and a contested/capturing accent. Keep the full name and progress bar on hover/selection. This preserves the current clean view while making the strategic state readable at the camera's normal zoom range. This is a UI/readability recommendation, not a proposed rules change.

### P1 — Make the capture area legible as an interaction area (visual contract)

`Settlement.cs:45-50` places the claim point away from the building and creates a 1.55 m ring, while the capture controller uses a larger unit query in runtime code. The ring is therefore a useful center marker but not a complete visual explanation of the space in which units can contest a city. In the city screenshot, the ring/defender formation is easy to read at close range; in the overview it is effectively lost among trees and unit dots. The source map also used a square capture rect, so a circular ring alone does not communicate the legacy boundary shape.

Bounded fix: keep the existing ring as the precise center marker, and add a subdued outer interaction halo or a short-lived selection overlay showing the actual contest/arrival area. If source parity is required, offer a square outline in an inspection/debug mode rather than changing capture logic. Label this as a range visualization so it cannot be mistaken for a new mechanic.

### P2 — Add restrained terrain value/elevation separation (visual comparison/opinion)

The v12 expanded and classic captures have attractive clearings and readable buildings, but most land is a continuous dark-green `Meadow` field with similarly valued tree clusters. The highlands and river screenshots show the height break and water path, yet the cliff face is visually smooth and the bright cyan water edge competes with the land in places. The supplied WC3 screenshot (`references/visual/BetaTest3_SS_3.png`) has stronger large-scale water/cliff contrast and a more textured coastline; the SC2 cliff reference has a more explicit plateau edge. These are visual comparisons, not evidence of a simulation defect.

Bounded fix: introduce a small, controlled palette ramp for elevation/biome bands (for example, darker lowland, warmer clearing, desaturated highland) and a less saturated water-edge transition. Keep the current procedural geometry and tree-clearance rules. The user impact is faster route, height, and shoreline recognition at zoomout without adding map mechanics.

### P2 — Preserve unit identity at strategic zoom (visual comparison/opinion)

The current perspective camera and 49 degree pitch give the close city and river views a strong 3D presentation. At zoomout, however, the unit silhouettes reduce to small colored figures/dots while the bottom HUD occupies 208 of the 900 captured pixels (`BattleHud.cs:79-80`); the minimap is also a 48x37 raster (`:362-370`). The WC3 reference uses stronger, larger iconography and heavier UI framing, while the current view favors a cleaner modern RTS slab.

Bounded fix: add camera-facing unit/city badges or scale only the strategic markers as zoom increases; do not globally enlarge the unit models. A small shape cue in addition to blue/red color would also improve team recognition for color-impaired players. Keep the existing camera framing unless playtest data shows that the HUD/world balance is the primary complaint.

## What is already working

- Team colors are repeated on rings, roofs/flags, town squares, and minimap dots (`VisualFactory.cs:127`, `BattleHud.cs:365-374`), and the close city capture clearly separates blue units from red/neutral elements.
- The selected-city panel is information-dense and readable at 1600x900; recruitment, tower status, and the city name are in distinct areas (`BattleHud.cs:201-212`).
- The expanded/classic captures show deliberate clearings around buildings, readable bridges, and distinct forest edges. No new forest-canopy or clearing fix is proposed here because that work is already being handled elsewhere.

## Validation limits

This report is based on static screenshots and source inspection. It does not establish runtime occlusion, exact camera projection at every aspect ratio, tower attack visibility, or the success of a ranged capture maneuver. Those require the live build and are outside this visual-only pass.
