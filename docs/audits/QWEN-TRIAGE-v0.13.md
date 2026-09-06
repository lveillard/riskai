# Revisión de los informes Qwen — v0.13

Qwen3.8 se ejecutó mediante Grok CLI (`qwen38`, metadatos `Qwen/Qwen3.8-27B-FP8`). Los paquetes y respuestas brutas permanecen en `.tools/`, ignorados. Esta tabla recoge la verificación final del integrador sobre las primeras tandas de revisión; una etiqueta «confirmed» de un modelo no constituye evidencia.

| Propuesta | Resolución comprobada |
| --- | --- |
| Un soldado muerto conserva `IsAlive` y sigue moviéndose | Rechazada. `CombatTarget.IsAlive` depende de salud/jerarquía; `Soldier.TakeDamage` pone salud a cero, desregistra y desactiva agente/componente. `Initialize` limpia las órdenes al reutilizar. |
| Las órdenes de muertos/guarniciones atraviesan la cola | Rechazada. `BattleCommands.Valid` se aplica tanto al enviar como al consumir la orden. |
| Stop/Hold quedan fuera del enum permitido | Rechazada. `Core/UnitCommand.cs` sitúa ambas antes de `Follow`, dentro del rango validado. |
| La animación de ataque sigue durante pausa | Rechazada. `Soldier.LateUpdate` comprueba `session.Paused` antes de animar. |
| Un puerto neutral regala su defensor al entrar un enemigo | Rechazada. `CityClaimZone.Step` conserva el defensor vivo y trata al neutral como team 2. Es necesario derrotarlo. |
| La primera compra de Archer falla con 4 de oro | Rechazada para v0.13: cuesta 1. El fallo real anterior era intentar comprar Guard por 5; ya se corrigió la secuencia de IA. |
| Una compra fallida avanza `recruitsOrdered` | Rechazada, también frente al primer borrador de la revisión Luna. `Settlement.Recruit` devuelve **null si tiene éxito**; una cadena de error provoca `break` antes del incremento. |
| La IA no tiene móviles durante los primeros 14/30 s | Comportamiento intencionado de Estándar/Relajada. Ya hay guarniciones y torres. No es una captura sin oposición ni justifica regalar una tropa a la IA. |
| El transporte puede desembarcar en puerto enemigo | Comportamiento intencionado: permite una invasión. Se comprueban distancia, navegación y tierra; la guarnición enemiga permanece. No se añadió la restricción de desembarcar solo en puertos aliados. |
| La fragata debe atacar el edificio invulnerable | Rechazada. Los objetivos son barcos o soldados costeros; `Ship.FindNearbyEnemy` incluye estos últimos. La fragata no ocupa círculos. |
| La IA naval no funciona sin barcos gratuitos | Brecha real detectada durante integración: la lógica anterior solo ordenaba barcos existentes. v0.13 compra fragatas por la cola del puerto y reserva oro para la primera cuando tiene móviles y ninguna amenaza inmediata. Desembarcos de IA pendientes. |
| El río clásico puede ser atravesable | Pendiente de autoría. `TerrainHydrology.IsChannel` bloquea el río del mapa expandido; el clásico mantiene su comportamiento anterior. La política debe resolverse junto con los cruces, no mediante un cambio aislado que cierre rutas. |

La primera revisión asistida también confundió el ingreso inicial (el constructor ya asigna 4 de oro) y el reparto por división entera (ya implementado). Se corrigieron en `INITIAL-ECONOMY-v0.13.md`. Para Qwen conviene pedir extracciones pequeñas de valores/condiciones antes de una revisión semántica. La revisión de arquitectura y las decisiones de diseño siguen requiriendo contraste directo.

## Tanda sobre el JASS original

Después del benchmark se enviaron cuatro paquetes nuevos: guarniciones, flujo de reclutas, ingresos/recompensas y notas de autoría de terreno. Los tres primeros devolvieron informe (32,1 / 67,1 / 33,3 s); el de ideas para editor agotó 120 s. No se le atribuyen ideas que no entregó.

- **Guarniciones:** verificados `StartingDefenderNormal` y `StartingDefenderShipyard = h00B` (`war3map.j:5456-5457`), creación de una unidad en la ubicación del círculo (`7068-7075`) y transferencia al jugador asignado (`8844`).
- **Refuerzos:** verificados la fórmula base `(RegionCityAmounts + 1) / 2`, variantes multiplicadas por tres en otros modos, límite `RegionCityAmounts * ModesSpawnLimit` y creación de un `h00B` por paso (`19330-19527`). El fragmento no establece la frecuencia del temporizador: no se inventó un valor de goteo.
- **Ingresos:** la recompensa dividida por `ModesBounty` está en `18693`. Qwen llamó «predeterminado» al 5 de `19164`, pero esa rama exige **modo 4**, según `19064-19069`; el preset Conquista normal establece 4 en `5583`. Se rechazó esa generalización y no se cambió el oro inicial del juego.

Lección de empaquetado: incluir las funciones que definen las condiciones de una rama, no solo su cuerpo. Incluso una extracción breve con números de línea puede confundir una excepción con el valor predeterminado.
