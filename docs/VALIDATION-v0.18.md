# Validación v0.18

## Alcance y resultado

La validación reúne la suite Unity, el lector Python, un build Windows y
sondas controladas del ejecutable. No es un replay determinista ni una prueba
de input físico.

**171 casos Unity distintos aprobados**, tomando el último resultado de cada
nombre y sin sumar repeticiones; además, **7 pruebas Python** del lector
pasivo aprobaron.

- `v18-editmode-release`: 54 aprobadas.
- `v18-playmode-final`: 110 de 115 antes de aislar las regresiones afectadas.
- `v18-regressions`: 33 de 34 antes de resolver el último caso en su fixture.
- `v18-camp-release`: 1 de 1, que resuelve el caso restante.
- `v18-naval-release`: 23 de 23; repite los casos navales afectados.
- `v18-camera-frame`: 4 de 4; sustituye los cuatro casos de encuadre
  anteriores con comprobaciones de margen del HUD a 16:9, 21:9 y 9:16.
- Python: 7 aprobadas.

Los recuentos anteriores describen las ejecuciones y su orden de resolución;
el total de 171 es la unión de casos distintos, no la suma de ejecuciones
solapadas.

El build Windows terminó correctamente: **205.534.280 bytes**, registrado por
`RiskAI/Logs/v18-build-camera-release.log` como `RISKAI_BUILD_OK`.

## Sonda de rendimiento

Configuración de las sondas finales: Europe, 16 jugadores, semilla 160212,
1600×900, VSync desactivado, Intel Core Ultra 9 285H y NVIDIA RTX 5080 Laptop
GPU. La sonda calienta la simulación, vuelve a 1x, espera dos segundos de
estabilización y mide comandos sintéticos con el controlador físico
desactivado y picking sintético.

La ejecución previa a la optimización naval, `v18-probe-advanced-900.log`,
sirve únicamente como referencia: 479→386 unidades, 13,05 ms de media,
58,85 ms de máximo, cinco frames >50 ms, 908 órdenes aplicadas, cero
rechazadas y cero pendientes, con cinco de cinco identidades movidas. No se
presenta como replay idéntico frente a los finales: los cambios de navegación
y la simulación alteran la batalla aun con semilla común.

Las cargas antiguas `v18-probe-load-6.log` y `v18-probe-load-32.log` no se
usan para afirmar máximos estables: contienen trabajo de carga. Las métricas
finales proceden exclusivamente de `v18-final-probe-*.log`, tras la
reutilización de memoria de búsqueda marítima, límite de 32 celdas al
suavizar cada tramo de ruta y la estabilización de dos
segundos. Los máximos de carga anteriores no se mezclan con esta tabla.

| Ejecución final | Duración / carga | Frame medio / máximo | >50 ms | Órdenes aplicada / rechazada / pendiente | Movimiento |
| --- | --- | ---: | ---: | ---: | --- |
| `advanced-900` | 900 s simulados de calentamiento; 90 s a 1x | 13,30 / 62,76 ms | 2 | 967 / 0 / 0 | 6/6 seguidas, 6 supervivientes móviles; 19,95 m máximo |
| `load-6` | 6 reclutas por bando; 60 s a 1x | 10,47 / 36,13 ms | 0 | 179 / 0 / 0 | 5/5 seguidas; 12,45 m máximo |
| `load-32` | 32 reclutas por bando; 60 s a 1x | 14,02 / 26,74 ms | 0 | 233 / 0 / 0 | 6/6 seguidas; 12,21 m máximo |

Las cantidades inicial/final fueron 484→368 unidades en `advanced-900`,
295→330 en `load-6` y 625→660 en `load-32`. La carga mayor aumenta el coste
medio, pero estas batallas no aíslan todas las variables ni prueban que cada
retraso de movimiento se deba al número de unidades.

`advanced-900` avanzó exactamente 90,00 segundos de simulación durante su
medición. Las dos ventanas completas a 1x de esa sonda registraron,
respectivamente, aplicación de orden de 38,39 / 37,46 ms de media
(50,98 / 69,28 ms de máximo) y primer movimiento elegible de 120,90 / 249,86
ms de media (445,19 / 945,55 ms de máximo). No se combinan esas dos ventanas
en una media única y no se usa el tramo de transición anterior.

En esas ventanas, la búsqueda marítima máxima bajó de 23,00 / 21,87 ms en
la referencia a 12,18 / 11,01 ms; el máximo del paso de IA naval pasó de
40,96 / 40,22 a 12,34 / 12,49 ms. Se conserva el presupuesto de búsqueda y
la comprobación de costa. El peor fotograma total no mejoró: 62,76 ms frente
a 58,85 ms. Tampoco se ha eliminado toda espera antes del movimiento: el
máximo elegible final llegó a 945,55 ms. Aplicar una orden y empezar a
desplazarse son métricas diferentes.

La media de primer movimiento y la de aplicación de orden se informan sólo
para las dos ventanas completas a 1x de la sonda avanzada. Son telemetría
desde Submit, no el tiempo desde un clic físico. Las capturas secuenciales se
inspeccionan por separado y no se usan como benchmark.

## Revisión visual del ejecutable

Se revisaron las capturas a 1600×900 del menú independiente, ciudades,
guarniciones, hogueras, colas múltiples, entrenamiento, artillero, caballero y
vista estratégica. Las capturas de los cuatro mapas parten con 16 jugadores
y 15 comandantes: no hay tropas móviles iniciales, barcos, defensores heridos
ni disparos de torre antes de empezar la partida.

| Mapa | Ciudades | Puertos | Defensores iniciales |
| --- | ---: | ---: | ---: |
| Las Marcas | 33 | 7 independientes | 40 |
| Cuatro Riberas | 44 | 8 independientes | 52 |
| Europe | 212 | 44 incluidos en las ciudades | 212 |
| New World | 293 | 59 incluidos en las ciudades | 293 |

El encuadre deja un margen adicional de 16 px respecto al HUD. Los cuatro
casos de cámara comprueban las esquinas geográficas a tres proporciones de
pantalla. El mapa estratégico sigue la malla existente: los bordes rectos
del límite del mapa y los escalones de la costa importada todavía existen;
esta ronda no los convierte en una costa de geometría suave.

Como comprobación separada de presentación, se mantuvo la misma cámara y
partida pausada durante dos ventanas de cinco segundos, alternando detalle
táctico y estratégico. Europe registró 28,93 / 10,14 ms de media y New World
27,69 / 8,99 ms. Los máximos respectivos fueron 192,17 / 88,83 ms y
54,38 / 16,75 ms. Son intervalos de fotograma de reloj real, incluyen límite
de 120 FPS y espera de GPU, y no equivalen a un perfil CPU/GPU ni a una
medición de simulación. La reducción de detalle baja el coste medio visible,
pero estas capturas no prueban ausencia de picos.

## Límites

La validación no cubre ARM, navegador, Android, red autoritativa ni replay
determinista. Una sonda de carga no sustituye una sesión manual: controla
destinos, cohortes y cadencia para poder comparar señales del ejecutable.
Los contadores de heap/GC son diagnósticos, no bytes asignados por frame.
