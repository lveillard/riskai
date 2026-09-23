# Mapas v0.31 — vista estratégica, países de los mapas propios, hogueras y separación

Capturas en `Captures/v0.31-maps/` (ignorado por git), generadas con
`-executeMethod RiskAI.Editor.RiskTerritoryReview.Capture --riskai-territory-output Captures\v0.31-maps --riskai-territory-tag TAG`.
`before-*` son los grupos, posiciones y hogueras de v0.30 (ya con el shader nuevo); `after-*` es v0.31. Para la vista estratégica antigua, ver `Captures/v0.30-territories/after-*-strategic.png`. Nuevas por mapa: `*-strategic-neutral.png`
(mapa completo con todos los países neutrales salvo dos), `*-strategic-near.png` (zoom estratégico cercano),
`*-layout.json` (mapas propios: relieve, territorio, ciudades, puertos, hogueras) y
`review-{classic,riverlands}-layout-{before,after}.png` (dibujado desde ese JSON con los ids de las ciudades).

## 1. Vista estratégica

- **Relleno por país**: color del propietario sin lavar; los neutrales tienen un relleno apagado
  (`TerritoryAtlas.NeutralFill`), un poco distinto por país (alfa 0 en la paleta marca "neutral").
- **Fronteras entre países**: contorno oscuro (semiancho 3,2 px) con una línea clara fina (0,8 px), con
  antialiasing y **el mismo ancho en píxeles a cualquier zoom**. Las fronteras entre ciudades del mismo país
  son una línea fina y tenue. La costa nunca se dibuja como frontera.
- **Sin escalones**: `TerritoryField.Sample` vota con un B-spline cúbico sobre 4×4 celdas (antes era
  bilineal 2×2) y devuelve el *margen* del voto. `TerritoryAtlas.EncodeBorders` convierte ese margen en la
  distancia sub-texel a la frontera y la propaga (chamfer) a una textura nueva `_Borders` (R países, G ciudades).
  El shader reconstruye la distancia con interpolación bilineal *con signo* (los vecinos del otro lado
  cuentan negativo), así el cero cae sobre la frontera suave y no sobre la escalera de texels.
- **Nombres de país** (`BattleHud.DrawCountryLabels`): en el punto interior más alejado de fronteras y
  costa (`Atlas.LabelAnchors/LabelRadii`). Un nombre aparece cuando su país es lo bastante ancho en pantalla
  (los pequeños aparecen al acercar), los grandes se colocan primero y los que se solapan se omiten.
  Son IMGUI y las capturas del editor no los muestran; el test comprueba que cada anclaje está dentro de su país.
- Una sola fuente: todo sale de `TerritoryField`.

## 2. Las Marcas: nuevos grupos (33 ciudades, 11 países, 2–4 por país)

Criterio: cada isla con la costa de enfrente, cada meseta y cada cuenca un país, y el suroeste seco
repartido en cuadrantes compactos a ambos lados de la rambla. Ids como en `MapLayout`:

| # | País | Ciudades | Cambio |
|---|---|---|---|
| 0 | Marca del Alba | dawn, pine, cordillera-norte, mill | meseta occidental (recibe mill) |
| 1 | **Bahía de Poniente** (antes Valle de los Pinos) | isla-bruma, meadow | (A) isla del oeste + orilla de la bahía |
| 2 | Escarpa de Poniente | crest-west, dehesa-norte, encinar-centro, west | west deja de cruzar el lago hacia el centro |
| 3 | Cuenca del Fresno | ford, stone | valle central |
| 4 | Puertas de Oriente | ash, watch, senda-orient | meseta oriental |
| 5 | Sierra Carmesí | red, highland, guardia-oriental | sin cambios |
| 6 | Dehesa de Poniente | dehesa, encina, isla-roble | sin cambios |
| 7 | Campos del Secano | secano, trigal | oeste de la rambla |
| 8 | Lomas de Azafrán | azafran-norte, azafran, olivar, loma-sur | recibe azafran-norte (este de la rambla) |
| 9 | Costa de Sal | costa-sur, vigia-sal, isla-faro | sin cambios |
| 10 | **Estrecho del Norte** (antes Islas del Norte) | isla-viento, gate, torre-norte | (B) isla central + costa de enfrente |

Cómo leí las peticiones: (A) = isla-bruma (isla con palmeras y muelle arriba a la izquierda) + meadow (costa
verde de la bahía, con el puerto de Pinar y el de la bahía). (B) = isla-viento (isla con muelle largo) +
gate (ciudad portuaria justo al sur del canal). torre-norte también se une a (B) porque está en la misma
costa y más cerca del centro de (B) que del de Puertas de Oriente.

## 3. Cuatro Riberas: el mismo problema, corregido (44 ciudades, 11 países de 4)

Antes había grupos de 5–6 ciudades a lo largo del río y del este, y dos columnas occidentales entrelazadas.
Ahora el río separa oeste (24) y este (20), y cada isla va con la costa de enfrente:
0 Marca Occidental (west-01, west-05, west-07, isle-03) · 1 Bosques de Poniente (west-06, river-05, river-01, west-08) ·
2 Cuenca del Río (river-02, river-06, river-03, west-12) · 3 Costa Occidental (west-09, west-02, west-10, west-03) ·
4 **Bahía del Noroeste** (antes Colinas Occidentales: west-13, isle-01, west-11, west-04) ·
5 Ribera Alta (river-04, river-08, west-14, river-07) · 6 Altos Centrales (high-01, high-05, high-06, high-02) ·
7 Frontera Oriental (east-01, east-05, east-06, east-02) · 8 Llanuras de Levante (east-07, east-08, east-04, isle-05) ·
9 Puertas del Estuario (high-04, high-09, isle-04, isle-02) · 10 **Llano Central** (antes Archipiélago Norte: high-07, high-03, high-08, east-03).
Con tope de 4 y 44/11, todos tienen 4: la geografía decide cuáles.

## 4. Separación mínima entre ciudades y puertos

Regla: ≥ 16 u entre ciudades y desembarcos de puerto en mapas propios (dos círculos de captura de 6 u
más la huella del edificio); en los importados, ≥ 19 u (el mínimo original es 19,2, sin cambios).
Posiciones movidas (unidades de pad, ×1,4 = mundo):

| Mapa | Ciudad | Antes | Después | Motivo |
|---|---|---|---|---|
| Las Marcas | torre-norte | (15,45) | (10,43) | 10 u de la Dársena del Roble |
| Las Marcas | gate | (-2,24) | (-2,21) | 13,5 u del Puerto del Paso |
| Cuatro Riberas | high-09 | (18,63) | (18,61) | 14,4 u de la Dársena del Roble |
| Cuatro Riberas | west-14 | (-31,58) | (-31,55) | 15,0 u del Puerto del Pinar |
| Cuatro Riberas | west-13 | (-63,55) | (-63,54) | 15,9 u del Muelle del Oeste |

## 5. Hogueras centradas

Mapas propios: `CountryCamp.TryCentredSite` busca en anillos alrededor del centroide de las ciudades el
punto transitable **del territorio del país**, a ≥ 7 u de un edificio, ≥ 8,5 u del centro de un círculo de
captura y ≥ 7 u de un puerto, con pendiente suave y camino NavMesh a las ciudades. Si el país está partido
por un canal, gana el punto que llega a más ciudades (el empate va a tierra firme). Antes la hoguera iba
6 u a la izquierda de la primera ciudad del país.

Distancia media hoguera–centroide: Las Marcas 21,6 → 1,9 u; Cuatro Riberas 31,0 → 1,3 u. Distancia
mínima a una ciudad: 7,2 → 10,6 u y 7,2 → 7,0 u. Todas las hogueras de los mapas propios se han movido
(`review-*-layout-after.png`). Bahía de Poniente queda en la orilla de meadow (20 u del centroide, que está en el mar).

Importados: se conservan las hogueras originales. Ninguna está pegada a una ciudad: la más cercana está a
5,4 u (Gales), con 2 ciudades a 6–7 u del centroide; para países de 2 ciudades es normal quedar a un lado
del eje. Test `TerritoryFieldTests.SourceCampsStandInsideTheirTerritoryAwayFromCities`: dentro de su territorio y a más de 5 u de toda ciudad.

## 6. Tests

- `AuthoredLayoutTests.AuthoredCountriesAreCompactBalancedGroups` (2–4 ciudades; ninguna ciudad está más
  cerca del centro de otro país que del suyo, con un 5 % de tolerancia) y `SettlementsAndHarboursKeepAMinimumSpacing` (4 mapas).
  Se cambió el test de densidad: ahora exige 2–4 por país en lugar de "más de dos tamaños distintos".
- `ClassicExpansionTests`: Bahía de Poniente = {isla-bruma, meadow}, Estrecho del Norte = {isla-viento, gate, torre-norte}.
- `CountryCampTests.CampsSitCentredAmongTheirCitiesInsideTheirTerritory` (Las Marcas) y lo mismo en
  `MapVariantTests` (Cuatro Riberas): dentro del territorio, separación, cerca del centroide y camino.
  Además, los anclajes de los nombres están dentro de su país.
- `StrategicCountryBorderTests`: contorno oscuro con núcleo claro entre países, ancho constante, costura
  tenue entre ciudades, sin borde por cambio de dueño ni en la costa.
- Resultado: EditMode 31/31 (`TerritoryField`, `AuthoredLayout`, `ClassicExpansion`, `TerritoryMarkers`,
  `LocalizationAndAiPriority`); PlayMode 21/21 (`StrategicCountryBorder`, `CountryCamp`, `MapVariant`,
  `CityClaim`, `RandomStart`).

## 7. Coste

`TerritoryAtlas` pasa de ~220 ms a ~300–530 ms en el editor, por el voto B-spline y las distancias de
frontera; ya incluye un atajo interior y chamfers sin llamadas. La fase `territory_atlas`: Las Marcas 558 ms,
Cuatro Riberas 846 ms, Europa 759 ms, New World 1962 ms (máquina compartida, mucho ruido). El campo se
calcula ahora al crear las hogueras (fase `roster_and_ports`) en los mapas propios.
