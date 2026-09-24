# Disposición v0.34 — rediseño del oeste de Las Marcas y reglas por mapa

Encargo del dueño tras ver la v0.34 previa: el oeste de **Las Marcas** estaba mal resuelto
("Marca del Alba" era una franja fina por todo el borde oeste y "Escarpa de Poniente" una banda
este–oeste). Se rehace la zona oeste desde la geografía, se mueven ciudades, puertos y se fija un
suelo de compacidad por país. Capturas finales en `Captures/v0.34-layout/` (`before-west-*` = el
estado que el dueño rechazó, `after-west-*` = esta versión).

## 1. Interpretación de las peticiones del dueño

| Petición | Lectura aplicada |
|---|---|
| (a) la isla isla-bruma + el puerto que la mira al otro lado del canal (Muelle del Oeste) en una sola zona | La **zona de la meseta**: su territorio llega a la orilla donde amarra el Muelle del Oeste (el ancla es *Cordillera del Alba*, ciudad al pie oeste de la meseta) e incluye la isla. Se comprueba por partida territorial (`TerritoryField.CountryAt` del desembarco) y por el enlace del puerto (el atlas asigna el puerto al país de su ciudad enlazada). |
| (b) las ciudades de la meseta (pine, mill, meadow + una más en o junto a la meseta) juntas en una zona | pine (*Pinar Alto*), mill (*Molino Viejo*), meadow (*Valdeluz*) **encima** de la meseta (plano del acantilado oeste, polígono 0) y *cordillera-norte* (*Cordillera del Alba*) en su pie oeste: las cuatro comparten país, codificadas en `ClassicExpansionTests.PlateauCitiesShareOneZoneAndTheFourthStandsOnOrAtThePlateau` con `MapLayout.CliffDistance` (≥ 2 sobre la meseta; el pie en [−8, 2]). |
| (c) pine y mill (dos ciudades de meseta lado a lado) juntas | Mismo país que (b), además pine–mill separadas 27 u (37,8 de mundo) comparten zona, con aserción propia. |

(a) y (b) se satisfacen en **una sola zona**: separar la isla + puerto de la meseta obligaba a un
país de orilla más angosto que la franja que el dueño rechazó (geometría: la orilla entre el mar y
el borde de la meseta mide 5–10 u de ancho). La zona cumple así las dos frases literales ("isla +
puerto en una zona", "las ciudades de la meseta … en una zona").

## 2. Reglas por mapa (cada una con sus constantes en sus tests, sin constantes compartidas)

| Regla | Las Marcas (`ClassicExpansionTests`, `AuthoredLayoutTests`, `TerritoryFieldTests`) | Cuatro Riberas (`MapVariantTests`, `AuthoredLayoutTests`, `TerritoryFieldTests`) |
|---|---|---|
| Ciudades / países | **33 / 11** | **44 / 11** (los 11 los eligió el dueño) |
| Ciudades por país | 2–5, **como mucho un país de 5** (Meseta de los Pinos) | 2–5, **como mucho un país de 5** (Bahía del Noroeste) |
| Separación entre puestos (ciudades y puertos) | 22 u; solo una isla y el muelle de su propia isla pueden 18 | 22 u; solo una isla y el muelle de su propia isla pueden 18 |
| Suelo de compacidad (pieza principal) | relleno de envolvente ≥ 0,72 · eje menor/mayor ≥ 0,42 · caja ≥ 0,55 | relleno ≥ 0,72 · eje ≥ 0,42 · caja ≥ 0,55 |
| Recompensas | derivadas: +1 de oro por ciudad de país completo, `ceil(ciudades/2)` créditos por turno, tope 5·ciudades (`AuthoredLayoutTests.AuthoredCountryRewardsFollowTheirCityCount`, sin constantes fijadas a mano) | igual, derivadas |
| Hogueras | centradas entre sus ciudades, dentro del territorio y con camino (`CountryCampTests.AssertCampsCentred`, en ambos mapas) | igual |
| Fronteras | siguen acantilados (coste 15×), agua (6×) y relieve de coste (Perlin ±35 %); las filas de ciudades se desalinean para que ninguna frontera salga recta más de 24 u | igual |

## 3. Las Marcas: los grupos del oeste (11 países)

El oeste se parte por unidades de terreno —meseta, vega, dehesa, secano— en bloques compactos:

| # | País | Ciudades | Terreno |
|---|---|---|---|
| 0 | **Meseta de los Pinos** | pine (-47,12), mill (-21,5), meadow (-21,23), **cordillera-norte (-64,16)**, isla-bruma (-47,57) | la meseta del oeste (plano del acantilado), su pie y orilla oeste con el **Muelle del Oeste**, y la isla del canal |
| 1 | **Marca del Alba** (capital *Bastión del Alba*) | dawn (-38,-12), west (-22,-36) | el cuadrante NE de la vega bajo el muro sur de la meseta |
| 2 | **Escarpa de Poniente** | crest-west (-62,-26), dehesa-norte (-52,-44), encinar-centro (-34,-52) | el cuadrante SO de la vega (antigua banda E–O, ahora triángulo compacto) |
| 3 | Cuenca del Fresno | ford, stone | la cuenca central |
| 4 | Puertas de Oriente | ash, watch, senda-orient | la meseta oriental |
| 5 | Sierra Carmesí | red, highland, guardia-oriental | el sureste rocoso |
| 6 | Dehesa de Poniente | dehesa, encina, isla-roble | la dehesa del suroeste |
| 7 | Campos del Secano | secano, trigal | el secano seco |
| 8 | Lomas de Azafrán | azafran-norte, azafran, olivar, loma-sur | las lomas del sur |
| 9 | Costa de Sal | costa-sur, vigia-sal, isla-faro | la costa de sal |
| 10 | Estrecho del Norte | gate, torre-norte, isla-viento | la costa del estrecho + su isla |

Las dos bandas que el dueño rechazó se rehacen sin franjas: "Marca del Alba" ya no recorre el
borde oeste (es el bloque NE de la vega junto a dawn y west) y "Escarpa de Poniente" ya no es una
banda E–O (es el triángulo SO de la vega). La meseta encabeza el array porque su orilla enlaza los
puertos continentales (la prueba de hogueras de `CountryCampTests` busca el puerto del país 0).

### Ciudades movidas (pad; mundo = ×1,4)

| Ciudad | Antes | Después | Motivo |
|---|---|---|---|
| cordillera-norte | (-66,-10) | **(-64,16)** | pie oeste de la meseta, encadena la orilla del Muelle del Oeste a su zona (intención (a)) y es la "cuarta ciudad en o junto a la meseta" (b) |
| crest-west | (-60,-30) | (-62,-26) | vértice NO del triángulo de Escarpa de Poniente |
| dehesa-norte | (-48,-44) | (-52,-44) | reparte la vega hacia el oeste |
| encinar-centro | (-33,-53) | (-34,-52) | vértice SE del triángulo de Escarpa de Poniente |
| gate | (-1,18) | (7,20) | boca del paso entre mesetas: Estrecho deja de ser reloj de arena y se compacta |
| Muelle del Oeste (puerto) | x=-58 | **x=-55** | frente a la isla por el canal, ≥22 u del muelle insular y de las ciudades |

pine, mill, meadow, dawn, west y el resto quedan donde estaban. Fronteras: el muro de la meseta
separa 0 y 1 (coste de acantilado), el borde de la dehesa y el arroyo seco separan 1 de 5/6, y la
orilla dibuja 0 y 9.

## 4. Cuatro Riberas: los mismos 11 países del dueño, puestos despejados

No cambia ningún grupo (el dueño eligió los 11 países; uno de 5 ciudades). Sí se mueven puestos
para desaturar la costa norte (la regla de 22 u es estricta también entre puertos), romper las
filas paralelas que generaban fronteras rectas de 25–34 u y cerrar los huecos de compacidad de
Frontera Oriental, Llanuras de Levante y Llano Central:

| Puesto | Antes | Después | Motivo |
|---|---|---|---|
| west-13 | (-66,49) | (-68,44) | 22 u al Muelle del Oeste |
| west-14 | (-31,54) | (-28,50) | hueco entre el Muelle del Pinar y el Puerto del Paso |
| river-04 | (-19,43) | (-26,34) | 22 u a west-14 y al Puerto del Paso |
| Muelle del Oeste | x=-58 | x=-60 | 22 u al Muelle del Pinar y al muelle insular 1 |
| Puerto del Pinar | x=-45 | x=-42 | 22 u al Muelle del Oeste y al muelle insular 1 |
| Dársena del Roble | x=20 | x=8 | 22 u al muelle insular 3 y a high-09 |
| high-03 | (18,9) | (16,10) | alinea Llano Central sin dejar franja |
| high-07 | (32,-3) | (35,3) | ídem |
| east-03 | (48,17) | (48,12) | ídem |
| east-07 | (66,-4) | (64,4) | ídem |
| high-02 | (22,-28) | (22,-30) | alinea Frontera Oriental (relleno 0,74 → 0,81) |
| high-06 | (35,-43) | (38,-40) | rompe el bisector recto a 45° de 25 u con east-01 |
| east-02 | (44,-18) | (52,-30) | quita el zarcillo que entrelazaba Llano Central |
| east-06 | (68,-40) | (67,-43) | ídem |
| east-04 | (51,47) | (46,50) | ensancha Llanuras de Levante (caja 0,53 → 0,63) y desalinea la frontera con Puertas del Estuario |
| high-05 | (42,-85) | (44,-88) | rompe la frontera vertical recta del sur |
| west-05 | (-68,-81) | (-63,-78) | agrupa el suroeste en bloque (caja 0,54 → 0,58) |
| west-06 | (-43,-83) | (-42,-81) | ídem |
| river-05 | (-20,-86) | (-23,-86) | recoge el zarcillo norte de Marca Occidental |
| isle-03 | (-60,-95) | (-53,-93) | ídem |

Además se desalinean las filas de ciudades que generaban bisectrices rectas (la rugosidad de
`TerritoryField` se ensayó más fuerte — ±45 % y escala 0,10 — pero alejaba las fronteras de sus
acantilados y encarecía el test de rectas en Las Marcas: se mantienen ±35 % y se corrige con las
posiciones). Ninguna frontera pasa recta por el filtro B-spline del máscara más de 24 u (máximos
v0.34: Las Marcas 20 u, Cuatro Riberas ≤ 24 u).

## 5. Suelo de compacidad por país

`TerritoryFieldTests.AuthoredCountriesAreCompactRegionsNotStrips` mide, sobre la pieza principal
de cada país (las islas con su ciudad viajan como pieza aparte), relleno de envolvente convexa
(área tierra / área casco), razón de ejes propios (menor/mayor) y razón de caja. Una franja como
la de Marca del Alba v0.34 anterior marcaba relleno 0,75 · eje 0,30 · caja 0,32: el suelo
(0,72 · 0,42 · 0,55) la detecta. Los valores reales de esta versión (log `RISKAI_TERRITORY_COMPACT`):

| Las Marcas | relleno | eje | caja | | Cuatro Riberas | relleno | eje | caja |
|---|---|---|---|---|---|---|---|---|
| Meseta de los Pinos | 0,85 | 0,56 | 0,70 | | Marca Occidental | 0,95 | 0,53 | 0,58 |
| Marca del Alba | 0,89 | 0,50 | 0,97 | | Bosques de Poniente | 0,85 | 0,44 | 0,63 |
| Escarpa de Poniente | 0,90 | 0,47 | 0,86 | | Cuenca del Río | 0,82 | 0,54 | 0,60 |
| Cuenca del Fresno | 0,88 | 0,80 | 0,99 | | Costa Occidental | 0,89 | 0,73 | 0,69 |
| Puertas de Oriente | 0,89 | 0,83 | 0,79 | | Bahía del Noroeste | 0,89 | 0,74 | 0,72 |
| Sierra Carmesí | 0,89 | 0,55 | 0,70 | | Ribera Alta | 0,88 | 0,96 | 0,99 |
| Dehesa de Poniente | 0,97 | 0,74 | 0,83 | | Altos Centrales | 0,91 | 0,80 | 0,97 |
| Campos del Secano | 0,86 | 0,75 | 0,98 | | Frontera Oriental | 0,82 | 0,53 | 0,69 |
| Lomas de Azafrán | 0,86 | 0,84 | 0,97 | | Llanuras de Levante | 0,96 | 0,62 | 0,68 |
| Costa de Sal | 0,99 | 0,78 | 1,00 | | Puertas del Estuario | 0,81 | 0,52 | 0,73 |
| Estrecho del Norte | 0,83 | 0,60 | 0,95 | | Llano Central | 0,77 | 0,50 | 0,70 |

Máximos de frontera recta (máscara dibujada, límite 24 u): Las Marcas 20 u, Cuatro Riberas 20 u.
Fronteras sobre acantilados: 6,7 % de los pasos de acantilado son frontera (0,9–1,0 % de los pasos
llanos) en Las Marcas y 3,3 % (0,8 %) en Cuatro Riberas.

## 6. Tests

- `ClassicExpansionTests`: 33 ciudades / 11 países, los grupos del oeste con sus miembros
  exactos (incluidas las dos mitades de la vega), intención (a) con `TerritoryField` y el enlace
  del puerto, (b) con `CliffDistance`, (c) como par pine–mill, más el despeje de torres.
- `AuthoredLayoutTests`: densidad, separación estricta de 22 u (18 solo isla–muelle propio) con
  constantes por mapa, grupos equilibrados por mapa y recompensas derivadas (sin tocar la prueba
  existente de recompensas).
- `TerritoryFieldTests`: suelo de compacidad por mapa, fronteras sin rectas >24 u, bordes sobre
  acantilados, contención de ciudades y contigüidad.
- `MapVariantTests`: los 11 grupos de Cuatro Riberas por miembro, un solo país de 5, separación de
  22 u, campamento de su país 0 enlazado con puerto, rutas NavMesh por país.
