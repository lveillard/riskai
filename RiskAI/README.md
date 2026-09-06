# RiskAI · proyecto Unity · v0.18

Unity **6000.3.23f1**. `Prepare playable scene` crea dos escenas: `FrontEnd` (índice 0) y `LasMarcas` (índice 1). La primera sólo configura una partida; el bootstrap de batalla no crea superficie, NavMesh ni sesión hasta `FrontEndController.StartBattle()` carga Las Marcas. **RiskAI > Build Windows prototype** genera `../Builds/Windows-v0.18/RiskAI.exe`.

## Límites del código

| Capa | Responsabilidad |
| --- | --- |
| `Scripts/Core/`, `RiskAI.Core.asmdef` | Reglas, perfiles, economía, RNG por semilla, reloj, comandos y clasificación de sucesores. Sin UnityEngine. |
| `BattleWorld`, `CombatWorld`, `BattleCommands` | Tick de 20 Hz, impactos por ID independientes de la vista y validación/aplicación de órdenes. |
| `BattleSession`, `SkirmishCommander` | Ciclo de partida, registro, economía y política de IA por equipo. |
| `Soldier`, `Settlement`, `CityClaimZone`, `DefenseTower` | Adaptadores Unity de unidades, colas, relevos de guarnición y torres permanentes. |
| `NavalWorld`, `Harbor`, `Ship`, `SeaNavigation` | Puertos independientes o asociados a ciudades fuente, flotas, carga, desembarcos y rutas marítimas en caché. |
| `SpatialTargetIndex`, pools y efectos | Índice espacial y reutilización de soldados, proyectiles e impactos. |
| `MapLayout`, `StrategicTerrain`, `TerrainHydrology`, `ImportedMapData`, `ImportedTerrain` | Dos mapas originales y dos imports JSON con alturas, agua, ciudades, países y puertos. |
| `CountryCamp`, `TerritoryMarkers`, `StrategicMapView` | Refuerzos, territorios, postes y presentación estratégica sobre superficies existentes. |
| `RtsController`, `RtsCameraRig`, `RtsPicking` | Input, selección de tropas/edificios, comandos y cámara común con zoom anclado. |
| `FrontEndController` | Configuración independiente de mapa, jugadores, semilla, reparto y dificultad antes de crear la batalla. |
| `BattleHud`, `BattleMenu`, `WorldArt`, `VisualFactory` | HUD, ayuda durante la partida, arte de edificios y efectos. |

`MapLayout.Configure` sigue siendo global y asume una sola batalla activa. La vista estratégica reduce detalle de presentación, no frecuencia ni precisión de simulación. `TerritoryAtlas` se genera al cargar y no admite terraformación durante una partida.

Pruebas puras en `Tests/Editor`; pruebas de escena real en `Tests/PlayMode`. CLI y controles en el [README principal](../README.md). [Validación v0.18](../docs/VALIDATION-v0.18.md): 171 casos Unity, 7 pruebas Python, build Windows y mediciones del ejecutable con 16 jugadores.

Tick fijo y RNG sembrado no garantizan replay determinista: `NavMeshAgent` integra movimiento por frame. Una futura capa autoritativa tendría que incorporar compras y naval al protocolo, separar arranque de arte y replicar estado. Gestos táctiles y plataformas Web/Android aún no están implementados. [Decisiones v0.18](../docs/ITERATION-v0.18.md) · [Auditoría de combate](../docs/audits/v0.18-combat-source.md) · [Observabilidad](../docs/OBSERVABILITY.md) · [TODO](../TODO.md).
