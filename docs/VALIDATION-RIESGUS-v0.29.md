# Riesgus v0.29 — agua, torres y virotes

22 de septiembre de 2026. **PUBLICADA** en https://riesgus.com.
221 casos Unity aprobados; Windows/Web compilados y tres comprobaciones Web
locales completadas. Archivos públicos verificados 10/10.

## Cambios visuales aceptados

- La cama visual sumergida continúa hasta 1,8 m. Es una capa visual y no añade
  colisión ni cambia la navegación.
- El canal alfa de la textura de costa codifica profundidad 0–2 m y conserva
  el RGB de navegación. El agua azul deja ver el fondo en aguas compartidas
  de `0,27 / 0,538 / 0,75 m`; se vuelve opaca a 1,45 m, antes de terminar
  la cama visual. Mar abierto conserva su opacidad desde 0,45 m.
- El ruido fino está rotado para evitar venas sinusoidales.
- Las torres integran parapetos de piedra en una misma malla de 72 vértices:
  cuatro renderers para esa pieza y diez renderers en el conjunto, sin aumento
  del total observado.
- Los muelles integrados añaden tres piezas visuales de unión, sin colisión.
- El virote queda en cuatro renderers, sin emisión en punta, asta ni aletas
  (aproximadamente 52 vértices). Gameplay y el pool de 128 elementos no cambian.

## Evidencia disponible

Previews definitivas aceptadas visualmente por `root`:

- `Captures/water-v029-world-final/newworld-bolt-detail-z1.65.png`
- `Captures/water-v029-world-final/newworld-integrated-town-z12.png`
- `Captures/water-v029-world-final/newworld-newworld-033-near-z12.png`
- `Captures/water-v029-world-final/newworld-newworld-033-normal-z22.png`
- `Captures/water-v029-world-final/newworld-open-sea-z22.png`

El log de la revisión termina correctamente (`exit 0`):
`RiskAI/Logs/water-v029-world-final.log`.

## Validación

- [x] EditMode: 178/178 aprobados, sin fallos ni omitidos
  (`TestResults/v029-editmode.xml`).
- [x] PlayMode: 43/43 aprobados, sin fallos ni omitidos
  (`TestResults/v029-playmode-final.xml`). Incluye óptica, costa, geometría,
  selección, los dos mapas importados, navegación naval, capturas, torres,
  daño al llegar el virote y reutilización de proyectiles. Es una batería
  seleccionada, no todas las clases PlayMode del repositorio.
  La primera ejecución obtuvo 42/43: una comparación geométrica contra cero
  falló por un residuo de `1,49e-8 m`. La repetición usa tolerancia `1e-4 m`
  en ese test; no se cambió el comportamiento para resolverlo.
- [x] Windows compilado en `Builds/Windows-v0.29.0/RiskAI.exe`:
  `RISKAI_BUILD_OK: 212143713 bytes` en `RiskAI/Logs/build.log`.
- [x] Web compilada en `Builds/Web-v0.29.0`:
  `RISKAI_WEB_BUILD_OK: 57649317 bytes` en `RiskAI/Logs/build-web.log`.
- [x] Reproductor Web real (Edge), todos `success=true`, `errors=[]`:
  Europe 1600×900/DPR 1 (17 capturas), World 390×844/DPR 2 (16) y
  Cuatro Riberas 1024×768/DPR 1 (15). Incluye selección, compras, colas,
  puertos, ingresos, clasificación, mapa estratégico y vistas de costa.
  Informes `Captures/v029-{europe,world,riverlands}-final/result.json`.
  Los tamaños móvil/tablet son emulación de escritorio, no hardware físico.
- [x] Release `20260922T191712Z-water-v029` activado y 10/10 hashes públicos
  idénticos al manifiesto. Recibo y verificación en
  `.deploy/20260922T191712Z-water-v029/{receipt,public-verification}.json`.
  Se conserva `20260922T173612Z-pareto-v028` y su orden de rollback en el recibo.
  IP de salida verificada `176.223.53.50`, ya autorizada: sin cambios de NSG
  ni nuevos tokens Cloudflare. Se mantiene el Worker/proxy del dominio.
- [x] Partida pública en https://riesgus.com: World 390×844/DPR 2,
  `success=true`, sin errores, 16 capturas, 64,56 s. Selección, compras,
  colas, clic real en ingresos/clasificación y puerto comprobados en
  `Captures/v029-public-world-final/result.json`. `www` responde 301 al apex.
  Servidor local temporal cerrado al acabar; versiones anteriores conservadas.

## Comprobación de recursos

Europe normal, semilla 19031, 16 jugadores, Edge 1600×900/DPR 1,
30 s medidos tras calentamiento, sin otra prueba ni build nuestra simultánea:
`Captures/resources-v029-final/result.json`, `success=true`, sin errores.

- Cadencia de `requestAnimationFrame`: 53,8 Hz, media 18,589 ms,
  p95 33,3 ms, máximo 34 ms, ningún intervalo mayor de 50 ms.
- Hilo principal del navegador ocupado 94,9 %. CPU del proceso renderer
  100,5 % de **un núcleo**, no del equipo completo; GPU-process 43,2 %
  es también CPU de ese proceso, no utilización de GPU.
- Buffer WASM 492,25 MiB; memoria privada del renderer 857,76 MiB.
  No son magnitudes sumables ni una medida de RAM exclusiva.
- Assets comprimidos de Web: 57.628.078 bytes (v0.28: 57.630.719),
  prácticamente el mismo tamaño de descarga.

Es una observación corta en el portátil de desarrollo, no FPS del profiler
Unity, benchmark móvil físico, carga de 900 unidades ni A/B controlado.
La CPU sigue cerca de un núcleo completo; esta revisión artística no afirma
resolver ese límite de rendimiento.

La revisión no añade texturas ni pases de agua. Reutiliza la textura de roca
filtrada y el canal alfa del campo costero existente; sí extiende los triángulos
visuales del fondo y cambia el cálculo del shader. Conservar los renderers de
la torre no implica por sí solo coste de GPU idéntico. No se atribuye una
mejora de FPS a estos cambios artísticos sin una medición comparable.

Límite visual preexistente: algunos rótulos flotantes largos de dos líneas
siguen recortados y pueden cubrir parte de la almena en el acercamiento
diagnóstico; el nombre completo queda disponible en el panel de selección.
No se ha modificado ese sistema de rótulos en esta revisión.
