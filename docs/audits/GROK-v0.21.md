# Revisión naval de Grok · v0.21

La respuesta final de la revisión está conservada en
`.tools/grok-v21/naval-review/output.json`. Grok 4.6 completó la revisión en 343,78 segundos. Las
propuestas se contrastaron con el código y las pruebas de esta ronda antes de
marcar su estado.

## Límites deliberados

La expedición mantiene una misión por IA, 2–4 tropas, presupuestos acotados de
sondeo (4/8/6), decisiones escalonadas cada segundo, una pasada de galeras cada
18 segundos, sin teletransporte y con la sucesión de `CityClaimZone` compartida.
El estado de captura gradual no se introduce como una regla nueva. Una espera
NavMesh de 1,24–1,8 segundos aparece como límite de observabilidad, pero esta
revisión no demuestra que sea causa de los fallos navales.

## Hallazgos y disposición

| Hallazgo | Disposición en v0.21 |
|---|---|
| El `Berth` importado o lejano puede quedar fuera del radio de carga y hacer que embarque o desembarque no terminen. | **Aceptado y corregido.** `Harbor.TryTransportLanding` valida la playa y busca el punto de océano dentro de `Ship.LoadRadius - .45`; ese punto se usa para reunir, cargar y descargar. `Harbor.Berth` permanece como ancla de guardia/producción. |
| `Gather` puede esperar para siempre a que el transporte coincida con un atraque ocupado por una fragata. | **Aceptado y corregido.** La expedición comprueba la distancia al punto de carga y vuelve a ordenar el transporte si está fuera del radio. La ruta considera el atraque ocupado y el barco guardia sigue anclado. |
| La reserva de tropas no cubre el enlace de guarnición y la captura puede robar soldados que esperan en el muelle. | **Rechazado como excepción local.** Se propuso excluir de `CityClaimZone` las tropas reservadas por la IA. La regla de sucesión es compartida entre jugador, IA, tierra y mar; introducir una exención sólo para reservas de expedición cambiaría ese contrato. La implementación conserva la regla común y, si quedan menos de dos tropas elegibles para embarcar, abandona la misión sin mantener el hueco naval hasta el timeout. |
| Un transporte con carga varada puede impedir para siempre la siguiente expedición de esa IA. | **Aceptado con recuperación acotada.** La planificación busca primero transportes cargados y los dirige a un puerto de recuperación válido. Se prueban como máximo seis puertos por decisión, primero propios y luego otros, con ruta marítima y playa válidas. La fase de recuperación tiene plazo dependiente de la ruta; sin puerto o si falla el desembarque, entra en enfriamiento y reintenta. La carga no se destruye ni se teletransporta para maquillar el resultado. La fase de ataque se libera inmediatamente si ya no queda tropa elegible. |

## Riesgos y límites que permanecen

La búsqueda de recuperación sigue limitada a seis candidatos por decisión y a
los puertos que ofrecen `TryTransportLanding`; una costa arbitraria no se
convierte en destino. Un barco puede continuar cargado durante el enfriamiento
si todos los puertos son inválidos. La revisión no prueba miles de barcos,
multijugador autoritativo, hardware móvil, rendimiento del jugador ni una
causa CPU para esperas NavMesh.

La tanda PlayMode consolidada de v0.21 aporta 72 casos distintos aprobados,
incluidos los escenarios de carga, guardia y expedición. La recuperación de
cargamento tras fallos se revisó en código, sin una prueba específica de
misión fallida en esta tanda. Las
builds y la inspección visual de Windows/Web siguen pendientes.
