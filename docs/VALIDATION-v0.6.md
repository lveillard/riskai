# Validación v0.6 — 5 de septiembre de 2026

Unity 6000.3.23f1, URP, Windows x64. Ejecutable: `Builds/Windows-v0.6/RiskAI.exe`. El lanzador raíz apunta a esta versión.

- **19/19 pruebas PlayMode** aprobadas (`TestResults/playmode-v06.xml`): navegación entre las 12 ciudades (66 pares), ejército inicial, combate y asedio, captura, compras y cancelación, mejoras, límite de población con todas las colas, grupos de formación, reunión, zoom normalizado y arrastre, países/refuerzos y victoria por capitales.
- **10/10 pruebas EditMode** aprobadas (`TestResults/editmode-v06.xml`): captura disputada, economía y rondas, propiedad por país, cálculos MIT de refuerzos/umbral y validación de entradas.
- Tras ajustar bosques y detalles ambientales, **5/5 pruebas PlayMode** repetidas y aprobadas (`TestResults/playmode-v06-final-terrain.xml`): 66 rutas entre ciudades, terreno llano bajo cada edificio, desniveles unidos por rampas, escarpes que bloquean caminar en línea recta, lago no transitable, zoom anclado en altura, conquista con combate y destrucción de torre. El aviso del primer ensayo de asedio provenía de teletransportar las tropas de prueba a Y=0 sobre una meseta; se corrigió el escenario de prueba para usar una posición NavMesh válida y la repetición no registra ese aviso.
- Compilación final Windows aprobada: `RISKAI_BUILD_OK: 163233496 bytes` en `RiskAI/Logs/build-v06-final.log`. Se incluyen los avisos MIT y KayKit junto al ejecutable.
- Revisión visual sobre el ejecutable real con su HUD a 1600 × 900 mediante `--riskai-capture`: vista general, ciudad/compras, meseta oriental y ayuda. El modo de captura desactiva entradas externas y pausa la simulación, para conservar el encuadre; la ejecución normal conserva los controles. Imágenes: `RiskAI/Screenshots/v06-player-{overview,city,highlands,help}.png`. La captura final registra `RISKAI_PLAYER_CAPTURE_OK` en `TestResults/player-capture-v06-final.log`, sin errores de runtime ni de shaders.

La revisión detectó y corrigió una ciudad colocada en el borde de una meseta, el ingreso mostrado en la fila del país y el aspecto inicial demasiado brillante de la cascada. La cámara usa raycast sobre terreno y una altura fija del ancla durante el zoom para evitar saltos en los bordes de escarpes.

No se ha realizado una sesión manual completa hasta victoria ni una medición de FPS/poblaciones masivas. Los modos se verificaron mediante simulación automatizada; el equilibrio económico, la IA y la semejanza visual requieren feedback de juego. Diplomacia, niebla, multijugador y audio continúan fuera de esta entrega.
