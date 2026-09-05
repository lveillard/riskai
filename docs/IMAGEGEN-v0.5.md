# Textura original v0.5

- Modo: generación nueva con la herramienta integrada ImageGen.
- Entrada: descripción; sin reutilizar ni editar la captura de Warcraft III del usuario.
- Archivo integrado: `RiskAI/Assets/RiskAI/Resources/Painted/StrategicAtlas.png`.
- Salida: PNG cuadrado, atlas 2×2: hierba, tierra/musgo, granito, agujas de conífera.
- Importación: sRGB, mipmaps, filtrado trilineal, anisotropía 8. Unity mezcla los materiales sobre geometría propia.
- La arquitectura conserva el atlas original documentado en IMAGEGEN-v0.4.md; los abetos se modelan en código. Los personajes siguen usando KayKit CC0.

## Prompt exacto

Create a production texture atlas for an actual Unity medieval strategy game whose target is the subdued, cool Warcraft III Risk map aesthetic. One square image, EXACTLY four equal square material tiles in a 2x2 grid without gutters. Top left: very fine short dark emerald-gray meadow grass, subtly brush-painted tiny narrow blades with low contrast, no clover or large leaves, no yellow. Top right: compact muted gray-green earth and sparse moss, low contrast woodland clearings, no orange sand. Bottom left: irregular rough cool blue-gray granite bedrock, natural cracked rock masses, never brick or masonry or paving, cold slate shadows. Bottom right: dense evergreen fir needle boughs, finely painted dark pine green with cool teal midtones and almost black undersides, very few restrained light edges, no broad round leaves. These are seamless repeating albedo materials, flat orthographic evenly lit, each quadrant fills its entire square and must tile naturally. Hand-painted classic fantasy RTS aesthetic, small-scale readable detail, no photographic gloss or giant shapes, no text, labels or watermarks. Exact quadrant boundaries at the image midpoint. Intended to read as terrain at a high elevated strategic camera, not a colorful toy game.
