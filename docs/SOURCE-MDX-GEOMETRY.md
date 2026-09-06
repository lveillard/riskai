# Metadatos de geometría MDX de RoC

Los números de [source-mdx-geometry.json](../data/source-mdx-geometry.json) se extrajeron de `WAR3.MPQ` del ISO RoC de propiedad local. Sólo se conserva el resultado numérico y hashes de procedencia; no se distribuyen MDX, texturas ni otros recursos de Warcraft III.

`Units\\UnitUI.slk` establece `modelScale=1` para `hrif`, `hmpr`, `hkni`, `hmtm` y `hbar`. El analizador lee los chunks `MODL`, las posiciones `VRTX` de `GEOS` y las cotas de cada secuencia `Stand`. El eje vertical de esas cotas es Z: las secuencias de unidades comienzan aproximadamente en Z=0 y terminan en su altura animada.

Las cotas fuente utilizables, con la conversión común de 50 unidades WC3 por unidad Unity, son las siguientes. La anchura es el mayor de los tamaños horizontales X/Y; son límites de animación, no radios de colisión.

| Modelo | Secuencia | altura WC3 / Unity | anchura WC3 / Unity |
| --- | --- | ---: | ---: |
| Rifleman (`hrif`) | Stand | 81.837 / 1.637 | 94.895 / 1.898 |
| Priest (`hmpr`) | Stand | 122.367 / 2.447 | 94.414 / 1.888 |
| Knight (`hkni`) | Stand -1 | 152.895 / 3.058 | 148.094 / 2.962 |
| MortarTeam (`hmtm`) | Stand | 80.253 / 1.605 | 113.644 / 2.273 |
| HumanBarracks (`hbar`) | Stand | 370.338 / 7.407 | 347.975 / 6.960 |

Las variantes de espera pueden ocupar más espacio: Rifleman `Stand - 3` llega a 132.466 de altura, Priest `Stand - 4` a 136.664, Knight `Stand Victory` a 240.811 y MortarTeam `Stand Victory` a 104.305. Para culling o espacio conservador se deben usar los máximos de `standSequenceBounds` del JSON, no sólo la fila principal.

`SourceGeometry.json` hereda estas rutas, escalas, alturas y anchuras para Archer/Rifleman, Medic/Priest, Guard/Knight y Mortar/MortarTeam. `ModelMetrics.MatchStandingHeight` mide cada modelo propio en su pose Idle y normaliza uniformemente su altura contra `SourceGeometry.StandingHeight`, sin copiar la silueta, los vértices o las texturas del modelo fuente. Conserva la proporción del arte propio y coloca sus pies sobre el suelo. La medida inicial queda en caché por tipo, no se repite por fotograma.

`SourceGeometryRuntimeTests` verifica las cuatro alturas resultantes en objetos reales y la cobertura de selección de su cabeza. `SkinnedMeshRenderer.BakeMesh(mesh, true)` permite medir los vértices posados y convertirlos al espacio local del modelo sin contar dos veces la escala del rig. La calibración sustituye `UnitScale=.46` en estos cuatro tipos; Footman/Mage conservan su escala local. La selección usa altura/anchura fuente como caja generosa, separada de la colisión física.

Esta igualdad de altura no hace iguales las siluetas ni sus anchuras: por ejemplo, el guardia propio es un infante y la referencia Knight incluye montura. Aunque `hbar` conserva su métrica MDX como procedencia, no calibra todavía el compuesto de ciudad propio. Las animaciones distintas de la pose de espera también pueden exceder estas medidas.
