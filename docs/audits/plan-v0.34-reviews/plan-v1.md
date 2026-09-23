# Plan v0.34 · Unidades por configuración, comportamiento único y cola de órdenes

Estado: **listo para ejecutar** en esta misma PR. Se parte de `main` después de la v0.33
(PR #9: fixes navales, aviso de zona perdida, burdeos, limpieza de legacy).

## Por qué

El bug de «los barcos se acercan en vez de atacar» salió porque barcos y soldados resuelven
lo mismo por caminos distintos:

| Concepto | Soldado | Barco |
|---|---|---|
| Selección por clic | radio de colisión | caja fija de 1,8 m (arreglado en v0.33 con `TryGetHullBounds`) |
| Alcance | borde a borde (cuerpo a cuerpo, v0.30) | casco (v0.33) |
| Prioridad del clic derecho | unidad → edificio | caso especial para puertos |
| Adquisición automática | `Soldier` | `Ship`, reglas propias |
| Cola con Shift | mover, atacar-mover, patrullar (35) | no existe |

Además, los datos de cada unidad están repartidos en tablas escritas a mano:
`Core/UnitCatalog.cs`, `Core/NavalProfiles.cs`, `Core/WeaponProfile.cs` (`SourceWeapons`),
`Core/SourceGeometry.cs`, `Core/SupportAbilities.cs`, `Core/ProductionCatalog.cs`,
`Core/ProductionHotkeys.cs`, `VisualMetrics.cs`, `UnitVariantViews.cs`.

Un barco es una unidad con otras propiedades (dominio mar, huella de casco, transporte).
Con una única fuente de datos y una única capa de comportamiento, los barcos heredan
automáticamente todo lo que funciona para las unidades de tierra, incluida la cola de órdenes.

## Objetivo

1. **Una fuente de verdad (SSOT)**: `RiskAI/Assets/RiskAI/Resources/Config/units.json` con
   todas las unidades de tierra, barcos y torres/puestos.
2. **Esquema TypeBox** que valida ese JSON en modo estricto.
3. **Una capa de comportamiento maestra** que lee las propiedades del JSON y decide huella,
   alcance, acercamiento, adquisición y resolución de órdenes para cualquier unidad.
4. **Cola de órdenes con Shift** general (mover, atacar-mover, atacar objetivo, seguir,
   patrullar, embarcar/desembarcar), con ruta visible, válida para cualquier unidad por diseño.

Añadir una unidad = una entrada en `units.json` + su modelo. Sin capas de compatibilidad,
sin alias, sin tablas duplicadas.

## Diseño

### 1. `units.json`

Una entrada por tipo. Campos (todos obligatorios salvo indicación):

```jsonc
{
  "id": "Knight",                     // nombre del enum UnitKind
  "names": { "es": "Caballero", "en": "Knight" },
  "role": { "es": "Caballería pesada", "en": "Heavy cavalry" },
  "source": { "rawcode": "h00G", "base": "hkni", "notes": "…" },  // procedencia WC3
  "economy": { "cost": 5, "points": 5, "trainSeconds": 1 },
  "production": { "building": "city", "gridSlot": 6 },           // o null (torres)
  "health": { "max": 650, "armor": 7, "armorType": "Heavy" },
  "movement": { "domain": "land", "speed": 7 },                  // land | sea | static
  "footprint": { "shape": "circle", "radius": 0.64 },            // circle | hull {length, beam} | square {size}
  "weapons": [{
    "attackType": "Normal", "base": 37, "dice": 2, "sides": 5,
    "range": 2, "minRange": 0, "cooldown": 1.36,
    "attackPoint": 0.66, "backswing": 0.44,
    "delivery": "melee",                                          // melee | instant | missile | artillery | msplash
    "projectileSpeed": null, "splash": null,                      // { full, medium, small, factors[] }
    "targets": ["land", "sea"]                                    // land | sea | structure
  }],
  "acquisition": { "range": 10, "leash": 12 },
  "capabilities": {
    "canCapture": true, "canGarrison": true, "canEmbark": true,
    "transportCapacity": 0,
    "heal": null,                                                 // { amount, range, cooldown, manaCost }
    "roar": null,                                                 // { area, duration, manaCost, damageBonus }
    "mana": null                                                  // { max, initial, regen }
  },
  "visual": { "model": "RoyalGuard", "variant": "Mounted", "standingHeight": 3.06,
              "standingWidth": 1.1, "portrait": "Knight" }
}
```

Unidades de tierra, los cinco barcos y las dos torres (`Bunker`, `CityPost`) entran igual.
Valores iniciales = los que hoy están en código (se extraen, no se reescriben a mano).

### 2. Esquema TypeBox

- `scripts/config/` con `package.json` (versiones fijadas de `@sinclair/typebox` y `ajv`).
- `units.schema.ts`: esquema TypeBox con enums, rangos y `additionalProperties: false`.
- `build.mjs`: genera `units.schema.json` junto a `units.json`.
- `validate.mjs`: valida `units.json` y comprueba invariantes que el esquema no expresa:
  ids únicos, `gridSlot` único por edificio, `transportCapacity > 0` solo en dominio `sea`,
  `heal`/`roar` exigen `mana`, `delivery: melee` exige `range ≤ 3`.
- Se ejecuta con `npm test` en `scripts/config/` y se documenta en `docs/DEVELOPMENT.md`.

### 3. Lado C#

- Clases `[Serializable]` espejo del esquema en `Core/UnitConfig.cs`, cargadas una vez
  (`Resources.Load<TextAsset>("Config/units")`) con `JsonUtility`. Si el proyecto ya tiene
  `com.unity.nuget.newtonsoft-json`, se usa ese; no se añaden dependencias pesadas.
- `UnitCatalog` pasa a ser una vista de solo lectura sobre esa configuración.
- Se eliminan las tablas duplicadas de `NavalProfiles`, `SourceWeapons`, `SourceGeometry`,
  `SupportAbilities`, `ProductionCatalog`/`ProductionHotkeys` (slots) y `VisualMetrics`
  (alturas objetivo). Solo quedan las funciones que calculan a partir de la configuración.
- `NavalUnitKind` se fusiona en `UnitKind`: una única enumeración de tipos, el dominio lo da
  `movement.domain`.

### 4. Capa de comportamiento maestra

Nuevo `UnitBehaviour` (o `Core/UnitRules.cs` + adaptador de motor), usado por `Soldier`, `Ship`
y `DefenseTower`:

| Función | Qué decide |
|---|---|
| `Footprint(target)` / `SurfaceDistance(a, b)` | distancia de superficie a superficie según `footprint` |
| `InRange(attacker, target, weapon)` | `SurfaceDistance ≤ range` y `≥ minRange`, y `targets` admite el dominio del objetivo |
| `ApproachPlan(attacker, target)` | ir a un punto ligeramente dentro del alcance, empezar a atacar al entrar, seguir mientras esté dentro |
| `Acquire(unit)` | adquisición automática con `acquisition.range`/`leash`, mantener posición y respeto al objetivo explícito |
| `ResolveOrder(selection, cursorHit, modifiers)` | clic derecho / A + clic → atacar, mover, seguir, embarcar, capturar, con las mismas prioridades para cualquier selección |

`Soldier` y `Ship` conservan solo lo que de verdad cambia en el motor: `NavMeshAgent` frente a
navegación marítima (`SeaNavigation`), y la presentación. La selección por clic (`RtsPicking`)
usa `Footprint`.

### 5. Cola de órdenes (Shift)

- Una cola única de órdenes por unidad en la capa maestra (no en `Soldier`), con tipos:
  `Move`, `AttackMove`, `Attack(target)`, `Follow(target)`, `Patrol`, `Embark`, `Unload`.
- **Shift + clic** añade al final; clic sin Shift reemplaza la cola. Límite 35 como hoy.
- Una orden sobre un objetivo que muere o desaparece se descarta y pasa a la siguiente.
- `UnitCommand` gana el tipo de orden completo y `append` para todos los tipos.
- **Ruta visible** mientras se mantiene Shift, o siempre para la selección: puntos y líneas
  finas en el suelo con el color de la orden (verde mover, rojo atacar, azul embarcar) y un
  pequeño icono en los objetivos. Pooled y cortado por LOD.
- Caso de uso guía: Shift + clic en un punto intermedio para decidir el punto de entrada y
  luego Shift + clic derecho sobre la ciudad para atacarla.
- Táctil: mantener pulsado el botón de orden actúa como Shift (o un conmutador «encolar» en
  la barra rápida).

## Pasos de ejecución

Cada paso termina con `python scripts/quick_compile.py` limpio y las pruebas afectadas en
verde. Sin capas de compatibilidad: cada paso borra lo que sustituye.

1. **Extraer la configuración.** Script que vuelca los valores actuales de las tablas de
   código a `units.json`. Esquema TypeBox, `validate.mjs`, `npm test`. Prueba EditMode que
   carga el JSON y comprueba que cada tipo de runtime tiene exactamente una entrada.
2. **Leer de la configuración.** `UnitCatalog` y el resto de consumidores leen de
   `UnitConfig`. Se borran las tablas escritas a mano. Las pruebas de procedencia comparan
   `units.json` con `data/derived/reforged-source-combat.json`; las demás leen valores del
   JSON en vez de números mágicos.
3. **Fusionar `NavalUnitKind` en `UnitKind`.** Un solo enum; retratos y nombres de objeto
   derivados del id.
4. **Capa maestra: huella, alcance y acercamiento.** `Soldier`, `Ship` y `DefenseTower` usan
   `SurfaceDistance`/`InRange`/`ApproachPlan`. Se borran `Ship.TryGetHullBounds` y la
   geometría duplicada de soldados.
5. **Capa maestra: adquisición y resolución de órdenes.** Una sola ruta para clic derecho,
   A + clic y adquisición automática, sin casos especiales por clase.
6. **Cola de órdenes general.** Cola en la capa maestra con todos los tipos, `append` en
   `UnitCommand`, ruta visible, soporte táctil.
7. **IA.** `SkirmishCommander`, `NavalExpeditionCommander` y `NavalWorld` emiten órdenes por
   la capa maestra (la IA puede usar la cola para rutas de entrada).
8. **Documentación.** `docs/RISK-RULES-v0.34.md` generado desde `units.json` (tabla de
   unidades), sección en `docs/DEVELOPMENT.md` sobre cómo añadir una unidad.

## Pruebas y criterios de aceptación

- `npm test` en `scripts/config/` valida `units.json`.
- EditMode: carga de configuración, invariantes, unicidad de slots y de ids.
- PlayMode, **el mismo escenario por la API maestra para un soldado y para un barco**:
  - orden de ataque con objetivo dentro de alcance → no se mueve más de 1 m y hace daño;
  - objetivo fuera de alcance → se acerca hasta el alcance y ataca;
  - adquisición automática y mantener posición.
- PlayMode de cola:
  - Shift + mover ×2 y luego Shift + atacar ciudad → recorre los puntos en orden y ataca;
  - mismo escenario con barcos;
  - objetivo que muere a mitad de cola → pasa a la siguiente orden;
  - clic sin Shift reemplaza la cola.
- Siguen en verde `NavalStandoffTests`, `MeleeReachTests`, `TowerCombatTests`, las suites
  naval, de puertos, de IA y de HUD, y la batería completa (EditMode + PlayMode).
- `GroundZoneShapeTests` y el resto de guardas visuales sin cambios.
- Una búsqueda de `Ship`/`Soldier` en la capa maestra no encuentra ramas por tipo de clase.

## Riesgos

- **Rendimiento con 900 unidades y 16 IA**: la capa maestra no debe asignar memoria por tick;
  la configuración se resuelve a estructuras planas al cargar.
- **WebGL**: el JSON va en `Resources`; se comprueba el tamaño y el tiempo de carga.
- **Pruebas con números mágicos**: se migran a leer del JSON en el paso 2.
- **Tamaño del cambio**: se hace por pasos con compilación y pruebas entre cada uno.
