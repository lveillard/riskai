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
