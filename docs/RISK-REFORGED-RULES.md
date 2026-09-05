# Auditoría de Risk Reforged v3.0

Fuente: `references/maps/Risk_Reforged_v3.0_by_Saran_OPEN_SOURCE.w3x`, publicado como mapa editable. SHA-256 verificado: `ACFA7048A5C48D61CB80FB42222D87B0F0CE9204FF4517A256F8502A68907F26`.

## Estado de extracción

La extracción fue completada con StormLib 9.40 (`SFileOpenArchive`/`SFileOpenFileEx`/`SFileReadFile`). Solo se extrajeron los archivos de reglas y metadatos permitidos a `references/maps/reforged-v3-source`; no se copiaron modelos ni texturas. `mpyq 0.2.5` no era compatible con esta variante (`Encryption is not supported yet`), pero esto no implica que el mapa esté protegido.

## Reglas auditadas

### Valores y funciones observados

- Configuración por defecto en `war3map.j:5565-5594`: Conquest (`ModesMainGamemode=0`), 60% de ciudades (`ModesSubGamemode=60`), FFA, bounty `4.00` (1/4), límite de spawns `5`, turno de 60 segundos, ingreso inicial y básico `4`, multiplicador `1.00`, y `ModesIncomeType=0` con comentario “Region Income (not implemented)”. La derrota por turnos sin ciudad está desactivada con valor 100.
- `war3map.j:5414-5441` define un ejemplo de región especial: The British Isles, una unidad generada (`SpawnedAmount=1`) y bonus de ingreso `+2`.
- Los textos `war3map.wts:1768-1900` describen Conquest, Russian Brawl, Barrier, Capitals y Timed; el texto de Timed indica fin tras X turnos y victoria por mayor ingreso. `war3map.wts:1838-1863` documenta Random teams, Vassals, una alianza, Free Diplomacy, Lobby Teams y FFA.
- `war3map.wts:2220` documenta el tiempo de turno y `war3map.wts:1495` la recompensa configurable por bajas (bounty). `war3map.wts:2390-2425` describe puntos de reunión y que las capitales generan spawns.
- Hay una referencia sonora a upkeep en `war3map.j:747,3318-3322`, pero no se encontró en esta pasada una fórmula activa de mantenimiento; no se atribuye un coste.

Quedan fuera de esta matriz las fórmulas completas de guardias y pillage; solo se registran cuando hay una función JASS y una llamada verificable.

## Matriz de presets

| Parámetro | Valores por defecto cargados por `Trig_Setup_default_modes_Actions` | Preset Classic documentado en `war3map.wts` |
| --- | --- | --- |
| Conquista | `ModesSubGamemode=60`: objetivo del 60% de ciudades; `ModesMainGamemode=0` | El texto del preset anuncia 65%; la fórmula operativa también lo aplica sobre ciudades |
| Límite de spawns | `ModesSpawnLimit=5` | 10 turnos |
| Turno | 60 s, sin incremento | 60 s (+0) |
| Diplomacia | FFA (`ModesAllyMode=0`) | FFA |
| Bounty | `4.00`, documentado como 1/4 | 1/4 |
| Árboles / niebla | Árboles activos, niebla desactivada | Igual |

El porcentaje 60 aparece en el preset que realmente se carga en JASS (`war3map.j:5565-5587`), mientras que el 65% y el límite 10 pertenecen al restablecimiento Classic descrito en `war3map.wts:2530-2537`; no deben mezclarse. La fórmula de objetivo está en `war3map.j:8348` y usa `ModesSubGamemode` sobre `AmountOfCities`.

La inicialización llama a `Set_Recruitment_Spawns` y `Set_Regional_Spawns` desde `war3map.j:4257-4319`; la región especial observada registra cuatro centros, una unidad por generación y +2 ingresos (`war3map.j:5424-5438`). `AmountOfRegions` se fija inicialmente en 69 (`war3map.j:5454-5455`) y se recalcula al terminar la preparación (`war3map.j:7002`).

### Posesión, ingresos y victoria

`Trig_Income_Give_Actions` (`war3map.j:19230-19290`) primero ejecuta la función por jugador que calcula `PlayersLastIncome`, después procesa ingresos regionales y regiones especiales, y finalmente llama a la función de entrega por jugador. La función por jugador aplica `ModesFirstIncome`/`ModesBasicIncome`, el multiplicador y el redondeo (`war3map.j:19161-19228`). El trigger se ejecuta al inicio (`war3map.j:4486-4487`) y al cierre de turno (`war3map.j:4783-4789`).

La captura se implementa en `Trig_City_Claim_Actions` (`war3map.j:18169-18364`), con condiciones auxiliares anteriores. Sustituye al defensor por `ClaimKiller`, lo sitúa en el círculo de ciudad y actualiza propiedad y puntos de reunión. Sus condiciones de país comparan `PlayerCitiesOwnedInRegions` con `RegionCityAmounts` (`war3map.j:18081-18095`). Al completar el país, asigna `RegionOwnersGroup` y el centro de refuerzos al conquistador (`war3map.j:18243-18254`); al perder la unidad necesaria para mantenerlo completo, neutraliza el centro y la propiedad del país (`war3map.j:18301-18310`). En este script, «Region» suele designar el país; las agrupaciones mayores son `SpecialRegions`.

En Conquest normal (`ModesMainGamemode=0`, FFA, `MODES_ALTERNATIVE_INCOME=0`), la rama regional de ingresos está activa: recorre `RegionOwnersGroup`, exige un único dueño y suma `PlayerCitiesOwnedInRegions` (`war3map.j:18944-19004`, llamadas `19237-19254`). Las ramas que pagan todas las ciudades sin ese control corresponden a otros modos (`Func001Func004C` y `Func001Func005C`, `war3map.j:18770-18804`). Por tanto, completar países sí es central para el ingreso normal; el comentario de configuración «Region Income (not implemented)» no sustituye la comprobación del trigger activo. La alternativa opcional mantiene un contador de agotamiento y recuperación del ingreso por país (`war3map.j:18853-18940`).

La configuración llama a la comprobación de victoria al final de turno (`war3map.j:4788-4791`). `VictoryCitiesRequired` se calcula como porcentaje de `AmountOfCities` (`war3map.j:8348-8398`): tanto el 60 operativo como el 65 del preset Classic son porcentajes de ciudades en esta fórmula JASS.

Los spawns regionales se configuran antes del inicio y las unidades futuras reciben el grupo de centros de su región (`war3map.j:5424-5434`, `war3map.j:4257-4319`). La semántica del límite sí está confirmada en `Trig_Begin_Recruit_Actions` y `Trig_Recruit_Step_Actions` (`war3map.j:19374-19408`, `war3map.j:19444-19533`): el tope por región es `RegionCityAmounts * ModesSpawnLimit` (multiplicado por 3 en el modo que lo requiere), y se compara con `A_SPAWNS_ALIVE_VALUE[player*100+region]`. La unidad solo se genera cuando el valor vivo es inferior al tope; al crearla se incrementa ese valor por su point value (`war3map.j:19502-19524`). Por tanto es un límite de fuerza viva/point value, no cinco oleadas fijas por turnos ni un simple contador de unidades creadas. Las muertes reducen el valor mediante la lógica de bajas asociada al contador vivo, permitiendo nuevos reclutamientos.

El texto Classic de “10 turns” (`war3map.wts:2530`) describe el preset y su interfaz; el código operativo por defecto usa `ModesSpawnLimit=5` (`war3map.j:5569`, `5613`) como multiplicador del tamaño de la región. Las unidades de regiones especiales configuradas con `SpecialRegionsSpawnedAmount=1` (`war3map.j:5424-5438`) son una ruta separada de la cola de reclutamiento y no deben confundirse con el límite regional anterior. No se halló fórmula activa de upkeep; la referencia sonora no basta para atribuir mantenimiento.

Las bajas decrementan explícitamente `A_SPAWNS_ALIVE_VALUE` en `war3map.j:18753`. Este contador no se debe confundir con el turno de partida ni con la cantidad de unidades compradas.

La auditoría describe la referencia. La adaptación implementada en Unity está documentada en [ART-v0.6.md](ART-v0.6.md): conserva la idea de países e ingresos y usa cálculos MIT de otra implementación de Risk; no incorpora JASS ni recursos del motor Blizzard. No se deduce de este análisis qué regla causó la popularidad del mapa.
