# Validación v0.13

Unity 6000.3.23f1, Windows, URP. Verificación local del 6 de septiembre de 2026. XML, logs, binarios y capturas completas quedan fuera de Git; la selección visual propia se conserva en `docs/images/`.

## Pruebas

**101 casos distintos aprobados: 32 EditMode y 69 PlayMode.** La pasada final incluye el caso lento del ballestero y terminó a las 13:10:56 UTC.

| Ejecución | Resultado | Evidencia local |
| --- | --- | --- |
| EditMode, núcleo sin cambios posteriores | **32/32**, 0,117 s | `TestResults/editmode-v13.xml` |
| Regresión de combate tras corregir la posición de tiro | **6/6**, 30,72 s | `TestResults/playmode-v13-firing-position.xml` |
| PlayMode final, incluida regresión con fotogramas largos | **69/69**, 274,71 s | `TestResults/playmode-v13-release-final.xml` |

La suite cubre guarniciones únicas en los 19/28 puestos, reparto por semilla, cero tropas móviles/barcos iniciales, compra de IA con 4 de oro, compra naval pagada y encolada, defensa reactiva con reserva, identidad/salud del relevo, torres permanentes, separación de puestos iniciales, táctica del ballestero, formaciones, topes de refuerzo, transporte, órdenes, pausa, pools y separación entre daño y vistas.

Las pasadas previas detectaron una formación incorrecta en parejas, fixtures dependientes de los antiguos ejércitos gratuitos, esperas por frames en lugar de ticks y fuego entre puestos iniciales. Se corrigieron esos casos. Una ejecución interrumpida por falta de espacio no produjo resultados; se retomó después de que el usuario liberara disco.

El fallo intermitente del ballestero se reprodujo de forma controlada a 10 FPS y velocidad ×4: la unidad llegaba a 5,843 del defensor aunque `stoppingDistance` era 7,88. Una pasada completa 69/69 en condiciones más rápidas no bastaba para descartarlo. El ajuste final termina la ruta en una posición de tiro transitable con línea de visión, en vez de depender solo del radio de frenada alrededor del enemigo. La prueba conserva el caso lento y restaura FPS, sincronización vertical y escala temporal al terminar. La API describe `stoppingDistance` como un radio dentro del cual puede detenerse el agente, no como un límite exacto de posición. [Documentación Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AI.NavMeshAgent-stoppingDistance.html).

## Datos

`python scripts/extract_risk_circles.py` procesó 4.925 registros del `.doo`, encontró 212 círculos y los asoció a las 212 ciudades sin duplicados. Cada círculo tiene una única ciudad dentro del radio fuente de 512. El parser verifica magic/versión, longitud fija, secciones variables no admitidas, finitos y trailer. La salida conserva hashes y coordenadas nativas; no contiene recursos gráficos.

## Ejecutable e inspección visual

Compilación Windows v0.13.0 (Mono para iteración) completada: `RISKAI_BUILD_OK: 195790454 bytes`, en `RiskAI/Logs/build-v13-release-final.log`. Ejecutable: `Builds/Windows-v0.13/RiskAI.exe`.

Capturas del ejecutable a 1600×900, semilla 701, reparto por ciudades:

| Escenario | Recuento observado | Inicio sin combates |
| --- | --- | --- |
| Cuatro Riberas | 20 ciudades, 8 puertos; 28 guarniciones, **14/14**; 0 móviles y 0 barcos | 0 heridos, 0 disparos de torre |
| Las Marcas | 12 ciudades, 7 puertos; 19 guarniciones, **9/9 y 1 neutral**; 0 móviles y 0 barcos | 0 heridos, 0 disparos de torre |

Ambas secuencias generaron 13 capturas y registraron `RISKAI_PLAYER_CAPTURE_OK`, sin excepciones, en `RiskAI/Logs/player-v13-verified-expanded.log` y `player-v13-verified-classic.log`. Se inspeccionaron ciudad, isla y puerto: versión V0.13.0, guarniciones visibles, claros ante edificios, puertos separados y ausencia de barras de captura en reposo. Las capturas pausan la simulación después de verificar el arranque; no sustituyen una partida completa.

[Ciudad y reclutamiento](images/v0.13-city.png) · [Islas ampliadas](images/v0.13-island.png) · [Puerto del Paso recolocado](images/v0.13-harbor.png).

## Límites

Estas pruebas no acreditan multijugador, determinismo de NavMesh, plataformas móviles/Web, rendimiento a 200+ unidades, balance entre todas las semillas o diversión. La táctica del ballestero se demuestra contra una guarnición cuerpo a cuerpo aislada y con un flanco transitable, no contra cualquier defensa. No hay evasión automática de torres. La importación de Europe/World, el estuario y el acabado del terreno siguen pendientes.

Las métricas de Qwen están [en su informe separado](audits/QWEN-CONCURRENCY-v0.13.md); miden duración/completitud de extracciones, no rendimiento del juego ni corrección de sus respuestas. Los intentos Grok4.6 no dieron una revisión final y no cuentan como validación.
