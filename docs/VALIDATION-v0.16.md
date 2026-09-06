# Validación v0.16

## Pruebas y compilación

**140 casos distintos pasan: 48 EditMode y 92 PlayMode.** Se combinó la suite inicial con repeticiones dirigidas después de las correcciones, conservando el resultado más reciente de cada caso vigente. No se suman repeticiones ni nombres de pruebas reemplazadas como pruebas adicionales.

Los XML quedan en `TestResults/v16*.xml`, ignorados en Git. La suite inicial PlayMode pasó 83/85; las repeticiones de refuerzos y puertos resolvieron sus fallos. Las últimas tandas comprueban además límites compartidos de colas, embarque con cambio de selección/foco, fallos de patrulla, movilización de refuerzos de IA, ruta incompleta hacia una isla y centrado tras reducir el zoom. El caso de la isla reveló un bucle real de peticiones de ruta: se corrigió y pasó en `v16-playmode-path-verified.xml`. Cámara: 3/3 en `v16-playmode-camera-final.xml`. EditMode: 48/48 en `v16-editmode-final.xml`.

Las pruebas cubren mapas Europe/New World, 16 bandos, daño y dados, ingresos por ciudad, créditos de país, captura/torres, Marines, fragata, transporte, colas y navegación. Las dos rondas reales de [Grok 4.6](audits/GROK-v0.16.md) se contrastaron con código y fuente; se rechazaron sugerencias que cambiaban reglas deliberadas del JASS.

**Build Windows v0.16.0: 205.449.087 bytes**, Unity 6000.3.23f1, URP, Mono de iteración. Resultado `RISKAI_BUILD_OK` en `RiskAI/Logs/v16-build-water-final.log`. El primer intento descubrió un error del generador de arte al recorrer perfiles que comparten modelo; la generación ahora procesa cada recurso una sola vez. Ejecutable: `Builds/Windows-v0.16/RiskAI.exe`; acceso mediante `Play-RiskAI.cmd`.

## Revisión visual del ejecutable

Capturas a 1600×900, semilla 160212 y 16 jugadores en `Captures/v16-verified-<mapa>/`; logs en `RiskAI/Logs/v16-verified-player-<mapa>.log`.

| Escenario | Ciudades / puertos | Guarniciones | Ciudades por jugador / neutrales |
| --- | --- | ---: | --- |
| Las Marcas | 18 + 7 puertos independientes | 25 | 1 / 2 |
| Cuatro Riberas | 20 + 8 puertos independientes | 28 | 1 / 4 |
| Europe | 212, incluidos 44 puertos | 212 | 13 / 4 |
| New World | 293, incluidos 59 puertos | 293 | 18 / 5 |

Los cuatro escenarios registraron cero heridos iniciales, cero disparos de torre y `RISKAI_PLAYER_CAPTURE_OK`, sin excepciones de gameplay ni errores de shader en sus logs. La apertura normal conserva cuatro de oro por jugador, cero tropas móviles y cero barcos gratis. Los puertos independientes de las arenas pequeñas se reparten aparte. Las capturas de caballero y flota añaden esas unidades **después** de registrar la apertura y pausan la simulación: son escenas de inspección visual, no una afirmación sobre el reparto inicial.

Se revisaron selección conjunta de casa/torre, retrato y montura del caballero, perfiles del puerto, distintas siluetas de barcos, marcadores con Tab, overlay del país, claros y vegetación, suelo y agua. La inspección encontró y corrigió el centrado de cámara que retenía límites del zoom anterior y la etiqueta duplicada de los puertos importados.

[Ciudad y selección](images/v0.16-city.png) · [Caballero](images/v0.16-knight.png) · [Naval y agua](images/v0.16-naval.png) · [Tab](images/v0.16-scores.png) · [Hoguera](images/v0.16-camp.png) · [Cuatro Riberas](images/v0.16-riverlands.png)

## Respuesta y rendimiento

Ambas sondas finales terminan con `success=True`, 70 segundos de simulación, todas las tropas azules seguidas en movimiento y cero órdenes rechazadas o pendientes al finalizar. Equipo: Intel Core Ultra 9 285H y NVIDIA RTX 5080 Laptop, 1600×900, semilla 160212.

| Mapa | Media / máximo de fotograma en el segundo tramo de 30 s | Unidades al registrar ese tramo | Órdenes aplicadas en 70 s | Tropas azules movidas |
| --- | --- | ---: | ---: | ---: |
| Europe | 9,49 / 14,15 ms | 314 | 103 | 5/5 |
| New World | 10,93 / 16,61 ms | 400 | 95 | 4/4 |

Logs: `RiskAI/Logs/v16-verified-probe-europe.log` y `v16-verified-probe-newworld.log`. La primera ventana incluye un fotograma de arranque de 4,11 s / 4,63 s respectivamente, por generación y preparación de la escena; no se oculta dentro de la media estable. La medición todavía no equivale a una partida larga ni a un perfil detallado de CPU/GPU. Los cambios de heap del segundo tramo fueron +389.120 B / +581.632 B y las cuentas Gen0 45 / 31; se registran como diagnóstico, no como tasa de asignación.

La sonda `--riskai-probe` ejecuta 70 segundos reales con 16 bandos y las IA activas, añade hasta seis tropas móviles por bando donde hay un punto terrestre válido y envía órdenes a unidades del jugador cero cada dos segundos. Es una carga controlada de diagnóstico: no mide el input físico, equilibrio prolongado, miles de unidades ni dispositivos móviles. Se distingue la primera ventana, que incluye generación/bake, del tramo estable posterior. El cambio de heap no equivale a bytes asignados ni prueba ausencia de fugas.

## Límites

La costa importada todavía conserva escalones de la malla W3E; el agua y suelo nuevos no eliminan esa limitación geométrica. La superposición territorial es una aproximación por proximidad, no polígonos políticos extraídos. Faltan pathing de World Editor, calibración de edificios/árboles y variantes visuales de Marines. La capacidad de seis pasajeros es local, no un dato de `A00V`.

NavMesh, compra/naval fuera del inbox, IMGUI y parte de las misiones en el adaptador de input siguen necesitando separación antes de servidor autoritativo. Touch, Web/Android, niebla, guardado y multijugador no se han implementado ni validado en esta entrega. No se afirma que las incidencias de la sesión anterior fueran todas el mismo bug.
