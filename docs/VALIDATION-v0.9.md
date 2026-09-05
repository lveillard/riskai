# Validación v0.9

Ejecutado con Unity 6000.3.23f1 mediante la CLI instalada de Unity Hub.

- `TestResults/editmode-v09-final.xml`: **17/17**. Economía, reparto, tabla de daño, dados discretos reproducibles y armadura negativa. Se eliminan dos tests del antiguo temporizador de captura al retirar ese código.
- **46 pruebas PlayMode distintas aprobadas**, combinando la ejecución general y las repeticiones tras correcciones. Resumen por test y archivo: `TestResults/v09-validation-summary.json`.
- Primera pasada `playmode-v09.xml`: 44/46. Detectó ciudades vaciadas al mover tropas al muelle; se corrige reservando primero las guarniciones. El otro fallo era una prueba que asumía que daño bruto = daño recibido contra armadura media; se corrige el cálculo letal y se comprueba que no se cobra dos veces la misma baja.
- `playmode-v09-final.xml`: 20/21. Captura, guarniciones, igualdad de recursos, IA tranquila, selección de semilla, barco/carga, islas, tiros de torre y asedio. El fallo restante usaba un número fijo de soldados en la cámara inicial, que ya se distribuían entre guarnición y puerto.
- `playmode-v09-terrain.xml`: **3/3**, incluyendo llegada de la formación local (con su tamaño real), cámara sobre terreno elevado y continuidad del río/islas.
- `playmode-v09-ramps.xml`: **1/1**, confirma bloqueo del acantilado, conexión por rampas, ciudades y exclusión de agua tras suavizar las transiciones.

Se conservan los informes fallidos iniciales para trazabilidad. El resumen refleja el resultado más reciente de cada prueba, no una sola pasada general sobre el último binario.

## Ejecutable y revisión visual

`RiskAI/Logs/build-v09-release.log` confirma compilación Windows correcta. Ruta: `Builds/Windows-v0.9/RiskAI.exe`. El indicador final es `RISKAI_BUILD_OK` (aprox. 197 MB).

Captura automatizada del **Player real**, con semilla 20260905 y ventana 1600 × 900: visión general, ciudad, meseta, puerto, flota, río, isla y ayuda. Archivos `RiskAI/Screenshots/v09-player-*.png`; log `TestResults/player-capture-v09-release.log`.

La primera revisión detectó una espuma de costa que cruzaba la desembocadura y el texto obsoleto `550 / 320` de la torre. Se corrigieron ambos, se reconstruyó y se repitió la captura del ejecutable. Se afinó también la máscara de desembocadura para eliminar un corte rectangular de color; ese último cambio es exclusivamente de shader.

Estas pruebas cubren el prototipo local. No constituyen validación de equilibrio competitivo ni de equivalencia completa con Saran/Rome. Ver las adaptaciones y la herencia de estadísticas en `ITERATION-v0.9.md` y `REFORGED-BASE-STATS-v0.9.md`.

Revisión final: `RISKAI_PLAYER_CAPTURE_OK` confirmado y sin excepciones de juego ni errores de shader en el Player. Inspeccionados río, ciudad, meseta, seis botones de reclutamiento y ayuda. Compilación final: **197015084 bytes**. Lanzador actualizado a v0.9 y partida normal abierta con registro `TestResults/player-v09-live.log`.
