# Third-party notices

## Cinzel Decorative

`RiskAI/Assets/RiskAI/Resources/UI/CinzelDecorative-Regular.ttf` is by Natanael
Gama, copyright 2012, licensed under the SIL Open Font License 1.1. The
unmodified font is used for display headings; body text uses Unity's default
font. Source: https://github.com/google/fonts/tree/main/ofl/cinzeldecorative

The complete license is in `RiskAI/Assets/RiskAI/Art/UI/Cinzel-OFL.txt` and is
copied into both Windows and Web export directories.

## KayKit Adventurers

Character models, animations and equipment under `RiskAI/Assets/RiskAI/Art/KayKit/` are by Kay Lousberg, distributed under CC0. The full license is included in that directory. Source: https://kaylousberg.itch.io/kaykit-adventurers

Warcraft III maps and visual reference screenshots are local research inputs, excluded from this repository. Terrain textures and environment meshes in the Unity project are original prototype artwork.

## wc3-risk-system rules

`RiskAI/Assets/RiskAI/Scripts/Core/RiskReferenceRules.cs` adapts the pure spawn and victory calculations from the local `references/wc3-risk-system` project:

- `references/wc3-risk-system/src/app/spawner/spawner-logic.ts`
- `references/wc3-risk-system/src/app/managers/victory-logic.ts`

Copyright (c) 2019 trigger. The upstream project is provided under the MIT License:

> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

Full text: `references/wc3-risk-system/LICENSE`.

## Risk map geography references

`RiskAI/Assets/RiskAI/Resources/Maps/Europe.json` derives numerical terrain, WPM pathing, city, capture-circle and country placement from **Risk Reforged v3.0 by Saran**. `NewWorld.json` derives the corresponding numerical layout from **Risk - New World v3.0**. Credit for the source map designs remains with their creators; Riesgus's terrain rendering, models, materials and building variants are independently authored.

- Risk Reforged listing: https://maps.w3reforged.com/maps/categories/risk/risk-reforged
- Risk New World v3.0 listing: https://www.wc3maps.com/map/167651/Risk_-_New_World_v3.0
- Extraction method, source archive hashes and adaptation details: `docs/ITERATION-v0.14.md`, `docs/audits/SOURCE-SHALLOWS-v0.27.md` and each JSON's `metadata`.

The original archives and Warcraft art are not bundled. These numeric data files are source-derived map layouts; they should not be described as wholly original maps or as KayKit CC0 content.

## NASA Blue Marble Next Generation (ground colour)

`RiskAI/Assets/RiskAI/Resources/Maps/EuropeGround.bytes` and `NewWorldGround.bytes` are small PNGs
(one texel per W3E cell) baked by `scripts/bake_ground_colors.py` from NASA's **Blue Marble Next
Generation** land-surface mosaics (Reto Stöckli, NASA Earth Observatory), May and July 2004,
5400×2700: https://visibleearth.nasa.gov/collection/1484/blue-marble (records 74042 and 74092).
NASA imagery is not copyrighted and is used under NASA's media usage guidelines
(https://www.nasa.gov/nasa-brand-center/images-and-media/); credit: "NASA Earth Observatory — Blue
Marble Next Generation". The source images are downloaded to `references/bluemarble/`
(git-ignored); each imported map is georeferenced to approximate latitude/longitude with an
affine + thin-plate warp fitted to its country positions, and the map's own coastline stays
authoritative (only colour and a derived aridity value are taken from the imagery).
