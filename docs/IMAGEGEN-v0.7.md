# Materiales generados para v0.7

Modo: herramienta ImageGen integrada; sin CLI. Texturas originales, sin importar arte de Blizzard al juego.

- Atlas de pizarra, caliza, grava y musgo: [CliffAtlas-v07.png](../RiskAI/Assets/RiskAI/Resources/Painted/CliffAtlas-v07.png). Original: `C:/Users/lveil/.codex/generated_images/01a06f0f-278e-7ae1-8117-3f140f998561/exec-94a5bdce-bb5e-424f-90a3-020096795ded.png`.
- Rama de abeto: [FirBough-v07.png](../RiskAI/Assets/RiskAI/Resources/Painted/FirBough-v07.png). Original: `C:/Users/lveil/.codex/generated_images/01a06f0f-278e-7ae1-8117-3f140f998561/exec-0d4d732a-66c8-4560-bbbb-0bd23bbd410c.png`.

La primera rama llegó como RGB con fondo gris y se descartó para el juego por sus bordes claros y exceso de detalle. El material final usa [FirBough-v07b.png](../RiskAI/Assets/RiskAI/Resources/Painted/FirBough-v07b.png), original `C:/Users/lveil/.codex/generated_images/01a06f0f-278e-7ae1-8117-3f140f998561/exec-9cc19c6a-4744-4e0a-8e75-9096021e2325.png`. Su fondo negro permite un recorte estable en el shader. Se monta en 42 ramas tridimensionales por árbol, con sombras recortadas.

## Prompt del atlas

```text
Use case: stylized-concept. Asset type: original production albedo texture atlas for a Unity fantasy RTS terrain. Generate one square raster image with EXACTLY four equal square texture tiles in a precise 2x2 grid without gutters. TOP LEFT: natural dark gray slate escarpment face, large angular fractured slabs with irregular diagonal and vertical cracks, geological rough facets and subtle taupe variation, NO masonry, NO brick courses, NO evenly stacked horizontal blocks. TOP RIGHT: weathered pale cool limestone bedrock, flat irregular broad stone plates with subtle gray fissures and small earthy seams, suitable for exposed highland ground. BOTTOM LEFT: mixed gravel and coarse brown grit with small gray pebbles and chips, sandy soil between, small scale, no large focal stones. BOTTOM RIGHT: lush muted forest floor moss and short deep olive grass with sparse fine brown leaf litter, very fine readable texture, no large clover leaves. All four are flat, evenly lit, tileable albedo materials, no perspective, no scene, no directional baked shadows. Style: refined hand-painted game texture, angular rock language and restrained palette inspired by natural cliffs in StarCraft II, harmonized with a Warcraft III inspired green medieval landscape, original artwork. Moderate fine detail and medium-low contrast, no noisy photoreal texture, no giant shapes or dominant focal motif. Exact boundaries at image midpoint. No labels, writing, grid lines, borders, watermarks, architecture, icons, models, characters, scenery.
```

## Prompt de follaje descartado

```text
Use case: stylized-concept. Asset type: production cutout texture for 3D fir tree foliage cards in a Unity fantasy RTS. Create ONE single flat spread of a fir bough seen from above, shaped like an elongated triangular fan, with a narrow brown stem at bottom center and dense sweeping branch fingers radiating toward the broad upper half. Needles arranged in distinct feathery clumps, strongly serrated natural silhouette, many transparent gaps between the outer branch tips. Fine layered green needles, painterly Warcraft III inspired stylization with modern material clarity. Medium emerald and moss green with yellow green highlights and dark blue green recesses. Occupy about 90 percent of a square image. Fully isolated on a genuinely TRANSPARENT background with alpha, including gaps between needles. Even diffuse illumination, no directional cast shadow, no perspective, no ground, no tree trunk or full tree, no objects, no text, no watermark. This is an original game foliage material, not a scene or illustration of a forest.
```

## Prompt del follaje final

```text
Use case: stylized-concept. Asset type: single production texture for a 3D pine bough mesh card in a fantasy real time strategy game. ONE flat top-down fir branch fan centered on an EXACT PURE BLACK (#000000) opaque background. No checkerboard, no transparency preview, no gray, no white. The bough has a short stem at bottom center, five chunky wide branches fanning out to the sides and toward the top, composed of BROAD SIMPLE hand-painted needle clusters, with jagged feathered outer edges. Dense opaque green masses within the branch, only large readable cutout gaps between fingers, avoid individual hair thin needles or tiny speckled details. Warcraft III inspired stylized game foliage with modern clean painted shading, ORIGINAL artwork. Muted medium emerald greens, dark forest green undersides and restrained light moss green tips. No neon yellow, no white highlights. Fill the square canvas generously, entire bough fits with 5 percent pure black margin all around. Flat albedo illustration, no lighting effect around edges, no shadows on background, no ground, no full tree or scene, no labels or watermark.
```
