# Territorios v0.32 — fronteras que siguen el relieve y overlay de la hoguera

Capturas: `Captures/v0.32-overlay/` (ignorado por git). `before-*` = v0.31 publicado, `after-*` = esta rama.
Casos pedidos: `review-{puertas-de-oriente,marca-del-alba,estrecho-del-norte}-before-after.png` (hoguera
seleccionada, cámara de juego a 55°) y `review-island-before-after.png` (isla del Estrecho: sin selección
antes / sin selección después / seleccionada después). Cada `*-tactical-*.png` tiene su `*-plain.png` sin selección.
Se generan con `RiskTerritoryReview.Capture ... --riskai-territory-focus "País;País"`.

## Qué dibuja el resaltado en el suelo

Al seleccionar una hoguera, `StrategicMapView` activa copias de las mallas del terreno con el material
`StrategicTerritory` en modo inspección (`_Overview=0`): relleno beige del país seleccionado y contorno.
Muestrea el mismo atlas que la vista estratégica (`TerritoryField` suavizado con B-spline y distancias de
frontera sub-texel), así que no hay polígonos ni celdas. `TerritoryOverlay.shader` no lo usa ningún script.
El relleno beige se queda como estaba, a petición del usuario.

Las líneas rectas venían de otro sitio:

1. **Bisectrices rectas en llano** (la banda vertical bajo la ciudad): en campo abierto, el crecimiento en
   8 direcciones deja la frontera entre dos ciudades en una recta alineada con los ejes o las diagonales,
   de hasta 29 u en Las Marcas y 34 u en Cuatro Riberas.
2. **Fronteras que cortan el terreno bajo un acantilado**: el crecimiento no veía el relieve y las mesetas
   se comían franjas de terreno bajo el precipicio.
3. **La recta diagonal sobre la arena de la isla**: es la malla del lecho marino (`StrategicTerrain.CreateSeabed`,
   360×64 celdas a `Height-0,012`), que también cubría las islas; sus cuerdas asomaban sobre la arena y además
   ocultaban el resaltado en las islas (la isla del país seleccionado salía sin tintar).

## Cambios

- `TerritoryField` (solo mapas propios; en los importados manda la pintura del mapa WC3):
  - **Acantilados**: un paso con pendiente > 0,55 (coste completo a partir de 1,0) cuesta hasta 15× (el
    agua cuesta 6×), así las fronteras siguen el precipicio salvo que las ciudades del país estén a ambos lados.
  - **16 vecinos** (saltos de caballo, que no atraviesan agua) en vez de 8: el crecimiento es casi
    isótropo y deja de producir rectas a 0°, 45° y 90°.
  - **Rugosidad** determinista (Perlin, ±35 %, escala ~14 u) en el coste: dobla las bisectrices rectas.
  - Ríos, lagos y mar ya eran agua (coste 6×).
- `StrategicTerrain.CreateSeabed`: se omiten los triángulos del lecho que quedan del todo dentro de una isla.
  La isla ya tiene su malla; las orillas del lecho no cambian.
- Minimapa: la preferencia pasa a `riskai.hud.minimap.v2` (`BattleHud.DesktopMinimapPreference`). Visible
  por defecto en escritorio; un "oculto" guardado por una versión anterior ya no se aplica. Al pasar de la
  disposición compacta a la de escritorio (un canvas web que empieza pequeño) se vuelve al valor de escritorio.
  El PlayerPrefs del editor y el de los jugadores web son almacenes distintos, así que los tests no pueden
  filtrarse a los jugadores; el cambio de clave cubre el valor antiguo del navegador.

## Métricas (EditMode, `TerritoryFieldTests`)

| Mapa | Recta más larga de frontera (u) antes → después | Pasos de acantilado que son frontera antes → después | Pasos llanos que son frontera |
|---|---|---|---|
| Las Marcas | 29 → 19 | 0,4 % (18) → 5,0 % | ~1 % |
| Cuatro Riberas | 34 → 24 | 0,3 % (8) → 3,8 % | ~0,9 % |

Nuevos tests: `AuthoredCountryOverlayMasksHaveNoStraightBorders` (misma detección que `GroundZoneShapeTests`,
sobre la máscara exacta que pintan el atlas y el resaltado; máximo 24 u) y
`AuthoredBordersPreferCliffsOverTheTerrainBelow` (un paso de acantilado debe ser frontera > 3× más a menudo que
un paso llano), además de `HudPreferenceTests`. Siguen pasando la contención de ciudades, la contigüidad y los
tests de hogueras. EditMode 41/41; PlayMode 35/35 (`StrategicCountryBorder`, `CountryCamp`, `MapVariant`,
`CityClaim`, `BattleHud`).

## Pendiente (terreno)

En la isla del Estrecho queda un borde recto donde el lecho marino pinta arena de orilla por encima del agua
fuera de la forma analítica de la isla (lóbulo este). No es territorio (no es tierra) y está en la malla del
lecho y en su sombreado de costa, que son del agente de terreno.
