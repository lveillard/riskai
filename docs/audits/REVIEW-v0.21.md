# Revisión independiente v0.21 · ronda 1

## Alcance y base

Esta ronda compara `553113a` contra la base `c37a36f`. Se usaron tres
revisiones aisladas y sólo sus informes finales:

| Revisor | Informe | Resultado usado |
|---|---|---|
| Codex API · Astra (low) | `.tools/v21-review-round1/astra-a-report.md` | Dos P2 navales: recuperación que no alcanza el fallback y selección de puerto sin comprobar conectividad marítima. |
| Claude-VEI · Opus 5 A | `.tools/v21-review-round1/opus-report.json` | Sólo el campo `result`: guardia naval sin señal `Contested`, ola terrestre mínima, reservas durante espera, cursor sensible a cambios de roster, tooltip táctil y varios P3. |
| Claude-VEI · Opus 5 B | `.tools/v21-review-round1/opus-b-report.json` | Sólo el campo `result`: barrera de relevo de galera, coste de sondeos de atraque, recuperación sin salida, cobertura de relevo, regla de costa pintada y no-op/dead code. |

La ejecución adicional `extraAstraB` falló y no se cuenta. Los informes fueron
estáticos: esta página no declara pruebas, builds, rendimiento ni resultado de
hardware.

## Disposición de Root

Se han aplicado las correcciones siguientes; la ejecución dirigida está aprobada (20 casos, con una repetición de fixture):

- La recuperación debe conservar el pase y el cursor en el primer candidato no
  sondeado, para alcanzar un puerto propio o neutral/enemigo válido cuando los
  primeros seis puertos propios no tienen ruta.
- La selección de fuente debe comprobar la conectividad marítima del transporte
  y continuar hacia otra fuente si la primera no es alcanzable.
- `TryTransportLanding` usa caché de resultados válidos por puerto con preparación al cargar para no
  repetir sondeos completos de NavMesh por cada candidato y cada puerto.
- El puerto con guardia naval debe actualizar `Contested` antes del retorno
  temprano de la lógica de sucesión.
- El cursor de objetivos terrestres debe sobrevivir al cambio de roster, sin
  volver a sondear siempre los mismos primeros objetivos.
- Mientras se espera un transporte comprado y pagado, las tropas terrestres no
  deben desaparecer del ejército activo; al quedar transporte disponible se
  reevalúa la selección.
- El horizonte que usa `Meadow` debe recibir UV1 con los pesos de costa, y el
  test de orilla debe seleccionar por terreno fuente, NavMesh y océano, excluyendo
  docks legítimos, antes de comprobar que la política rechaza la orilla.
- Se limpia el helper naval sin referencias y los no-op identificados por la
  revisión.

Además, el plazo de reunión incluye la ruta que el barco ya tiene hasta el
puerto; antes sólo estimaba la caminata de las tropas. La recuperación reutiliza
la distancia de la ruta aceptada para calcular su plazo, evitando otro A*
idéntico dentro de la misma decisión.

La regresión de relevo de galera pasó con una ruta real desde más de 5 m.
La separación naval permite entrar en el radio común de 2 m y relevar al
guardián; no se reprodujo la barrera dura de 2,8 m planteada por Opus B.
No se amplió el radio ni se simuló el éxito teletransportando al relevo.

## Rechazos y contratos conservados

Se rechaza sustituir la prioridad actual por una prioridad terrestre antigua:
el usuario pidió que un guardián vivo mantenga el puerto. Se conserva la regla
compartida de sucesión y se añade la señal `Contested` donde falta.

Las anclas de movimiento terrestre y marítimo son adaptadores intencionales;
no se unifican sólo para igualar coordenadas visuales. Un transporte varado no
se teletransporta para fabricar una recuperación: debe encontrar geometría y
ruta válidas o permanecer en el flujo acotado de reintento.

El texto permanente de ayuda para compras se rechaza porque la ayuda solicitada
es por hover. La ayuda táctil mediante pulsación larga queda registrada para la
fase 2.

## Decisiones de producto aún abiertas

La ola mínima sigue sin decisión. La revisión observa que una fuerza de 3–4
unidades puede quedar sin orden si una unidad está aislada. Para mañana quedan
las opciones de conservar el mínimo y mostrar el bloqueo, o permitir una ola
parcial/reducir el mínimo; este documento no elige.

También queda abierta la alineación entre arena pintada y arena validada. La
revisión observa que la máscara visual de costa y los pesos de `ShoreAccess`
pueden producir conjuntos distintos, especialmente cerca de puertos. Las
opciones son conducir el shader con el peso horneado o limitar ese peso con la
misma máscara de costa antes de hornear; no se cambia la política en esta ronda.

La geometría de costa, el comportamiento en ARM y la carga sostenida de unas
800 unidades siguen pendientes. Los 86 casos Unity consolidados se detallan en [Validación](../VALIDATION-v0.21.md);
las exportaciones y medidas finales siguen pendientes.

## Ronda 2 · revisión de implementación

Esta ronda compara `060ed60` contra la misma base `c37a36f`. Se leyeron tres
revisiones actuales, aisladas de la documentación y entre sí:

| Revisor | Informe | Resultado usado |
|---|---|---|
| Codex API · Astra (low) | `.tools/v21-review-round2/normal-astra-report.md` | Dos P2: conservar pares puerto/objetivo alternativos y avanzar a otra fuente cuando la primera no puede producir una misión. |
| Claude-VEI · Opus 5 A | `.tools/v21-review-round2/opus-a-report.json` | Sólo el campo `result`: errores de embarque del jugador sin progreso, tropas no embarcadas incluidas en el ataque, cursor sensible a cambios de propietario y toast de fallos navales visible para el jugador. |
| Claude-VEI · Opus 5 B | `.tools/v21-review-round2/opus-b-report.json` | Sólo el campo `result`: desacuerdo arena/pintura, coste de sondeos por frame, retención de guardia, barrera de relevo, alcance de galera, cursor de tropas, recuperación y test de guardia terrestre. |

Un intento `codex-vei` terminó con error 401 y otro intento anterior quedó sin
cuota de API; no se cuentan. No se usó Grok en esta ronda. La revisión vuelve a
ser estática: no declara tests, builds, rendimiento ni comportamiento ARM para
este parche.

### Disposición de Root

Se acepta y se está implementando:

- Mantener pares alternativos de puerto y objetivo, probar conectividad marítima
  y equidad de fuente/recuperación dentro del presupuesto, y avanzar tras un
  candidato que no pueda producir una misión.
- Capturar el cargamento real al pasar a navegación y liberar inmediatamente a
  los rezagados que no embarcaron, para que no reciban una orden de ataque
  imposible.
- Hacer que el cursor de objetivos terrestres ignore cambios de propietario no
  relacionados con la continuidad del cursor.
- Dar al embarque del jugador progreso finito, reintento y salida con error, con
  cadencia de 0,2 s y sin repetir sondeos de orilla en cada frame.
- Filtrar los avisos de fallo de barcos de IA al equipo local correspondiente;
  no deben aparecer como toasts del jugador humano.

Queda pendiente añadir y ejecutar el caso de una guarnición terrestre viva
frente a un barco enemigo que intenta tomar el puerto. Es una comprobación de
invariante de propiedad, no una autorización para desplazar al guardián vivo.

### Rechazos y límites conservados

Se rechaza ampliar el radio de `FindNearbyEnemy`: la consulta de reclamación ya
incluye alcance y la distancia se comprueba contra `AttackRange`. La retención
de 7,5 m no se cambia arbitrariamente; el guardián normal es fijado por
`MaintainHarborGuard` en el atraque en cada tick, y los límites actuales deben
quedar cubiertos por prueba y documentación.

Se conservan las anclas compartidas como adaptadores intencionales y la ayuda
por hover solicitada. Las decisiones de pintura de playa y ola mínima siguen
abiertas para el usuario; esta ronda no las resuelve.

El coste de hornear costa durante el arranque se medirá mediante la fase
`terrain` de `StartupMetrics`. No se presentan cifras nuevas: Root añadirá los
números después de ejecutar la medición. Los tests, builds, rendimiento
sostenido de unas 800 unidades, ARM y geometría visual de costa permanecen
pendientes.
