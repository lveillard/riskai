# Validación v0.19 · Windows y preparación Web

Este documento reúne resultados ya producidos. No sustituye la validación Web,
ARM ni una nueva ejecución completa posterior a cada corrección.

## Resultados de pruebas

| Evidencia | Resultado | Alcance |
| --- | ---: | --- |
| TestResults/v19-editmode-second.xml | 77/77 aprobadas | EditMode |
| TestResults/v19-playmode-first.xml | 124/127 aprobadas | Primera pasada completa de PlayMode |
| TestResults/v19-regressions-third.xml | 22/22 aprobadas | Regresiones dirigidas posteriores |
| TestResults/v19-ui-release.xml | 13/13 aprobadas | Oro al pausar entre ticks, frontend y encuadre de cámara |
| Pruebas Python | 8/8 aprobadas | Observación runtime y servidor Web local |

La primera pasada completa de PlayMode tuvo estas tres sustituciones exactas,
todas aprobadas en la pasada dirigida posterior:

| Caso que falló en la primera pasada | Resultado posterior |
| --- | --- |
| RiskAI.Tests.DirectPointerInputTests.BarrelPressCancelsPenMarqueeBeforeContext | Aprobado en v19-regressions-third.xml |
| RiskAI.Tests.GeneratedResourceOwnerTests.SceneRestartDestroysTrackedRuntimeMeshAndMaterial | Aprobado en v19-regressions-third.xml |
| RiskAI.Tests.GeneratedResourceOwnerTests.TrackingAfterDisposeAlsoReleasesTheLateRuntimeResource | Aprobado en v19-regressions-third.xml |

La pasada dirigida añade además
RiskAI.Tests.GeneratedResourceOwnerTests.BootstrapDestroysItsOwnedRuntimeNavMeshDataOnSceneUnload.

Por nombre completo de caso, EditMode no solapa con los dos XML de PlayMode;
la pasada dirigida solapa 21 casos con la primera pasada PlayMode y añade el
caso de NavMeshData. La unión de los tres XML contiene **205 casos únicos**,
todos con resultado aprobado en su resultado más reciente. Esto no afirma que
los 127 PlayMode actuales se hayan repetido completos después de las tres
correcciones: se repitieron los 22 casos dirigidos.

La última pasada añade la regresión del oro: una concesión entre ticks debe
reflejarse en la etiqueta retenida aun pausando antes del siguiente tick.
La unión final es de **206 casos Unity únicos, 206 aprobados**, tomando el
resultado más reciente por nombre completo. Las ocho pruebas Python también
se repitieron y aprobaron.

La compilación Windows tras las regresiones terminó en
`RiskAI/Logs/v19-build-release.log` (206.252.497 bytes). La final, con la traza
opt-in y las colas navales compactadas, terminó en
`RiskAI/Logs/v19-build-final.log`: **206.256.257 bytes**. Corrige además el estado de salida y
propietario de las hogueras sin reconstruir su panel, y usa iconos vectoriales
distintos para fragata y transporte. Esta evidencia no valida Web ni ARM.

## Sonda avanzada nativa

RiskAI/Logs/v19-probe-advanced-900.log corresponde a la tercera compilación
Windows, antes de las últimas correcciones de presentación de oro y colas. En
Europe, con 16 jugadores, completó 900 segundos de calentamiento simulado y
una medición de 90 segundos a escala 1x. La población medida pasó de 380 a
293 unidades; se aplicaron 843 órdenes, con 0 rechazadas, 0 pendientes, seis
de seis móviles desplazados y cuatro supervivientes móviles.

El promedio de 8,954 fotogramas fue 10.05 ms, el máximo 36.10 ms y no hubo
fotogramas por encima de 50 ms. Las dos ventanas completas de 30 segundos a
1x registraron, respectivamente:

| Ventana | Latencia aplicar orden humana, media / máximo | Primer movimiento humano, media / máximo |
| --- | --- | --- |
| 1 | 28.36 / 55.84 ms | 68.77 / 105.96 ms |
| 2 | 22.66 / 55.02 ms | 83.03 / 305.21 ms |

Es una observación de esta semilla, equipo y build, no una comparación
antes/después con v0.18: las semillas y la evolución de la partida no son
idénticas. Tampoco valida las correcciones posteriores de presentación y
colas.

## Sonda nativa de reinicios

La compilación final también ejecutó Europe con 32 reclutas de fixture por
bando: 625→660 unidades durante 60 s a 1x, 233 órdenes aplicadas, ninguna
rechazada o pendiente, 6/6 unidades seguidas movidas y cuatro supervivientes
móviles en ambas ejecuciones. La primera registró **11,96 ms medios / 162,49
ms máximos**, con ocho fotogramas >50 ms y seis >100 ms. La repetición registró
**11,76 / 31,25 ms**, sin fotogramas >50 ms. Registros:
`v19-release-load-32.log` y `v19-release-load-32-repeat.log`.

No se elimina la primera muestra ni se presenta la segunda como una mejora
de código: ambas usan el mismo ejecutable. Los picos se concentraron al
principio de la primera ejecución; su siguiente ventana completa de 30 s
tuvo 22,48 ms de máximo. El temporizador de simulación tuvo 21,40 ms de
máximo por tick en la primera ventana, sin búsquedas marítimas. Esto acota
la investigación, pero no identifica si la espera vino de presentación,
motor, GC, controlador gráfico o trabajo externo del equipo.

La sonda ejecutó tres ciclos Europe y toma el valor después de volver al menú,
UnloadUnusedAssets y GC. Los valores de bytes son los que informa Unity; no
son una medición completa de memoria del proceso ni del navegador.

| Registro | Menú tras limpieza, ciclo 1 → 3 | Conteos tras limpieza |
| --- | --- | --- |
| RiskAI/Logs/v19-first-restart-europe.log | unityalloc 164.8 MB → 166.8 MB | 32 mallas, 176 materiales, 0 renderers en los tres ciclos |
| RiskAI/Logs/v19-second-restart-europe.log | unityalloc 174.9 MB → 184.4 MB | 32 mallas, 176 materiales, 0 renderers y 0 NavMeshData en los tres ciclos |

La segunda sonda compara correctamente el primer menú limpio con el último:
unityallocDelta=9,448,096, reserveDelta=33,554,432 y
managedDelta=552,960 bytes. Sus conteos de mallas, materiales, renderers y
NavMeshData no cambian. El primer registro no incluía contador de NavMeshData.

Los dos procesos no son una comparación antes/después de rendimiento ni
muestran mejora de memoria: el segundo termina con más bytes asignados. Los
conteos estables descartan crecimiento de esos tipos dentro de estas tres
vueltas, pero no prueban ausencia de otras retenciones. Tampoco se reprodujo
en nativo el incremento de aproximadamente 120 MB por carga visto durante
las cargas consecutivas de PlayMode del Editor; no debe atribuirse ese patrón
a una fuga del reproductor sin una medición nativa adicional.

La compilación final añadió cinco reinicios de New World
(`v19-release-restart-world.log`). Tras cada limpieza mantuvo 32 mallas,
176 materiales, cero renderers y cero NavMeshData. Los bytes asignados
fueron 166,20 / 169,97 / 185,26 / 188,22 / 191,05 MB. El incremento de
24.855.744 bytes y la reserva de +74.457.088 bytes siguen documentados;
estos contadores estables no permiten concluir que toda memoria se libere.

## Interfaz del ejecutable

La traza opt-in se ejecutó sobre la build final
(`v19-final-trace-load-32.log`): sus contadores de Main Thread están disponibles
y varían entre muestras; Render Thread y GC Allocated no están disponibles
en este player, y FrameTiming está desactivado. Se registran como ausentes,
sin inventar valores GPU. Los cuatro hitches observados corresponden al
arranque/fixture previo a la medición. La medición posterior mantuvo
625→660 unidades, 233/0/0 órdenes, 6/6 unidades movidas y 11,58/30,58 ms
medio/máximo, sin >50 ms. Al incluir instrumentación, no reemplaza las dos
sondas normales anteriores.

Se inspeccionaron capturas nativas a 1600×900, 1024×768, 360×800 y 800×360:
inicio, selección, producción, órdenes, menú, ranking y cola naval. Son
ventanas Windows a esas resoluciones, no dispositivos móviles emulados ni
pruebas de navegador. Las capturas confirmaron la corrección del oro al
pausar, el inicio visible en vertical y el texto del ranking envuelto.

[Menú](images/v0.19-menu.png) · [Selección vertical](images/v0.19-portrait.png)
· [Colas navales](images/v0.19-port-queues.png).

## Pendiente

- La instalación de Web Build Support sigue pendiente de elevación UAC.
- No se ha generado ni probado una compilación Web en navegador.
- No hay prueba ni perfil en hardware ARM físico.
- Los 22 casos dirigidos posteriores cubren las correcciones sustituidas.
  Una pasada completa adicional de PlayMode sería evidencia complementaria,
  no un requisito implícito de cierre.
- Faltan mediciones de reinicio comparables en las plataformas objetivo.
