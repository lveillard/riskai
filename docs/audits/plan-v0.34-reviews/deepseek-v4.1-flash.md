# Revisi├│n adversarial del PLAN.md (v0.34)

## 1. Veredicto

**No es ejecutable tal como est├í escrito.** El objetivo es correcto y la motivaci├│n (barco = unidad con otras propiedades) es la adecuada, pero el plan subestima tres acoplamientos duros del c├│digo real y su modelo de datos es m├ís pobre que las tablas que pretende sustituir. Los tres mayores riesgos: (a) el paso 3 no compila (`Core` es `noEngineReferences`); (b) la "API maestra" de ├│rdenes no puede servir a barcos (el inbox est├í tipado a `Soldier` y valida contra `NavMesh`); (c) el esquema JSON no puede expresar el comportamiento actual (falta `Ranged`, `Mechanical`, y `targets` es en realidad una m├íscara de flags con sem├íntica de fuego amigo). Con los pasos actuales el resultado probable es un juego que compila pero cambia comportamiento en silencio ΓÇö justo lo que el propietario proh├¡be.

## 2. Hallazgos por severidad

**P0-1 ┬╖ `Core/UnitConfig.cs` con `JsonUtility`/`Resources.Load` no compila.**
`Scripts/Core/RiskAI.Core.asmdef` declara `"noEngineReferences": true`, y ning├║n archivo de `Scripts/Core/` usa `UnityEngine` (verificado: 0 ocurrencias). El paso 3 lo contradice literalmente.
*Cambio:* el DTO `[Serializable]` (`System.Serializable`, sin `UnityEngine`) vive en Core; el cargador (`Resources.Load<TextAsset>` + `JsonUtility`) vive en `RiskAI.Runtime` (`RiskAI/UnitConfigLoader.cs`) y llama a `UnitCatalog.Bind(config)` expl├¡cito. `Resources.Load` en un inicializador est├ítico de Core es imposible, as├¡ que hace falta bind expl├¡cito + fallo ruidoso si nadie carg├│ (hoy `BattleRules.TowerHealth => UnitCatalog.Tower.Health` en `DefenseTower.cs:14` reventar├¡a con NRE).

**P0-2 ┬╖ El inbox gen├⌐rico de ├│rdenes es en realidad "solo soldados".**
`BattleCommands.cs:85-89` valida todo `Move/AttackMove/Patrol` con `NavMesh.SamplePosition`, y `:74`/`:88` hacen `as Soldier` y `(Soldier)session.FindTarget(...)`; `Follow` exige `target is Soldier` (`:83`). Los barcos hoy se mandan por m├⌐todos propios (`RtsController.cs:331,372,419`; `NavalWorld.cs:134,147,178,263-277`; `NavalExpeditionCommander.cs:160,194,443`) y `Ship` no usa `NavMeshAgent`.
*Consecuencia:* los criterios de aceptaci├│n "el mismo escenario para un soldado **y** para un barco" y la cola Shift de barcos son inalcanzables sin reescribir el inbox. Ninguna orden de barco pasar├¡a la validaci├│n (el mar no es NavMesh) ΓåÆ rechazo silencioso.
*Cambio:* el paso 5/6 debe incluir expl├¡citamente: `UnitCommand` resuelto contra `CombatTarget`/interfaz de actor (no `Soldier`), validaci├│n de destino enrutada por `movement.domain` (`NavMesh.SamplePosition` vs `SeaNavigation.HasClearance`), y `Kind` nuevos `Embark/Unload` (hoy `UnitCommandKind` no los tiene y `Valid` usa un rango cerrado `Kind<Move||Kind>Follow`).

**P0-3 ┬╖ Fusionar `NavalUnitKind` en `UnitKind` colisiona con subsistemas indexados por ordinal.**
`AiPlanning.cs:127-132` construye su tabla como `new AiUnitTraits[UnitCatalog.Land.Length]` con `Build((UnitKind)i, UnitCatalog.Land[i])`; si `UnitKind` crece con 5 barcos, la tabla se desalinea o revienta (y `AiPlanningTests.cs:63-65` recorre *todos* los `UnitKind` exigiendo `Cost>=1`, `Value>0`). Igual: `SoldierPool.cs:28-29` (`Key(team,kind)`), `UnitPresentationLod.cs:38,182` (array de mallas por ├¡ndice), `AttackPresentationTiming.cs:14-40` (switches por kind), y `SourceRosterV030Tests.cs:48` (`UnitCatalog.Land.Length == Enum.GetValues(UnitKind).Length`). Adem├ís `UnitCatalog.Definition/Profile` (`UnitCatalog.cs:56-63`) indexan por ordinal.
*Cambio:* el plan debe fijar "los ids navales se **a├▒aden al final**", sustituir la tabla de IA por una filtrada por `movement.domain`, y a├▒adir test de alineaci├│n ordinal `units.json[i].id == ((UnitKind)i).ToString()`.

**P1-1 ┬╖ El esquema no expresa `Ranged` ni `Mechanical`.**
`BattleRules.Ranged` (`BattleRules.cs:47`, campo `LandUnitDefinition.Ranged`) decide la visibilidad (`NavMesh.Raycast` melee vs `Physics.Raycast` a distancia, `Soldier.cs:262-273`), la distancia borde-a-borde (`Soldier.cs:427`), las filas de formaci├│n (`BattleSession.cs:335,352`) y el tracer instant├íneo (`CombatWorld.cs:47-49`). No es derivable de `delivery`: `MarinePrivate`/`EliteRifleman` tienen `delivery Instant` (`WeaponProfile.cs:150,163`) pero `Ranged=true`; `range>2` funciona hoy pero es accidental y ning├║n invariante del plan lo fija. `Mechanical` (usado por `MedicSupport.cs:79` para no curar h00M/h01A) tampoco est├í en el JSON.
*Cambio:* a├▒adir `ranged` y `mechanical` expl├¡citos (o `meleeReach` bool) con invariantes validados.

**P1-2 ┬╖ `targets: ["land","sea"]` es una p├⌐rdida sem├íntica grave.**
Lo que existe es `WeaponTargetMask` (`WeaponProfile.cs:20-30`) combinando clases (Ground/Soldier/Structure/Tree/Wall/Debris/Item/Ward) **y relaciones** (Enemy/Neutral/Ally/Self). `EligibleForSplash` (`CombatWorld.cs:170-189`) cambia la resoluci├│n seg├║n los bits de relaci├│n: si no hay bits de relaci├│n se permite da├▒o aliado por la v├¡a `int.MinValue` (`CombatWorld.cs:192-201`). M├íscaras reales: `MortarSplash = Tree|Ground|Structure`, `WarshipSplash = Debris|Enemy|Ground|Neutral|Structure|Wall`, mago `Soldier|Enemy|Neutral` (`WeaponProfile.cs:125-127,171-174`). Con `["land","sea"]` el mortero pasar├¡a a da├▒ar aliados y dejar├¡a de afectar ├írboles/muros ΓåÆ cambio de comportamiento no declarado.
*Cambio:* el JSON debe llevar la m├íscara completa (clases + relaciones), no un dominio de 3 valores.

**P1-3 ┬╖ `InRange(... targets admite el dominio del objetivo)` introduce una regla que hoy no existe.**
Hoy un soldado melee puede atacar un barco: la adquisici├│n no filtra por tipo (`Soldier.cs:297-319`) y `CombatTarget.CanBeAttacked` no distingue dominio. Si `Knight.targets=["land"]`, deja de atacar barcos.
*Cambio:* declarar el filtrado como cambio intencional expl├¡cito, o eliminarlo del plan.

**P1-4 ┬╖ `footprint.radius` colapsa tres radios distintos y rompe el picking.**
Hoy conviven: `Agent.radius`=ucol (`SourceGeometry.cs:29-52`, p.ej. Knight 0.64), `BodyRadius` para alcance melee (`Soldier.cs:413`), y el radio de picking visual (`VisualMetrics.RadiusFor` = `standingWidth/2`, `VisualMetrics.cs:20`; Knight Γëê1.48) usado en `RtsPicking.cs:46-48`. Con un ├║nico `radius` de colisi├│n, el clic sobre un caballero se vuelve mucho m├ís dif├¡cil. En barcos pasa lo mismo: el fix de v0.33 es la caja del `BoxCollider` creada desde constantes de presentaci├│n (`NavalArt.cs:18-19`, `IsWarship/HullScale` en `:72-73`), y el plan la borra (`Ship.cs:357`) sin garantizar n├║meros id├⌐nticos ni que `NavalArt` lea el mismo dato.
*Cambio:* separar `collisionRadius`, `visual.height/width`, `spawnRadius` y `hull{length,beam}` (que debe ser tambi├⌐n la fuente del collider de `NavalArt`). Falta adem├ís `HullClearance` por unidad (`SeaNavigation.cs:12`, hoy 1.3 global) y la separaci├│n entre barcos (2.8 m fijo, `Ship.cs:281`).

**P1-5 ┬╖ Comportamiento por clase oculto que el plan no mapea.**
Soldado: pesos de presi├│n `Kind==Footman ? .48f : .1f` (`Soldier.cs:311`); `AutonomousLeash` con constantes 7/11/5/7.5 y `Ranged+2` (`Soldier.cs:292-295`); alerta a aliados a 5 m (`Soldier.cs:~575`); hist├⌐resis melee (`MeleeReachMargin/ApproachMargin`, `Soldier.cs:415-419`); velocidad de bosque (`Soldier.cs:394-403`). Barco: guardia de puerto que *teletransporta* la posici├│n (`Ship.cs:326`), `LoadRadius=10.24`, `RouteStallSeconds=3`, orden de atraque (`orderedHarbor`), desembarco diferido (`pendingShoreUnload`), muerte que destruye la carga. Nada de esto est├í en la tabla de `UnitBehaviour` ni en el JSON; sin inventariarlo, "la capa maestra" no puede reproducirlo.

**P1-6 ┬╖ Las pruebas que el plan da por verdes se rompen.**
`MeleeReachTests.cs:28` usa `Soldier.BodyRadius`; `NavalStandoffTests.cs:48-52` depende de `ApproachPoint`/hull; `NavalSourceProfileTests.cs:64` usa `NavalProfiles.Profile`; `ProductionCatalogTests`/`ProductionHotkeysTests` fijan listas y orden; `SourceGeometryTests` fija ucol y el default `.24`; `SourceRosterV030Tests.cs:35,48` fija `Ranged == range>2` y la igualdad de longitudes. Y "migrar las pruebas con n├║meros m├ígicos a leer del JSON" las convierte en tautol├│gicas: se pierde la garant├¡a de procedencia. Peor: `data/derived/reforged-source-combat.json` **no contiene** `ucol` (no existe `ucol` entre los campos), ni `uabi`, ni man├í, ni bounds MDX ΓÇö esos viven en `Resources/Maps/SourceGeometry.json`, `data/source-mdx-geometry.json` y las fixtures binarias.
*Cambio:* mantener intactas las pruebas de valor existentes durante el cambio (que pasen a ejercitar el JSON), y escribir las pruebas de procedencia contra las **cuatro** fuentes, no una.

**P1-7 ┬╖ Doble fuente de verdad de nombres.** `GameText.cs:77-88` traduce "Espadach├¡nΓåÆSwordsman", "FragataΓåÆFrigate", etc. Si `units.json` aporta `names.es/en`, hay dos SSOT de localizaci├│n y riesgo de doble traducci├│n o de que `BattleRules.Name` (usado en ~12 sitios de HUD) siga devolviendo espa├▒ol.

**P2 ┬╖ Determinismo y orden.** `SimClock` es 20 Hz determinista (`SimClock.cs:9`); la cola debe vaciarse en un punto fijo del tick (hoy `BattleCommands.Tick()` no aparece en el camino de `tickWorld`, mientras `Soldier`/`Ship` corren en el bucle de `BattleSession`). Al desempatar por `EntityId` y al consumir una orden por tick, el orden importa.

**P2 ┬╖ Pooling/identidad.** Mover la cola a la capa maestra indexada por `EntityId` exige purga en `SoldierPool.Retire`/`Initialize` (hoy `Soldier.cs:106` hace `orders.Clear()`). El patr├│n correcto ya existe: `RtsController.SoldierRef.ShipRef` (`RtsController.cs:40-56`).

**P2 ┬╖ Sem├íntica Shift ambigua.** Hoy `Issue` con `append` **no** encola si `mode==Idle||Hold` (`Soldier.cs:209`) y `Attack/Follow/Stop/Hold` ignoran `Append` y vac├¡an la cola (`:237,244-252`); `Complete()` invierte patrulla (`:246`). Hay que especificar el caso "Shift sobre unidad idle" y el l├¡mite 35 por unidad y por tipo.

**P2 ┬╖ Rendimiento.** 900 unidades ├ù cola de 35 + ruta visible: las l├¡neas deben ir pooled y con tope agregado (no 1 `LineRenderer` por unidad), y `Acquire` no debe asignar (hoy reusa `nearby`/`alerted`). `UnitBehaviour` no puede ser un `MonoBehaviour` por unidad; arrays planos indexados por kind (lo dice el plan, hay que hacerlo cumplir con medici├│n, p.ej. `scripts/measure_web_resources.py`).

**P3 ┬╖** `gridSlot` expl├¡cito rompe el reparto autom├ítico y StableSort por coste de `ProductionHotkeys.cs:71-83`; hay que conservar paginaci├│n (celda V) y validar colisiones. `ProductionOption`/`ProductionBuilding` necesitan redefinirse sin `IsShip` (`ProductionHotkeys.cs:12-20`). `JsonUtility` no acepta enums como string, ni `Dictionary`, ni `float?`, y **ignora campos desconocidos** ΓåÆ `additionalProperties:false` no protege en runtime.

## 3. Faltantes que el plan debe incluir

- **Deriva TypeBoxΓåöC#**: ning├║n mecanismo evita que el DTO se desincronice del esquema. A├▒adir test EditMode que lea `units.schema.json` y compare nombres de propiedades contra el DTO por reflexi├│n (o generar el DTO desde el esquema).
- **Conversi├│n stringΓåÆenum**: `JsonUtility` no la hace; hay que implementarla (tabla de conversi├│n expl├¡cita) o guardar enteros y perder legibilidad.
- **Extracci├│n a m├íquina, no a mano**: un test/men├║ de editor que serialice las tablas actuales a `units.json` (as├¡ los valores no se transcriben mal), y luego borrar tablas en el mismo PR.
- **Acoplamiento IA**: `AiPlanning.Table/Build/IsHealer` (rol derivado de la cadena `Role`, `AiPlanning.cs:139-152`), `AiForceMix`, `SkirmishCommander` (`SkirmishCommander.cs:728,943,970`), `NavalExpeditionCommander` (llamadas directas a `Ship`), `NavalWorld.cs:192,289`.
- **Harnesses**: `RuntimeCommandProbe(.Sustained).cs` (env├¡a `UnitCommand`), `RuntimePresentationCapture.cs:161,213`, `RuntimeVisualCapture.cs:237`, `RuntimeUiCapture.cs`; `RuntimeCommandProbeFixtureTests`. 89 usos directos de `MoveTo/Attack/TryEmbark/BodyRadius/TryGetHullBounds` en pruebas y probes.
- **Torres**: `DefenseTower` **no** lee `AttackType/ArmorType` del perfil (hardcode Piercing/Divine, `DefenseTower.cs:22-23`), usa `CapturableTower` para rango/cooldown/da├▒o (`:36-37,116,122`) pero `BattleRules.TowerHealth` viene de `UnitCatalog.Tower` (`BattleRules.cs:35`), y el arma depende del anfitri├│n (`Town ? MilitaryBase : Shipyard`, `:114`). El esquema necesita `armorType`, `canBeAttacked:false` y la variante de arma por anfitri├│n.
- **`Level`/`RequiredLevel`** (`UnitProfile.Level`, `BattleRules.cs:57`), `PointValue` vs `Cost` (transporte: coste 10, puntos 2), `CanCapture` por barco, `RoarEvaluationInterval`/`RescanInterval` (adaptaciones locales, `SupportAbilities.cs:57`, `MedicSupport.cs:18`).
- **Guardar identidad de unidad**: no hay sistema de guardado; el riesgo real es pooling + `EntityId` (`BattleSession.cs:151-163`), no "save".
- **SimClock**: punto exacto de drenaje de la cola y orden estable.
- **WebGL**: `Resources.Load` es viable y s├¡ncrono; el riesgo es el enum-string y el peso; a├▒adir medici├│n al script existente.

## 4. Orden de pasos propuesto

1. **Extracci├│n mec├ínica + esquema.** Test/men├║ de editor que vuelca tablas ΓåÆ `units.json`; TypeBox, `validate.mjs`, `npm test`; tests EditMode de presencia, **alineaci├│n ordinal `i==(int)kind`**, unicidad de `gridSlot`, invariantes (incl. `ranged`/`mechanical`/m├íscara completa). No borrar nada a├║n.
2. **Cambio de consumidores sin borrar tablas todav├¡a**: DTO en Core + loader en Runtime + `Bind` expl├¡cito; `UnitCatalog` como vista. Criterio: **todas las pruebas de valor actuales pasan sin editarlas**. Esto demuestra fidelidad sin capa de compatibilidad duradera (las tablas se borran en el mismo commit que el verde).
3. **Borrar tablas duplicadas** (`NavalProfiles`, `SourceWeapons`, `SourceGeometry`, `SupportAbilities`, `ProductionCatalog/Hotkeys`, `VisualMetrics`), conservando las funciones derivadas y las pruebas de valor.
4. **Fusi├│n de enums + arreglo de los indexados por ordinal** (`AiPlanning`, `SoldierPool`, `UnitPresentationLod`, `AttackPresentationTiming`, tests). Test de alineaci├│n.
5. **Capa maestra geom├⌐trica** (`Footprint`/`SurfaceDistance`/`InRange`/`ApproachPlan`) *extrayendo las f├│rmulas actuales*, con los tests de `MeleeReachTests`/`NavalStandoffTests`/`TowerCombatTests` sin cambios de expectativa; migrar solo los accesos (`BodyRadius`, `TryGetHullBounds`).
6. **Generalizar el inbox** (`CombatTarget` + dominio + `Embark/Unload`) y pasar barcos y torres por ├⌐l; IA y probes incluidos. *Antes* de prometer cola de barcos.
7. **Adquisici├│n y resoluci├│n de ├│rdenes en una ruta** (conservando alerta, hist├⌐resis, presi├│n de Footman, leash, guardias de puerto).
8. **Cola Shift + ruta visible** (tope, pooling, t├íctil), con los casos de aceptaci├│n (incl. "objetivo muerto ΓåÆ siguiente", "clic sin Shift reemplaza").
9. **IA sobre la capa maestra**, luego **documentaci├│n generada**.
