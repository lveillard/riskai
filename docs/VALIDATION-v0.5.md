# Validación v0.5 — 5 de septiembre de 2026

## Resultado

- Suite PlayMode: **14/14**, 66,25 s. Informe: `TestResults/playmode-v05.xml`.
- Después de ajustar el encuadre inicial: **2/2** pruebas de cámara y rueda, 2,27 s. Informe: `TestResults/camera-v05.xml`.
- Después de prolongar el paisaje exterior: **1/1** prueba de mapa, 4,54 s. Informe: `TestResults/map-v05.xml`.
- Build Windows terminado correctamente, **154.819.736 bytes** según Unity. Ejecutable: `Builds/Windows-v0.5/RiskAI.exe`; registro: `RiskAI/Logs/build-v05.log`.

La suite comprueba 12 ciudades, 48 soldados iniciales, rutas entre los 66 pares de ciudades, agua no navegable, formaciones, captura, ataque y retirada, asedio, reclutamiento, reembolsos, mejora de ciudad, pausa, refuerzos y límites de población. El asedio verifica accesos desde ocho lados y la destrucción real de una torre por infantería.

La regresión de rueda introduce un MouseState con scroll normalizado de +1 en Input System y llama al controlador real tras procesar el evento. En batch se hace explícitamente porque no hay una Game view enfocada. Comprueba un acercamiento superior al 15 % y conserva el punto bajo el cursor. Otra prueba comprueba perspectiva, transición sin salto, arrastre del terreno y desplazamiento lateral con la cámara girada.

## Revisión visual

Se han inspeccionado capturas del ejecutable Windows con el HUD a 1600 × 900, incluyendo vista del ejército y panel de compra de una ciudad. La captura general final está en `RiskAI/Screenshots/v05-player-overview.png`. Se ha comprobado el encuadre inicial, la eliminación de los cortes rectos del terreno, el tamaño de unidades/edificios, el color de las etiquetas y la legibilidad de los paneles. No equivale a una partida completa jugada manualmente.

El comando de diagnóstico es `RiskAI.exe --riskai-capture <DIR>`. Pausa la simulación, genera dos capturas y cierra esa instancia. La ventana debe estar visible: Windows no produjo imágenes con la ventana oculta. No afecta al arranque normal. El registro del último arranque de captura contiene `RISKAI_PLAYER_CAPTURE_OK` sin errores de captura ni excepciones. Las capturas iniciales del editor, `v05-world-*.png`, son anteriores a los últimos ajustes; la referencia de entrega es la captura del jugador.

## Límites

El arte continúa siendo provisional y los modelos KayKit y el marco de la interfaz difieren de Warcraft III. La IA es local y sencilla; no se ha certificado equilibrio de una partida completa de 12 ciudades ni rendimiento a gran escala. Los cambios de paisaje exterior son decorativos, sin colliders, y no amplían el NavMesh jugable.
