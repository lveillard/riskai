# Validación v0.21 · controles, puertos y expediciones

## Resultado y alcance

La ronda corrige cámara en pausa, selección propia y rueda dentro del HUD;
unifica la guarnición terrestre/naval del puerto; incorpora expediciones
marítimas de la IA por las órdenes normales del jugador. Los cuatro escenarios
consumen las mismas reglas. La clasificación de orilla usa los datos del mapa,
sin introducir excepciones de IA ni cambios de escala.

## Cambios comprobados en Unity

- El arrastre derecho se cancela una vez al cambiar pausa, en lugar de cada
  fotograma pausado. Ratón, touch y lápiz permiten inspección/cámara en pausa;
  las órdenes de simulación siguen bloqueadas. Modales y pérdida de foco
  cancelan gestos pendientes. Las coordenadas del ratón ya no dependen del
  último `Pointer` actualizado por otro dispositivo.
- La caja de edificios incluye sólo puestos propios; Shift sobre terreno
  vacío conserva selección. El clic directo sigue permitiendo inspección.
- `CityClaimZone` contiene un único guardián de tierra o mar. Propiedad,
  torre y producción leen ese estado. Relevo voluntario común a 2,0 unidades
  frente al círculo pintado de 1,55; prioridad aliada, distancia e identidad.
- La fragata puede ocupar un puerto vacante; un transporte no. Un guardián
  vivo no es sustituido por cercanía. Las anclas físicas terrestre y marítima
  continúan siendo distintas: no se presenta como reproducción exacta del
  círculo único de WC3.
- Embarque y descarga comparten `ShoreAccess`: playa fuente `Vcbp` en los
  importados, o una pasarela de puerto transitable. El atraque para carga se
  busca a distancia de embarque de la pasarela; no depende de que el ancla
  militar del puerto esté lo bastante cerca.
- La IA filtra caminos terrestres completos con presupuesto acotado; mantiene
  su cursor de búsqueda ante objetivos inaccesibles. Su comandante naval
  compra transportes, reserva 2–4 tropas móviles, reúne, carga, navega,
  descarga y da ataque en formación. Respeta la gracia naval y el ahorro de
  la primera fragata. Recupera transportes con carga tras una misión fallida.
- ScrollView usa rueda nativa; el zoom de cámara no cambia sobre el panel.
  Fragata/transporte tienen iconos en botones. La explicación de las colas
  pasa a ayuda al pasar ratón/lápiz, con overlay de runtime, sin ocupar
  permanentemente el HUD. El indicador de rendimiento espera una muestra
  en vez de mostrar ceros iniciales.

## Pruebas automatizadas

Unity **6000.3.23f1**, Windows: **83 casos PlayMode distintos aprobados**
según el resultado más reciente de cada caso, más **3/3 EditMode** de
argumentos de lanzamiento. No se suman repeticiones como casos nuevos.

| Informe local | Resultado de esa ejecución |
| --- | --- |
| `RiskAI/Logs/v21-input-final.xml` | 30/30 |
| `RiskAI/Logs/v21-camera-final.xml` | 18/18 |
| `RiskAI/Logs/v21-integration-final.xml` | 29/36; fallos revisados debajo |
| `RiskAI/Logs/v21-integration-r2.xml` | 17/21; repeticiones dirigidas |
| `RiskAI/Logs/v21-integration-r3.xml` | 15/16; último mensaje de error corregido |
| `RiskAI/Logs/v21-ui-r2.xml` | 10/10; todos los fallos anteriores cubiertos |
| `RiskAI/Logs/v21-launch-arguments.xml` | 3/3 EditMode |
| `RiskAI/Logs/v21-release-fixes.xml` | 16/16; catálogo importado, colas y expedición |
| `RiskAI/Logs/v21-review-fixes.xml` | 19/20; una expectativa de fixture revisada |
| `RiskAI/Logs/v21-review-fixes-r2.xml` | 1/1; expedición con comandante naval aislado |

Cobertura: cámara/pausa, ratón, lápiz y touch, selección, guarnición de
puertos en cuatro mapas, playa/descarga en Europe y New World, navegación
terrestre, expedición completa, menú, HUD, scroll y tooltip. La expedición
usa cola pagada y fotogramas reales, con una fragata ocupando el atraque de
origen. Verifica identidad de tropas embarcadas/desembarcadas y camino
completo hasta el objetivo enemigo; no concede propietario para simular éxito.

Las primeras invocaciones detectaron errores de compilación en fixtures,
referencias tras cambiar la API de costa y una ambigüedad de `PointerType`.
Se corrigieron antes de las ejecuciones aprobadas. Las repeticiones también
corrigieron selección de islas del fixture, posiciones de relevo todavía
ocupadas, ahorro de la primera fragata y mensaje de fallo naval. Los eventos
sintéticos de rueda necesitaban el modo de foco del editor apropiado; el
cambio de foco está limitado al fixture y se restaura al terminar.

Ratón/touch/lápiz sintéticos recorren Input System y UI Toolkit. Esto no
certifica periféricos físicos ni tablet Android/ARM.

La ronda posterior a revisión cubre relevo de galera con navegación real,
presión enemiga junto a un guardián naval vivo, continuación del objetivo
terrestre tras reemplazar una tropa, siete puertos desconectados antes de un
fallback y selección de fuente según el componente marítimo del transporte.
Los dos últimos fixtures usan geometría marítima sintética y adaptadores de
puerto con puntos de carga precalculados; no prueban una travesía completa
de recuperación. La expedición completa se prueba por separado.

El primer pase completó el viaje con tropas reclutadas por el comandante
terrestre durante la espera, pero el fixture exigía sus dos soldados iniciales.
La repetición ejecuta sólo el comandante naval, con reloj y movimiento reales,
y verifica esas identidades. Se conservó la comprobación de que fabricar
el transporte no reserva a los soldados. No se cuentan como dos casos el
test de cursor anterior y su extensión para cambios de roster. Total: 86 casos.

## Exportaciones, inspección y rendimiento

La primera exportación Windows terminó correctamente (206.875.846 bytes).
Las capturas Europe de `Captures/v21-europe` detectaron que el HUD trataba
el alias terrestre del puerto como ciudad: mostraba el catálogo regular
antes del naval y ocultaba su segunda cola. Se corrige antes de dar por
válidas las exportaciones finales. La compilación Web intermedia se detuvo deliberadamente durante wasm-opt
al detectar ese defecto, antes de publicar. No se cuenta como exportación
Web validada. La corrección del catálogo y un filtro temprano de viabilidad
naval se aplicaron después de las sondas de rendimiento: las cifras siguientes
describen la primera build, no una medición del último parche.

La prueba de navegación usa Europe, 16 bandos, semilla 160212, 900 segundos
simulados de calentamiento, 24 reclutas por bando y 90 segundos medidos a 1x.
CPU Ultra 9 285H, RTX 5080 Laptop, 1600×900. No hubo compilación Unity en
paralelo. Sí se observó `rustc` de otro proyecto durante la pasada 500; no se
observó en la pasada 1000. No se detuvo ningún proceso ajeno.

| Presupuesto NavMesh | Unidades inicio→fin | Frame medio / máximo | Frames >50 / >100 ms | Órdenes aplicadas / rechazadas / pendientes |
| --- | --- | --- | --- | --- |
| 500 | 604→310 | 9,76 / 186,77 ms | 14 / 1 | 892 / 0 / 0 |
| 1000 | 614→334 | 9,38 / 72,31 ms | 15 / 0 | 1054 / 0 / 0 |

Las seis identidades seguidas se movieron en ambas pasadas. Quedaron tres
supervivientes móviles y ninguno inmóvil. En ventanas enteras a 1x, el
primer movimiento humano promedió 60,25–61,31 ms (500) y 59,63–73,35 ms
(1000). Los máximos de esas ventanas fueron 94,07 y 252,44 ms respectivamente.
Las muestras de rutas pendientes dieron cero; son instantáneas, no una prueba
de ausencia de esperas entre muestras. En 500, el pico de frame coincidió
con 174,72 ms en IA naval; sin traza detallada no se atribuye todo ese coste
a una función concreta.

No se sube el presupuesto por defecto: los combates y la carga externa
difieren, el primer movimiento no mejora de forma clara y el número de
unidades cae durante la medición. Esto no reproduce 800 unidades sostenidas
ni mide latencia desde un clic físico. Informes locales:
`RiskAI/Logs/v21-path-budget-500.log`, `v21-path-budget-1000.log`;
resumen en `Captures/v21-perf/summary.json`.

La observación pasiva anterior de v0.20 encontró órdenes aplicadas en
27–36 ms, pero una muestra de primer movimiento de 1,24 s y rutas pendientes
hasta 800 ms. Los picos de 23–26 s coincidían con cambios de foco y no quedan
explicados por los tiempos de tick registrados. Las mediciones anteriores
no se atribuyen al parche nuevo.

## Límites restantes

La separación visual arena/roca/verde no suaviza la geometría escalonada de
la costa. El shader interpola materiales; en el borde de una playa importada
manda el tile fuente para decidir si se puede embarcar. Las expediciones
buscan puertos con acceso al objetivo, no cualquier cabeza de playa posible.
No hay medición en hardware ARM, servidor autoritativo ni multijugador.

[Reglas navales y procedencia](audits/NAVAL-v0.21.md) ·
[Revisión Grok](audits/GROK-v0.21.md) · [Observabilidad](OBSERVABILITY.md) ·
[Pendientes](../TODO.md) · [Fase 2](PHASE2.md).
