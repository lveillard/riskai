# Recursos de Riesgus v0.26 · 22 septiembre 2026

Medición de la release pública `20260922T015400Z-riesgus-v026`, antes del
ajuste de touchpad 0.26.1. No se aplicaron optimizaciones de rendimiento.

## Entorno y método

Windows, Intel Core Ultra 9 285H (16 procesadores lógicos), 31,4 GiB de RAM,
RTX 5080 Laptop de 16 GiB y Edge 153.0.4234.48. Ventana 1600×900, DPR 1.
Quedaban aproximadamente 3,9 GiB de RAM física libre antes de las mediciones.
Otras aplicaciones permanecieron abiertas. Durante la primera pasada de Europe
arrancaron editores de otro proyecto; se repitió la medida cuando estaban
inactivos. No se cerraron procesos del usuario ni se midió en móvil físico.

Cada caso normal abre un navegador nuevo, espera a que termine la carga y la
cuenta atrás, y observa 30 segundos. Los intervalos normales proceden de
`requestAnimationFrame`: miden cadencia y disponibilidad del navegador, no son
contadores del profiler de Unity. La prueba de 900 unidades sí registra frames
de Unity durante 60 segundos. No deben tratarse como métricas idénticas.

## Resultados

| Escenario | Cadencia navegador | Media / p95 | Hilo principal ocupado | Heap WASM | Memoria privada del navegador completo |
| --- | ---: | ---: | ---: | ---: | ---: |
| Menú | 60,0 Hz | 16,67 / 17,1 ms | 7,6 % | 184,4 MiB | 807,6 MiB |
| Las Marcas | 60,0 Hz | 16,67 / 16,8 ms | 79,6 % | 265,5 MiB | 1210,0 MiB |
| Europe, primera pasada | 36,3 Hz | 27,54 / 33,4 ms | 99,9 % | 458,9 MiB | 1555,6 MiB |
| Europe, repetición | 49,8 Hz | 20,08 / 33,4 ms | 99,9 % | 458,9 MiB | 1557,9 MiB |

«Hilo principal» es tiempo ocupado de tareas CDP. Los procesos renderer de
Europe consumieron aproximadamente un núcleo completo (101,6–106,4 % de un
núcleo sumando sus hilos), lo que confirma presión de CPU. Eso no representa
el 100 % de los 16 procesadores lógicos del equipo.

El heap WASM es el tamaño del buffer del motor, no toda la RAM del juego.
La columna de navegador suma memoria privada comprometida de sus procesos,
incluidos GPU y servicios; no es RAM residente exclusiva ni una cifra de VRAM.
El renderer de juego más su renderer auxiliar sumaron 454/599/854/839 MiB de
memoria privada, respectivamente. El heap JS de la página fue 13–14 MiB.

La GPU global osciló aproximadamente entre 10–38 % y 1,0–1,7 GiB de memoria
usada en estas muestras, pero incluye escritorio y otras aplicaciones: no es
una medición aislada del coste GPU de Riesgus.

## Carga de 900 unidades

Europe, semilla 19031, presupuesto NavMesh 500, fixture
`sustained-navigation-v2`, hash `50D70A79`. Desactiva IA/combate/refuerzos y
congela los actores originales; no representa una partida con 900 unidades
combatiendo ni permite extrapolar rendimiento móvil.

- 900/900 unidades permanecieron vivas y se desplazaron.
- 9000 órdenes de movimiento, 18000 aplicaciones, cero rechazos ni cola final.
- 60,03 s medidos; 1426 frames Unity; 42,09 ms/frame, aproximadamente 23,8 FPS.
- Máximo 113 ms; 118 frames por encima de 50 ms y uno por encima de 100 ms.
- Rutas pendientes hasta 550 ms al ordenar la cohorte completa.
- Aproximadamente 4003 draw calls, 3123 batches, 52 SetPass y 520526 vértices
  por frame. El hilo principal permaneció ocupado alrededor del 99 %.
- El probe funcional terminó correctamente; la fluidez no alcanza 30/60 FPS.

## Lectura y prioridades

### Servidor de origen

Comprobación SSH de sólo lectura a las 12:06 UTC: VM de 2 vCPU, carga media
`0.00/0.00/0.00`, 897 MiB de RAM total y 480 MiB disponibles; Nginx indicaba
0,0 % de CPU. Disco de 30 GB: 8,5 GB ocupados y 22 GB libres. Las releases y
copias de recuperación ocupan 6,5 GB. El origen sirve archivos estáticos y
no muestra saturación; la simulación y el render se ejecutan en el navegador.

Conviene revisar la retención de releases si aumenta la frecuencia de
publicación: conservar todas las copias consume disco aunque no cause la
lentitud del juego. No se borraron releases en esta comprobación.

### Cliente

La partida pequeña mantiene la cadencia objetivo en este equipo, pero Europe
consume todo el margen del hilo principal y muestra variación apreciable con
la carga concurrente. La memoria crece mucho al cargar un mapa grande. Las
muestras cortas no demuestran una fuga de memoria ni estabilidad de horas.

La primera prioridad de optimización es reducir trabajo de presentación y
envío de dibujos: batching/instancing y LOD de unidades y escenario. Las miles
de llamadas de dibujo y la investigación previa del LOD justifican medir ese
camino; esta ronda no aísla por sí sola cuánto cuesta cada subsistema. Después
conviene medir NavMesh/evitación y minimapa por separado, manteniendo iguales
semilla, cámara y trabajo útil. Bajar solamente la resolución no resuelve un
hilo de CPU saturado.

## Reproducción

```powershell
python scripts/measure_web_resources.py --scenario menu --output Captures/resources-menu
python scripts/measure_web_resources.py --scenario classic --output Captures/resources-classic
python scripts/measure_web_resources.py --scenario europe --output Captures/resources-europe
python scripts/check_web_player.py --url https://riesgus.com --output Captures/resources-stress --map europe --seconds 60 --probe --sustained --browser-metrics --frame-trace --width 1600 --height 900 --dpr 1
```

Datos originales: `Captures/resources-v026-{menu,classic,europe,europe-repeat}/`
y `Captures/resources-v026-stress900/`. Los informes conservan IDs de proceso,
memoria, tiempos, consola y capturas; la sesión de navegador se cierra al acabar.
