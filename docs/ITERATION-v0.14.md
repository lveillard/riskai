# RiskAI v0.14 — geografía numérica importada

v0.14 incorpora dos escenarios estratégicos basados en la geografía numérica de los mapas locales de Warcraft III: **Europe** y **NewWorld**. La importación conserva relieve, agua, posiciones de ciudades, círculos de captura y centros de reclutamiento; el terreno visible, los edificios, los muelles y las reglas de RiskAI siguen siendo implementación propia.

| Escenario | Referencia local | Ciudades | Grupos | Puertos |
| --- | --- | ---: | ---: | ---: |
| Europe | Risk Reforged v3.0 de Saran | 212 | 69 | 44 |
| NewWorld | Risk - New World v3.0 | 293 | 100 | 59 |
| Las Marcas | escenario original de RiskAI | 18 | 9 | 7 puestos adicionales |

Europe corresponde a la familia europea/mediterránea de Saran. NewWorld amplía esa base hacia América, el Caribe, Groenlandia y las regiones definidas por su propio JASS. No es un globo terrestre completo ni pretende resolver una proyección mundial, fronteras contemporáneas o una simulación política global.

## Datos extraídos y trazabilidad

`scripts/export_playable_risk_maps.py` genera `Resources/Maps/Europe.json` y `NewWorld.json`. Cada recurso contiene una cuadrícula fila por fila de altura, agua, tierra y tipo de baldosa, más ciudades y grupos. El exportador conserva hashes y las fórmulas de decodificación en `metadata`, por lo que el resultado puede auditarse sin distribuir los archivos de mapa.

La cuadrícula `war3map.w3e` versión 11 se decodifica en celdas de 128 unidades nativas. Las coordenadas se centran en el origen de Unity, mantienen `x` de Warcraft como `x` de Unity, trasladan `y` de Warcraft a `z` de Unity y usan una escala de 50:1. La altura de terreno incorpora la capa de acantilado; el agua se lee por separado y se normaliza para que el nivel dominante del mar sea `-0.24` en Unity. La transitabilidad no se deduce solamente comparando alturas: el indicador de agua de W3E forma parte de `landSamples`.

Para Europe, las ubicaciones de edificios y la agrupación proceden de `war3mapUnits.doo` y de la secuencia de ciudades de JASS. Para NewWorld, el `.doo` de unidades publicado no contiene el inventario estático completo; el exportador recupera las 293 llamadas literales de creación y la secuencia de grupos desde JASS. En ambos casos, los doodads `B00R` se asocian de forma única a las ciudades para conservar la posición real del círculo de captura. Los rectángulos de `CountrySpawnRegions` de JASS dan el centro de reclutamiento de cada grupo.

Los archivos W3X y sus miembros extraídos permanecen en `references/maps/`, ruta excluida del repositorio. `scripts/extract_map_members.py` puede volver a extraer `war3map.w3e` y `war3map.doo` de los archivos locales; después se ejecuta el exportador. Los JSON publicados contienen números y nombres de grupo, no texturas, modelos, iconos ni otros recursos artísticos de Warcraft III.

## Adaptación jugable propia

`ImportedMapData` carga los recursos numéricos para los modos Europe y NewWorld. Las ciudades reciben sus posiciones y sus puntos de captura fuente. La opción de posiciones fijas alterna grupos entre los dos equipos; el menú también ofrece reparto de ciudades o grupos por semilla. La asignación actual solo contempla dos bandos y neutrales, y no pretende reproducir el reparto de jugadores, partidas guardadas ni todas las reglas multijugador de los mapas de referencia.

Cada ciudad comienza con un ballestero anclado al punto de captura que resulte navegable en el NavMesh. El ancla se resuelve una vez por círculo para que una guarnición sustituida conserve la misma posición y no derive por pendiente, evasión o replanificación. Esta regla también describe el comportamiento de Las Marcas, que mantiene sus 18 ciudades en 9 grupos: sus guarniciones pertenecen al círculo de la ciudad y no son tropas móviles gratuitas.

Los 44 puertos de Europe y 59 de NewWorld son ciudades portuarias, no puestos independientes añadidos al reparto. El adaptador naval comparte con su ciudad la propiedad, el círculo, el defensor y la torre. Cuando el círculo fuente cae en agua, RiskAI crea plataformas, pasarelas y un punto de embarque transitables con geometría y materiales propios. Es una adaptación para que el combate terrestre y naval sea jugable sobre los datos de posición; no copia ni exporta arte, modelos, texturas o muelles de Warcraft III.

Los nombres individuales de las ciudades no están disponibles como una tabla de topónimos en estas fuentes de posición. El recurso conserva el identificador estable de ciudad y el nombre del grupo con un ordinal, en vez de inventar nombres geográficos.

En los mapas importados, las guarniciones quedan fuera del presupuesto de 100 tropas móviles por bando. Los puertos comparten el cupo naval de 12 barcos. El menú ofrece los cuatro escenarios, y los paneles de grupos y ciudades tienen paginación. La cámara admite zoom estratégico, selección a esa distancia y desplazamientos entre extremos ajustados al tamaño del escenario.

El relieve usa la misma interpolación triangular para geometría y consultas. El NavMesh importado se hornea con voxel de 0,12 para reducir diferencias de altura en círculos. Las torres añadidas conservan el edificio y círculo fuente, pero su orientación se ajusta cuando invadiría el alcance de otro defensor al inicio.

## Límites actuales

- Los datos de altura y agua representan la cuadrícula del archivo W3E, no una garantía de que todas las reglas de pathing, puentes, destructibles o elevación visual del motor Warcraft se reproduzcan fuera de ese motor.
- Los puertos sobre agua requieren plataformas originales para el NavMesh. Esa decisión no desplaza las coordenadas fuente de ciudad o círculo, pero sí añade una superficie de juego de RiskAI.
- NewWorld no convierte el proyecto en un modo de mapa mundial. Europa, América, Caribe y Groenlandia son el alcance de esta referencia; el resto del planeta no está modelado.
- La economía, el límite de población móvil, los equipos iniciales, el combate, la cámara, los efectos y la IA siguen siendo decisiones de RiskAI. No se afirma equivalencia de reglas completa con Saran ni con la variante New World.

## Validación

107 casos distintos aprobados mediante la suite completa y la repetición del fixture corregido; build Windows y capturas reales de los cuatro escenarios. [Ejecuciones, fallos corregidos y límites de la evidencia](VALIDATION-v0.14.md).
