# Validación v0.8

- Unity 6000.3.23f1 / URP. Build Windows: `Builds/Windows-v0.8/RiskAI.exe`.
- Build de entrega: `RiskAI/Logs/build-v08-release.log`, `RISKAI_BUILD_OK: 196926220 bytes`.
- 17/17 pruebas de EditMode: `TestResults/editmode-v08.xml`.
- 34 pruebas distintas de PlayMode pasan al combinar el informe completo y las repeticiones pertinentes: `TestResults/v08-validation-summary.json`.
- El primer informe `playmode-v08.xml` dio 32/34. Dos pruebas presuponían suelo bajo a Y=0; el nuevo relieve tiene colinas de aproximadamente un metro. Se corrigieron para medir separación vertical entre terrazas y permanencia en XZ de la orden Mantener. El informe `playmode-v08-final.xml` dio 7/7, incluidas ambas y los cinco casos de combate de torres. Los informes iniciales se conservan.
- La prueba de rutas comprueba las 66 parejas de ciudades, superficies planas de edificios y bloqueo de la pared del acantilado. También se verificaron zoom anclado, cámara, selección, combate, compra, mejora y semillas de reparto.
- La prueba naval recorre físicamente el océano desde un puerto hasta una isla con tres soldados embarcados: comprueba margen del casco, llegada, ausencia de captura desde la carga, desembarque sobre NavMesh, salud/identidad/población conservadas y posterior conquista. Otros casos comprueban disparos, muerte de pasajeros, compra, cancelación y reembolso al cambiar de dueño el puerto, y niveles decrecientes del río.
- Tras esas pruebas se revisaron en el ejecutable los cambios de presentación: muelles, velas, retratos por color y malla continua del agua. Capturas reales con HUD: `RiskAI/Screenshots/v08-player-{overview,city,highlands,harbor,fleet,river,island,help}.png`. Las capturas se realizan con entrada desactivada y partida pausada; no son sustituto de las pruebas de simulación.

## Límites de esta entrega

La IA mueve galeras y la infantería continental; todavía no prepara invasiones en transportes. Los puestos insulares aportan oro pero quedan fuera del contador de doce ciudades para victoria. El balance naval es una adaptación inicial, no una reproducción completa de las variantes Saran/Rome. Agua y relieve son geometría y shaders, no simulación de fluidos ni herramienta de edición de terreno durante la partida. Los modelos de personajes siguen siendo provisionales.

Captura final de entrega: TestResults/player-capture-v08-release.log, RISKAI_PLAYER_CAPTURE_OK. Sin excepciones de juego ni errores de compilación de shader. El aviso D3D12 de la interfaz de depuración de la tarjeta también aparece en versiones anteriores.
