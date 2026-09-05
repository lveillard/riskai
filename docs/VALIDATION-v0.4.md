# Validación gráfica v0.4

Fecha: 5 de septiembre de 2026. Unity 6000.3.23f1, URP, Windows x64.

Compilación Windows aprobada: `RISKAI_BUILD_OK`, 146.426.398 bytes. Ejecutable: `Builds/Windows-v0.4/RiskAI.exe`. Registro: `RiskAI/Logs/build-v04.log`. El lanzador de raíz abre esta versión.

## Juego

Las 13 pruebas PlayMode pasaron en 63,16 segundos tras sustituir edificios, vegetación y puentes. Informe: `TestResults/playmode-v04.xml`. Incluye navegación por los puentes, combate/captura, torres, reclutamiento, formaciones, selección y cámara. Después se añadieron sombras visuales sin colliders y ajustes de muestreo de los materiales.

## Imagen

`RiskAI.Editor.RiskWorldPreview.Capture` construye la escena real con el mismo `RiskBootstrap` que utiliza el juego, coloca los modelos en pose Idle y la renderiza con una cámara Unity a 1920 × 1080. No es un montaje ni una ilustración del resultado pretendido. Estos renders muestran el mundo 3D; no incluyen el HUD que Unity compone después de la cámara.

- `RiskAI/Screenshots/v04-world-overview.png`: vista general.
- `RiskAI/Screenshots/v04-world-bastion.png`: vista cercana del bastión.

Se inspeccionaron los renders finales tras los ajustes de materiales y sombras (`RiskAI/Logs/preview-v04-final.log`). No se registraron errores de compilación C# o shaders en esa ejecución.

La inspección detectó y corrigió contaminación entre cuadrantes del atlas, juntas del terreno y un patrón de agua excesivamente regular. Los personajes conservan sus modelos KayKit; el rediseño se centra en entorno, arquitectura y materiales. No se ha medido un objetivo de FPS en el ejecutable.

La captura interactiva del HUD se intentó mediante el editor, pero quedó bloqueada antes de cargar el proyecto por la ventana inicial de términos. Computer Use devolvió `GetCursorPos failed: Access is denied. (0x80070005)`. No se realizaron acciones sobre permisos de Windows ni se modificaron sus ajustes.
