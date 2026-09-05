# Validación v0.7 · 5 septiembre 2026

Unity 6000.3.23f1 / URP / Windows x64, mediante Unity Hub CLI.

## Reglas y juego

- 17/17 EditMode: `TestResults/editmode-v07.xml`.
- 30 pruebas distintas de PlayMode aprobadas. Ejecución general: `playmode-v07.xml`; comprobaciones posteriores de cámara/combate: `playmode-v07-camera-combat.xml`, `playmode-v07-final.xml` y `playmode-v07-drag.xml`. Reinicio con una semilla de lanzamiento diferente: 2/2 en `playmode-v07-seed-restart.xml`. Se conservan los informes anteriores con sus fallos y los resultados corregidos. El resultado más reciente de cada prueba está en `TestResults/v07-validation-summary.json`.
- Las pruebas verifican las 66 rutas entre ciudades, terrenos elevados y rampas, cimentación plana, laguna no transitable, navegación de todas las unidades iniciales, economía por países, refuerzos vivos, captura y victoria, colas y límites, órdenes RTS, zoom/arrastre y selección.
- Reparto: 6/6 ciudades o 2/2/8 neutrales, una capital por bando, selección inicial correcta, semillas repetibles, variación por semilla y sobrantes neutrales en topologías impares.
- Combate: torres dañan neutrales y enemigos, ausencia de fuego amigo, bloqueo de visión por terreno, mortero fuera del alcance de la torre, tipo de proyectil conservado tras morir su fuente e impacto único. Compra y cancelación del mortero verificadas.
- La prueba de arrastre configura temporalmente el Input System para enviar eventos al buffer del jugador en el Editor sin foco; restaura sus ajustes después. La geometría del obstáculo LOS se sincroniza con física antes de comprobar el disparo.

## Compilación y arte

`RiskAI/Logs/build-v07-final.log`: `RISKAI_BUILD_OK: 188465644 bytes`.
Ejecutable: `Builds/Windows-v0.7/RiskAI.exe`. El lanzador raíz apunta a esa versión.

Previsualización final de bosque y roca: `RiskAI/Logs/preview-v07-foliage.log`, `RiskAI/Screenshots/v07-world-bastion.png` y `v07-world-highlands.png`.
Las texturas se describen en [ImageGen v0.7](IMAGEGEN-v0.7.md); las cifras y el reparto de referencia están auditados en [unidades](REFORGED-UNIT-STATS.md) y [asignación](REFORGED-ALLOCATION.md).

Capturas finales del ejecutable a 1600 × 900: `RiskAI/Screenshots/v07-player-overview.png`, `v07-player-city.png`, `v07-player-highlands.png` y `v07-player-help.png`. Log `TestResults/player-capture-v07-final.log` con `RISKAI_PLAYER_CAPTURE_OK`, sin excepciones del juego.

## Límites

Es una adaptación local de 12 ciudades contra IA. No reproduce las decenas de unidades, barcos, diplomacia ni todos los modos de Reforged. El equilibrio de vida, costes, distancias y tiempos es propio; los multiplicadores usados de ataques/armaduras sí siguen las columnas aplicables de su tabla. La simulación completa no es determinista: la semilla solo repite el reparto inicial. No se ha realizado una partida manual completa ni un benchmark de cientos de unidades. Audio, niebla de guerra, guardado y multijugador siguen pendientes.
