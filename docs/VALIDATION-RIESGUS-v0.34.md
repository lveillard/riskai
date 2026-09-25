# Validación Riesgus v0.34.0

Publicado en https://riesgus.com como `20260924T203350Z-v034-93b19c7` (merge de la PR #10, `93b19c7`).
El recibo da `activated=true` y 10 archivos verificados en el servidor. La verificación pública da `success=true`
(10/10 hashes; `.deploy/20260924T203350Z-v034-93b19c7/{receipt,public-verification}.json`). Sustituye a `20260923T194330Z-v033-b0303a1`,
que sigue en el servidor como vuelta atrás. Prueba pública: el arranque en 390×844 sale correcto, con 5 capturas y sin errores.
La partida de Europa con 16 IA aplica 171 órdenes, rechaza 0, da 18,2 ms/fotograma y 0 fotogramas >100 ms, sin errores de consola.

## Alcance

- `units.json` con esquema TypeBox como única fuente de las unidades. El contrato C# (`UnitConfig.g.cs`)
  y la validación (`UnitConfigValidation.cs`) se generan desde `scripts/config`. `npm test` comprueba
  el contrato, las invariantes y `docs/RISK-RULES-v0.34.md`.
- Capa común de reglas (`UnitRules`, `UnitTargeting`, `ClickRules`, `OrderValidation`,
  `CaptureOrderState`, `OrderAdvance`) para soldados, barcos y torres. Un único drenaje de órdenes
  al inicio del tick.
- Cola con Shift o **Encolar** para todas las órdenes y actores, con rutas visibles para la selección.
  Los cambios intencionados de comportamiento están en el plan §7.
- Mapas: oeste de Las Marcas y puestos de Cuatro Riberas recompuestos
  ([LAYOUT-v0.34](audits/LAYOUT-v0.34.md)).

## Pruebas

- EditMode: 322/322.
- PlayMode: 326/326. Se ejecuta en 4 tandas (`-Shard N/4`), cada una en un Unity nuevo, más un pase
  para los 6 tests que el reparto no incluye (`TestResults/playmode-simplify-*.xml`).
- `npm test` (scripts/config): 14/14.

## Fuga de memoria nativa entre partidas

`StaticBatchingUtility.Combine` creaba una malla combinada sin dueño en cada carga de mapa. Esa malla
contenía bosques, olivares, cascotes, cordilleras y puentes. Cada carga retenía unos 41 MB de memoria
nativa, y una suite PlayMode completa llegaba a unos 13 GB. Ahora todo recurso nativo generado
(mallas, materiales, texturas, batches) pertenece a `GeneratedResourceOwner` y se destruye con la
sesión. Las cachés globales acotadas llevan la etiqueta `RISKAI_SHARED_ASSET`.
`GeneratedResourceOwnershipGuardTests` falla si aparece un recurso sin dueño.
`PlayModeLeakTests` comprueba que Las Marcas y Europa no retienen memoria entre partidas.
Crecimiento tras el arreglo: −0,04 MB por carga en 304 cargas.

## Simplificación

Tras las revisiones se hizo una pasada de simplificación con el comportamiento idéntico:
−3.594 / +345 líneas.
- Se retira el fixture golden, que ya había verificado la paridad con v0.33 hasta `5490e79`.
- `portraitCamera` se reduce a los campos que varían.
- Se quitan los ganchos de prueba del código de producción.
- La admisión de órdenes, los chequeos de alcance y el cierre de fases navales usan una sola vía.
- `link.xml` preserva el ensamblado completo `RiskAI.Core`.

## Revisiones

Siete rondas adversariales independientes sobre la PR #10 (Grok 4.7, MiMo v2.6 Pro y
DeepSeek v4.1 Flash), cada una corregida y re-verificada en el código antes de la siguiente.
Informes en `.tools/exec/adv*-*.md` (locales).

## Rendimiento

Web, Europa, semilla 19031, probe sostenido de 900 unidades (`check_web_player.py --probe --sustained
--path-budget 500`, 60 s), v0.33 publicada frente a v0.34, con las ejecuciones intercaladas:

| Build | ms/fotograma (ejecuciones) | Mediana |
|---|---|---|
| v0.33 | 28,57 · 30,87 | 29,72 |
| v0.34 | 30,86 · 28,54 | 29,70 |

Cada orden de movimiento pide un solo camino y no detiene al agente
(`MovePathBudgetTests`). Medición en un portátil con RTX 5080 bajo carga de otros procesos. No es
hardware móvil físico. Combate con IA (308 unidades, 16 jugadores): 21,1 ms en v0.33 y 19,6 ms en v0.34.

## Web

IL2CPP con stripping `Minimal` fijado. `link.xml` preserva Newtonsoft.Json y el contrato de
`RiskAI.Core`. `LinkXmlContractTests` falla si falta un tipo del DTO.

## Límites conocidos

- Nombres de puerto sin traducir al inglés.
- Barra de desplazamiento vacía junto al pie en escritorio. El chevron del destinatario del chat en
  móvil apunta al revés.
- Sonidos de muerte retirados en v0.33; pendientes de rehacer.

## v0.34.1

Publicado como `20260924T222934Z-v0341-46c8647` (merge de la PR #12, `46c8647`). El recibo da `activated=true`, 10 archivos
verificados y verificación pública `success=true`. El arranque público en 390×844 funciona y no da errores.
La partida en Europa con 16 IA aplica 171 órdenes y rechaza 0, a 19,9 ms por fotograma, sin errores de consola.
Pruebas: EditMode 325/325, PlayMode 320/320 en 4 tandas más los tests nuevos, npm 14/14.

## v0.34.2

Publicado como `20260925T030132Z-v0342-3597a20` (merge de la PR #14, `3597a20`). Recibo `activated=true`, 10 archivos
verificados y verificación pública `success=true`. El arranque público en 390×844 funciona y no da errores.
La partida en Europa con 16 IA aplica 171 órdenes y rechaza 0, a 20,8 ms por fotograma, sin fotogramas de más de 100 ms
y sin errores de consola. Pruebas: EditMode 325/325, PlayMode 320/320 en 4 tandas (la tanda 3 repetida tres veces),
pase de huecos 9/9 y npm 14/14. El test de la oleada pasa 10/10 cuando se ejecuta solo.

## v0.34.3

Publicado como `20260925T140609Z-v0343-5508055` (merge de la PR #16, `5508055`). Recibo `activated=true`, 10 archivos
verificados y verificación pública `success=true`. El arranque público en 390×844 funciona y no da errores.
La partida en Europa con 16 IA aplica 166 órdenes y rechaza 0, sin fotogramas de más de 100 ms ni errores de consola.
El fotograma medio es de 34–37 ms. Una prueba A/B local en la misma máquina da 34,85 ms con v0.34.2 y 34,89 ms con v0.34.3,
así que la diferencia con los 20,8 ms anteriores viene del estado del equipo y no de esta versión.
Pruebas: EditMode 325/325, PlayMode 320/320 en 4 tandas (dos pasadas completas) y npm 14/14.
