# Rendimiento Pareto v0.28 · metodología A/B

Documento de medición reproducible. La comparación experimental principal usa la
misma build WebGL v0.28 en tres brazos, con la misma máquina, Edge, viewport,
semilla, cámara táctica y carga sintética. El player v0.27 de `:8082` queda
como referencia secundaria, no como brazo causal. No es una prueba de combate
ni una certificación de rendimiento móvil. Otras aplicaciones del usuario
permanecieron abiertas; no se solaparon estas ventanas de rendimiento con
Unity compilando ni con otros navegadores de QA. La variación entre controles
impide convertir las diferencias pequeñas en una afirmación causal firme.

## Contrato de la prueba

El probe `sustained-navigation-v2` crea 900 unidades, las separa en una malla
determinista y alterna órdenes de mantener/mover. Congela actores originales y
desactiva controlador, IA, combate y refuerzos. Por tanto, mide principalmente
NavMesh, `NavMeshAgent`, comandos y presentación de 900 unidades; no mide una
partida con 900 tropas combatiendo.

Los tres brazos v0.28 conservan estos invariantes:

| Campo | Valor |
| --- | --- |
| mapa / semilla / jugadores | Europe / `19031` / 16 |
| viewport / DPR | 1600×900 / 1 |
| NavMesh budget | 500 |
| ventana | 60 s reales, `timeScale=1` |
| cohort / fixture esperado | 900 / `15092308` |
| presentación conservada | modelos y sombras visibles; `overview=false`, minimapa visible |
| Edge / renderer | 153.0.4234.48 / ANGLE NVIDIA RTX 5080 D3D11 |

El fixture esperado actual es `15092308`. El hash histórico v0.26
(`50D70A79`) pertenece a otra versión/carga y no debe mezclarse en esta
comparación. Las únicas diferencias intencionadas entre brazos son las dos
flags de optimización descritas abajo.

## Artefactos candidate1

Antes de recompilar `Builds/Web-v0.28.0`, se registraron estos SHA256 bajo la
etiqueta `native-candidate1`. Todas las mediciones v0.28 documentadas aquí
de los brazos A–D y de la comparación CPU normal corresponden a esta build;
el experimento exploratorio candidate2 se separa más abajo. Una recompilación
posterior debe recibir otra etiqueta y no sobrescribir esta procedencia lógica.

| Artefacto | Bytes | SHA256 |
| --- | ---: | --- |
| `Web-v0.28.0.wasm.unityweb` | 12217698 | `F3B8798AB6290FFBD94BFA74A9B2D00210AAAD62A23020BA7305B18A79F971EA` |
| `Web-v0.28.0.data.unityweb` | 45258933 | `9911A4AEAD06B92D176F514879C26C472815DBDED9ECBC20B3F2D05629003BF6` |
| `Web-v0.28.0.framework.js.unityweb` | 89055 | `28A819974AEDF9722DF8533BF7C13D5BFC930D28DED2037EEDC876A6D31F3C75` |

Los artefactos de la recompilación candidate2 quedaron registrados antes del
siguiente build bajo `native-candidate2`:

| Artefacto | Bytes | SHA256 |
| --- | ---: | --- |
| `Web-v0.28.0.wasm.unityweb` | 12231048 | `4F4596B3D1E6843AD15A62206BA9A3466282C5B29DEBFBBE584FEEE592CFFFE3` |
| `Web-v0.28.0.data.unityweb` | 45260477 | `668FBC063F9BF90568FB03CA8D41E71CD3CEB31EC0F1425584DD448DC09BA2C2` |
| `Web-v0.28.0.framework.js.unityweb` | 89065 | `69E1A61D768E253E0F287DDF2E01F54E0CB479F9DA38F7604B2605E8801F9BCB` |

## Comandos históricos candidate1 (native default)

Estos son los comandos de la pasada histórica `native-candidate1`, en la que
native architecture batching era el valor predeterminado. Ejecutan la misma
build v0.28 en `:8083`, cada brazo con una carpeta de salida nueva. No añadir
`--overview`: `FrameMap` oculta arquitectura y tropas y enmascara el batching
que se quiere medir.

```powershell
python scripts/check_web_player.py --url http://127.0.0.1:8083 --output Captures/perf-v028-control900 --map europe --seconds 60 --recruits 6 --path-budget 500 --probe --sustained --browser-metrics --frame-trace --width 1600 --height 900 --dpr 1 --disable-architecture-batching --disable-unit-presentation-culling
python scripts/check_web_player.py --url http://127.0.0.1:8083 --output Captures/perf-v028-batching900 --map europe --seconds 60 --recruits 6 --path-budget 500 --probe --sustained --browser-metrics --frame-trace --width 1600 --height 900 --dpr 1 --disable-unit-presentation-culling
python scripts/check_web_player.py --url http://127.0.0.1:8083 --output Captures/perf-v028-combined900 --map europe --seconds 60 --recruits 6 --path-budget 500 --probe --sustained --browser-metrics --frame-trace --width 1600 --height 900 --dpr 1
```

Arm A desactiva ambas optimizaciones; B permite sólo architecture batching; C
permite batching y unit presentation culling. Los tres brazos deben abrir un
contexto Edge nuevo, conservar la misma versión
del navegador/driver y ejecutarse sin carga externa relevante. `--frame-trace`
es necesario para los contadores Unity; `--browser-metrics` sólo añade la
correlación CDP. `--suppress-draws` y los flags de ocultar sombras/modelos no
forman parte de estos tres brazos.

Para reducir ruido, repetir en orden ABBA si el primer resultado difiere menos
de aproximadamente 10 % o si una cola de frames largos domina la media. No
aceptar una mejora basada en un único máximo aislado.

## Comandos de fuentes finales v0.28

En la fuente final native architecture batching requiere la opción explícita
`--native-architecture-batching`; el modo normal sin flags queda con culling de
presentación activo. Estos comandos son reproducibles para una nueva ventana;
la captura final `release900` y su manifiesto se registran más abajo.

```powershell
python scripts/check_web_player.py --url http://127.0.0.1:8083 --output Captures/perf-v028-final-native900 --map europe --seconds 60 --recruits 6 --path-budget 500 --probe --sustained --browser-metrics --frame-trace --width 1600 --height 900 --dpr 1 --native-architecture-batching
python scripts/check_web_player.py --url http://127.0.0.1:8083 --output Captures/perf-v028-final-normal900 --map europe --seconds 60 --recruits 6 --path-budget 500 --probe --sustained --browser-metrics --frame-trace --width 1600 --height 900 --dpr 1
```

## Primera pasada v0.28

Los tres brazos terminaron con `success=true`, `valid=true`, fixture
`15092308`, cohort 900/900, 9000 movimientos enviados, 18000 aplicaciones,
cero rechazos y cero cola. Son resultados válidos del workload, no una
declaración de éxito de FPS ni una certificación de la optimización.

| Brazo | Flags | frameAvg ms | draw calls | batches | SetPass | vertices | max / >50 / >100 / >250 ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| A control | batching y culling desactivados | 30.45 | 3153 | 2263 | 52 | 804350 | 52 / 3 / 0 / 0 |
| B batching | sólo culling desactivado | 30.99 | 2648 | 1742 | 56 | 804533 | 59 / 6 / 0 / 0 |
| C combinado | ambas activas | 29.05–29.06 | 2645 | 1739 | 56 | 804353 | 58 / 3 / 0 / 0 |

La comparación A→B ya muestra aproximadamente 16.0 % menos draw calls y
23.0 % menos batches, pero el frame average sube 1.8 % en esa ejecución. C
queda 16.1 % por debajo de A en draw calls y 23.2 % en batches; su frame
average es 4.6 % menor que A en esta pasada. Esa diferencia de frame/FPS aún no
está demostrada: A2 muestra una variación temporal de 10 % y D frente a A2
sólo cambia 2.2 % sin prueba estadística; además, la referencia CPU normal C
es peor que A. C también conserva 56 SetPass frente a 52 en A, por lo que draw
calls y FPS no deben tratarse como la misma métrica.

El brazo D ya queda medido en esa tabla. La medición CPU normal y la agrupación
manual de mallas siguen siendo experimentos separados.

## D y repetición del control

Ya están disponibles el brazo D (batching desactivado, culling activo) y A2
(ambas optimizaciones desactivadas, repetición del control):

| Brazo | Flags | frameAvg ms | draw calls | batches | SetPass | vertices | max / >50 / >100 / >250 ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| D culling | sólo batching desactivado | 26.80 | 3153 | 2263 | 52 | 804352 | 53 / 1 / 0 / 0 |
| A2 control repeat | batching y culling desactivados | 27.40 | 3156 | 2266 | 52 | 804531 | 59 / 1 / 0 / 0 |

D frente a A2 es sólo 2.2 % menor en frame average y mantiene prácticamente
los mismos contadores de render; no es evidencia estadística de una mejora de
culling. A1 fue 30.45 ms frente a A2 27.40 ms: la variación temporal del
control es aproximadamente 10 %. Por ello no se atribuye a las optimizaciones
la diferencia de FPS entre A1 y C. La reducción de draw calls de B/C frente a
los controles A1/A2 se mantiene alrededor del 16 %, pero no demuestra una
mejora de FPS.

## CPU normal

Las capturas `Captures/resources-v028-control` (A) y
`Captures/resources-v028-combined` (C) no usan el workload sustained de 900
unidades: son ventanas normales de Europe de 30 s. C no mejora esta referencia:

| Caso | Cadencia | frame medio | main thread ocupado | renderer process `corePercent` (% de un núcleo) | GPU process `corePercent` (% de un núcleo) |
| --- | ---: | ---: | ---: | ---: | ---: |
| A control | 59.33 Hz | 16.854 ms | 90.7 % | 96.4 % | 44.0 % |
| C combinado | 55.9 Hz | 17.888 ms | 95.6 % | 100.1 % | 45.7 % |

El frame medio de C es aproximadamente 6.1 % peor y la cadencia cae 5.8 %;
por tanto no se acepta la implementación nativa por reducir draw calls
solamente. Ambos campos `corePercent` son CPU del proceso indicado, expresada
como porcentaje de un núcleo; no son utilización global del hardware GPU. El
scope de GPU de una métrica hardware global abarcaría toda la máquina, pero no
está representado por esta tabla. El candidato 2 de agrupación manual de
mallas, que busca menos renderers, se analiza como experimento separado más
abajo.

## Candidate2 manual · exploratorio

Estas tres ventanas usan la agrupación manual de mallas
(`manual-architecture-batching`) en una recompilación posterior cuyos hashes
quedaron registrados como `native-candidate2`. Se separan de la comparación
candidate1 y no cambian la decisión de producción.

| Brazo candidate2 | Flags | frameAvg ms | draw calls | batches | SetPass | vertices | max / >50 / >100 / >250 ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| C2 control | manual y culling desactivados | 41.91 | 3153 | 2263 | 52 | 804353 | 68 / 50 / 0 / 0 |
| C2 manual | manual activo, culling desactivado | 41.14 | 2628 | 1738 | 52 | 805783 | 70 / 35 / 0 / 0 |
| C2 combinado | manual y culling activos | 37.83 | 2625 | 1735 | 52 | 805600 | 63 / 7 / 0 / 0 |

Los tres conservaron fixture `15092308`, cohort 900/900, 9000 movimientos,
18000 aplicaciones y cero rechazos. La agrupación manual vuelve a mostrar
aproximadamente 16.7 % menos draw calls y 23.2 % menos batches frente a C2
control, pero la deriva temporal impide atribuir una mejora de FPS: C2 control
es 41.91 ms frente a 37.83 ms combinado, muy por encima de candidate1. Los
hashes `native-candidate2` ya quedan registrados arriba.

Las ventanas normales candidate2 ya completadas muestran el coste de memoria
del experimento manual. `wasmHeapBytes` convertido a MiB es un buffer WASM,
no RAM total; `PrivateMemorySize64` es memoria privada de cada proceso y no se
suma como si fuera RAM residente única:

| Caso normal candidate2 | Cadencia | frame medio | WASM heap | renderer private after | GPU private after |
| --- | ---: | ---: | ---: | ---: | ---: |
| C2 control | 44.43 Hz | 22.506 ms | 490.5 MiB | 865.9 MiB | 528.5 MiB |
| C2 manual | 49.47 Hz | 20.216 ms | 587.0 MiB | 950.1 MiB | 529.1 MiB |
| C2 culling | 45.77 Hz | 21.846 ms | 490.5 MiB | 867.4 MiB | 523.1 MiB |

Manual mejora esta ventana variable aproximadamente 10.2 % en frame medio,
pero añade 96.5 MiB de WASM y unos 84.2 MiB de memoria privada del renderer
frente a C2 control; no justifica un valor de producto. Culling solo cambia
aproximadamente 2.9 % frente a C2 control sin crecer el WASM heap, una señal
modesta sin promesa de producto. Ambos resultados normales son referencias de
30 s y no sustituyen el sustained de 900 unidades.

## Build final v0.28 · release900

Esta captura es una validación de la build final y queda separada de
`native-candidate1` y del experimento `native-candidate2`. En
`Captures/perf-v028-release900` terminó con fixture `15092308`, cohort 900/900,
9000 órdenes, 18000 aplicaciones y cero rechazos: `frameAvgMs=39.25`, 3153
draw calls, 2263 batches y 52 SetPass. Culling está activo
(`cullingDisabled=false`) y architecture batching queda desactivado por
defecto; los 25.5 FPS son sólo observacionales de esta ventana, sin atribuir
una ganancia A/B nueva. El `heapBytes` registrado es `617218048`.

Windows y Web finales están compilados y la validación registra 200 tests (173
EditMode y 27 PlayMode). El release y sus hashes públicos están verificados en
la sección de publicación inferior; la comprobación Worldphone también terminó
sin sustituir una validación en hardware móvil físico.

El manifiesto completo es
`.deploy/20260922T173612Z-pareto-v028/manifest.json`. Hashes SHA256 de los
artefactos Web finales:

| Artefacto | SHA256 |
| --- | --- |
| `Web-v0.28.0.data.unityweb` | `e1ec98bf15f2981e871c5808eb4ad2474c5a6a66e7a21c4a9a1cecba2213fae8` |
| `Web-v0.28.0.framework.js.unityweb` | `a7bdd91763edf8f80566ed9875af6335290382023a6674ef241afd62ee74eb50` |
| `Web-v0.28.0.loader.js` | `371cef5cbdff46655c2c67d801df076e38346bc94b2f600823acb1b6b7167ff1` |
| `Web-v0.28.0.wasm.unityweb` | `b0cf1ae192c8a65ac6d302e996c42c79703527492c4d4e4a5fdab13b32d8772d` |

## Repetición y diagnóstico de presentación

La repetición sustained C2 control marca 39.89 ms, 3153 draw calls, 2263
batches y 52 SetPass; sigue siendo la misma carga válida, pero no es una base
estable para atribuir FPS. El diagnóstico `perf-v028m-hidden-unit-diagnostic`
marca 31.66 ms, 2921 draw calls, 2031 batches y 48 SetPass con los modelos de
unidad ocultos. No es jugable: ocultar renderers también cambia culling de
animación y sombras, y no demuestra un ahorro puramente de polígonos GPU ni
elimina el coste de NavMesh. Sirve únicamente como diagnóstico de presentación
de unidades; no es un brazo jugable ni una previsión del producto.

## Referencia secundaria v0.27

Archivo: `Captures/perf-v028-baseline900/result.json` y su `console.log`.
Player v0.27, URL local `127.0.0.1:8082`, `success=true` y fixture válido.
Estos números sirven para contexto histórico y no para atribuir por separado
batching o culling.

| Métrica | Baseline |
| --- | ---: |
| frames Unity / `frameAvgMs` | 2121 / 28.29 ms |
| frame max / >50 / >100 / >250 ms | 455 ms / 15 / 3 / 2 |
| draw calls / batches / SetPass | 3153 / 2263 / 52 |
| vertices | 804355 |
| cohort viva / movida | 900 / 900 |
| órdenes enviadas / aplicadas / rechazadas | 9000 / 18000 / 0 |
| `simSecondsDelta` / `realSeconds` | 59.75 / 60.01 |

El resultado también registra `movingFrameAvg=849.89`, que es la media de
unidades observadas en movimiento, no milisegundos de frame. El baseline tuvo
`RuntimeDiagnostics` de 29.16 ms en su primera ventana y 27.48 ms en la
segunda; el `RISKAI_PLAYERLOOP_TRACE` de 28.29 ms cubre toda la ventana y es
la referencia secundaria histórica.

## Cómo leer los contadores y aislar efectos

`RISKAI_PLAYERLOOP_TRACE` aporta `frameAvgMs`, `drawCallsAvg`, `batchesAvg`,
`setPassCallsAvg`, `verticesAvg` y sus cantidades de muestras. Comparar cada
brazo v0.28 entre sí como:

```text
deltaPercent = 100 * (armB - armA) / armA
```

Para la atribución causal, sustituir los nombres por los brazos:

```text
effectArchitectureBatching = 100 * (armB - armA) / armA
effectPresentationCulling  = 100 * (armC - armB) / armB
effectCombined             = 100 * (armC - armA) / armA
```

Un valor negativo mejora el coste. Las flags A/B/C son diferencias
intencionadas y no invalidan el A/B; sí lo invalidan cambios de build, fixture,
cámara, viewport, DPR, presupuesto, renderer o carga externa.

Usar la media de frame y sus colas (`measureFrameMaxMs`, `measureOver50ms`,
`measureOver100ms`, `measureOver250ms`) como coste principal; draw calls,
batches, SetPass y vertices explican el trabajo de presentación. Un candidato
es Pareto sólo si reduce un coste objetivo sin empeorar de forma material la
media/colas de frame ni invalidar el fixture. Los resultados de `success`,
`valid`, `fixtureHash`, `cohort`, `rounds`, `probeMovesSubmitted`, `rejected` y
`queued` son primero criterios de validez, no métricas de mejora.

`mainThreadRawAvg=28290617` del baseline no se convierte a milisegundos: el
propio probe declara que los contadores raw requieren verificación de unidad en
la plataforma. `renderThreadRawAvg=-1` y cero muestras no permiten inferir
tiempo GPU en este WebGL.

## CPU, GPU y memoria: límites de atribución

Los campos `browser_metrics` (`TaskDuration`, `ScriptDuration`, `ThreadTime`,
`ProcessTime`) son métricas CDP del proceso/página del navegador durante la
ventana. No equivalen a CPU total de Unity, porcentaje de todos los cores ni
tiempo GPU. En el baseline fueron aproximadamente `TaskDuration=59.93 s`,
`ScriptDuration=59.45 s`, `ThreadTime=58.32 s` y `ProcessTime=61.27 s` para
unos 60.66 s de pared.

`canvas.heapBytes=618921984` es el buffer WASM reservado por Unity. No es la
RAM committed del proceso ni la RAM residente del navegador. El probe
sustained no recoge CPU total del sistema ni RAM committed; no deben
rellenarse esas métricas con `TaskDuration`, `ProcessTime` o `heapBytes`. La
captura separada `Captures/resources-v028-control` aporta una referencia de
juego normal de 30 s: 59.33 Hz, `browserMainThreadBusyPercent=90.7`, proceso
renderer con `corePercent=96.4` y proceso GPU con `corePercent=44.0`. Esos
porcentajes siguen siendo CPU de proceso, no utilización global del hardware
GPU, y no deben confundirse con el workload de 900 unidades.

## Decisión final: culling y ownership; batching arquitectónico desactivado

Culling y ownership quedan como la dirección aceptada, sin prometer un
porcentaje fijo de FPS. El batching arquitectónico native/manual queda opt-in y
desactivado por defecto; la fuente final requiere la flag native explícita.
La validación registrada suma 200 tests aprobados (173 EditMode y 27 PlayMode).
Además, dos casos de torres opt-in se omiten intencionadamente en la
configuración final sin agrupación.

Los builds finales están compilados y el release público está activo; se
conservan las limitaciones del rendimiento y la decisión de batching opt-in.

## Publicación

**PUBLICADA.** `https://riesgus.com` sirve el release
`20260922T173612Z-pareto-v028`. El recibo
`.deploy/20260922T173612Z-pareto-v028/receipt.json` registra
`activated=true`, el `current` nuevo y el anterior
`20260922T134715Z-shallows-v027` como rollback; los 10 archivos se verificaron
en el servidor.

La verificación pública
`.deploy/20260922T173612Z-pareto-v028/public-verification.json` terminó con
`success=true`: coinciden los 10/10 hashes públicos, incluido `index.html`.
`www.riesgus.com` redirige con 301 al dominio apex. La comprobación pública
Worldphone terminó con `success=true`, `errors=[]`, 16 capturas y 58,41 s; es
viewport/touch emulado, no validación en hardware móvil físico.

El intento anterior queda como historial de despliegue fallido, no como estado
actual: `.deploy/20260922T163835Z-pareto-v028/receipt.json` conserva
`activated=false` porque SSH agotó el tiempo antes del preflight y no hubo
cambios remotos. No se modificaron NSG/firewall ni se reutilizaron tokens de
Cloudflare para esta actualización. [Estado de validación y despliegue](../VALIDATION-RIESGUS-v0.28.md).
