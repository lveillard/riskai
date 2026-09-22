# Moscov/Moscow en Europe y reutilización de `wc3-risk-system`

Auditoría estática, 22 septiembre 2026. El checkout anidado quedó limpio tras
`git fetch --prune origin`: `HEAD == origin/main ==
12822eb3d9ff37b1514028cff831f12c762a0d67` (5 junio 2026). Su sparse-checkout
no materializa los mapas; para esta comprobación se leyeron sus objetos con
`git show`, sin cambiar el working tree ni ejecutar TypeScript, Warcraft o
Unity.

## Respuesta corta

Sí: dos ciudades en Moscov/Moscow es normal en las dos fuentes comprobadas,
pero no demuestra que todos los mapas históricos llamados «Europe» sean
idénticos. La Europe de este runtime procede de **Risk Reforged v3 de Saran**
(69 países, 212 ciudades); el `wc3-risk-system` actual describe otra Europe
(81 países, 233 ciudades). Ambas tienen un país de dos ciudades en esa zona.

La etiqueta repetida `Moscov (Russia) 211/212` tampoco es una duplicación
accidental del runtime: el exportador local documenta que el JASS asigna países
pero no contiene topónimos individuales, por lo que genera deliberadamente
`<país> <índice>` ([`export_playable_risk_maps.py:397-410`](../../scripts/export_playable_risk_maps.py:397)).

## Trazado de las dos ciudades en la fuente local

| Capa | Evidencia | Lectura |
| --- | --- | --- |
| Recurso runtime | `RiskAI/Assets/RiskAI/Resources/Maps/Europe.json`: `$.countries[68]` = `Moscov (Russia)`, `count: 2`; `$.cities[210]` y `$.cities[211]` = `europe-211`/`europe-212`, ambos `country: 68`, `port: false`. | El recurso que consume Unity contiene exactamente dos entradas. |
| Derivado auditable | [`risk-reforged-v3-placements.json:3-8`](../../data/derived/risk-reforged-v3-placements.json:3), [`:75-78`](../../data/derived/risk-reforged-v3-placements.json:75), [`:11051-11096`](../../data/derived/risk-reforged-v3-placements.json:11051), [`:11098-11143`](../../data/derived/risk-reforged-v3-placements.json:11098). | `city_count: 212`, `region_count: 69`; región 69 es Moscov y los índices de ciudad son 211 y 212. |
| Tabla compacta | [`risk-reforged-v3-placements.csv:1`](../../data/derived/risk-reforged-v3-placements.csv:1), [`:237-238`](../../data/derived/risk-reforged-v3-placements.csv:237). | Ambos son `h00N`, creación fuente 235/236, región 69; coordenadas nativas `(14144,9344)` y `(13952,8384)`. |
| JASS: unidades | `references/maps/reforged-v3-source/war3map.j:3617-3618`. | `gg_unit_h00N_0235` y `gg_unit_h00N_0236` se crean explícitamente en esos dos puntos. |
| JASS: pertenencia | `war3map.j:6992-6998`. | Las dos unidades se añaden consecutivamente al último grupo de región antes de incrementar `udg_tempRegionIt`; no es una segunda copia del mismo ID. |
| JASS: nombre/región/spawn | `war3map.j:3821-3822`, `6096`, `6193`, `7593`. | Existen rectángulos de texto y spawn de Moscov, `CountryRegions[69]`, texto `Moscov (Russia)` y `CountrySpawnRegions[69]`. |

La transformación está documentada como
`x=(nativeX-sourceGridCenterX)/50`, `z=(nativeY-sourceGridCenterY)/50`, con
centro nativo `[2560,0]` (metadata de `Europe.json`). Así `(14144,9344)` y
`(13952,8384)` producen `(231.68,186.88)` y `(227.84,167.68)`, los puntos de
`europe-211`/`europe-212`; no hay que corregirlos como si fueran una ciudad
duplicada.

La fuente descargada y su hash están registrados en
[`references/SOURCES.md:5-13`](../../references/SOURCES.md:5); la propia nota
advierte que es una entrada de investigación y que no se ha ejecutado en
Warcraft III/World Editor.

## Comparación con el `wc3-risk-system` actual

El mapa externo también declara dos ciudades, pero con topónimos distintos:

- `src/configs/terrains/europe.ts:785-792` define el país `Moscow` con
  `Vladimir` y `Moscow`, ambos sin puerto; [permalink al commit auditado](https://github.com/Warcraft-3-Risk/wc3-risk-system/blob/12822eb3d9ff37b1514028cff831f12c762a0d67/src/configs/terrains/europe.ts#L785-L792).
- `docs/gameplay/maps.md:51` describe 81 países/233 ciudades y `:81` lista
  `Moscow | 2 | 0`; [permalink](https://github.com/Warcraft-3-Risk/wc3-risk-system/blob/12822eb3d9ff37b1514028cff831f12c762a0d67/docs/gameplay/maps.md#L51).

Por tanto, «Moscov tiene dos» es coherente tanto con el mapa local Saran como
con la variante `risk_europe` del repositorio externo. No debe mezclarse el
nombre `Vladimir` ni sus coordenadas con el recurso local: son variantes de
catálogo/mapa. El exportador local conserva la procedencia numérica y no
inventa topónimos.

## Qué se puede reutilizar

El repositorio externo compila TypeScript a Lua para el motor Warcraft III y
separa `city`, `game`, `managers`, `state` y `utils`; sus reglas puras de
victoria, ingresos y distribución tienen tests sin globals de Warcraft
([README:146-180](https://github.com/Warcraft-3-Risk/wc3-risk-system/blob/12822eb3d9ff37b1514028cff831f12c762a0d67/README.md#L146-L180)).
Eso lo convierte en una buena **referencia de contratos y casos**, no en una
biblioteca binaria o un adaptador Unity.

| Área | Ya existe en RiskAI | Reutilización nueva segura |
| --- | --- | --- |
| País/ciudad e ingreso | `MapLayout.cs:48-64,121-149`, `BattleRules.cs:84-159`, `CountryRecruitment.cs:6-50`. | Comparar casos de país completo/fragmentado con `income-logic.ts:1-100`; no importar sus cifras sin reconciliar los dos mapas. |
| Victoria | `BattleSession.cs:35,206` y `RiskReferenceRules.cs`; los dos cálculos puros de spawn/victoria de upstream ya están atribuidos en [`THIRD_PARTY_NOTICES.md:19-34`](../../THIRD_PARTY_NOTICES.md:19). | Usar `victory-logic.ts` como matriz de casos para empate, eliminados y umbral; el runtime local añade 60 %, mantenimiento 20 s y no copia su overtime automáticamente. [Permalink](https://github.com/Warcraft-3-Risk/wc3-risk-system/blob/12822eb3d9ff37b1514028cff831f12c762a0d67/src/app/managers/victory-logic.ts#L1-L81). |
| Captura/guardia | `CityClaimZone.cs`, guarniciones, `CountryCamp.cs`. | Revisar los invariantes conceptuales de `enter-region-event.ts` y `city/components/guard.ts`; no portar llamadas WC3 ni alterar ahora la implementación. |
| Transporte/naval | Puertos, barcos y expediciones ya están en el runtime/tests locales. | Los helpers puros `transport-auto-load`, `transport-unload` y `transport-patrol` son buenos contratos de transición y regresión, no código Unity directo. [Patrol permalink](https://github.com/Warcraft-3-Risk/wc3-risk-system/blob/12822eb3d9ff37b1514028cff831f12c762a0d67/src/app/utils/transport-patrol-logic.ts#L1-L110). |
| Minimap/labels | `BattleHud.Minimap.cs`, `MinimapMarkerRaster.cs`, `CountryCamp.cs` y labels propios ya cubren la presentación. | La lógica aislada de pool y high-water mark (`minimap-frame-pool-logic.ts`) puede inspirar una futura prueba de fugas/expansión; no es una orden de optimización ni cambia el mapa. [Permalink](https://github.com/Warcraft-3-Risk/wc3-risk-system/blob/12822eb3d9ff37b1514028cff831f12c762a0d67/src/app/utils/minimap-frame-pool-logic.ts#L1-L181). |
| Fases/lifecycle | `BattleSession` ya concentra arranque, cuenta atrás, pausa, ronda y fin. | El contrato explícito `modeSelection → preMatch → inProgress → postMatch` y reset de `GlobalGameData` pueden servir para revisar lifecycle/tests, sin trasladar el singleton TypeScript. [Permalink](https://github.com/Warcraft-3-Risk/wc3-risk-system/blob/12822eb3d9ff37b1514028cff831f12c762a0d67/src/app/game/state/global-game-state.ts#L7-L53). |

La licencia del código no acredita derechos sobre todos los modelos, texturas
e iconos embebidos en `maps/risk_europe.w3x`. El código raíz publica MIT (copyright `trigger`;
[LICENSE completo](https://github.com/Warcraft-3-Risk/wc3-risk-system/blob/12822eb3d9ff37b1514028cff831f12c762a0d67/LICENSE#L1-L21)),
por lo que una adaptación de código debe conservar aviso y atribución, como
ya hace `THIRD_PARTY_NOTICES.md`. Eso no demuestra licencia MIT para cada
recurso embebido: el `Overhead Buff Pack` incluido en el mapa sólo identifica
al autor HerrDave y pide conservar créditos ([readme del asset](https://github.com/Warcraft-3-Risk/wc3-risk-system/blob/12822eb3d9ff37b1514028cff831f12c762a0d67/maps/risk_europe.w3x/Assets/Overhead%20Buff%20Pack/readme.html#L2)).
Las dependencias npm tienen además sus propias licencias. Para este proyecto
son especialmente útiles las reglas y los tests independientes del motor;
cambiar catálogos, mapas o balance exigiría reconciliar sus variantes primero.
