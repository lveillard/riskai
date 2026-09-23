# Territorios de país (hogueras) — auditoría v0.30

Queja: al pulsar una hoguera, el territorio dibujado a veces es absurdo: una tira de costa, trozos al
otro lado del mar o fronteras sin relación con el mapa original de WC3. Este documento mide el
problema en los cuatro mapas, explica la causa y describe el algoritmo nuevo.

Capturas: `Captures/v0.30-territories/` (ignorado por git). Se regeneran con
`-executeMethod RiskAI.Editor.RiskTerritoryReview.Capture --riskai-territory-output DIR --riskai-territory-tag TAG [--riskai-map all|classic|riverlands|europe|world]`
(menú *RiskAI > Review > Capture territory audit*). Por mapa escribe `TAG-MAP.json` (métricas por país),
`TAG-MAP-atlas.png` (mapa cenital: un color por país, borde negro, ciudad blanca = dentro de su país,
roja = fuera, cuadrado negro = hoguera), `TAG-MAP-strategic.png` (vista estratégica real) y
`TAG-MAP-camp-*.png` (inspección de las 5 hogueras peores del primer run; `focus-MAP.txt` fija la lista).
En los mapas importados también escribe `source-MAP-atlas.png`, que es la referencia WC3 (ver abajo).

## 1. Cómo se calculaba (v0.29–v0.30 antes)

`TerritoryAtlas` construía un **Voronoi euclídeo** con todas las ciudades (en su *claim point*) más
**dos sitios por puerto** (tierra y amarre en el agua), y luego lo recortaba con la máscara de tierra
(canal A). El país de cada píxel era el país del sitio más cercano en línea recta.
`TerritoryMarkers` usaba otro criterio (la ciudad más cercana por `Position`) y el minimapa no
mostraba fronteras. Tres fuentes distintas.

Fallos que produce:

- **El mar no separa nada.** Una celda cruza estrechos y bahías y se queda tierra de la otra
  orilla. Por eso aparecían *piezas sin ciudad* (exclaves): 48 en Europa y 69 en New World.
- **Los sitios de puerto están en el agua.** El amarre y el círculo anfibio (`B00R`) están mar
  adentro; su celda recortada por la costa es justo una franja costera, a veces de otro país.
- **Fronteras rectas sin relación con el original.** En WC3 el mapa pinta cada país con **una
  textura de suelo** (coloreado de cuatro colores: `Cdrd`, `cWc1`, `Ndrd`, `Ybtl`; `Ywmb` marca la
  hoguera y `Vcbp` el círculo del puerto). Comprobado: todas las ciudades no portuarias de un país
  están sobre la misma textura, y cada componente conexa de una textura contiene como mucho un país
  (Europa: 108 componentes, 66 con ciudades, 0 compartidas; New World: 155/97/0). Esa pintura es la
  frontera original y el Voronoi la ignoraba.
- **Islas partidas** (mapas propios): el puerto insular independiente no tiene país, así que media
  isla quedaba "sin grupo" (gris en `before-classic-atlas.png`, `before-riverlands-atlas.png`).
- En Cuatro Riberas un país cruzaba el río/estuario y dejaba trozos al otro lado.
- Coste: el Voronoi era barato, pero `MapLayout.IsLand` en 1024² píxeles cuesta ~2,8 s en Cuatro
  Riberas (canales).

## 2. Algoritmo nuevo (`TerritoryField`, fuente única)

`Scripts/TerritoryField.cs` calcula una vez por mapa configurado (caché por `MapLayout.Towns`) el país y
la ciudad propietaria de cada celda. Lo leen el atlas estratégico, la inspección de la hoguera
(`StrategicMapView` → mismo atlas), los postes de frontera (`TerritoryMarkers.CityAt/CountryAt`) y el
minimapa (bordes oscuros en `BattleHud.BuildMinimapTexture`).

1. **Rejilla.** Mapas importados: los vértices W3E (2,56 u), así la textura de suelo se lee sin
   remuestrear y la geometría costera (`SourcePositionAt`) se respeta. Mapas propios: 0,7 u sobre el
   rectángulo jugable. Tierra = `MapLayout.IsLand`.
2. **Crecimiento competitivo (Dijkstra multi-semilla, 8 vecinos).** Semillas: todas las ciudades del
   país, las hogueras importadas y, en los importados, el punto de desembarco del puerto
   (`ImportedPortLayout.Resolve(...).Shore`) si su tierra no está pintada para otro país. Coste por
   paso: tierra 1, **agua 6** (una isla sólo se une al país más cercano si nadie la alcanza por tierra),
   y en importados **entrar en la textura de otro país ×16**. Así la tierra interior entre dos ciudades
   propias es propia y la frontera sigue la pintura original.
3. **Piezas sueltas.** Una pieza de tierra sin ciudad/hoguera propia a ≤4 celdas que toca tierra de
   otro país pasa al vecino con el que comparte más frontera. Los islotes aislados se quedan con quien
   los alcanzó por mar.
4. **Suavizado.** Dos pasadas de filtro de mayoría 3×3 sobre tierra (sin mover celdas junto a
   semillas) y otra limpieza de piezas.
5. **Ciudades.** Segundo Dijkstra, cada ciudad sólo crece por tierra de su país (colores de
   propietario por ciudad en la vista estratégica).
6. **Atlas.** Por píxel (1024²): `IsLand` exacto (en mapas propios se salta cuando las 4×4 celdas
   vecinas coinciden), y país/ciudad por **voto bilineal** de las cuatro celdas (bordes suaves, la tierra
   vota antes que el agua). El agua lleva país 0 y alfa 0. Los puertos independientes (mapas propios)
   pintan su propietario en un círculo de 4,5 u de costa. Formato de textura y shader sin cambios.

Determinista (desempates por índice), sin datos precalculados.

## 3. Números antes / después

Métricas de `RiskTerritoryReview` sobre el atlas real (1024²). *Ciudades dentro*: su punto (en
puertos importados, el desembarco) cae en su país. *Piezas huérfanas*: piezas de tierra sin ciudad
propia. *Mar en la celda*: parte del polígono del país que caía en el agua (se recortaba al dibujar).
*IoU WC3*: coincidencia con la pintura de suelo original (mediana por país; países con IoU < 0,8).
*Acuerdo WC3*: fracción de tierra pintada cuyo país coincide con el original.

| Mapa | Versión | Ciudades dentro | Piezas huérfanas (área u²) | Países con huérfanas | Astillas (<12 u²) | Mar en la celda | Compacidad mediana | IoU WC3 mediana (<0,8) | Acuerdo WC3 |
|---|---|---|---|---|---|---|---|---|---|
| Las Marcas | antes | 33/33 | 0 | 0 | 0 | 23 % | 0,77 | — | — |
| Las Marcas | después | 33/33 | 0 | 0 | 0 | 0 % | 0,76 | — | — |
| Cuatro Riberas | antes | 44/44 | 6 (504) | 4 | 2 | 13 % | 0,36 | — | — |
| Cuatro Riberas | después | 44/44 | 0 | 0 | 0 | 0 % | 0,55 | — | — |
| Europa | antes | 212/212 | 48 (3297) | 24 | 22 | 43 % | 0,53 | 0,66 (50) | 82,1 % |
| Europa | después | 212/212 | 21 (661) | 12 | 14 | 0 % | 0,64 | 0,98 (4) | 99,1 % |
| New World | antes | 293/293 | 69 (9846) | 36 | 29 | 53 % | 0,53 | 0,71 (67) | 83,9 % |
| New World | después | 293/293 | 33 (1090) | 20 | 19 | 0 % | 0,62 | 0,99 (5) | 99,3 % |

Las piezas huérfanas que quedan son islotes sin ciudad propia (las mayores: Noroeste ruso 360 u²,
Cerdeña 94 u², Noruega 80 u², Francia 46 u², Escocia 7 piezas; en New World además Florida, República
Dominicana, Haití, Nova Scotia, Ontario) y el test garantiza que ninguna toca tierra de otro país. Los
países con IoU < 0,8 son los que sólo tienen puertos (Creta, Chipre, Cerdeña, Jamaica, Bermudas: la
referencia sólo usa ciudades no portuarias, así que su IoU es 0 por definición) más Siberia (0,76) y
Moscú (0,83), pendientes de revisar a mano. Con el punto de puerto del modelo anterior (la ciudad en
el círculo anfibio) salían 22 ciudades "fuera" en Europa y 18 en New World; en la tabla ambas versiones
se miden por el desembarco.

Casos inspeccionados (las 5 hogueras con peor puntuación antes; mismas en `after-*-camp-*.png`):
Europa: Escocia, Chipre, Nueva Zembla, Turquía, Cerdeña. New World: Escocia, Chipre, Nueva Zembla,
Florida, Turquía. Las Marcas: Cuenca del Fresno, Marca del Alba, Valle de los Pinos, Escarpa de
Poniente, Puertas de Oriente. Cuatro Riberas: Puertas del Estuario, Ribera Alta, Cuenca del Río,
Llanuras de Levante, Archipiélago Norte. Para la vista global compárense `before-MAP-atlas.png`,
`after-MAP-atlas.png` y `source-MAP-atlas.png`.

### Coste de arranque (editor, máquina compartida con otro Unity: ruido alto)

| Mapa | Atlas antes | Campo + atlas después | Fase `territory_atlas` después |
|---|---|---|---|
| Las Marcas | 162–1025 ms | 84–93 + 301–307 ms | 602–631 ms |
| Cuatro Riberas | 2795–2804 ms | 364–1270 + 497–1395 ms | 1158–2133 ms |
| Europa | 112–158 ms | 50–1160 + 219–223 ms | 465–1569 ms |
| New World | 125–127 ms | 123–388 + 205–219 ms | 628–1898 ms |

La fase `territory_atlas` ahora incluye también el campo y los postes de frontera
(`TerritoryMarkers.Create` se movió tras `StrategicMapView.Initialize`). En Cuatro Riberas el atlas baja
de ~2,8 s a ~0,5 s al saltarse `IsLand` lejos de la costa. En los importados sube ~100 ms; si hiciera
falta, el campo se puede precalcular en el JSON del mapa.

## 4. Tests

- `Tests/Editor/TerritoryFieldTests.cs` (4 mapas): cada ciudad cae en su país (también en la muestra
  dibujada y en el color de ciudad); cada pieza de tierra tiene una ciudad propia o es un islote que no
  toca otro país; toda la tierra tiene país; en Europa/New World > 97 % de la tierra pintada sigue la
  pintura WC3 y las ciudades de un país comparten textura; recalcular da el mismo resultado.
- `Tests/PlayMode/CountryCampTests.cs`: el atlas usa el mismo `TerritoryField` y cada ciudad está en el
  territorio de su hoguera.
- `AuthoredLayoutTests.EveryAuthoredCountryProducesOneContiguousVoronoiRegion` se eliminó (medía el
  Voronoi euclídeo incluyendo agua); lo sustituye el test de piezas.
- Pasan: EditMode `TerritoryFieldTests;TerritoryMarkersTests;AuthoredLayoutTests` (19/19); PlayMode
  `CountryCampTests;StrategicCountryBorderTests;MapVariantTests` (8/8).
