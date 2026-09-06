# World Editor y terreno: notas aplicables a RiskAI

Fecha: 2026-09-06. Esta nota estudia principios observables de Warcraft III / The Frozen Throne y los contrasta con el estado v0.12 de RiskAI. No se han abierto el mapa extraído ni el World Editor, y no se incorporan recursos de Blizzard.

## Qué está documentado por Blizzard

Blizzard describe el Terrain Editor como el módulo que modifica la geometría del mapa y coloca unidades, doodads y regiones. En su tutorial oficial crea un desnivel activando `Apply Cliff` y eligiendo `Decrease One`; también recomienda comprobar el espacio entre bases, minas y corredores para que las unidades no se atasquen. La misma guía muestra que el Doodad Palette pinta árboles con tamaño y forma de pincel. [Revisiting the Warcraft III Editor](https://news.blizzard.com/en-us/article/23395649/revisiting-the-warcraft-iii-editor)

El FAQ oficial explica que los tipos de terreno se agrupan en tilesets y que un tileset personalizado puede reunir elementos de varios tilesets. Esto acredita una paleta compuesta; no acredita un sistema de mezcla física o de biomas continuo. [World Editor FAQ](https://classic.battle.net/war3/faq/worldeditor.shtml)

La documentación oficial del juego establece que la altura afecta al combate: las unidades en terreno alto tienen ventaja, y los ataques a distancia pueden fallar al disparar hacia arriba. Los mapas oficiales usan rampas como accesos tácticos a posiciones elevadas. [Features FAQ](https://classic.battle.net/war3/faq/features.shtml), [Combat: High Ground](https://classic.battle.net/war3/basics/combat.shtml)

Estas fuentes no describen una API pública para editar una altura continua, el formato binario de `war3map.w3e`, las dimensiones de cada doodad, ni una regla que haga que una orilla de agua sea automáticamente caminable o bloqueada. Esas partes deben tratarse como comportamiento a verificar en el editor o en el juego, no como hechos derivados de la guía.

## Lo que aporta la extracción local

La fuente local conserva el mapa editable de Risk Reforged y registra que se extrajeron `war3map.w3e` (terreno), `war3map.doo` (doodads) y otros miembros del mapa; la extracción se verificó por SHA-256, pero el mapa no se abrió en Warcraft III ni en el World Editor. [references/SOURCES.md](../references/SOURCES.md)

El JASS extraído consulta explícitamente `PATHING_TYPE_FLOATABILITY` y `PATHING_TYPE_WALKABILITY` mediante `IsTerrainPathableBJ`. Eso es evidencia de dos consultas de transitabilidad usadas por esa implementación; no demuestra que toda orilla de Warcraft III tenga las mismas reglas ni que un doodad visual cree por sí solo una barrera. Archivo local de investigación: `references/maps/reforged-v3-source/war3map.j`.

## Traducción práctica para RiskAI

### Relieve continuo y escarpes discretos

El World Editor mezcla una cuadrícula de terreno con herramientas de nivel de cliff. Para RiskAI conviene conservar dos conceptos:

- `MapLayout.Height` y `TerrainHydrology.Carve` son el relieve continuo que debe mantener alturas suaves, bancos y depresiones.
- Una escarpa, un paso o una plataforma de ciudad son una decisión de navegación y de lectura visual. No deben inferirse solo de una diferencia de altura; se deben comprobar con el NavMesh y con colisiones de terreno.

El v0.12 ya tiene relieve procedural continuo, dos topologías de mapa, pads de ciudades y pruebas de rutas. Falta un flujo de autoría persistente para editar puntos de control y revisar cambios sin convertir el relieve en una colección de excepciones. Un futuro editor interno puede ofrecer `raise`, `lower` y `smooth` sobre una malla o height field, pero no hace falta prometer una copia del pincel de World Editor.

### Tiles, biomas y transición visual

La lección segura del sistema de tilesets es separar la selección de material de la forma del terreno. RiskAI ya usa materiales propios y parámetros compartidos para costa, islas y río. El siguiente paso es un perfil de bioma que asigne material, color de orilla, roca expuesta y vegetación por zonas, con una transición espacial continua alrededor de costa, río y escarpes. La mezcla debe ser visual; la máscara de navegación debe seguir siendo independiente.

No conviene describirlo como “blending de tiles de Warcraft”: la fuente oficial solo confirma tilesets y tilesets compuestos. Los atlas propios de RiskAI deben seguir siendo originales y con licencia adecuada.

### Agua, orilla y transitabilidad

El agua necesita tres capas separadas:

1. superficie y color;
2. geometría del lecho y la orilla;
3. máscara de transitabilidad para unidades terrestres y navegación naval.

En v0.12 `TerrainHydrology` comparte muestras para el cauce y el material. `StrategicTerrain` conserva el lecho visible continuo y excluye el canal únicamente de los triángulos de colisión; los cruces se crean antes de hornear el NavMesh. Esto es la arquitectura adecuada para no hacer que una superficie de agua visual abra una ruta terrestre.

Pendiente: mantener pruebas para el punto de agua, ambos lados de cada orilla, el tramo bajo el puente y el acceso naval. Cada prueba debe preguntar al sistema de navegación correspondiente; una muestra de color o una colisión visual no basta.

### Doodads y huella jugable

Blizzard recomienda reservar espacio para que las unidades no se queden atrapadas, y permite pintar doodads con pincel. La recomendación aplicable es autorar árboles, rocas y edificios con una huella de despeje explícita. En RiskAI los árboles y rocas son decoración generada; la transitabilidad debe validarse después de crear geometría y antes de considerar una ruta estable.

Pendiente: registrar para cada conjunto de decoración un radio de despeje, excluir ese radio del muestreo de árboles y añadir una comprobación de NavMesh alrededor de ciudades, puertos, puentes y rampas. No asumir dimensiones de `war3map.doo` sin decodificar y verificar el archivo.

### Rampas y puentes

La referencia de alto terreno justifica que una rampa sea un acceso táctico y que una ruta visualmente corta pueda estar bloqueada por altura. RiskAI ya crea rampas y puentes con collider en `TerrainHydrology.CreateCrossings` antes de `NavMeshSurface.BuildNavMesh`; los objetos usan `MapLayout.TerrainLayer` y las pruebas comprueban el canal cerrado y los cruces.

La mejora siguiente es un contrato de autoría: cada cruce debe declarar sus dos bancos, cota superior, ancho, pendiente y punto de prueba en ambos extremos. El bridge mesh, el material del agua y el NavMesh pueden cambiar por separado mientras ese contrato se conserve. Esto permite un editor futuro de raise/lower/smooth sin prometer que cada pincel sea determinista ni que el NavMesh sea un dato serializado todavía.

## Orden recomendado

1. Mantener la separación agua, colisión y navegación y ampliar las pruebas de orilla, isla, puente y vado.
2. Añadir perfiles de bioma y máscaras de mezcla visual sin introducir reglas de movimiento en los shaders.
3. Hacer que pads, bancos, escarpes y cruces sean datos editables y revisables; regenerar malla y NavMesh desde esos datos.
4. Solo después valorar una herramienta de edición interna con pinceles de `raise`, `lower` y `smooth`, además de vista de pathing y huellas de doodads.

El objetivo es aplicar las ideas de composición, espacio y lectura táctica del World Editor manteniendo una implementación propia, comprobable y compatible con la simulación de 20 Hz.
