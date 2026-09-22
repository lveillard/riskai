# Riesgus v0.26.1 · zoom de touchpad

La actualización atenúa los eventos finos de scroll en navegador conservando
los pasos de rueda habituales y el zoom proporcional de dos dedos en pantalla
táctil. No cambia reglas, mapas, economía ni rendimiento de la simulación.

## Cambio

El puente lee cada evento DOM antes de que InputSystem los acumule por frame.
Los eventos pixel de magnitud ≤40 reciben un cuarto de sensibilidad; los de
≥80 mantienen la sensibilidad de rueda. Entre ambos se interpola suavemente.
Es una heurística sobre el evento, no detección infalible de hardware. Los
eventos de líneas/páginas se normalizan por sus propias unidades.

La rueda habitual de 100/120 píxeles mantiene su respuesta. La ruta de ratón
nativa de Unity conserva su configuración. `ZoomByRatio` mantiene el cociente
del gesto táctil. El puente se consume incluso al bloquear input por HUD/modal
y se vacía al perder foco, salir del canvas o caducar una muestra.

No se confirmó la hipótesis inicial de que Unity convirtiera todo delta pequeño
en un paso completo: el backend conserva fracciones. El cambio real reduce la
ganancia de los eventos finos y conserva su granularidad antes de acumularlos.

## Pruebas

- Suite EditMode: 159 casos aprobados. Tras afinar la sensibilidad, los 13 casos
  de `ZoomResponseTests` se volvieron a comprobar.
- PlayMode: 12 casos aprobados de cámara/ratón, gesto táctil, HUD y modales.
- Puente JavaScript: 17 pruebas aprobadas.
- Builds Windows y WebGL 0.26.1 correctas.
- WebGL real, partida pausada: el hook observó las muestras que consumió C#,
  sin retirarlas ni sustituirlas. Capturas antes/después inspeccionadas.
- 40 eventos de 5 px: suma fina 200, equivalente a 0,5 pasos; multiplicador
  derivado ×1,1275 frente a ×1,6161 anterior. El exponente se reduce cuatro veces.
- Ráfaga CDP de 10×5 px separada por frames, rueda 100/120, dirección inversa
  y Ctrl+rueda correctos. Escala del navegador, DPR y viewport no cambian.
- Scroll real sobre el HUD y retorno al mapa: encuadre conservado, sin repetición
  residual visible. Consola sin errores.
- Móvil emulado 390×844 DPR 2: cambio a español y toque de inicio correctos.

Los cocientes del test web se calculan a partir de los deltas consumidos y la
política probada en Unity; no se presentan como lectura directa de TargetZoom.
Las capturas verifican visualmente la integración. No se probó hardware físico.

## Publicación

Release: `20260922T121400Z-zoom-v0261`, con recuperación conservada a
`20260922T015400Z-riesgus-v026`. El proxy Cloudflare y DNS no cambian; la
actualización se publica en el origen Azure con URLs de assets versionadas.
No se recrearon ni reutilizaron los tokens Cloudflare revocados.

Se verificaron los diez archivos públicos contra el manifiesto, y se repitió
la prueba completa de zoom en `https://riesgus.com`: correcta, sin errores,
incluyendo eventos CDP, dirección inversa, Ctrl+rueda y retorno del HUD al mapa.
[`Verificación pública`](audits/zoom-v0.26.1/public-verification.json) ·
[`Prueba de zoom publicada`](audits/zoom-v0.26.1/browser-production.json).

Archivo de despliegue SHA-256:
`6f35f531ec6bce86fc55f90fa34b56e819e3aaa3db056bea9c80f80ff9ed46de`.
Recibo local: `.deploy/20260922T121400Z-zoom-v0261/receipt.json`.

[Resultados de recursos](audits/RESOURCES-v0.26.md) y
[Moscov y reutilización del proyecto WC3](audits/EUROPE-MOSCOV-AND-REUSE-v0.26.md).
Evidencia local de zoom en `Captures/zoom-v0261-local/`, resumida en
[`browser-local.json`](audits/zoom-v0.26.1/browser-local.json).
