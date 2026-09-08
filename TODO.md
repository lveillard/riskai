# RiskAI — decisiones y siguientes pasos

## Ronda v0.21 · feedback de la partida, adicional a fase 2

Registro del feedback del 8 de septiembre. Estas tareas se mantienen junto
al objetivo activo de tablet/Web; no quedan sustituidas por él.
Las revisiones estáticas no sustituyen las pruebas. Tras tres rondas se han
aprobado 100 casos Unity distintos; exportaciones y evidencia del último parche
se cierran por separado.

- [x] Corregir arrastre derecho en pausa, conservar rueda de cámara y cancelar gestos pendientes al pausar/reanudar. Regresión Unity aprobada; cámara en pausa comprobada también en el reproductor Web.
- [x] Selección rectangular sólo de edificios propios, incluidos puertos; Shift conserva la selección al arrastrar vacío. Regresión Unity aprobada.
- [x] Verificar rueda sobre ranking y catálogo de producción en el reproductor Web real, incluida ventana 390×844; el mapa no recibe ese zoom. Entrada CDP, dispositivo físico pendiente.
- [x] Iconos de fragata/transporte en compras y colas; ayuda sólo en tooltip, sin explicación permanente. Inspección Windows/Web del ejecutable final `a4060df`; ayuda por pulsación larga pendiente de fase 2.
- [x] Un solo defensor y propietario por puerto; el barco guardián habilita producción mediante los mismos comandos que el resto. Se conserva la prioridad de un guardián vivo y las anclas duales; escaneo `Contested`, prioridad terrestre viva y sucesión naval aprobados en Unity.
- [ ] Eliminar interferencia entre círculo de selección, hover y círculo del guardián naval; mantener el barco anclado al disparar. Comparar visualmente al seleccionar/mover. Las anclas terrestre/atraque son adaptadores intencionales y no se unifican por apariencia.
- [x] Ampliar ligeramente el margen de relevo de guarnición, especialmente para caballeros; misma tolerancia terrestre y naval (2,0 frente a radio pintado 1,55). La prueba pasó navegando desde más de 5 m; no fue necesario ampliar el radio naval.
- [ ] Entrenamiento con luz visible saliendo de la puerta y umbral, compartida entre edificios/colas. Cambio de presentación pendiente de inspección.
- [x] Evitar que la IA reenvíe tropas a ciudades inaccesibles por tierra. El filtro por tropa queda implementado; cursor estable frente a cambios de roster/propietario validado; el umbral de ola mínima queda para decisión aparte.
- [x] IA con expediciones marítimas reales: comprar transporte, reunir tropas móviles, embarcar, navegar, desembarcar en una zona válida y atacar/capturar. Corregir pares puerto/objetivo alternativos, conectividad y equidad de fuente/recuperación, caché/precálculo de atraques, snapshot del cargamento real y liberación de rezagados; el embarque del jugador necesita progreso finito, reintento a 0,2 s sin sondeos por frame, y los fallos navales de IA deben quedar en el equipo local. Regresiones aprobadas, incluida expedición completa. La tercera ronda añade continuidad hasta ciudades más lejanas, barco compatible por fuente y recuperación de errores antiguos. Sin teletransportar carga. [Auditorías navales](docs/audits/NAVAL-v0.21.md) · [Revisión de ronda 1](docs/audits/REVIEW-v0.21.md) · [Revisión Grok](docs/audits/GROK-v0.21.md).
- [x] Corregir agua invisible en Web: el perfil Mobile aporta profundidad y color opaco al shader compartido. Tres revisiones independientes limpias y comprobación visual del lago en el reproductor Web `a4060df`. Android físico sin validar.
- [ ] Costa más natural: playas de desembarco, tramos rocosos y verdes no embarcables, con clasificación compartida entre representación y reglas. El horizonte debe recibir UV1; el test de orilla ya separa terreno fuente de la aserción de política. Geometría visual y alineación arena pintada/validada siguen abiertas; no elegir política todavía. El coste de bake de costa se medirá en `StartupMetrics`/fase `terrain`.
- [ ] Investigar retraso de órdenes en partida avanzada sin cerrar la sesión. La sonda de presupuesto sigue en 500 hasta tener medición reproducible; caché/precálculo de atraques aceptado para corregir picos de planificación. El arranque de terreno se medirá con `StartupMetrics`; la pasada `060ed60` de 957→443 unidades queda marcada con contención de RAM y se repetirá sólo la medición afectada. La repetición `a4060df` (1008→511) también coincidió con carga externa: órdenes sin cola, primer movimiento máximo 1086 ms en la ventana de 834 unidades. Contadores de ruta observada, inicio de velocidad y velocidad dirigida añadidos; seis pruebas dirigidas aprobadas. Etapas observadas en Windows `624743c`: la primera ventana de carga llega a 392 ms desde aplicación hasta ruta no pendiente, más espera hasta velocidad; hubo compilación ajena. La medición posterior sin contención (`624743c`, 903→349) completó 109 movimientos observados: máximo envío→velocidad 437 ms, aplicación→ruta 351 ms. Comparar presupuesto NavMesh con carga/posiciones controladas antes de cambiar el valor compartido; esta pasada no fue un A/B. Picos navales, carga sostenida de 800 y comportamiento ARM pendientes. Medidas v0.20: unas 500 unidades, envío→aplicación 27–36 ms, una muestra de primer movimiento 1,24 s y rutas pendientes hasta 800 ms. Separar navegación de picos de frame de 23–26 s antes de atribuirlos a simulación o renderizado; la sesión v0.20 ya terminó.
- [x] Exportar Windows/Web `624743c`, comprobar contadores en ambos jugadores y actualizar launcher/lector de logs. 100 casos Unity distintos y ocho Python aprobados. Capturas e informes publicados con procedencia.
- [x] Completar la medición acotada de rendimiento con memoria recuperada: Windows `624743c`, 903→349 unidades, 90 s a 1×, 988 órdenes aplicadas/0 rechazadas/0 pendientes al cerrar; 126 muestras de entorno sin compilador/sonda competidora y mínimo 7,97 GiB libres. Envío→velocidad máximo 437,03 ms; la mayor espera posterior a aplicación está hasta observar la ruta. [Datos y límites](docs/audits/PERFORMANCE-v0.21.json). Esta ejecución completa la medición pendiente; no certifica 800 unidades sostenidas, clic físico o ARM, ni resuelve todo el retraso o la fase 2. No se cambió el presupuesto ni se repitieron suites sin cambios.

- [x] Comprobar gestos de juego en el reproductor Web exportado: área con un contacto y lápiz, doble toque, toque de dos contactos y botón secundario del lápiz mueven unidades reclutadas normalmente. Tarjetas tocables verificadas en escritorio/vertical, layout también horizontal. Es entrada CDP; no se da por probada una tablet física. La reexportación final usa el target WebGL explícito del script y no reproduce los nuevos avisos de sampler.
- [x] Recuperar retratos individuales, barras de vida y selección por tarjeta del ejército; un solo roster para escritorio y pestaña compacta, incluidos grupos mixtos tierra/mar. Cinco regresiones nuevas; siete casos HUD finales aprobados. Tres revisores Opus5 independientes y seguimientos proporcionales sin P0/P1/P2 abiertos. [Auditoría](docs/audits/ROSTER-v0.21.md).
- [x] Identidad de selección durante pérdida prolongada de foco corregida en fuente local `f05c586`: selección, grupos y embarques pendientes rechazan otra vida del pool y conservan supervivientes. Unity 55/55 (diez nuevas), tras tres CS1503 corregidos antes de ejecutar; tres contextos independientes Opus5 y seguimientos sin P0/P1/P2 finales. Cierre de fuente/pruebas/revisión; [límites y cierre de exportaciones](docs/audits/SELECTION-LIFETIME-v0.21.md).

[Validación de la ronda](docs/VALIDATION-v0.21.md) · [Objetivo fase 2](docs/PHASE2.md).

## v0.20 · recuperar identidad RTS

- [x] Recuperar marcos propios, materiales, títulos y retratos sin duplicar UI ni reglas por plataforma.
- [x] Selección y órdenes/producción simultáneas en escritorio; pestañas compartidas en ancho compacto.
- [x] Corregir cambio de ayuda a victoria y acotar scroll compacto; revisión Grok 4.6 contrastada contra los consumidores reales.
- [x] Retirar el radio azul adicional de los muelles; conservar selección común y alcance de embarque en todos los mapas.
- [x] Instalar Web Build Support y comprobar el primer reproductor real; detectado y corregido en fuente el stripping de Collider al crear la batalla.
- [x] Verificar la exportación Web final con batallas, comandos, tres reinicios Europe/NewWorld y lápiz/toque sintéticos dentro de Unity.
- [ ] Mejorar y atribuir el coste Web: Europe táctico sin compilador externo midió 37,20 ms medios/62 ms máximo; vista estratégica 28,35/56 ms. Las pasadas bajo carga tenían fuertes picos y omitir draws no los eliminó. Seguir con trazas de CPU del navegador; no presentar esto como rendimiento ARM validado.
- [x] Mostrar «recogiendo muestra» en el menú de rendimiento hasta tener el primer informe; los ceros iniciales no son una medición.

[Capturas, pruebas y límites v0.20](docs/VALIDATION-v0.20.md).

## v0.19 · adaptación compartida y rendimiento

- [x] Menú y HUD interactivos UI Toolkit, con áreas seguras y disposiciones para escritorio/vertical/horizontal.
- [x] Reconocedor táctil y lápiz con regresiones sintéticas; cámara y órdenes compartidas entre los cuatro mapas.
- [x] Catálogo común de producción, IDs de edificio y ejecutor local preparado para una futura frontera de autoridad.
- [x] Cuatro revisiones adversariales Grok 4.6; 213 casos Unity distintos y ocho Python aprobados.
- [x] Adaptador de lápiz DOM → Input System compartido; 12 pruebas Node y siete comprobaciones CDP en Edge. v0.20 comprueba también lápiz/toque sintéticos dentro del reproductor Web; hardware físico pendiente.
- [x] Sondas Windows de partida avanzada, 625–660 unidades y reinicios Europe/New World. Mantener en el informe los picos no reproducidos y la variación de memoria.
- [x] Traza opcional de hitches y contadores de motor; no activarla por defecto.
- [x] Completar instalación de Web Build Support y generar primera build Web; validación de batalla seguida arriba.
- [ ] Validar memoria, respuesta, rotación, trackpad y lápiz en hardware tablet/ARM real.

[Resultados y limitaciones v0.19](docs/VALIDATION-v0.19.md) · [Fase 2 activa](docs/PHASE2.md).

Feedback del 6 de septiembre de 2026. Las reglas verificadas y las adaptaciones se distinguen en [la auditoría de Risk](docs/RISK-RULES-v0.12.md). Esta lista conserva también las propuestas que todavía no están implementadas.

## Revisión v0.18 — territorios, controles y reglas compartidas

- [x] Vista estratégica al alejar: superficies de terreno con color de dueño, sin árboles, modelos tácticos ni barras; histéresis y simulación intacta.
- [x] Atlas único para propiedad e inspección de países, con costa y puertos; neutral blanco.
- [x] Caja izquierda selecciona edificios cuando no contiene tropas; Shift, doble clic en ciudades propias y una identidad para casa/torre/puerto.
- [x] Colas múltiples visibles y una compra total a la cola compatible más corta; salidas de ciudades, puertos y hogueras.
- [x] Relevo voluntario atómico dentro del círculo; las fragatas conservan el ancla marítima y la misma política de sucesión.
- [x] Menú inicial separado de la escena de batalla; ranking por ciudades y ayuda reorganizada.
- [x] Puerta existente iluminada al entrenar, lance del caballero, artillero de cañón corto y efectos por tipo de impacto.
- [x] Verificar cadencia y preparación de ataques contra W3U/SLK; documentar el soldado local y campos heredados todavía no resueltos.
- [x] Las Marcas 33 ciudades; Cuatro Riberas 44. Refuerzos, población móvil, perfiles y cámara compartidos. [Auditoría SSOT](docs/audits/MAP-SSOT-v0.18.md).
- [x] Reutilizar buffers A* navales y acotar suavizado conservando validación de costa y rutas independientes.
- [x] Tres revisiones Grok 4.6, pruebas de partida avanzada y carga controlada; resultados y límites en [Validación](docs/VALIDATION-v0.18.md).
- [ ] Reducir y explicar las esperas de navegación hasta el primer movimiento: la sonda avanzada v0.18 tiene 0 órdenes pendientes/rechazadas, pero registra un máximo elegible de 945,55 ms. Medir eventos físicos y esperas de ruta por orden antes de atribuir todo a cantidad de unidades.
- [x] Catálogo de producción común para HUD e IA, incluyendo Marines; impedir que un adaptador de puerto permita compras que otro no ofrece (v0.19).

La fase 2 de navegador, tablet, móvil y lápiz está autorizada después de cerrar
esta validación. Se trabajará con un objetivo explícito y un solo proyecto de
Unity; el servidor autoritativo sigue siendo una etapa posterior.

## Revisión v0.17 — respuesta, legibilidad y bosque

- [x] Observar una partida abierta sin cerrarla: lector pasivo `scripts/observe_runtime.py`, con ventanas y rechazos. [Datos v0.16 y límites del diagnóstico](docs/audits/LIVE-CONTROLS-v0.16.md).
- [x] Instrumentar la siguiente build con fases de tick, máximos de cola, tiempo desde Submit hasta aplicación/primer movimiento elegible y rutas pendientes; separar foco/pausa de frames de juego. No equivale a medir el clic físico.
- [x] Eliminar arrays temporales del picking y de marcos del HUD, reutilizar estilos y contar presencia/ciudades una vez por tick para victoria.
- [x] Precalcular aristas y componentes de navegación naval durante carga; no repetir órdenes de la IA al mismo destino y distribuir sus decisiones por bando.
- [x] Resolver el rodeo marítimo entre puntos que caen en la misma celda cuando el segmento directo está bloqueado por costa.
- [x] Agrupar peticiones terrestres autónomas mientras hay ruta pendiente; las órdenes explícitas conservan prioridad. Presupuesto de NavMesh explícito para escritorio, pendiente de medición en tablet.
- [x] Cola naval de cinco con iconos; ocultar anillo grande de embarque cuando no se selecciona el puerto, conservar círculo de guardia.
- [x] Entrada y reunión inicial próximas a la puerta, independientes del color del propietario; conservar el punto elegido por el jugador.
- [x] Cámara de 55 grados también en mapas importados; confirmación de órdenes con pulso reutilizable.
- [x] Indicador animado de reclutamiento por edificio; pausa y reutilización. Puerto importado comparte la vista de su ciudad.
- [x] Mostrar barra de vida aunque esté llena cuando una copa puede ocultar la unidad. Índice estático, comprobación a 10 Hz, sin colisionadores ni pase de render adicional.
- [x] Bosque denso reduce velocidad un 18 % y permite el paso; árboles originales e importados decorativos. Es una adaptación local, no una cifra extraída de Risk. El planificador todavía no prefiere rutas por coste de bosque.
- [x] Banda visual de orilla precalculada con variación del material. Conserva geometría y navegación fuente.
- [x] Saeta, orbe y proyectil de mortero diferenciados en pools; orientación del arco e impactos distintos. Daño sigue en simulación.
- [x] Verificar matriz de counters en ambos W3X: mismo miembro `war3mapMisc.txt`; documentar proyección de columnas y unidades locales. [Auditoría](docs/audits/COUNTERS-v0.17.md).
- [x] Dos rondas reales adicionales de Grok 4.6; corregir falsa confirmación de embarque, margen de descarga y recuperación de rutas abandonadas. [Revisión v0.17](docs/audits/GROK-v0.17.md).
- [x] Validar respuesta tras 20 minutos simulados y medir 90 s a velocidad normal en Europe con 16 bandos: máximo de 40,03 ms, sin frames >50 ms; cinco identidades seguidas responden. [Datos y límites](docs/VALIDATION-v0.17.md).
- [ ] Suavizar la silueta geométrica escalonada de la costa sin hacer que el dibujo contradiga puertos, islas y pasos navegables.
- [ ] Revisar retroceso/animaciones específicas de mortero y Marines; las animaciones de ataque existentes siguen siendo parte de la presentación.
- [ ] Llevar gesto/orden/cámara al adaptador Input Actions y táctil descrito abajo, con trazabilidad desde evento hasta intención. No introducir dependencia del dispositivo en la simulación.

## Implementado en v0.12

- [x] Edificios y torres permanentes; el objetivo de combate es su guarnición. Cambio de dueño conjunto.
- [x] Sucesión inmediata: aliado cercano primero, después enemigo más próximo, neutral si no hay candidato. Mantener vida e identidad del soldado.
- [x] Anillos inspirados en WC3 bajo las unidades y alrededor de la ocupación; color marfil para neutrales.
- [x] IA que interrumpe expediciones para defender posiciones amenazadas y conserva una reserva.
- [x] Oro en la escala del mapa, recompensa fraccionaria por bajas, perfiles navales comunes y refuerzos limitados por valor vivo.
- [x] Hogueras como puntos de refuerzo; selección con superposición territorial y ciudades resaltadas. Suelo sin grandes tintes permanentes por propietario.
- [x] Dos escenarios seleccionables: Las Marcas y Cuatro Riberas, con cuatro dominios continentales y archipiélago.
- [x] Río más largo por el centro del segundo mapa, montaña de origen, más espacio al norte y cruces navegables por infantería.
- [x] Desvanecimiento correcto de sombras por distancia y variación moderada de pradera y agua, anclada al mundo y sin pases adicionales.
- [x] Menú dividido en partida, controles y ajustes. Eliminar el modo Capitales de esta adaptación.
- [x] Botón derecho: pulsación y suelta dan una orden; arrastre mueve cámara y no emite una orden accidental.
- [x] Desventaja de tiro perforante cuesta arriba: 25 % de fallo con desnivel de al menos 2,5 unidades. No aplicar a pequeñas ondulaciones, cuerpo a cuerpo ni hechizos.
- [x] Compilación Windows y 97 pruebas (32 EditMode + 65 PlayMode).
- [x] Inspección del ejecutable: ciudades, puertos, islas, río, hogueras, menú y zoom en ambos escenarios.
- [ ] Recoger feedback jugado de esta versión.

## Entrada táctil y lápiz — implementación v0.19; validación física pendiente

Intenciones compartidas con ratón, sin mutar directamente salud, propiedad u oro:

- [x] Un dedo: toque selecciona; arrastre crea selección en área. Reconocedor puro y dispositivos sintéticos.
- [x] Dos dedos: centro mueve cámara; separación cambia zoom con el mismo rig que ratón.
- [x] Toque de dos dedos sin movimiento: orden contextual al soltar.
- [x] Doble toque: orden contextual conservando la selección. Toque simple espera 240 ms; ratón conserva doble clic tradicional.
- [x] Lápiz: punta y botón secundario, con doble toque como alternativa. Pruebas Input System, no certificación de hardware.
- [x] Cancelación por modal/foco, propiedad del gesto y cuarentena hasta soltar los contactos. Tercer dedo y ratón sintético no generan órdenes.
- [x] `RtsInputRouter` y `DirectPointerGesture` separan dispositivo y reconocimiento de las intenciones existentes.
- [x] HUD y menú UI Toolkit adaptables; viewport y área segura compartidos con cámara/picking.
- [ ] Validar dedos, lápiz, trackpad, rotación y teclado virtual en tablet/móvil físicos y navegador.
- [ ] Migrar los atajos de teclado restantes a Input Actions configurables; el router compartido no equivale a remapeo completo.

## Servidor y plataformas — todavía pendiente

- [ ] Servidor autoritativo con conexión autenticada, validación de órdenes y snapshots. La identidad del jugador la decide el servidor.
- [ ] Incorporar compras, barcos, embarque y desembarque al protocolo de comandos; actualmente la cola por ID cubre infantería.
- [x] Preparar catálogo único y ejecutor local de compras, cancelaciones, salidas y torres con IDs de edificio. No es todavía un protocolo de red ni autoridad remota.
- [ ] Separar vistas y arranque de arte para Dedicated Server; navegación autoritativa sin exigir que NavMesh coincida entre clientes.
- [ ] Hornear el campo de coste del bosque como dato de mapa compartido: hoy se genera desde las cotas de las copas de la escena, y un servidor sin arte necesita el mismo campo numérico.
- [ ] Transporte compatible con navegador, reconexión, latencia, visión por jugador y niebla de guerra.
- [ ] Build Web y Android, medir memoria/descarga/renderizado e input en dispositivos. No hay validación de esas plataformas todavía.

## Mapas, reglas y presentación

La [investigación de World Editor](docs/WORLD-EDITOR-TERRAIN.md) orienta el siguiente flujo de autoría.

- [ ] Convertir alturas, biomas, ciudades y cruces en datos editables con vista previa.
- [ ] Añadir pinceles propios de subir, bajar, aplanar y suavizar; visualizar navegación y huellas de edificios. Mantener materiales separados de las reglas de paso.

- [ ] Comparar el mapa original jugándolo en una instalación propiedad del usuario y en World Editor. Esto permite comprobar valores heredados; no proporciona el código fuente del motor Warcraft.
- [ ] Obtener una copia verificable de Reforged: Rome antes de atribuirle estadísticas. Saran, New World y Rome no son la misma variante.
- [ ] Exportar datos y perfiles desde una fuente externa, conservando procedencia, campos heredados y ajustes locales. Completar tipos de unidades y barcos de forma gradual.
- [ ] Refinar puntos de refuerzo y rendimientos según número de ciudades, pasos de entrada y dificultad de defensa. Comparar grupos pequeños de islas con grandes dominios; medir por valor de unidades además de cantidad.
- [ ] Refinar acantilados, encuentros de terreno con puentes, estuario y distribución de árboles con capturas y partidas. Mantener el agua eficiente y sin ondulaciones geométricas exageradas.
- [ ] Añadir más biomas y variación local de tiles sin perder legibilidad de unidades y fronteras.
- [ ] Migrar menú/HUD a UI Toolkit, perfiles a datos, mapas/NavMesh a horneado en editor y carga de arte a un sistema adecuado cuando se mida su necesidad.
- [ ] Medir CPU y GC a 100/200 unidades antes de Jobs/Burst o ECS. El tick fijo no hace determinista por sí solo la navegación.

## Apertura y siguiente revisión (v0.13)

- [x] Un ballestero por ciudad y puerto, también neutral; sin ejércitos o flotas regalados.
- [x] Primer recluta asequible para la IA; priorizar refuerzo de ciudades amenazadas y frontera. Reserva compatible con una apertura sin ejército móvil; ahorro y compra de la primera fragata por cola.
- [x] Ampliar las islas al norte y recolocar dos puertos clásicos para separar puestos y evitar fuego entre guarniciones al empezar.
- [x] Ballesteros de hoguera en proporción `ceil(ciudades / 2)`, conservando límite de puntos vivos.
- [x] Reservar claros de bosque considerando la copa proyectada ante la cámara; barras solo durante captura real.
- [x] Frenada de unidades a distancia cerca de su alcance útil, para evitar sobrepasarlo entre ticks.
- [x] Prueba táctica del ballestero que se aproxima por el lado opuesto a la torre, mata una guarnición cuerpo a cuerpo y ocupa su círculo.
- [x] Regresión de aproximación a distancia con fotogramas largos (10 FPS, velocidad ×4); ruta hasta la posición de tiro. Suite final: 101 casos aprobados, 32 EditMode + 69 PlayMode.
- [x] Emitir los refuerzos de país de uno en uno cada 500 ms según JASS, con crédito persistente por país y límite regional de puntos.
- [ ] Completar adquisición, alcance mínimo y activación de armas heredados desde las tablas apropiadas; no presentar ajustes locales de torre como estadísticas exactas del mapa.
- [ ] Sustituir las suposiciones de IA sobre navegación y visión por consultas de ruta/visión. Mejorar concentración de expediciones y desembarcos.

## Importación de Europe y World

- [x] Importar Saran Europe/Mediterráneo: 212 ciudades y 69 grupos, conservando XY de ciudad, círculo y punto de refuerzo. Las dos arenas pequeñas siguen siendo mapas originales.
- [x] Mantener puerto como una clase de ciudad del mapa importado, incluida en reparto, grupos, ingresos y victoria; los puertos adicionales actuales aún tienen reglas separadas.
- [x] Incorporar **New World v3.0**, 293 ciudades y 100 grupos, como escenario seleccionable. No equivale a todo el planeta ni a haber identificado la versión histórica más popular.
- [ ] Leer altura, rampas, agua, pasos, biomas y límites desde datos propios de autoría; reconstruir el arte con materiales/modelos originales. Verificar rutas, distancia al círculo y ángulos de tiro por ciudad.
- [x] Separar guarniciones del presupuesto de 100 tropas móviles en los mapas importados para permitir compras desde el inicio.
- [x] Generalizar propietarios, economía, IA, combate, captura y reparto a 2–16 jugadores. Neutral separado del jugador 2.
- [ ] Alianzas/diplomacia y participantes remotos; hoy todos los oponentes son IA locales.

## Héroes y bucle de partida — fase posterior

- [ ] Selección previa de héroe, aparición en una ciudad aliada y progresión de niveles durante la partida, como ha propuesto el usuario. Diseñar muerte/reaparición y reparto de experiencia antes de implementarlo.
- [ ] Mantener la conquista por soldados, territorios y hogueras como núcleo: los héroes deben abrir decisiones, no reemplazar la guarnición o volver irrelevante el control del mapa.
- [ ] Auditar las decisiones que sostienen Risk (frentes, completar grupos, ventanas de ingreso, alianzas, riesgo de extenderse) y las útiles de DotA/LoL (identidad de rol, progresión, respuesta al rival). Distinguir ideas de reglas extraídas.
- [ ] Validar con partidas cortas: tiempo hasta primera conquista, posibilidad de remontar, claridad del siguiente objetivo y diferencias entre mapas. No inferir diversión o popularidad del código por sí solo.

## Revisiones asistidas

- [x] Medir Qwen3.8 por Grok CLI en tandas de 3 a 10: 51/52 respuestas completas en tareas pequeñas. [Medición y límites](docs/audits/QWEN-CONCURRENCY-v0.13.md). Una respuesta completa no implica que el hallazgo sea correcto.
- [ ] Dar paquetes pequeños por tema; contrastar contra código y tests antes de aplicar sugerencias. Terra/Luna pueden preparar pruebas y verificar hallazgos; decisiones de diseño e integración a cargo del agente principal.
- [x] Dos rondas adversariales reales de Grok 4.6, contrastadas y convertidas en correcciones y regresiones: [revisión v0.16](docs/audits/GROK-v0.16.md).

## Expansión v0.14

- [x] Anclar cada defensor a un punto navegable estable y excluirlo de la evasión que lo desplazaba del círculo. Restaurar movimiento al liberarlo.
- [x] Las Marcas: 18 ciudades, nueve grupos y secano con vegetación baja al suroeste.
- [x] Menú con cuatro mapas; listas de grupos y ciudades paginadas, zoom estratégico y foco de cámara adaptados a los mapas grandes.
- [x] Importar alturas, agua, tiles y posiciones numéricas; crear terreno y muelles con materiales propios.
- [ ] Refinar navegación y decoración de los cientos de puestos importados mediante partidas; medir carga y renderizado, y hornear NavMesh en editor.
- [ ] Incorporar datos fuente de pathing, puentes y destructibles cuando sean necesarios; la cuadrícula de altura no reproduce por sí sola todo el World Editor.

- [x] Leer límites jugables y de cámara W3I para recortar el espacio exterior de los imports sin mover las coordenadas de ciudades.

## Fidelidad y 16 jugadores · v0.15

- [x] Mantener XY de ciudades, círculos y hogueras; aplicar la misma conversión nativa /50 a alcance, movimiento y colisiones verificadas, sin comprimir Europe/New World.
- [x] Extraer árboles DOO, transformar ejes/rotación/escala correctamente, seleccionar meshes propios por especie y agrupar su renderizado. Dejar claros los puestos.
- [x] Menú 2–16 participantes, paleta de 16 colores, identificación por IA y panel de jugadores.
- [x] Cordilleras opcionales: relieve suave y transición a roca/nieve que preserva claros, costa y agua.
- [x] Obtener bounds efectivos de Rifleman, Priest, Knight, MortarTeam y HumanBarracks; calibrar la altura de espera de nuestros cuatro tipos de unidad correspondientes y comprobar la selección. No confundir ucol con anchura/altura de un mesh.
- [ ] Calibrar edificios, torre y árboles propios; revisar siluetas, anchuras y animaciones. El caballero ya tiene una montura propia; falta calibrar anchuras y otros edificios/árboles.
- [ ] Resolver pathing fuente de árboles y edificios: los árboles importados siguen siendo decoración sin nuevos bloqueadores NavMesh.
- [ ] Diseñar un Rin continuo desde los Alpes al mar con lecho, riberas y cruces. El prototipo de traza chocó con exclusiones de ciudades/círculos y no se incorpora.
- [ ] Medir partidas prolongadas con 16 ejércitos, optimizar decisiones de IA y hornear navegación en editor. No equiparar 15 IA locales con multiplayer autoritativo.
- [x] Usar inclinación, orientación y distancia inicial de cámara del JASS para Europe/New World; mantener FOV explícitamente adaptado y extender el zoom estratégico.
- [x] Decodificar el índice de suelo W3E desde el nibble correcto, independientemente de variación y flags de agua/acantilado.

## Control, puertos y presentación · v0.16

- [x] Selección conjunta de casa y torre, incluyendo tejado/base, con anillo amplio del edificio.
- [x] Salida opcional por hoguera; por defecto los refuerzos esperan allí. Países incluyen sus puertos en la superposición; fronteras internas del mismo dueño sin postes.
- [x] Corregir ingreso FFA: 4 + ciudades propias, aunque estén en grupos fragmentados; 0 sin ciudades. Revalidar estadísticas y torre contra W3U y tablas heredadas disponibles.
- [x] Caballero montado propio, sin agrandar un infante; reclutas Marines de puerto con costes y perfiles verificados.
- [x] Embarque con aproximación, desembarque costero en cola y permanencia de órdenes al cambiar selección. Puertos sin guarnición defendidos/ocupados por fragatas cercanas.
- [x] Fragata larga y transporte ancho con carga; puntos de muelle visibles. Rechazar desembarco en agua abierta o terreno escarpado.
- [x] Tab para marcadores, cámara más rápida, órdenes rechazadas con motivo y diagnóstico periódico de fotogramas/GC/cola.
- [x] Reducir patrón de cuadrícula con manchas de vegetación, mezcla de suelos/agua y posiciones menos regulares en Cuatro Riberas.
- [ ] Completar pathing y playas del mapa fuente: los marcadores actuales y la prueba de costa transitable son adaptaciones, no áreas de World Editor extraídas.
- [ ] Permitir varios embarques simultáneos independientes desde el adaptador de input, y mostrar progreso/cancelación por transporte en HUD.
- [ ] Calibrar siluetas de marina por variante de mapa, capacidad naval y el resto de armas heredadas TFT aún no resueltas; no atribuirles valores supuestos como exactos.
- [ ] Medir una partida prolongada con órdenes reales y 16 ejércitos; usar los nuevos logs para reproducir la incidencia de unidades propias sin respuesta.

- [x] Corregir rutas parciales que se reiniciaban, fallos silenciosos de patrulla y foco de cámara al volver del zoom estratégico.
- [x] Movilizar refuerzos que esperan en hogueras de la IA, manteniendo la espera por defecto del jugador.

- [x] Sondas reproducibles de 70 s con 16 bandos en Europe/New World: todas las tropas del jugador seguidas se movieron, sin rechazos ni órdenes pendientes al finalizar. No sustituyen una partida prolongada ni una prueba de input físico; [mediciones](docs/VALIDATION-v0.16.md).
