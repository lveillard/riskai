# Validación v0.22 — presentación y navegación

Ronda del 8 de septiembre de 2026 sobre `main` tras el merge `a2d593d`.
Código de ejecución de esta evidencia inicial: `8de052e`. Este informe conserva
sus resultados, capturas e intentos rechazados; no los atribuye al seguimiento.
Un mismo proyecto Unity 6000.3.23f1 mantiene las reglas de Windows y Web.

Estado posterior en `61f386d`: completado el suavizado **acotado** de esquinas
costeras de Europe/NewWorld, con 10 casos EditMode y 10 PlayMode aprobados
(`coast-edit-r1` y `coast-play-r1`). Se desplazan como máximo 0,2 celdas
(0,512 m), con radios protegidos de 9 m alrededor de ciudades y 24 m alrededor
de puertos, tanto en el ancla del modelo como en la de ocupación. Tierra, agua,
collider y consultas CPU usan los mismos triángulos deformados; alturas, flags
y conectividad fuente se conservan. No se añaden vértices, triángulos ni draws.
El campo de arena/roca conserva sus coordenadas de mundo y su umbral compartido.
Es un redondeo local de esquinas, no una remodelación realista de toda la costa.

Las pasadas limpias 500 y 1000 del seguimiento mantienen 900 unidades durante
90 s; la de 2000 coincidió con Rust externo y debe repetirse. La comparación
completa y la confirmación avanzada siguen abiertas; el presupuesto continúa
en 500. El seguimiento también incorpora el minimapa optimizado de `2a9192b`,
Windows/Web finales y 27 pruebas adicionales dirigidas. Sus capturas y
mediciones se documentan por separado en
[Seguimiento v0.22](VALIDATION-v0.22-FOLLOWUP.md).

## Cambios

- El guardián naval no inicia una persecución autónoma al perder línea de tiro.
  El círculo exterior de selección/hover del barco tiene radio 4,3; el de
  ocupación conserva 1,55 y su ancla marítima. Un barco seleccionado no dibuja
  otro hover encima. Las anclas terrestre y marítima siguen siendo distintas.
- Las colas terrestres y navales muestran una puerta cálida y luz sobre el
  umbral, por encima de los escalones. La presentación usa el tiempo de
  simulación y se limpia al reutilizarla. La mancha transparente añade dibujo,
  sin una luz dinámica o pase de sombras por edificio.
- Terreno y comandos comparten un campo lineal de arena/roca con el mismo
  muestreo bilineal y umbral de arena 0,55. Los tiles Vcbp importados siguen
  siendo la fuente de arena; interpolación y presentación son adaptaciones
  locales. Se mantienen las comprobaciones de muelle, NavMesh, pendiente y
  proximidad al océano: el material arenoso por sí solo no garantiza acceso.
  Arena texturada, roca más fría y agua somera distinguen mejor las orillas.
  En `8de052e` la silueta geométrica angular seguía pendiente y no se habían
  desplazado ciudades, islas, agua ni datos de navegación fuente. El redondeo
  acotado posterior pertenece al seguimiento `61f386d` descrito arriba.
- El caballero tiene patas articuladas en pares diagonales, balanceo y
  transición gradual al reposo. La fase se integra sin saltar cuando cambia
  la velocidad o la partida lleva mucho tiempo. La lanza lee la preparación
  del golpe programado; no modifica daño, cadencia ni órdenes. La pose se
  reinicia al desactivar/reactivar la vista.

## Pruebas y revisión

31 casos Unity distintos aprobados: 30 en la primera ejecución y el caso de
GPU corregido en una segunda ejecución dirigida. La primera comparación de
textura usaba una lectura bilineal CPU que no era un oráculo adecuado del
shader; falló y se conserva. La nueva prueba renderiza 1×1 en GPU incluyendo
`CoastSurface.hlsl`, comprueba arena, roca y mezcla a ambos lados del umbral
en los cuatro mapas, y comprueba destrucción del recurso.

Los casos incluyen guardián con tiro obstruido, sucesión, producción,
entrenamiento, rutas navales, expedición, embarque/desembarque real en playas
de Europe/NewWorld y muelles. La suspensión de refuerzos está cubierta y
sólo se activa en el benchmark explícito. [Primera ejecución](audits/v0.22/tests-r1.xml)
· [Caso GPU corregido](audits/v0.22/tests-r2.xml).
Siete pruebas Python verifican que el comparador rechaza cambios de
escenario, cantidad, presupuesto, órdenes, errores y carga externa.

Tres agentes Astra con esfuerzo bajo implementaron los frentes iniciales;
dos cruzaron sus revisiones y el coordinador revisó la integración. Un agente
Terra implementó el caballero. La revisión corrigió arena sobre acantilados,
luz enterrada en escalones y discontinuidades del ciclo del caballo/lanza.
No se necesitó el fallback Sol.

## Evidencia visual

Capturas de la cámara real mediante petición de render de Unity, con el
reproductor Windows oculto. Son fixtures de presentación: no demuestran
input físico, funcionamiento del HUD superpuesto ni rendimiento de una
ventana visible. Se inspeccionaron Classic, Europe y NewWorld.

| Vista | Evidencia |
| --- | --- |
| Entrenamiento | [Inactivo](audits/v0.22/classic-town-idle.webp) · [Luz](audits/v0.22/classic-town-training.webp) · [Puerto](audits/v0.22/classic-port-training.webp) |
| Guardián naval | [Reposo](audits/v0.22/classic-guard-idle.webp) · [Seleccionado](audits/v0.22/classic-guard-selected.webp) · [Europe](audits/v0.22/europe-guard-selected.webp) |
| Orilla Europe | [Arena](audits/v0.22/europe-shore-sand.webp) · [Roca](audits/v0.22/europe-shore-rock.webp) · [Verde](audits/v0.22/europe-shore-green.webp) |
| Orilla NewWorld | [Arena](audits/v0.22/newworld-shore-sand.webp) · [Roca](audits/v0.22/newworld-shore-rock.webp) · [Verde](audits/v0.22/newworld-shore-green.webp) |
| Caballero | [Animación](audits/v0.22/knight-animation.webp) · [GIF](audits/v0.22/knight-animation.gif) · [Fotogramas](audits/v0.22/knight-contact-sheet.webp) · [Registro](audits/v0.22/knight-capture.json) |

El clip del caballero tiene 72 fotogramas: trote, pausa y ataque real; 23
fotogramas con velocidad y 16 con preparación de golpe. Se ocultan las copas
decorativas únicamente en el fixture para poder ver al jinete durante el
combate. El GIF es una muestra de poses, no una medición de fluidez/FPS.

## Carga y límites

El nuevo escenario `sustained-navigation-v2` mantiene 900 soldados reales,
con corredores, semilla y órdenes reproducibles. Congela los actores
originales y suspende refuerzos sólo dentro de esta sonda, detecta cambios
de población y conserva las comprobaciones de ruta. Aísla navegación; no
representa un combate con 900 soldados ni entrada física.

La comprobación funcional de 30 segundos mantuvo 900/900 unidades, todas
se desplazaron; 4500 movimientos y 9000 órdenes totales aplicadas, cero
rechazos y cero órdenes pendientes al finalizar. Había compilación externa:
**no es una comparación limpia de rendimiento ni justifica cambiar 500**.
El primer fixture de cuadrícula de 8 m sólo admitió 300 corredores y se
rechazó; el de 4 m conserva todos los criterios y alcanza 900.

`scripts/measure_navigation_ab.py` prepara ejecuciones separadas de 90 s
con presupuestos 500/1000/2000, verifica identidad de posiciones/órdenes y
registra RAM y compiladores externos. Al cierre de `8de052e`, la comparación
limpia esperaba una ventana sin compilación externa. Dos intentos de preparar una partida
avanzada llegaron a 900 s simulados y se rechazaron porque la sonda no logró
crear una cohorte móvil del jugador cero; el segundo activaba su comandante
durante el calentamiento. El registro no distingue eliminación del jugador
de falta de puntos navegables válidos en sus ciudades. Ninguno llegó a medir
los 90 s. Hace falta además resolver esa preparación antes de validar la
partida avanzada, sin cambiar propietarios ni resucitar tropas.
El presupuesto compartido permanece en **500**.
No se certifica ARM, FPS de una partida visible, clic físico ni 800 unidades
sostenidas en combate. La sonda inicia el modo de segundo plano antes de
esperar la batalla para que pueda arrancar con la ventana oculta.

## Exportaciones

Windows y Web v0.22 generados correctamente. Windows se utilizó en las
capturas y la prueba funcional. Web se comprobó en Edge con WebGL real:
Europe a 1600×900 y NewWorld a 768×1024. Ambos cargan la batalla sin
excepciones de página ni errores de shaders; sólo aparece un 404 del favicon.
La vista vertical usa un navegador de escritorio, no un dispositivo ARM.

| Web | Captura | Registro |
| --- | --- | --- |
| Europe | [1600×900](audits/v0.22/web-europe.webp) | [Resultado](audits/v0.22/web-europe.json) |
| NewWorld | [768×1024](audits/v0.22/web-newworld-portrait.webp) | [Resultado](audits/v0.22/web-newworld-portrait.json) |

El launcher y el servidor local apuntan a las carpetas v0.22. El
[recibo de validación](audits/v0.22/receipt.json) identifica código, pruebas,
intentos rechazados y hashes SHA-256 de fuentes, builds y evidencia.

Se conservan los intentos fallidos: captura ScreenCapture sin imágenes al
ocultar la ventana, disco lleno durante la escritura del auxiliar (recuperado),
un error de compilación del auxiliar por una referencia URP innecesaria,
y el primer fixture de carga insuficiente. Los resultados aceptados usan
petición de render estándar y no esos intentos. Las builds v0.21 se conservan.
