# Validación v0.15

Unity 6000.3.23f1, URP, Windows. Verificación local del 6 de septiembre de 2026. Los XML, logs, ejecutables y capturas completas permanecen fuera de Git.

## Pruebas

**120 casos distintos con su última ejecución aprobada: 44 EditMode y 76 PlayMode.** Se hizo una pasada completa y repeticiones dirigidas tras las correcciones; no existe un único informe final de 120 casos.

| Ejecución | Resultado | Evidencia local |
| --- | --- | --- |
| EditMode final | 44/44, 0,514 s | `TestResults/v15-editmode-release.xml` |
| PlayMode completo | 75/76, 354,090 s | `TestResults/v15-playmode.xml` |
| Cámara, controles, integración y combate entre 16 jugadores | 25/25, 102,007 s | `TestResults/v15-playmode-controls-final.xml` |
| Mismos controles más calibración visual | 25/26, 78,836 s | `TestResults/v15-playmode-scale-final.xml` |
| Calibración visual corregida | 1/1, 1,941 s | `TestResults/v15-model-scale-release.xml` |

El fallo del PlayMode completo estaba en la expectativa del nuevo test de botín: exigía un oro por un espadachín, cuando la regla fuente acumula un cuarto del valor de puntos. La prueba corregida exige cero oro entero tras una baja y un oro al completar cuatro; no se aumentó el botín del juego.

La calibración visual detectó que medir el rig con `BakeMesh(false)` y transformar después sus vértices contaba su escala incorrectamente. Con `BakeMesh(true)` la medida local es estable. La prueba mantiene una tolerancia de 0,01 unidades y verifica ballestero, sanador, guardia y mortero, sus radios físicos y que la cabeza siga dentro de la caja de selección. El caso del ballestero reutiliza la medida en caché creada para las guarniciones iniciales.

Las nuevas pruebas también verifican:

- Europe y New World reales con 16 propietarios: todos reciben ciudad reclutable, cuatro de oro y cero móviles gratis; las guarniciones neutrales usan el identificador 16.
- Los 15 comandantes reclutan su primera unidad usando su propio presupuesto al avanzar la simulación hasta 35 segundos.
- El jugador 15 puede ordenar, combatir, cobrar botín y capturar; eliminar a un rival no termina la partida mientras quedan otros, y sostener el objetivo territorial sí permite ganar aunque sobrevivan tropas rivales.
- Conversión numérica de distancias y colisión, encuadre del rectángulo W3I, cámara de partida extraída del JASS y lectura del nibble correcto de suelo W3E.
- Relieve opcional sin cambiar coordenadas de los puntos de juego ni muestras de costa/agua, claros alrededor de ciudades y aplicación idempotente.

## Ejecutable y revisión visual

Build Windows v0.15.0, Mono para iteración: **205.353.632 bytes** según el informe de compilación. `RISKAI_BUILD_OK` en `RiskAI/Logs/v15-build-final.log`. Ejecutable: `Builds/Windows-v0.15/RiskAI.exe`; acceso local mediante `Play-RiskAI.cmd`.

Capturas del ejecutable final a 1600×900, semilla 160212, 16 jugadores, reparto por ciudades:

| Escenario | Ciudades / puertos independientes | Defensores totales | Reparto inicial de ciudades | Móviles / barcos |
| --- | --- | ---: | --- | --- |
| Las Marcas | 18 / 7 | 25 | 1 por jugador, 2 neutrales | 0 / 0 |
| Cuatro Riberas | 20 / 8 | 28 | 1 por jugador, 4 neutrales | 0 / 0 |
| Europe | 212; 44 puertos incluidos | 212 | 13 por jugador, 4 neutrales | 0 / 0 |
| New World | 293; 59 puertos incluidos | 293 | 18 por jugador, 5 neutrales | 0 / 0 |

Los puertos independientes de las arenas pequeñas se sortean por separado entre participantes elegidos por semilla: pueden aportar un defensor adicional a algunos bandos. Todos empiezan con cuatro de oro. Las cuatro escenas registran cero heridos y cero disparos de torre al inicio.

Evidencia: `RiskAI/Logs/v15-release-player-<mapa>.log` y `Captures/v15-release-<mapa>/`. Se revisaron vistas de ciudad, mapa completo, Alpes, menú y panel de 16 jugadores, así como el secano de Las Marcas. El último shader reduce la mancha blanca de nieve detectada en la primera compilación. Las capturas pausan la simulación tras observar la apertura; no acreditan equilibrio ni rendimiento de partidas prolongadas.

[Europe](images/v0.15-europe.png) · [New World](images/v0.15-newworld.png) · [Panel de participantes](images/v0.15-players.png).

## Límites

La equivalencia 1:1 cubre coordenadas y campos numéricos bajo una conversión común de unidades; no significa igualdad completa de arte o navegación. Cuatro tipos tienen altura de espera calibrada. El compuesto propio de ciudad/torre y las copas de árboles siguen pendientes de calibración; las siluetas y animaciones son propias. Los árboles importados no añaden bloqueadores mientras no se resuelva su pathing fuente. El nuevo Rin se pospone porque su traza invadía claros de ciudades; el agua base sigue siendo W3E, con costa todavía angular.

Las cordilleras son una opción local y se pueden desactivar al preparar una partida. No hay benchmark de 16 ejércitos desarrollados, ni validación Web/Android, touch o servidor remoto. Los oponentes son IA locales sin niebla de guerra ni desembarcos tácticos. [Escala y procedencia](MAP-SCALE-v0.15.md) · [Pendientes](../TODO.md).
