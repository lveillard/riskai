# Investigación de rendimiento v0.23

## Contexto

La carga usada es `sustained-navigation-v2`: Europa, semilla `19031`, 16 jugadores, 900 ballesteros controlados, rutas rectas alternas, IA/combate/refuerzos desactivados y actores originales congelados. Es una carga de navegación y presentación, no una partida completa.

La exportación pública permanece en `20260909T091323Z-966b9a9`. Estas observaciones no modifican la release.

## Hechos medidos

| Medición | Resultado | Lectura válida |
| --- | ---: | --- |
| Carga sostenida inicial | 49,27 ms/frame; 900/900 movidos; 0 rechazos | La carga funciona, pero no alcanza el presupuesto de 30 FPS móvil. |
| ABBA, DPR 1 | 44,88 y 53,14 ms; media 49,01 ms | Línea base repetida. |
| ABBA, DPR 0,5 | 46,10 y 53,18 ms; media 49,64 ms | Reducir el canvas de 1280×800 a 640×400 no mejora de forma discernible. |
| Sin llamadas WebGL de dibujo | 39,14 ms en una pasada válida | El camino de dibujo merece investigación, pero la intercepción también cambia el trabajo de CPU y no prueba un límite de GPU. |
| Presupuesto NavMesh 100 | 46,86 ms; rutas pendientes hasta 2.750 ms; 699 unidades moviéndose de media | Reduce trabajo útil y empeora respuesta; no es una mejora equivalente. |
| Perfil CPU Edge | 59,7 s muestreados; funciones WASM anónimas dominantes; `bufferSubData` ~0,675 s | El tiempo ocurre en WASM/Unity, no en layout/CSS; la release no conserva símbolos para nombrar subsistemas. |
| PlayerLoop local, 900 unidades | Main Thread ~65,3 ms/frame; 3.651 draw calls; 2.880 batches; 50 SetPass; 392.417 vértices | WebGL ejecuta en un hilo principal. La presión principal es CPU de preparación/envío de render y batches, no fill-rate. |

## Correcciones metodológicas

- `worldAvgMs` es coste por tick, no por frame. Para compararlo con un frame hay que usar `worldAvgMs × worldTicks / frames`.
- El ABBA descarta el fill-rate del framebuffer principal como causa dominante. No descarta geometría, skinning, sombras de resolución fija, envío al driver ni sincronización GPU.
- Las ejecuciones normales variaron con carga externa y calentamiento. No atribuir al minimapa una diferencia de una sola pareja.
- El fixture no cubre combate, embudos, navegación marítima, IA activa ni selección humana.
- `success=True` significa que el fixture terminó funcionalmente; no certifica fluidez ni latencia de rutas.
- El contador de draw calls interceptados incluye toda la ejecución, no solo la ventana de medida.
- Desactivar `SoldierAnimator` no aislaría animación: los modelos usan `Animation` legacy, que puede seguir evaluándose y haciendo skinning.

## Conclusión actual

No está identificado todavía un subsistema único. Sabemos que la mayor parte del tiempo queda fuera de `World.Tick`, que reducir píxeles no ayuda y que el hilo principal procesa miles de batches por frame. Las hipótesis abiertas, sin orden de culpabilidad, son:

1. Evaluación de `Animation` legacy y skinning de unidades.
2. Actualización de `NavMeshAgent` y evitación local fuera del tick instrumentado.
3. Culling, preparación y envío de render desde Unity/WASM.
4. Sombras dinámicas de unidades y barcos.
5. IMGUI/minimapa, como contribución secundaria aún no cuantificada de forma repetida.

## Próximos pasos por prioridad

1. Reducir batches de entidades repetidas con un experimento local de sombras dinámicas desactivadas para tropas/barcos, conservando las sombras pintadas. Medir draw calls, batches y trabajo útil con el mismo fixture.
2. Aplicar LOD de presentación a unidades lejanas: primero sombra dinámica y detalle de animación; después malla/impostor. Mantener simulación y selección intactas.
3. Aislar UI y después minimapa con intervenciones ABBA, sin sumar ahorros entre intervenciones.
4. Si un bloque sigue ambiguo, ampliar los markers de PlayerLoop disponibles para Animation, AI/NavMesh, culling y GUI. No usar Deep Profile ni serializar durante el intervalo.
5. Validar que el propio profiling no altera de forma material frame time, ticks, unidades móviles ni latencia de ruta.
6. Aplicar interruptores diagnósticos locales, uno por vez y en orden ABBA, sobre el bloque que destaque:
   - sombras desactivadas;
   - UI completa, después solo minimapa;
   - evaluación de `Animation` realmente congelada;
   - cámara/mundo sin render;
   - evitación de agentes manteniendo rutas.
7. Mantener constantes semilla, cámara, cohort, rutas, presupuesto de NavMesh y duración. Registrar trabajo útil, no solo ms: unidades movidas, rutas pendientes, latencia y órdenes rechazadas.
8. Solo después de localizar el bloque dominante, implementar una optimización de producto y repetir el mismo ABBA más una prueba en dispositivo móvil real.

## Herramienta disponible

`scripts/check_web_player.py` admite `--browser-metrics`, `--cpu-profile` y `--frame-trace` para conservar métricas del hilo principal, un perfil CPU de Edge y los counters de PlayerLoop disponibles junto con la evidencia del probe.
