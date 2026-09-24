# Plan v0.34 · Unidades por configuración, comportamiento único y cola de órdenes

Estado: **listo para ejecutar** en esta misma PR, después de mergear la #9 (v0.33).
Versión 2 del plan: incorpora cuatro revisiones adversariales (GPT-6-astra, GPT-6-sol,
Grok 4.7 y DeepSeek v4.1 Flash), guardadas en `docs/audits/plan-v0.34-reviews/`.

Reglas del propietario que el plan respeta en cada paso: **una sola fuente de verdad**,
**ni legacy, ni capas de compatibilidad, ni alias, ni forwarders**, el juego se comporta
**igual** salvo en lo que se cambia a propósito (lista explícita al final), y rendimiento
válido en WebGL/móvil con 900 unidades y 16 IA.

## 1. Por qué

El bug «los barcos se acercan en vez de atacar» (v0.33) salió porque barcos, soldados y torres
resuelven lo mismo por caminos distintos, y los datos de cada unidad están repartidos en
tablas escritas a mano. Un barco es una unidad con otras propiedades; con datos y reglas
comunes, lo que funciona para una unidad de tierra funciona para un barco.

## 2. Lo que el plan v1 no veía (resumen de las revisiones)

| Hallazgo | Fuente | Qué cambia en el plan |
|---|---|---|
| `Core` es `noEngineReferences`: no puede usar `Resources.Load` ni `JsonUtility` | todas | DTO puro en Core; carga y validación en Runtime; `UnitCatalog.Bind` explícito |
| `JsonUtility` no admite enums como texto, `float?` ni arrays raíz, e ignora campos desconocidos | todas | Newtonsoft (`com.unity.nuget.newtonsoft-json`) como dependencia **directa** de Runtime, con `MissingMemberHandling.Error` |
| El esquema v1 pierde datos de combate: `WeaponTargeting` (mortero apunta al punto), `WeaponTargetMask` con clases **y relaciones** (sin bits de relación hay fuego amigo), tiempos de vuelo mín./máx., splash por anillos, `Ranged`, `Mechanical`, `Level`, puntos ≠ coste | todas | Esquema completo (§4.1), generado a partir de lo que consume el código, no de un ejemplo |
| Un único `SurfaceDistance` cambia el balance: hoy cuerpo a cuerpo mide borde a borde con histéresis, distancia mide centro→punto de acercamiento, torre centro→centro, barco hasta el casco orientado | todas | **Política de medición por arma** en datos que reproduce cada caso; no se unifica la fórmula, se unifica el código que la aplica |
| `footprint` único mezcla colisión (ucol), cuerpo de alcance, selección (ancho visual) y casco (`NavalArt`) | astra, sol, deepseek | Geometrías separadas en datos; `NavalArt` construye el collider desde esos datos |
| La torre mezcla dos perfiles (vida/alcance de `o000`, daño/cadencia/alcance 13 de `h00N/h00O`), con armadura `Divine` fija y arma según anfitrión | grok, deepseek | La torre entra en el JSON con sus campos reales, `canBeAttacked:false` y arma por anfitrión |
| Fusionar enums rompe todo lo indexado por ordinal (`UnitCatalog.Definition`, `SoldierPool`, `AiPlanning.Table`, `UnitPresentationLod`, `AttackPresentationTiming`, pruebas) | todas | Primero se eliminan los índices por ordinal; los tipos se identifican por id estable |
| `gridSlot` no es dato: la rejilla se **calcula** por coste, orden estable, tierra antes que mar y paginación | todas | La rejilla sigue siendo un algoritmo; el JSON solo dice en qué edificio se produce |
| `BattleCommands` solo acepta `Soldier` y valida contra la NavMesh; los barcos reciben órdenes directas en el acto; la IA naval lee `LastActionError` justo después de ordenar | todas | Despacho único por dominio con resultado identificado por orden; productores y consumidores (IA, UI, probes) migran en el mismo paso |
| «Shift + atacar ciudad» no es `Attack(target)`: la torre es inatacable y el clic se traduce al guardián o a atacar-mover al punto de captura | astra, sol, grok | Orden nueva `Capture(buildingId)` que resuelve guardián, punto y sucesión al ejecutarse |
| La cola actual: solo mover/atacar-mover/patrullar, no encola si la unidad está Idle/Hold, `Attack`/`Follow` vacían la cola, la patrulla no drena, el atacar-mover del barco sobrevive a la muerte del objetivo | todas | Matriz de semántica de cola explícita (§4.5) |
| Embarque = pareja soldado+transporte con playa, reservas de plazas y `Stop` al embarcar/desembarcar | astra, sol, grok | Máquina de estados de transporte en la capa común; `Embark`/`Unload` como órdenes emparejadas |
| Comportamiento por unidad escondido: sesgo de presión del Espadachín, correas 7/11/5/7,5, alerta a aliados a 5 m, bosque, guardia de puerto que teletransporta, separación entre barcos 2,8 m, calado 1,3 | grok, deepseek | Todo pasa a parámetros con nombre en datos o a reglas comunes documentadas; inventario antes de mover nada |
| Migrar las pruebas a leer del mismo JSON las vuelve tautológicas; `reforged-source-combat.json` no contiene ucol, maná ni bounds MDX | todas | Fixture de oro independiente y procedencia contra **cuatro** fuentes |
| La IA deduce «sanador» del texto del rol | sol, deepseek | Capacidades tipadas (`heal`, `roar`…) |
| Nombres duplicados entre `GameText` y los nombres de unidad | deepseek | Los nombres de unidad salen solo del JSON; se borran sus entradas de `GameText` |
| Orden de tick (`BattleWorld`), RNG, reconstrucción espacial tras mover barcos, proyectiles al tick siguiente | astra, sol, deepseek | Punto de drenaje de la cola fijado y probado; mismo orden de fases |
| Shift ya acelera la cámara; la ruta marítima no es una línea recta | grok | Resolver el conflicto de tecla; la ruta visible usa la polilínea de `SeaNavigation` |

## 3. Objetivo

1. `units.json` como **única fuente de verdad** de todos los tipos: unidades de tierra, barcos y
   torres/puestos.
2. Esquema **TypeBox** estricto que valida el JSON y del que sale el contrato C#.
3. **Una capa de reglas común** (datos → decisiones) que usan `Soldier`, `Ship` y `DefenseTower`:
   medición de alcance, acercamiento, adquisición, captura, transporte y resolución de órdenes.
   Las clases de actor solo conservan su adaptador de motor (NavMesh o `SeaNavigation`) y su
   presentación.
4. **Cola de órdenes con Shift** para todos los tipos, con ruta visible, que los barcos heredan.

Añadir una unidad = una entrada en `units.json` + su modelo (+ su retrato renderizado).

## 4. Diseño

### 4.1 `units.json`

Raíz envolvente `{ "version": 1, "units": [ … ] }`. Una entrada por tipo, con **id estable**
(string) que no depende del orden del array. Campos, agrupados:

- **Identidad**: `id`, `names {es,en}`, `role {es,en}`, `source {rawcode, base, notes}`,
  `adaptation` (lista de desviaciones locales respecto a la fuente, p. ej. Sanador 220 HP).
- **Economía**: `cost`, `points`, `trainSeconds`, `level`.
- **Producción**: `building` (`city` | `harbor` | `none`). La casilla de la rejilla la calcula
  `ProductionHotkeys` con su algoritmo actual.
- **Vida**: `maxHealth`, `armor`, `armorType`, `mechanical`, `canBeAttacked`.
- **Movimiento**: `domain` (`land` | `sea` | `static`), `speed`, `forestPenalty`, `hullClearance`,
  `separation`.
- **Geometría** (separadas):
  `collision {radius}` (agente/ucol), `body {radius}` (alcance cuerpo a cuerpo),
  `hull {length, beam}` (barcos; también construye el `BoxCollider` de `NavalArt`),
  `footprint {size}` (torres/edificios), `visual {standingHeight, standingWidth}` (selección y
  escalado), `spawnRadius`.
- **Armas** (`weapons[]`): `attackType`, `base`, `dice`, `sides`, `cooldown`, `attackPoint`,
  `backswing`, `range`, `minRange`, `ranged` (visibilidad y formación),
  `rangeMeasure` (`bodyEdges` | `centerToApproach` | `centerToCenter` | `toHull`),
  `reach {approachMargin, holdMargin}`, `delivery` (`Instant` | `Missile` | `Artillery` |
  `MissileSplash`), `targeting` (`TrackTarget` | `LaunchPoint`), `projectileSpeed`,
  `flightTime {min, max}`, `splash {rings:[{radius, factor}], mask}`, `targetMask` (clases **y**
  relaciones, con los mismos bits que `WeaponTargetMask`), `uphillMiss`.
- **Adquisición**: `range`, `leash {hostile, neutral, ranged, hold}`, `pressureBias`,
  `allyAlertRadius`, `retaliate`.
- **Capacidades**: `canCapture`, `canGarrison`, `canEmbark`, `transport {capacity, loadRadius,
  loadLimit}`, `heal {amount, range, cooldown, manaCost, rescan}`, `roar {area, duration,
  manaCost, damageBonus, evaluation}`, `mana {max, initial, regen}`, `harborGuard`.
- **Torres**: `hostWeapons {town, harbor}` para el arma según anfitrión.
- **Presentación**: `model`, `variant`, `portrait`, `attackClip`.

### 4.2 Esquema TypeBox y contrato C#

- `scripts/config/` con `package.json` (`@sinclair/typebox`, `ajv`, versiones fijadas).
- `units.schema.ts` → `build.mjs` emite `units.schema.json` y **genera** `Core/UnitConfig.g.cs`
  (DTO puros con `System.Serializable`, sin `UnityEngine`). El DTO no se escribe a mano.
- `validate.mjs`: esquema estricto (`additionalProperties:false`) e invariantes: ids únicos,
  `heal`/`roar` exigen `mana`, `transport` solo en `sea`, `rangeMeasure: bodyEdges` exige
  `body.radius`, `targeting: LaunchPoint` exige `delivery: Artillery`.
- CI: `npm test` en `scripts/config/` y una prueba EditMode que regenera el DTO y falla si
  difiere del commiteado (sin deriva TypeBox↔C#).

### 4.3 Carga

- `Runtime/UnitConfigLoader.cs`: `Resources.Load<TextAsset>("Config/units")` (síncrono también
  en WebGL), Newtonsoft con `MissingMemberHandling.Error` y enums como texto, validación de
  invariantes, y `UnitCatalog.Bind(perfiles)` antes de crear cualquier consumidor.
- Los perfiles se resuelven al cargar a **structs planos** indexados por un índice denso
  asignado en `Bind`; en el tick no hay diccionarios por id ni asignaciones.
- Si el catálogo no está enlazado, el acceso falla con un error claro (no un valor por defecto).
- Editor y pruebas: `SubsystemRegistration` reinicia el catálogo.

### 4.4 Capa de reglas común

`Core/UnitRules.cs` (funciones puras sobre perfiles y estado) + `IUnitMotor` (adaptador de
motor: NavMesh o `SeaNavigation`) implementado por `Soldier` y `Ship`; `DefenseTower` usa las
mismas reglas con motor estático.

| Regla | Qué decide | Conserva |
|---|---|---|
| `Measure(attacker, weapon, target)` | distancia según `rangeMeasure` | las cuatro fórmulas actuales |
| `InRange` / `ReachPlan` | entrar, sostener (histéresis), retirarse por `minRange` | márgenes 0,2/0,4 del cuerpo a cuerpo, retirada del mortero |
| `Acquire` | objetivo automático con correas, `pressureBias`, desempate por `EntityId`, alerta a aliados | el comportamiento de soldados y el «solo en alcance de arma» de barcos, ahora como datos |
| `CanAttack` | `targetMask` contra clase y relación del objetivo | que un soldado cuerpo a cuerpo pueda atacar barcos como hoy |
| `ResolveClick(selection, hit, modifiers)` | atacar / mover / seguir / embarcar / capturar / atracar, con la **prioridad actual** escrita como tabla | barco enemigo antes que puerto, transporte propio embarca, torre → captura |
| Captura, guarnición y relevo | una sola ruta para soldado y barco guardián | `CityClaimZone` sin ramas por clase |
| Transporte | máquina de estados: reserva de plazas, embarque, descarga parcial, muerte del transporte | radio 10,24 y tope 10 |

Criterio: en `UnitRules` y en el despacho no hay ramas `is Ship` / `is Soldier`; solo se decide
por datos y capacidades. Las diferencias de motor viven en `IUnitMotor`.

### 4.5 Órdenes y cola

- `UnitCommand` gana `Capture(buildingId)`, `Embark(transportId)`, `Unload(point)` y un
  `CommandId`. `BattleCommands` despacha a cualquier actor por `EntityId`, valida el destino por
  `domain` (NavMesh o `SeaNavigation.HasClearance`) y publica un resultado por `CommandId`
  que leen UI e IA. Los barcos dejan de recibir órdenes directas.
- **Punto de drenaje**: el mismo que hoy (inicio del tick de `BattleWorld`), para todos los
  actores; se mantiene el orden de fases soldados → torres → barcos → proyectiles → IA.
- **Semántica de la cola** (por unidad, tope 35; la cola vive en el estado de la unidad y se
  borra en `Initialize`, así el pool no la arrastra):

| Orden | Con Shift | Sin Shift | Al completarse |
|---|---|---|---|
| Move / AttackMove | se añade | reemplaza la cola | siguiente orden cuando el punto se alcanza y no queda un blanco vivo. Atacar-mover con un blanco vivo sigue ocupado (soldado y barco); el combate no forma parte de una aproximación de captura |
| Attack(target) | se añade | reemplaza | objetivo muerto → siguiente. Un blanco vivo mantiene la orden |
| Capture(building) | se añade | reemplaza | tierra: capturado o el dueño pasa a otro jugador → siguiente. Un transporte (no puede reclamar) termina solo cuando la vela y la descarga acaban, aunque el puerto siga hostil o lo tome otro jugador. Un barco de guerra sobre un puerto hostil con guardián vivo sigue combatiendo hasta que el puesto cambia de dueño, igual que v0.33; la cola no avanza mientras tanto |
| Follow(target) | se añade como terminal | reemplaza | no termina; objetivo muerto → siguiente |
| Patrol | se añade como terminal | reemplaza | no termina |
| Embark / Unload | se añaden | reemplazan | al embarcar se conserva la cola del pasajero para después de desembarcar |
| Stop / Hold | — (no se encolan) | vacían la cola | — |
| Shift sobre unidad Idle/Hold | la primera orden empieza ya | — | — |

  El atacar-mover conserva su destino si su objetivo muere (hoy lo hace el barco; pasa a ser
  la regla para todos).
- **Ruta visible**: puntos y líneas finas con el color de la orden, usando la polilínea real
  (`NavMeshPath` o `SeaNavigation`), con un **tope agregado** de segmentos dibujados y pooling;
  se muestra para la selección mientras se mantiene Shift.
- **Tecla**: Shift deja de acelerar la cámara (pasa a otra tecla) para no chocar con la cola.
- **Táctil**: un conmutador «Encolar» en la barra rápida.

### 4.6 Identidad y pool

- El tipo se identifica por el id del JSON; el índice denso solo existe dentro del proceso.
- `EntityId` nuevo en cada alquiler del pool (como hoy); cola, buffs, reservas y referencias se
  limpian en `Initialize`.
- Preparado para red/guardado: versión y hash del catálogo, `CommandId`, `EntityId` y
  `BuildingId`; ninguna referencia de Unity como identidad.

## 5. Pasos de ejecución

Cada paso termina con `python scripts/quick_compile.py` limpio y **todas** las pruebas en verde.
Cada paso borra lo que sustituye en ese mismo paso: no quedan alias, forwarders ni tablas viejas
entre pasos.

1. **Inventario y fixture de oro.** Script de Editor que vuelca desde las tablas vivas cada
   campo consumido por el código a `Tests/Fixtures/unit-golden.json` y una matriz de conductas
   (quién mide qué, correas, prioridades de clic, semántica de cola actual). Prueba de paridad
   que compara el catálogo contra ese fixture. No se borra nada.
2. **Esquema, generación y carga.** TypeBox, `units.schema.json`, DTO generado, `validate.mjs`,
   `npm test`, `UnitConfigLoader` con Newtonsoft (dependencia directa). `units.json` se genera
   desde el fixture. Pruebas de ida y vuelta (enums como texto, nulos, campos desconocidos →
   error).
3. **Catálogo desde datos (atómico).** Todos los lectores (`UnitCatalog`, `NavalProfiles`,
   `SourceWeapons`, `SourceGeometry`, `SupportAbilities`, `ProductionCatalog`, `VisualMetrics`,
   `DefenseTower`, `NavalArt`, `GameText` para nombres de unidad) leen del catálogo enlazado y
   se **borran** las tablas en el mismo paso. La prueba de paridad del paso 1 sigue en verde sin
   editar sus expectativas. `ProductionHotkeys` conserva su algoritmo.
4. **Fuera los índices por ordinal.** `SoldierPool`, `AiPlanning`, `UnitPresentationLod`,
   `AttackPresentationTiming`, `UnitVariantViews` y pruebas usan el índice denso del catálogo.
   Después se fusiona `NavalUnitKind` en `UnitKind` (o se sustituyen ambos por el id), también
   en compras (`PlayerBuildingIntent`), HUD y pruebas, en el mismo paso.
5. **Reglas de alcance y adquisición comunes.** `UnitRules.Measure/InRange/ReachPlan/Acquire/
   CanAttack` extrayendo las fórmulas actuales como datos; `Soldier`, `Ship` y `DefenseTower` las
   usan. `MeleeReachTests`, `NavalStandoffTests`, `TowerCombatTests` y el daño de área sin
   cambiar expectativas.
6. **Despacho único y órdenes nuevas.** `BattleCommands` para cualquier actor por dominio,
   `CommandId` y resultados, `Capture`/`Embark`/`Unload`, `ResolveClick` como tabla, captura y
   transporte comunes. **En el mismo paso** migran la UI, la IA (`SkirmishCommander`,
   `NavalExpeditionCommander`, `NavalWorld`) y los probes (`RuntimeCommandProbe*`,
   `RuntimeVisualCapture`, `RuntimePresentationCapture`).
7. **Cola completa.** Matriz de §4.5, ruta visible con tope, cambio de tecla de cámara,
   conmutador táctil.
8. **Medición y documentación.** CPU, GC y memoria con 900 unidades y 16 IA en Windows y WebGL
   (`scripts/measure_web_resources.py`, probes existentes), antes/después.
   `docs/RISK-RULES-v0.34.md` generado desde `units.json`, marcando qué es fuente y qué es
   adaptación local. Sección en `docs/DEVELOPMENT.md`: cómo añadir una unidad.

## 6. Pruebas y criterios de aceptación

- `npm test` en `scripts/config/` y la comprobación de DTO generado sin cambios.
- **Paridad**: el fixture de oro del paso 1 sigue igual al final (salvo los cambios intencionados
  de §7, que actualizan el fixture en el mismo commit con su motivo).
- **Procedencia** contra las cuatro fuentes: `data/derived/reforged-source-combat.json`,
  `Resources/Maps/SourceGeometry.json`, `data/source-mdx-geometry.json` y las tablas RoC/TFT;
  las adaptaciones listadas en `adaptation` son las únicas diferencias permitidas.
- Mismo escenario por la API común para un soldado (en tierra) y un barco (en el mar):
  atacar con el objetivo en alcance (no se mueve más de 1 m y hace daño), objetivo fuera de
  alcance (se acerca hasta el alcance y ataca), adquisición automática y mantener posición.
- Cola: Shift + mover ×2 + Shift + capturar ciudad → recorre los puntos y captura; lo mismo con
  barcos (mover ×2 + atacar barco); objetivo que muere a mitad de cola; clic sin Shift reemplaza;
  embarcar con órdenes encoladas del pasajero; pausa; selección mixta tierra/mar.
- Siguen en verde todas las suites actuales (EditMode + PlayMode completas), incluidas
  `GroundZoneShapeTests` y el resto de guardas.
- Rendimiento no peor que v0.33 con 900 unidades / 16 IA (medido, no supuesto).

## 7. Cambios de comportamiento intencionados

Solo estos; todo lo demás debe quedar igual:

1. Cola con Shift para `Attack`, `Capture`, `Follow`, `Embark`/`Unload` y para barcos.
2. Shift sobre una unidad parada empieza la primera orden (hoy no encola).
3. Shift deja de acelerar la cámara.
4. El atacar-mover conserva su destino si muere el objetivo también en soldados.

## 8. Riesgos

- **Tamaño**: ocho pasos atómicos; si un paso no queda en verde, no se empieza el siguiente.
- **Fidelidad**: el fixture de oro es el árbitro; nada se reescribe a mano.
- **Rendimiento**: structs planos, sin asignaciones por tick, tope de ruta visible, medición.
- **WebGL**: Newtonsoft con IL2CPP/stripping (añadir `link.xml` si hace falta) y medición de
  tamaño y tiempo de carga.
