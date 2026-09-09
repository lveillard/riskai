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
| ABBA, sombras dinámicas de unidades apagadas | Normal: 54,01 / 48,16 ms (media 51,09); sombras apagadas: 53,44 / 49,83 ms (media 51,64) | No hay ahorro repetible. Se apagaron 13.344 casters de 1.112 soldados, preservando las sombras pintadas; draw calls y batches siguieron cerca de 3.651 / 2.880. |
| ABBA, modelos de unidad ocultos | Normal: 51,82 / 44,49 ms (media 48,16); modelos ocultos: 36,57 / 34,67 ms (media 35,62) | Ahorro de 9,82–15,25 ms con 900/900 unidades móviles y cero rechazos. Draw calls bajan 3.649→3.459, batches 2.872→2.688 y vértices 392.320→276.663. |
| ABBA, animación legacy congelada | Normal: 45,56 / 44,30 ms (media 44,93); animación congelada: 43,44 / 42,18 ms (media 42,81) | Ahorro repetible de 2,12 ms (~4,7 %). Se congelaron 1.112 `Animation` y sus controladores, manteniendo iguales mallas, draw calls, batches, vértices, rutas y órdenes. |

## Correcciones metodológicas

- `worldAvgMs` es coste por tick, no por frame. Para compararlo con un frame hay que usar `worldAvgMs × worldTicks / frames`.
- El ABBA descarta el fill-rate del framebuffer principal como causa dominante. No descarta geometría, skinning, sombras de resolución fija, envío al driver ni sincronización GPU.
- Las ejecuciones normales variaron con carga externa y calentamiento. No atribuir al minimapa una diferencia de una sola pareja.
- El fixture no cubre combate, embudos, navegación marítima, IA activa ni selección humana.
- `success=True` significa que el fixture terminó funcionalmente; no certifica fluidez ni latencia de rutas.
- El contador de draw calls interceptados incluye toda la ejecución, no solo la ventana de medida.
- La prueba de animación desactiva tanto `Animation` legacy como `SoldierAnimator` y `MountedKnightView`; desactivar sólo el controlador no habría aislado la evaluación de pose.
- Ocultar renderers también puede alterar culling y actualización de bounds. El ahorro de modelos demuestra que la presentación visible es material, pero aún no atribuye el coste sólo a skinning o sólo a geometría.

## Conclusión actual

La presentación de modelos visibles es el bloque dominante encontrado en este fixture: retirarla ahorra aproximadamente 12,5 ms/frame de media. La animación legacy explica aproximadamente 2,1 ms de ese camino, por lo que queda una parte mayor de culling, actualización de bounds, skinning y preparación/envío de render sin separar.

Reducir píxeles no ayuda y WebGL mantiene este trabajo en el hilo principal. Actualización de `NavMeshAgent`, evitación local e IMGUI/minimapa continúan sin cuantificar de forma repetida, pero no son el siguiente candidato de producto mientras el modelo visible conserve esa señal.

## Próximos pasos por prioridad

1. Comparar `SkinnedMeshRenderer` congelado con una malla estática equivalente: misma pose, materiales, submeshes y transforms. No cambiar batching o instancing en esta prueba. Separará el camino skinned de dibujo/culling de geometría equivalente.
2. Convertir sólo el ahorro que se confirme en LOD de producto: modelo completo cerca, detalle estático o simplificado lejos, y conservar simulación, selección y respuesta de ataque. Repetir ABBA y una prueba en móvil físico.
3. Aislar UI y después minimapa con intervenciones ABBA, sin sumar ahorros entre intervenciones.
4. Si un bloque sigue ambiguo, ampliar los markers de PlayerLoop disponibles para Animation, AI/NavMesh, culling y GUI. No usar Deep Profile ni serializar durante el intervalo.
5. Mantener constantes semilla, cámara, cohort, rutas, presupuesto de NavMesh y duración. Registrar trabajo útil, no solo ms: unidades movidas, rutas pendientes, latencia y órdenes rechazadas.

## Herramienta disponible

`scripts/check_web_player.py` admite `--browser-metrics`, `--cpu-profile` y `--frame-trace` para conservar métricas del hilo principal, un perfil CPU de Edge y los counters de PlayerLoop disponibles junto con la evidencia del probe.
