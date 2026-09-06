# Iteración v0.11 — simulación y guarniciones

Esta entrega aplica el primer corte de arquitectura sobre v0.10 y cambia la ocupación de edificios según el feedback. El [review completo](ARCHITECTURE-REVIEW-v0.11.md) conserva la evidencia del código anterior. No es una reescritura en ECS ni una implementación de multijugador.

## Comportamiento jugable

Cada ciudad y puerto tiene un círculo de radio **1,1**. Una unidad que asume su defensa queda como guarnición: conserva tipo y vida, sigue combatiendo dentro de su alcance, pero no puede marcharse con mover, seguir, detener o embarcar. La guarnición termina al morir. No se crea un soldado adicional al capturar.

Si el defensor cae, cualquier soldado del propietario a menos de **2,8** del círculo sigue protegiendo el edificio. Las alturas se distinguen con un margen vertical de 1,25. El atacante debe ocupar el círculo **1,25 segundos seguidos sin oposición** para completar el cambio; salir, morir, sustituir al candidato o recuperar oposición reinicia ese progreso. Una unidad del propietario que entra en su círculo vacío puede asumir la defensa de inmediato.

La torre cambia de equipo con el edificio, conservando su vida restante. Las torres no cuentan como soldados que disputan el círculo. Obras y colas del anterior propietario se cancelan con reembolso. Son reglas locales solicitadas para este prototipo; no se presentan como una reproducción exacta de todas las variantes de Risk.

Los puertos continentales tienen ahora su propia ocupación y propietario; su ciudad asociada determina el nombre y reparto inicial. Conquistar esa ciudad ya no entrega automáticamente un puerto lejano. Solo los puestos insulares añaden los 8 de oro por ronda anteriores. La inicialización asigna las guarniciones continentales usando tropas existentes y mantiene 24 soldados totales por bando en Reparto Risk. El número de soldados móviles depende de las guarniciones ocupadas.

Las galeras adquieren automáticamente blancos costeros además de barcos, con alcance y línea de visión. Vida, daño, armadura, velocidad, alcance, coste, formación y capacidad naval proceden de `Core/NavalProfiles.cs`, compartido con el HUD.

## Cambios de arquitectura

| Punto del review | Estado de esta entrega |
| --- | --- |
| Reloj y pausa | `Core/SimClock` a 20 Hz y `BattleWorld.Tick`: combate, proyectiles, captura, colas, economía e IA avanzan en un orden definido. Pausa/victoria detienen el mundo y los agentes sin escribir `Time.timeScale`. El adaptador Unity entrega el tiempo de frame al reloj; la cámara y la presentación conservan su tiempo independiente. |
| Daño desde efectos | `CombatWorld` conserva los proyectiles por ID, tiempo de vuelo y datos de ataque. Resuelve impactos y área aunque el efecto se destruya, se desactive o no se cree. `ArrowFlight` solo lee el estado visual. |
| Búsquedas cuadráticas | Cuadrícula reutilizable de celdas de 8 y presión agregada por objetivo/jugador. Se actualiza también después de mover barcos, antes de impactos y captura. Adquisición escalonada cada 0,2 s. La formación ya no ordena una lista para cada posición. |
| Allocations frecuentes | Captura usa buffers retenidos; población usa un recorrido sin LINQ; economía calcula propietarios una vez por consulta; el HUD conserva agregados por tick y los invalida con acciones. Sigue existiendo IMGUI, texto dinámico y asignación en rutas menos frecuentes: no se afirma cero GC. |
| Pooling | Soldados por equipo/tipo, con reinicio de vida, órdenes, guarnición, navegación y animación; proyectiles visuales e impactos con pools acotados; marcador de orden reutilizado. Los barcos aún conservan su ciclo de creación/destrucción. |
| RNG e identidad | ID creciente por batalla y nuevo ID al reutilizar una unidad. Proyectiles y comandos guardan IDs, por lo que no se reasignan a un objeto reciclado. La percepción y prioridad de navegación dejan de depender de `Random.value`/`GetInstanceID`. Los dados conservan su RNG sembrado. |
| Core | Asamblea `RiskAI.Core`, `noEngineReferences: true`; Runtime y Editor con referencias explícitas. Incluye reloj, transición de captura, comandos y perfiles navales además de las reglas anteriores. |
| Input y autoridad | `UnitCommand` contiene datos sin `Transform`. `BattleCommands` valida propietario, identidad, guarnición, tipo de orden y números finitos, y aplica la infantería al comienzo del tick. Formación e input usan esa entrada. Compras y órdenes navales todavía tienen adaptadores directos que deberán incorporarse al mismo protocolo. |
| IA | Política terrestre extraída de `BattleSession` a `ICommander`/`SkirmishCommander`. Conserva dificultad y ritmos anteriores; sigue viendo el mundo completo. La política naval sigue siendo limitada. |
| Higiene | Fuera Timeline, Visual Scripting, Collab Proxy y plantillas sin uso. Animation, ParticleSystem y Audio quedan como dependencias explícitas porque el juego sí los usa. Identificador `com.lveillard.riskai`. `Grant`/`Refund` centralizan las recompensas y devoluciones externas a economía. |

La simulación sigue usando actores Unity y `NavMeshAgent`. La navegación se integra por frame en Unity, mientras las decisiones avanzan por tick. **Semilla + tick fijo no equivalen todavía a replay determinista, simulación sin Unity o cliente listo para red.** Este corte elimina dependencias concretas de presentación y abre interfaces que permiten continuar esa separación.

## Decisiones para tablet, servidor y navegador

La siguiente capa de input debe traducir ratón, teclado, arrastre táctil y pinza a las mismas intenciones. Input Actions separa acciones y dispositivos ([manual del paquete usado](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Actions.html)); los gestos no deben alterar salud, oro o propiedad directamente. El confinamiento de cursor seguirá limitado al modo de ratón compatible. Esta entrega no añade gestos táctiles ni teclas reconfigurables.

La dirección prevista para red es **servidor autoritativo con comandos validados y snapshots**; no lockstep basado en que NavMesh coincida en cada cliente. La identidad del jugador debe proceder de la conexión autenticada, no de un campo confiado al cliente. Unity ofrece un [target Dedicated Server](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-introduction.html), pero aún falta separar la creación de arte del arranque del servidor, implementar transporte, sesiones, visión por jugador, reconexión y replicación.

Unity 6.3 **sí incluye soporte Web móvil** para Chrome en Android y Safari en iOS ([compatibilidad oficial de esta versión](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-browsercompatibility.html)). La variante de navegador deberá validar memoria, texturas, descarga inicial, gestos, foco y un transporte apto para navegador. Ni una build Web ni una build Android están acreditadas en esta entrega.

## Trabajo que se pospone explícitamente

- UI Toolkit, Input Actions completo, Addressables y mapa/NavMesh prehorneado requieren cortes propios y comprobación visual; no se añaden paquetes solo por fecha o por tener 100 unidades.
- Carga externa de todos los perfiles: los valores efectivos siguen siendo C# puro; `data/rules.json` conserva su condición de boceto.
- Modelo de jugadores/facciones para 6–12 participantes, niebla de guerra y visión de IA, desembarcos de IA, diplomacia, guardado, attack-ground y habilidades activas.
- Mono se mantiene para iterar el ejecutable local; IL2CPP y los targets finales se decidirán y medirán por plataforma.
- No se borran compilaciones antiguas del usuario. Tampoco se afirman mejoras porcentuales de rendimiento sin un benchmark comparable.
