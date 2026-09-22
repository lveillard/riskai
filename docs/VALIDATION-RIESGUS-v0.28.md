# Riesgus v0.28.0 — rendimiento y recursos

22 de septiembre de 2026. [Experimentos, datos y límites del A/B](audits/PERFORMANCE-PARETO-v0.28.md).

## Cambios aceptados

- Los controladores visuales `SoldierAnimator` y `MountedKnightView` dejan de
  actualizarse fuera de cámara. La clasificación se refresca cada 0,1 s con
  margen alrededor del viewport. Al volver a cámara se recupera el estado
  actual, incluido el punto de contacto del ataque.
- Cámara, LOD y selección no detienen soldados, navegación, IA ni combate.
  La visibilidad por cámara no sobrescribe los estados de renderers ni de
  `Animation`; conserva el culling nativo de Unity ya existente.
- Las mallas procedurales creadas por `Cone`, `Roof` y `Banner` comparten ahora
  explícitamente la vida del objeto padre. No se destruyen mallas primitivas
  ni assets compartidos de Resources.

La geometría, resolución, sombras, texturas y reglas de partida no se recortan.
No hay migración a DOTS ni cambio de motor.

## Experimentos no activados

El batching nativo y la combinación manual por material quedan **desactivados
por defecto**. La caída de draw calls (~16–17%) no bastó para demostrar una
mejora de CPU convincente. La variante manual además hizo crecer el heap WASM
de una partida normal aproximadamente 96,5 MiB. Se conservan como controles
diagnósticos explícitos, no como una optimización publicada por defecto:

- `--riskai-native-architecture-batching`.
- `--riskai-manual-architecture-batching`.
- `--riskai-disable-architecture-batching` prevalece sobre ambos.
- `--riskai-disable-unit-presentation-culling` permite comparar la animación.

En Web se utilizan las mismas claves de query sin `--`, con valor `1`.

## Validación

La configuración final aprueba 173 casos EditMode y 27 PlayMode: 200 casos,
sin fallos. Dos pruebas de torres de las variantes opt-in se omiten de forma
intencionada al estar desactivada la agrupación; sus rutas se comprobaron en
las candidatas nativa y manual anteriores. No es una ejecución de todas las
clases PlayMode del repositorio.

Incluye geometría/selección, variantes de mapa, apertura y navegación de los
dos mapas importados, animación al reentrar en cámara, reutilización de
unidades y vida de recursos procedurales. Informes locales:
`TestResults/v028-release-editmode.xml` y
`TestResults/v028-release-integration.xml`. Las variantes experimentales
conservan sus informes `v028-presentation.xml` y
`v028-manual-integration.xml`.

Los 17 checks del puente de pen/rueda y los checks del Worker de Cloudflare
siguen pasando; los scripts de diagnóstico Python compilan correctamente.

Windows compilado en `Builds/Windows-v0.28.0/RiskAI.exe`, accesible desde
`Play-Riesgus.cmd`. Web final compilada en `Builds/Web-v0.28.0`.

La revisión Web final supera Europe a 1600×900/DPR 1 y la comprobación pública
de New World a 390×844/DPR 2: selección, compras, colas, acciones del ejército,
ingresos, clasificación y puertos, sin errores. La comprobación pública terminó
con `success=true`, `errors=[]`, 16 capturas y 58,41 s; el clic de oro y la
clasificación también fueron correctos. Informes:
`Captures/v028-europe-final/result.json`,
`Captures/v028-world-final/result.json` y
`Captures/v028-public-world-final/result.json`. Las capturas locales son de
candidate2 con la
agrupación desactivada explícitamente, equivalente a los defaults visuales
finales; el último cambio de ownership sólo afecta a liberar recursos.
Las primeras llamadas usaron IDs abreviados incorrectos en el test; se
repitieron con `europe-033` y `newworld-033`, sin cambiar código del juego.
La prueba pública es emulación de viewport/touch, no hardware ARM físico.

La inspección visual detecta una limitación ya presente en las placas
flotantes: algunos nombres largos de dos líneas, como `(Russia) 33`, quedan
recortados. El panel de selección conserva el nombre y las acciones. Esta
ronda de rendimiento no modifica esas placas ni afirma que toda la interfaz
esté libre de defectos.

## Rendimiento: alcance

Las ventanas exploratorias sugieren un ahorro modesto con sólo culling
visual, sin crecimiento del heap en la comparación normal. La variación
temporal del equipo impide prometer un porcentaje fijo de mejora.

El fixture de 900 unidades mantiene cohorte, órdenes y navegación, pero
desactiva IA/combate/refuerzos: no equivale a 900 unidades combatiendo.
El hilo principal WebGL sigue saturándose. El diagnóstico que oculta unidades
separa parte del coste de presentación, pero no es un resultado jugable y
también afecta animación y sombras. No se midió móvil físico ni VRAM aislada.

La exportación final, sin flags experimentales, supera el probe completo:
fixture `15092308`, 900/900 unidades vivas y movidas, 9000 órdenes,
18000 aplicaciones, cero rechazos y cero cola. Registra 39,25 ms/frame
(~25,5 FPS), 3153 draw calls y 2263 batches: confirma que la agrupación está
desactivada por defecto. El log confirma el culling activo. Es una muestra
observacional de la build final, no una nueva comparación causal de mejora.
Informe: `Captures/perf-v028-release900/result.json`.

## Publicación

**PUBLICADA.** `https://riesgus.com` sirve el release
`20260922T173612Z-pareto-v028`. El recibo
`.deploy/20260922T173612Z-pareto-v028/receipt.json` registra
`activated=true`, el nuevo `current` y el anterior
`20260922T134715Z-shallows-v027` como rollback; los 10 archivos se verificaron
en el servidor.

La verificación pública
`.deploy/20260922T173612Z-pareto-v028/public-verification.json` terminó con
`success=true`: los 10/10 hashes públicos coinciden, incluido `index.html`.
Se conservan gzip, MIME y caché inmutable de los artefactos versionados.
`www.riesgus.com` redirige con 301 al dominio apex. La comprobación pública
Worldphone terminó con `success=true`, `errors=[]`, 16 capturas y 58,41 s; es
viewport/touch emulado, no validación en hardware móvil físico.

El intento anterior queda como historial de despliegue fallido, no como estado
actual: `.deploy/20260922T163835Z-pareto-v028/receipt.json` conserva
`activated=false` porque SSH agotó el tiempo antes del preflight y no hubo
cambios remotos. No se modificaron NSG/firewall ni se reutilizaron tokens de
Cloudflare para esta actualización. Los servidores locales de pruebas se
cerraron.
