# Escala y fidelidad de los mapas importados v0.15

`scripts/export_playable_risk_maps.py` convierte coordenadas numéricas de los mapas de Saran sin distribuir `.w3x`, texturas ni modelos de Warcraft. La conversión común es `Unity = Warcraft / 50`: mantiene `x`, convierte el eje nativo `y` en `z` y resta el centro W3E a ciudades, círculos, grupos, árboles y límites.

## Extensión y límites utilizables

W3E v11 usa celdas de 128 unidades nativas, equivalentes a 2.56 Unity. La malla conserva todo el grid fuente; sólo el encuadre y el minimapa usan el rectángulo jugable, sin recentrar ciudades. W3I v31 aporta el rectángulo de cámara, publicado como `playableMinX`, `playableMaxX`, `playableMinZ` y `playableMaxZ`; `ImportedMapData.PlayableBounds` usa el grid completo como fallback para recursos antiguos.

| Mapa | W3E (nodos / celdas) | Extensión completa Unity | Rectángulo W3I Unity |
| --- | --- | --- | --- |
| Europe | 257 x 257 / 256 x 256 | 655.36 x 655.36 | x `[-314.88, 314.88]`, z `[-312.32, 320.00]` |
| NewWorld | 385 x 385 / 384 x 384 | 983.04 x 983.04 | x `[-481.28, 478.72]`, z `[-312.32, 320.00]` |

Los hashes, versión, versión de editor y bounds complementarios W3I quedan en `metadata.sources.mapInfo`. Son límites de cámara/minimapa, no una afirmación de que el mapa original sea un globo.

## Unidades, alcance y colisión

Los campos nativos de distancia usan `/50`. La tabla distingue evidencia del mapa de adaptación visual actual.

| Caso | Fuente efectiva | Valor `/50` | Runtime actual | Estado |
| --- | ---: | ---: | --- | --- |
| Rifleman `h00B` | `ua1r` heredado `hrif=400` | 8.00 | Archer range 8 | exacto |
| Rifleman `h00B` colisión | override `ucol=16` | 0.32 | NavMesh radius 0.32 | campo fuente convertido |
| Priest `h00E` | `ua1r/uacq=400`; `ucol` heredado 16 | 8.00 / 0.32 | Medic range 8, NavMesh radius 0.32 | campos fuente convertidos |
| Mortar `h00H` | `ua1r/uacq=900`, `umvs=230`; `ucol` heredado 32 | 18.00 / 4.60 / 0.64 | Mortar range 18 / speed 4.6 / radius 0.64 | campos fuente convertidos |
| Knight `h00G` | `ucol` heredado 32 | 0.64 | Guard NavMesh radius 0.64 | campo fuente convertido |
| Bunker `o000` | `ua1r=425`; `ucol` heredado 72 | 8.50 / 1.44 | profile tower 8.5 | alcance exacto |
| Ciudad/puerto `h00N/h00O` | override `ua1r=650`, `ucol=50` | 13.00 / 1.00 | torre de captura range 13 | alcance exacto; arte distinto |

No hay un rifle de alcance 600 activo en `h00B`: hereda 400. La ficha histórica de 60 corresponde a Long Rifles, sin evidencia de mejora aplicada en estas instancias.

Las colisiones heredadas se leen de `references/owned-disc-data/RoC-Units-UnitData.slk`; los overrides de `h00B`, `h00N` y `h00O` se leen de `war3map.w3u`. `TFT121b-Resolved-UnitBalance.slk` no se usa porque empieza por `BSDIFF40`, no por texto SLK.

`Resources/Maps/SourceGeometry.json` es el artefacto numérico reproducible para `h00B`, `h00G`, `h00E`, `h00H`, `h00N` y `h00O`: publica `ucol`, movimiento y sus equivalentes `/50`, además de los overrides encontrados. `ucol` es un campo de tamaño de colisión Warcraft, no una caja MDX ni una afirmación sobre radio o diámetro visual. Ninguno de esos seis objetos redefine `umdl` o `usca` en el W3U, pero sus bases `hrif`, `hkni`, `hmpr`, `hmtm` y `hbar` ya se resuelven de forma verificable en `data/source-mdx-geometry.json`: path, `modelScale=1`, secuencia `Stand`, altura y anchura `/50`, con hashes de `WAR3.MPQ`, `UnitUI.slk` y cada MDX. No se incluyen los MDX. Archer, Medic, Guard y Mortar pueden normalizar la altura de arte propio contra `standingHeightUnity`; esto no copia siluetas ni convierte sus anchuras en colisión. `hbar` queda como metadato de procedencia y no calibra todavía el compuesto propio de ciudad/puerto.

`Core/SourceGeometry.AgentRadius` aplica los cuatro tamaños verificados al `NavMeshAgent`: Archer y Medic 0.32; Guard y Mortar 0.64. Footman y Mage conservan 0.24 porque son adaptaciones sin una colisión fuente verificada. `Agent.height=1.3` sigue siendo un parámetro de navegación, no la altura del modelo.

`ModelMetrics.MatchStandingHeight` calibra la pose Idle de esos cuatro modelos propios a 1.637 / 2.447 / 3.058 / 1.605 unidades Unity mediante escala uniforme y apoyo de los pies. `SourceGeometryRuntimeTests` comprueba el resultado real, incluidos modelos que reutilizan la medida en caché. `RtsPicking` emplea altura y anchura fuente para una caja de selección generosa y los anillos consideran el radio físico. La altura de espera está calibrada; la silueta, anchura y animaciones siguen siendo propias. [Procedencia MDX y límites](SOURCE-MDX-GEOMETRY.md).

`WorldArt` no incluye MDX de Warcraft. Sus Towns son primitivas propias: la mayor cubierta estructural mide 4.8 x 4.45 y con `TownScale=.72` queda en 3.456 x 3.204; la cubierta de torre es diámetro 4.5 y con `TowerScale=.70` queda en 3.15. Son bounds de arte local, no del modelo original. `UnitScale=.46` y `UnitHeight=1.4` quedan como fallback para tipos sin altura fuente; el compuesto de ciudad todavía no se calibra contra HumanBarracks.

## Vegetación y pathing

El exportador analiza cada registro de 54 bytes de `war3map.doo`: rawcode, variante, posición, ángulo, escala XYZ, flags, vida y número de registro. La tabla `war3map.w3b` local de Europe identifica `B000`–`B00P` incluidos como variantes de bases de árbol (`BTtw`, `ATtc`, `BTtc`, `WTst` o `VTlt`); los rawcodes nativos se aceptan sólo cuando la fuente/JASS los usa como árbol. Así se excluyen rocas, hielo, decoraciones y props sin inventar clasificación.

`B00Q` se excluye aunque base en árbol: sus 69/100 instancias están en centros de spawn de país. `B00R` es el anillo de claim y `B00T` el marcador especial de New World. `NWf1` y `NWf4` se excluyen: son props de hielo flotante Northrend, no vegetación. El resultado contiene 4.635 árboles estáticos en Europe y 5.992 en NewWorld, en `sourceTrees`, con `treeSpecies` indexado. La instancia Unity usa cada XY, ángulo y proporción DOO; no hay siembra Perlin ni posiciones aleatorias. La altura base del abeto propio no declara equivalencia MDX inexistente; su multiplicador vertical nativo `scaleZ` sí se conserva. Los ejes horizontales nativos X/Y pasan a Unity X/Z y el yaw se niega por el cambio de orientación.

`pathingFlags` conserva el byte DOO para auditoría. No se convierte en un obstáculo NavMesh nuevo: no hay resolución de pathing por modelo en estos datos y añadir bloqueadores propios podría cerrar rutas. Para legibilidad, `ImportedTerrain` descarta sólo copas que invaden el clear de ciudad o claim, además de nodos W3E de agua; no reubica los demás árboles.

## Paleta y semántica de los registros

Los nueve identificadores de suelo W3E preservados en `tileNames` son `Ywmb`, `Agrs`, `Yrtl`, `Cdrd`, `Adrt`, `cWc1`, `Ndrd`, `Vcbp` y `Ybtl`. Cada `tileSamples` compacta variación en bits 0–7, textura/capa de acantilado en 8–15, índice de suelo y flags en 16–23, y el bit de borde de agua en 24; es una paleta numérica para un adaptador RiskAI, no una textura Warcraft. Los colores de `ImportedTerrain.GroundColors` y los materiales de los árboles son, por ello, una paleta propia y no se deben interpretar como reproducción 1:1 de píxeles, normal maps o sombreado Reforged.

En DOO, `sourceTrees` significa un destructible estático que pasó la clasificación de árbol: la lista incluye rawcode/base, variante, transformación, vida y byte de flags. Ese byte se retiene sin cambiar su significado a bloqueo NavMesh. `B00Q` es el marcador de spawn de país, `B00R` el anillo de claim y `B00T` un marcador especial; `NWf1` y `NWf4` son hielo flotante y tampoco son árboles. La posición, ángulo y escala DOO son 1:1 bajo `/50`; la forma, altura y radio de copa de cualquier mesh propio siguen sin tener bounds MDX verificados y no deben presentarse como equivalentes visuales del original.

## Relieve y agua adicionales

W3E sigue siendo la geografía base. Cualquier relieve suave o río posterior debe ejecutarse antes de `PrepareBuildingPads`, mantener XY de ciudades/círculos/camps y dejar pasos transitables. No se añaden montañas a márgenes que W3I permite retirar de vista. Los resultados de pruebas y build se registran en VALIDATION-v0.15.md.

En esta versión la opción de relieve añade cordilleras en los Alpes y Pirineos, con un desplazamiento X de 163,84 en New World. No altera muestras de costa/agua, conserva un claro de 8 unidades en torno a ciudades/círculos/hogueras y mezcla alturas hasta 20 unidades. Los ríos adicionales se posponen: la traza candidata del Rin no superó la comprobación de exclusiones. W3E conserva el agua existente.

La paleta de suelo se decodifica con `(tileSamples >> 16) & 15`: el byte bajo es variación, no índice de textura. Es el empaquetado de [W3E v11](https://github.com/corepunch/open-realm/blob/main/games/warcraft-3/docs/file-docs/w3e.md), contrastado con [WC3MapSpecification](https://github.com/ChiefOfGxBxL/WC3MapSpecification/blob/master/Terrain/12.md). Las muestras JSON preservan ambos bytes desde la importación inicial.

## Cámara del script de partida

En ambos `war3map.j`, `Trig_Variables_Settings_Actions` fija distancia 4000, `ANGLE_OF_ATTACK=290` y `ROTATION=90`. Con nuestros ejes esto equivale a distancia 80, inclinación Unity 70° y yaw 0° (norte arriba). Los mapas importados usan esos valores al abrir/restablecer cámara. FOV 44 se conserva como adaptación: no hay un FOV de partida explícito verificable. El preset de editor Camera_001 no se aplica en el JASS y no se toma como cámara jugable. El zoom mínimo equivale a distancia nativa 900; el máximo se extiende para permitir una vista moderna del mapa completo.

Las arenas propias usan 55° y norte arriba. La referencia de base inicial conserva sólo la función de cámara; los imports no añaden un campanario de «capital» a la primera ciudad de cada jugador.
