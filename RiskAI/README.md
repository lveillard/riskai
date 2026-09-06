# RiskAI · proyecto Unity · v0.11

Abre esta carpeta desde Unity Hub con **6000.3.23f1**. La escena jugable es `Assets/RiskAI/Scenes/LasMarcas.unity`: abre la escena y pulsa Play.

El menú **RiskAI > Prepare playable scene** regenera la configuración de compilación. **RiskAI > Build Windows prototype** crea `../Builds/Windows-v0.11/RiskAI.exe`.

## Código

- `Assets/RiskAI/Scripts/Core/BattleRules.cs`: reglas y economía en C# sin dependencia del motor. Perfiles de unidades en Core/ReforgedProfiles.cs; aquí se ajustan rondas y condiciones de victoria.
- `MapLayout.cs`: alturas, países y 12 ciudades. `StrategicTerrain.cs`: mallas, costa, lagunas y bosques. `WorldLife.cs`: detalles ambientales. `RiskBootstrap.cs`: población inicial y construcción del NavMesh de Unity.
- `BattleSession.cs`: partida, ingresos y victoria. `SkirmishCommander.cs`: decisiones de la IA; `Core/StartingAllocation.cs`: reparto determinista por semilla; `Core/CombatRules.cs`: ataques y armaduras según las columnas aplicables de Reforged.
- `TerritoryMarkers.cs`: postes de países. `CliffDetails.cs`: afloramientos angulares; `Art/FirFoliage.shader`: ramas recortadas de abeto.
- `Soldier.cs`: órdenes y combate; el desplazamiento y evitación usan `NavMeshAgent`.
- `Settlement.cs`: captura y cola de reclutamiento.
- `RtsController.cs`: Input System, selección, grupos y órdenes. `RtsCameraRig.cs`: cámara, zoom y arrastre.
- `BattleHud.cs` y `VisualFactory.cs`: interfaz y gráficos provisionales.

El prototipo incluye captura temporizada por guarnición y protección cercana en `CityClaimZone.cs` (ciudades y todos los puertos, incluidos los continentales), perfiles de combate de Saran en `Core/ReforgedProfiles.cs` y barcos en `Core/NavalProfiles.cs`, sanadores (`MedicSupport.cs`), IA tranquila y estadísticas visibles. `TerrainHydrology.cs` comparte una curva con el tallado del terreno y el agua. `NavalWorld`, `Harbor`, `Ship` y `SeaNavigation` controlan flotas, puertos, carga y rutas marítimas. Los modos siguen siendo Conquista y Capitales, con reparto aleatorio y economía por países. [Detalles y límites de la adaptación](../docs/ITERATION-v0.11.md).

Las pruebas de reglas están en `Tests/Editor`; las pruebas que montan y ejecutan una batalla están en `Tests/PlayMode`. Se ejecutan desde Test Runner o con la CLI indicada en el README del repositorio.

Es una v0 local contra IA. El estado de simulación entra por `BattleWorld.Tick` a 20 Hz y las reglas puras viven en `RiskAI.Core`; los actores y el NavMesh siguen siendo adaptadores Unity, por lo que no se garantiza replay determinista. Antes de añadir multijugador habrá que definir autoridad y replicación de órdenes/estado. El mapa se genera al entrar en Play; la escena de edición contiene el componente de arranque.


La entrada de simulación es `BattleWorld.Tick` (20 Hz). `Core/` tiene `RiskAI.Core.asmdef` sin referencias a Unity; los proyectos Runtime, Editor y tests lo referencian de forma explícita. `BattleCommands` recibe intenciones de infantería por ID; `CombatWorld` conserva los impactos aunque se elimine su vista. `SoldierPool`, `SpatialTargetIndex` y el snapshot del HUD evitan recrear objetos y agregados en las rutas principales. Los actores y NavMesh siguen siendo adaptadores Unity: [alcance real y siguientes cortes](../docs/ITERATION-v0.11.md).
