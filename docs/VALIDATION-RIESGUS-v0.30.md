# Validación Riesgus v0.30.0

Publicado en https://riesgus.com como `20260923T120008Z-v030-c73fd2f` (commit `c73fd2f`).
Recibo `activated=true`, 10 archivos verificados en el origen y verificación pública
`success=true` (10/10 hashes) en `.deploy/20260923T120008Z-v030-c73fd2f/{receipt,public-verification}.json`.
Se conserva `20260922T194407Z-tower-v0291` como rollback. Sin cambios en Cloudflare ni
en reglas de acceso.

## Pruebas

- EditMode: 233/233.
- PlayMode: 265/267 superadas, 0 fallos, 2 omitidas por diseño (variantes de agrupado de
  arquitectura desactivadas por defecto), en 5 shards; `ImportedPlayableBoundsTests` 2/2.
- Tras la revisión Grok 4.7: EditMode 233/233 y PlayMode filtrado de las áreas tocadas,
  con los dos fallos resueltos y re-ejecutados (18/18). Detalle en
  [GROK-v0.30](audits/GROK-v0.30.md).
- `HoldingMortarKeepsHullRangeTargetBeyondShipPivotLeash` es sensible a tiempos bajo carga.

## Web

Build `Builds/Web-v0.30.0` (datos 61,0 MB, +3,4 MB por audio y música). Comprobada en
Chromium local a 1600×900, 390×844, 844×390 y 1024×768: rejilla de órdenes, barra
compacta, panel plegable, registro, chat y Espacio sin recompras. Partida pública en
390×844 sin errores de consola. Emulación, no hardware móvil físico.

## Límites conocidos

- Nombres largos de ciudad se parten en dos líneas y se recortan en la etiqueta.
- Retratos mejorables (Rugidor, barcos, estandarte del General).
- Volga prolongado al este fuera del mapa; franja gris junto a Groenlandia.
