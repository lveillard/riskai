# Latencia observada durante una partida de v0.16

Revisión del 6 de septiembre de 2026, sobre la partida abierta de Europe con
16 jugadores y semilla 160212. Lectura pasiva de `RiskAI/Logs/v16-opened.log`
y contadores del proceso; no se cerró, reinició, pausó ni inyectó código en el juego.

## Lo medido

Ventanas de 30 segundos con avance casi completo de simulación:

| Tiempo simulado al cierre | Unidades | Media por frame | Máximo por frame | Órdenes pendientes al cierre |
| --- | ---: | ---: | ---: | ---: |
| 1102,0 s | 408 | 14,28 ms | 664,01 ms | 0 |
| 1224,9 s | 455 | 14,77 ms | 580,55 ms | 0 |
| 1284,3 s | 463 | 16,54 ms | 471,77 ms | 0 |
| 1371,4 s | 422 | 14,46 ms | 455,65 ms | 0 |
| 1400,9 s | 430 | 14,59 ms | 634,87 ms | 0 |

La media oculta los tirones. Un valor pendiente de cero al final de una ventana
no descarta una espera anterior: el contador no mide latencia ni la cola interna
de NavMesh. Tampoco permite atribuir un frame concreto a un clic del jugador.

Hay además ventanas con máximos de 17–30 segundos donde la simulación avanzó
mucho menos de 30 segundos. La build tiene `runInBackground=false`; esos datos
son compatibles con pérdida de foco. v0.16 no registra foco ni pausas, así que
no se atribuyen esos máximos al coste de la simulación.

Los contadores Gen0 aumentan incluso en el menú inicial sin avance de simulación
(aproximadamente 83–85 por ventana). Esto muestra actividad del recolector fuera
del combate, pero no identifica cuánto tarda ni cuántos bytes se asignan.
`managedHeapDeltaB` mide variación del heap vivo, no asignaciones acumuladas.

## Camino de una orden y límites del diagnóstico

- El botón derecho emite una orden al soltarlo. Un movimiento de más de 12 píxeles
  lo convierte en arrastre de cámara; no llega a la cola de órdenes.
- `BattleCommands` drena la cola al principio del siguiente tick de 50 ms.
- `SetDestination` solicita una ruta asíncrona. `Soldier.Travel` espera mientras
  `NavMeshAgent.pathPending` sea verdadero. No hay duración de esa espera en v0.16.
- Los rechazos registrados incluyen órdenes sobre defensores retenidos y un
  destino no transitable. Son distintos de una orden móvil aceptada que tarda.
- La captura no reconstruye las fronteras enteras: actualiza materiales y
  visibilidad de los postes cuyos propietarios han cambiado.

La observación no demuestra todavía una causa única. La prueba sintética anterior
de 70 segundos no cubría esta partida de más de veinte minutos ni el clic físico.

## Trabajo evitable encontrado en código

- `RtsPicking.Target` usa dos `Mathf.Max` de tres argumentos por objetivo/frame:
  ese overload `params` crea arrays temporales. Se puede mantener el mismo cálculo
  con operaciones binarias sin esas asignaciones.
- El HUD construye estilos repetidos en `OnGUI`; se pueden reutilizar por estilo
  y por color de propietario. Sus marcos también construyen arrays temporales.
- La victoria recorre ciudades y presencia repetidamente por cada jugador en
  cada tick; un recuento reutilizable por tick conserva las reglas y su cadencia.

Estas correcciones reducen trabajo conocido. Su impacto sobre los tirones requiere
medición en una build posterior, sin presentar una revisión estática como un perfil
de CPU ni como una reparación ya aplicada a la partida abierta.

El lector externo y sus límites están en [OBSERVABILITY.md](../OBSERVABILITY.md).
