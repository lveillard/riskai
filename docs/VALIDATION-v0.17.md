# Validación v0.17

## Pruebas

**153 casos Unity distintos aprobados: 51 EditMode y 102 PlayMode**, más siete
pruebas Python del lector pasivo. Se toma el último resultado de cada nombre,
sin sumar las repeticiones como pruebas adicionales.

- `TestResults/v17-editmode-final.xml`: 51/51.
- `TestResults/v17-playmode-final.xml`: 99/100, 305,3 s. Pasaron los casos de
  gameplay y movimiento; el nuevo testigo de costa propuesto para la regresión
  no reproducía el obstáculo con el muestreador de Unity.
- `TestResults/v17-grok-navigation-verified.xml`: 17/17, 44,2 s, después de
  sustituir ese testigo por un terreno numérico aislado y ajustar el caso de
  ruta recién emitida frente a ruta abandonada. También comprueba los errores
  de embarque, margen de desembarco y recuperación tras atasco.
- `TestResults/v17-land-budget-verified.xml`: 27/27, 99,1 s, después de aplicar
  el presupuesto de NavMesh en `Start`, tras cargar la configuración de escena.
- `python -m unittest discover -s scripts -p test_observe_runtime.py`: 7/7.

Se verifican colas y cancelación, reunión y entrada, puertos importados,
pausa de entrenamiento, bosque y reutilización de soldados, barras por
oclusión conservadora, encuadre de cámara, independencia entre impacto visual
y daño, rutas marítimas y telemetría que no consume la cola de órdenes.

**Windows v0.17.0: 205.483.471 bytes**, Unity 6000.3.23f1, URP, Mono de
iteración. Build correcta en `RiskAI/Logs/v17-build-release.log`.

## Rendimiento: configuración y lectura

Equipo: Intel Core Ultra 9 285H, NVIDIA RTX 5080 Laptop, 1600×900.
Europe, 16 jugadores, semilla 160212. Cada ejecución calienta 1.200 segundos
de simulación a 8x, estabiliza dos segundos a 1x y mide 90 segundos reales.
La IA actúa durante la prueba; se añaden hasta seis ballesteros por bando al
terminar el calentamiento. El controlador físico está desactivado y el picking
usa un puntero sintético. [Procedimiento y límites](OBSERVABILITY.md).

El primer candidato, anterior a la optimización naval, registró en la medición
11,00 ms de media, 149,32 ms de máximo, cinco frames >50 ms y tres >100 ms.
Durante el calentamiento hubo un frame de 1.020,63 ms: 1.006,88 ms estaban en
barcos. Otra invocación de IA llegó a 920,70 ms. Esto sí identifica trabajo
síncrono costoso, pero el calentamiento a 8x no mide respuesta habitual.

Las dos ventanas estables finales de ese candidato registraron primer
movimiento elegible a 101,19/99,33 ms de media y aplicación a 30,77/28,86 ms.
No reprodujeron una demora constante de varios segundos a velocidad normal.
El desplazamiento final de dos unidades de esa primera sonda quedó contaminado
por reutilización de objetos: por eso no se usa su cifra «6/6» como validación
individual. La sonda final conserva la identidad original y deja de seguir un
objeto cuando representa a otro soldado.

Log previo: `RiskAI/Logs/v17-probe-europe-advanced.log`.
Log posterior: `RiskAI/Logs/v17-probe-europe-nav-final.log`.

Tras optimizar mar, la misma configuración produjo una batalla distinta:
681 unidades al iniciar la medición y 360 al acabar, frente a 371/252 antes.
Registró 13,79 ms de media, **41,65 ms de máximo y cero frames >50 ms**;
889 órdenes aplicadas, cero rechazadas y cero pendientes al acabar.
Cinco de las seis identidades seguidas recorrieron al menos un metro; sólo
quedó una móvil viva al final y se había movido. No se cuentan sustitutos del
pool. La media de primer movimiento todavía varió entre 554,83 y 290,80 ms
en las dos ventanas completamente a 1x. Esto motivó el ajuste terrestre
posterior: eliminar peticiones autónomas repetidas y aumentar de forma acotada
el trabajo asíncrono de NavMesh por frame.

No se presenta el descenso del máximo como una comparación de replay idéntico:
la semilla es la misma, pero el cambio de cadencia y la navegación por frame
alteran el desarrollo de la partida.

La ejecución intermedia `v17-probe-europe-final.log` confirmó que una asignación
temprana del presupuesto nativo quedaba restablecida a 100 por la carga de escena.
Con las peticiones autónomas agrupadas, esa ejecución registró seis identidades
movidas, 1.003 órdenes aplicadas, cero rechazos/pendientes y máximo de 60,34 ms.
La release aplica el valor después de cargar y lo verifica mediante
`navIterationsPerFrame=500`; sus datos están en `v17-probe-europe-release.log`.

## Resultado de la release

La sonda final termina con `valid=True` y `success=True`. Son 1.200 segundos
simulados de calentamiento y 90 segundos reales de medición a 1x:

| Medida | Primer candidato | Release |
| --- | ---: | ---: |
| Unidades al iniciar / acabar la medición | 371 / 252 | 419 / 310 |
| Media de fotograma | 11,00 ms | 12,04 ms |
| Máximo de fotograma | 149,32 ms | **40,03 ms** |
| Frames >50 / >100 / >250 ms | 5 / 3 / 0 | **0 / 0 / 0** |
| Órdenes aplicadas | 804 | 903 |
| Rechazadas / pendientes al acabar | 0 / 0 | 0 / 0 |

La release mide 7.476 fotogramas. Las cinco identidades que pudo crear para
el jugador se desplazaron al menos un metro; cuatro seguían móviles y vivas
al acabar, ninguna inmóvil. Se enviaron 194 órdenes de movimiento a esa cohorte,
sin destinos ausentes ni timeouts de la fase hold. El total de órdenes incluye
hold e IA, no sólo los movimientos sintéticos del jugador.

Las dos ventanas completamente a 1x dan **117,43 y 119,23 ms de media** hasta
el primer movimiento elegible, con máximos de 516,04 y 653,92 ms. La aplicación
de la orden promedia 30,37 y 33,75 ms; su máximo queda en 56,39/55,35 ms.
Ambas ventanas muestrean cero rutas pendientes. Esto mejora la cola bajo carga,
pero no promete respuesta uniforme ni mide el tiempo desde el clic físico.
La preparación del grafo naval costó 454,38 ms durante la carga. Las búsquedas
marítimas de esas dos ventanas llegaron a 20,87/22,82 ms.

La media gráfica no baja en esta comparación: la batalla final tiene más
unidades y se desarrolla de otra manera. La mejora observada es el máximo y
la eliminación de frames >50 ms **durante esta medición**, no una garantía
para cualquier partida o máquina.

## Inspección visual

Capturas del ejecutable a 1600×900 en `Captures/v17-europe/`. Se inspeccionaron
ciudad, cámara, orilla/puerto y cola naval de cinco con iconos. El log
`RiskAI/Logs/v17-capture-europe.log` confirma 212 defensores, cero móviles,
cero barcos, cero heridos y cero disparos iniciales, antes de añadir unidades
para las capturas de demostración. El puerto muestra cinco encargos navales
y uno terrestre en la captura de entrenamiento. El oro adicional y los barcos
de esa escena son fixtures posteriores a la apertura, no el reparto normal.

[Ciudad](images/v0.17-city.png) · [Puerto y cola](images/v0.17-training.png).

## Límites conservados

La banda de orilla mejora el material, no elimina los escalones geométricos.
La barra tras árboles usa intersección de cotas de copa, no visibilidad exacta
por píxel. La ralentización del bosque es local y el planificador no la usa
todavía como coste de ruta. El indicador de entrenamiento es una animación
propia pequeña; faltan animaciones específicas de mortero y Marines.

La navegación y parte de los actores siguen en Unity; no hay replay
determinista ni validación en servidor, tablet, navegador o Android. La
telemetría desde Submit no equivale a medir el clic físico. El heap y los
contadores de GC no son una medición de bytes asignados por frame.
