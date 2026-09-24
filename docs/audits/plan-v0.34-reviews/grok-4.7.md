Voy a leer el plan completo y contrastarlo con el c├│digo real que toca, sin modificar nada.El plan asume una fusi├│n limpia de tablas y un comportamiento ├║nico. Voy a contrastar eso con los enums, las tablas y las ramas reales de soldado, barco y torre.## 1. Veredicto

**No es ejecutable tal como est├í.** El diagn├│stico mezcla el bug real (un barco fuera de alcance navega hasta el agua a Γëñ8 m del objetivo, `Ship.cs:130-132` y `:246`) con diferencias que son reglas vigentes. El esquema de `units.json` no puede representar el combate que hoy est├í en c├│digo, y el paso 2 borra esas tablas. Adem├ís, `RiskAI.Core.asmdef` tiene `noEngineReferences: true` y `references: []` (`:4-6`): `Resources.Load` y `JsonUtility` no compilan ah├¡.

El mayor riesgo es de comportamiento: un `InRange`/`ApproachPlan` ├║nico cambia el cuerpo a cuerpo, el alcance a distancia, el mortero y el casco orientado, y una resoluci├│n de clic ├║nico elimina el atraque, el embarque y el relevo de guarnici├│n.

## 2. Hallazgos

### P0

**El esquema pierde el arma y el paso 2 la borra.** `WeaponDelivery` es `Instant | Missile | Artillery | MissileSplash` (`WeaponProfile.cs:5-13`); no existe `melee`. El cuerpo a cuerpo es `Instant` con id `local-melee` (`:207`). Faltan en el JSON: `WeaponTargeting` (mortero y artiller├¡a son `LaunchPoint`, `:181` y `:193`), radios y factores de splash, `MinimumFlightTime`/`MaximumFlightTime` (mago `:201-204`), `WeaponTargetMask` (├írbol, escombros, enemigo, neutral, soldadoΓÇª `:125-127`, no `land|sea|structure`), `mechanical` (`BattleRules.cs:59`), `MinimumRange` del mortero (`UnitCatalog.cs:26`), `level` y el bool `Ranged` (el caballero tiene alcance 2 y `Ranged=false`, `:25`). El tanque es `MissileSplash` sin ├íreas: `HasSplash` es falso (`:197-199`). Los transportes no tienen arma (`:228-229`). Borrar `SourceWeapons` en el paso 2 deja el combate sin datos. El ejemplo ya no es un extracto: el caballero tiene ancho de pie 148,094/50 Γëê 2,96 (`SourceGeometry.cs:33`), no `standingWidth: 1.1`.

**La carga no cabe en Core ni en `JsonUtility`.** El plan pone el espejo en `Core/UnitConfig.cs` y lo lee con `JsonUtility`. Los enums del esquema son strings; `JsonUtility` los escribe como enteros y un `"Heavy"` acaba en el valor 0. No admite `float?`: `projectileSpeed: null` pasa a 0. Newtonsoft est├í en el lockfile solo como dependencia transitiva de `com.unity.pipeline` (`packages-lock.json:82-99`), no referenciado por `RiskAI.Runtime` ni por Core. Hay que declararlo como dependencia directa y parsear en Runtime, inyectando una tabla plana en Core.

**`SurfaceDistance` ├║nico cambia reglas que los tests fijan.** El cuerpo a cuerpo resta los dos radios y usa hist├⌐resis (`MeleeReachMargin` 0,2, `MeleeApproachMargin` 0,4, `Soldier.cs:412-428` y `:436-442`). A distancia se mide centro ΓåÆ `ApproachPoint`, no superficie (`:424-427`). Bajo el alcance m├¡nimo el mortero retrocede (`:446-456`). El barco mide hasta el `ClosestPoint` del `BoxCollider` orientado (`Ship.cs:75-79`, `:353-355`); `NavalStandoffTests.cs:51-64` y `:67-78` exigen ese casco, no un largo/manga de JSON. `SourceGeometry.cs:3-7` separa colisi├│n de NavMesh, visual y picking. Borrar `TryGetHullBounds` en el paso 4 rompe el picking (`RtsPicking.cs:42-45`) antes de que exista un casco con yaw.

**Fusionar enums rompe el ├¡ndice por ordinal.** `UnitKind` indexa `UnitCatalog.Land` (`UnitCatalog.cs:56-58`; el comentario de `BattleRules.cs:6` dice ┬½append only┬╗). `NavalUnitKind` indexa `Naval` (`:60-62`). `SoldierPool.cs:28-29` hace `team * count + (int)kind`. `AiPlanning.cs:129-131` exige `traits.Length == Land.Length`. `SourceRosterV030Tests.cs:42-47` fija `(int)EliteRifleman == 9`. No hay partida guardada que persista el enum; el peligro es ese ├¡ndice. El id de JSON no sustituye al enum mientras `UnitVariantViews.TryCreate` (`:63-79`) y `AttackPresentationTiming.Clip` (`:15-29`) sigan siendo `switch` por tipo. ┬½A├▒adir una unidad = una fila + un modelo┬╗ es falso.

**La orden ├║nica borra la matriz naval y no entra en el canal de red.** `BattleCommands.Tick` hace cast a `Soldier` (`BattleCommands.cs:114`) y valida el punto contra el NavMesh (`:85-88`). Los barcos se ordenan en el acto: `ship.Attack` (`RtsController.Input.cs:128`, `:158`). La prioridad del clic derecho est├í escrita (`:150-170`): barco enemigo antes que el puerto, puerto antes que atacar, transporte propio embarca, aliado solo si es `Soldier`, torre ΓåÆ attack-move al punto de captura porque `DefenseTower.CanBeAttacked` es false y `TakeDamage` no hace nada (`DefenseTower.cs:15`, `:152-155`). `UnitCommand` ya tiene `Append` y siete tipos (`UnitCommand.cs:3-13`); no tiene `Embark` ni `Unload`. Embarcar es una pareja soldado+barco con playa (`Ship.cs:151-159`, radio 10,24 y tope 10 en `:60-61`).

### P1

**`gridSlot` no es dato.** `ProductionHotkeys` ordena por coste y luego por cat├ílogo, tierra y luego cascos, rejilla QWER/ASDF/ZXCV (`ProductionHotkeys.cs:36-42`, `:66-77`). Hay tres listas: ciudad, infanter├¡a de puerto, barcos (`ProductionCatalog.cs:9-24`). Guardar el slot y borrar el algoritmo cambia el test ┬½el casco m├ís barato abre el bloque naval┬╗.

**La torre viva mezcla dos perfiles.** Vida y `BattleRules.TowerRange` (8,5) salen del b├║nker `o000` (`UnitCatalog.cs:52`, `BattleRules.cs:35-36`). Cadencia, alcance 13, da├▒o y `AttackPoint` salen de `CapturableTower` (`DefenseTower.cs:36-37`, `:114-122`). Armadura hardcodeada a `Divine` y 3 (`:23-24`). Dos filas JSON no reproducen ese mix si el actor pasa a leer una sola.

**La cola de 35 no es la cola que el plan describe.** Solo mover, attack-move y patrulla encolan, y solo si el modo no es Idle ni Hold (`Soldier.cs:209`). `Attack` y `Follow` vac├¡an la cola (`:237-245`). Una patrulla, al completarse, intercambia extremos y no drena lo que va detr├ís (`:256-258`). Si el objetivo de un attack-move muere, el barco reanuda el destino (`Ship.cs:232-233`, `:297-306`); la regla ┬½objetivo muerto ΓåÆ siguiente orden┬╗ cancelar├¡a ese attack-move. Hay dos colas: la global de `BattleCommands` (1024, se aplica al inicio del tick, `BattleWorld.cs:31`) y la de 35 del soldado. La IA naval llama `MoveTo` despu├⌐s del `SimTick` de barcos (`BattleWorld.cs:50-56` y `:85`). Meter los barcos en `Commands.Tick` en el paso 6, con la IA a├║n en el paso 7, deja las suites navales en rojo en medio.

**Adquisici├│n no es un par rango/correa com├║n.** El soldado usa `uacq`, correa distinta para neutral y un sesgo de presi├│n solo del `Footman` (`Soldier.cs:292-314`), y escalona el sentido con `EntityId % 4` (`:119`). El barco solo adquiere quien ya est├í en alcance de arma (`Ship.cs:255-265`) y, a igualdad, no desempata por `EntityId`. Copiar la adquisici├│n del ejemplo (10/12) hace que las fragatas persigan.

### P2

`ApproachPlan` como objeto y un diccionario por id en el tick no cumplen 900 unidades: la consulta espacial ya reutiliza listas (`Soldier.cs:78`). El resultado tiene que ser un struct indexado por ordinal resuelto al cargar. En WebGL, `Resources.Load<TextAsset>` s├¡ es s├¡ncrono; un `File.Read` del JSON no. El desfase TypeBoxΓåöC# no tiene generador: hace falta un test que refleje cada campo. Shift tambi├⌐n acelera la c├ímara (`RtsController.cs:627`). La ruta dibujada ┬½en el suelo┬╗ no es la polil├¡nea de `SeaNavigation`.

### P3

Los clips de `AttackPresentationTiming` y los fallbacks de retrato (`UnitVariantViews.cs:36-52`) son presentaci├│n. El documento generado deber├¡a marcar adaptaciones locales (m├⌐dico 220 HP, `UnitCatalog.cs:27`; rayo de ballesta; splash del mago) frente a `data/derived/reforged-source-combat.json`. Una comparaci├│n estricta con esa fuente falla a prop├│sito.

## 3. Lo que el plan debe a├▒adir

- Volcado hecho por un script de Editor (Node no ejecuta esas tablas) con arma completa, m├íscaras, tiempos de vuelo, radios de agente distintos del ancho visual, man├í (`SupportAbilities.cs:47-76`), `RoarEvaluationInterval` (`:60`), bloqueo de cura a mec├ínicos, y el mix de la torre.
- Funciones puras que reproduzcan las f├│rmulas actuales, con adaptador NavMesh frente a `SeaNavigation` (calado, separaci├│n 2,8 m, `Ship.cs:277-285`). La prohibici├│n ┬½cero ramas Ship/Soldier┬╗ deja fuera m├⌐dico/rugido en el mismo tick (`Soldier.cs:326-330`), bosque (`:386-395`), guarnici├│n, carga y formaci├│n.
- Matriz de `append` por tipo, patrulla terminal, attack-move que sobrevive al objetivo, y `Embark`/`Unload` como orden pareada. Fase de aplicaci├│n: el clic del barco hoy surte efecto en el mismo tick; la orden de soldado espera a `Commands.Tick`.
- Identidad: el id estable es el string del JSON. Los ordinales de tierra no se reordenan hasta que no quede ning├║n `(int)kind` como ├¡ndice. El pool acu├▒a `EntityId` nuevo en cada alquiler (`SoldierPool.cs:6`); una cola fuera del componente tiene que morir en `Initialize` (`Soldier.cs:88`).
- Tests de paridad con n├║meros de oro, no tests que leen el mismo JSON que acaban de escribir. `MeleeReachTests` y `NavalStandoffTests` se portan antes de borrar geometr├¡a. El escenario PlayMode ┬½soldado y barco┬╗ tiene que colocar cada uno en su dominio.
- Invariante de producci├│n: orden por coste, no `gridSlot` ├║nico. `canCapture` solo est├í en cascos de guerra (`NavalProfiles.cs:43` deriva la armadura del transporte).

## 4. Orden propuesto

1. **Fixture de oro** generado desde las tablas vivas, m├ís test de igualdad, sin borrar nada.
2. **Esquema completo** (TypeBox) y parser Newtonsoft en Runtime; tabla plana inyectada en Core. Test de ida y vuelta de enums string y objetos nulos.
3. **Lectores** pasan a esa tabla. En el mismo cambio, con el fixture en verde, se borran las tablas. Los hotkeys se quedan como algoritmo.
4. **Se eliminan los ├¡ndices por ordinal** (`UnitCatalog.Definition`, pool, IA) sin fusionar a├║n los enums. La fusi├│n, si sigue haciendo falta, va cuando no quede ning├║n cast.
5. **Reglas de alcance** que clonan cuerpo a cuerpo, distancia, m├¡nimo, casco orientado y punto de espera en el agua. `NavalArt` genera el collider desde la huella. Siguen `MeleeReachTests` y `NavalStandoffTests`.
6. **Cola por unidad** encima de la que ya tiene el soldado, m├ís ingreso de barcos en `BattleCommands` en la misma fase de tick. Matriz de Shift, ruta de mar, t├íctil. La IA solo cambia de llamada cuando esa API ya est├í verde.
7. **Documentaci├│n** marcada con lo que es fuente y lo que es adaptaci├│n local.
