# RiskAI · proyecto Unity · v0.12

Unity **6000.3.23f1**. Escena: `Assets/RiskAI/Scenes/LasMarcas.unity`. **RiskAI > Build Windows prototype** genera `../Builds/Windows-v0.12/RiskAI.exe`; `Prepare playable scene` conserva configuración y materiales.

## Límites del código

| Capa | Responsabilidad |
| --- | --- |
| `Scripts/Core/`, `RiskAI.Core.asmdef` | Reglas, perfiles, economía, RNG por semilla, reloj, comandos, clasificación de sucesores y reconocimiento de clic/arrastre. Sin UnityEngine. |
| `BattleWorld`, `CombatWorld`, `BattleCommands` | Tick de 20 Hz, impactos por ID independientes de la vista y validación/aplicación de órdenes de infantería. |
| `BattleSession`, `SkirmishCommander` | Ciclo de partida, registro, economía y política de IA separada. Defensa por amenazas y ofensivas con dificultad configurable. |
| `Soldier`, `Settlement`, `CityClaimZone`, `DefenseTower` | Adaptadores Unity de unidades, colas y guarniciones. Torres permanentes vinculadas al propietario del puesto. |
| `NavalWorld`, `Harbor`, `Ship`, `SeaNavigation` | Puertos independientes, flotas, carga, desembarcos y navegación marítima. Perfiles navales comunes con el HUD. |
| `SpatialTargetIndex`, `SoldierPool`, efectos | Cuadrícula espacial y reutilización de soldados/proyectiles/impactos. |
| `MapLayout`, `StrategicTerrain`, `TerrainHydrology` | Dos topologías, relieve, islas y río que comparte geometría con el tallado y los materiales. Cruces construidos antes del bake. |
| `CountryCamp`, `TerritoryMarkers` | Hogueras de refuerzo, overlay de grupo bajo demanda y postes de propiedad. |
| `RtsController`, `RtsCameraRig` | Input, selección y comandos; cámara con zoom anclado y arrastre independiente del dispositivo. |
| `BattleHud`, `BattleMenu`, `WorldArt`, `VisualFactory` | Presentación. El HUD mantiene snapshot; menú en parcial separado. IMGUI sigue pendiente de migración. |

El bootstrap configura el mapa antes de generar sus superficies y hornear NavMesh. Cambiar mapa requiere iniciar otra partida. Los mapas aún se generan en runtime. `MapLayout.Configure` es global y asume una sola batalla activa.

Pruebas puras en `Tests/Editor`; pruebas de escena real en `Tests/PlayMode`. CLI y controles en el [README principal](../README.md).

Tick fijo y RNG sembrado no garantizan replay determinista: `NavMeshAgent` sigue integrando movimiento por frame. La siguiente etapa de servidor autoritativo debe incorporar compras/naval al protocolo, separar el arranque de arte y replicar estado. Gestos táctiles y plataformas Web/Android aún no implementados. [Decisiones v0.12](../docs/ITERATION-v0.12.md) · [TODO](../TODO.md).
