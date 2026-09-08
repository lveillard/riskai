# Identidad de selección durante pérdida de foco · v0.21

Fuente y exportaciones: `f05c586c8804b792b0a42f6873779428f885e159`.
Esta corrección evita que una referencia conservada
por el controlador seleccione u ordene a otra unidad cuando el pool reutiliza
el mismo objeto durante una pérdida prolongada de foco.

## Corrección y alcance

`RtsController` conserva la identidad de simulación por objeto, sin volver a
capturar la identidad de una referencia conocida al reordenar las listas
públicas. La purga compacta cada lista en una pasada estable y reutiliza
diccionarios. Se ejecuta antes del retorno por falta de foco y en las entradas
de selección/órdenes pertinentes. Los supervivientes siguen seleccionados;
las referencias reemplazadas se descartan sin modificar al nuevo actor.
Grupos terrestres/navales y embarques pendientes comprueban la identidad.
`Clear()` y `ClearMobileAndCamp()` purgan antes de deseleccionar, incluyendo
actores vivos añadidos directamente a las listas públicas.

Es el mismo controlador para mapas y plataformas, sin nueva política de
selección ni cambios de balance o navegación. No incorpora servidor ni
multijugador.

## Evidencia y revisión

El [resultado Unity publicado](selection-lifetime-v0.21/tests.xml)
registra **55/55 aprobadas en 182,843 s**, incluidas diez pruebas nuevas de
`RtsSelectionLifetimeTests`: muerte y reutilización real del pool, foco,
supervivientes, edición pública, limpieza, alternancia, grupos terrestres y
navales, barco destruido, embarque pendiente y retirada de un pasajero.
El primer intento, conservado localmente en `RiskAI/Logs/v21-selection-lifetime-tests.log`,
falló antes de ejecutar por tres errores CS1503 en restricciones de colección;
se corrigieron con `Has.No.Member`/`Has.Member`. No cuenta como aprobado.
Los trece hashes de fuente del manifiesto coinciden con los archivos actuales.
El [recibo publicado](SELECTION-LIFETIME-v0.21.json) incluye ese manifiesto,
los resultados íntegros de las seis revisiones, sus identidades y los hashes
de logs y exportaciones. No requiere acceso a `.tools` para leer los dictámenes.

Tres contextos independientes reales de claude-vei Opus5 completaron
revisión y seguimiento; los seis informes están leídos y reconciliados.
No quedan P0/P1/P2 finales; no participó Grok.

## Límites

P3 restantes: ambigüedad al duplicar una referencia obsoleta mediante una
readición pública fuera de las API normales; cierre en alternancia y doble
purga fuera de la ruta por fotograma; escaneo preexistente acotado a diez
embarcandos. Las ramas defensivas de deselección tienen cobertura limitada.
`Ship.TryEmbark` ya llamaba a `soldier.Select(false)`; la prueba aporta
pertenencia a la lista, no demuestra corregir una fuga nueva del anillo.
Asimismo, `Selected=false` del reemplazo tras `Clear()` también se satisface
por `Initialize`; no prueba por sí sola que la limpieza no lo haya tocado.

No hay mediciones nuevas de rendimiento ni prueba física ARM. La medición
aislada de `624743c` permanece completa y separada en
[PERFORMANCE-v0.21.json](PERFORMANCE-v0.21.json).

## Exportaciones

Las exportaciones anteriores `edbc79e` siguen archivadas en
`Builds/Archive-v0.21-edbc79e`. Las candidatas terminaron correctamente con
Unity 6000.3.23f1 y targets explícitos Win64/WebGL. Se han calculado los hashes
de 194 archivos: 185 Windows (207.090.560 bytes) y nueve Web (57.435.975 bytes).
Son tamaños del directorio, distintos del total comunicado por Unity.
La procedencia se apoya en los logs de compilación posteriores al commit y
el manifiesto de fuente congelado; el ejecutable no incorpora un sello Git.

Windows completó siete capturas del reproductor y emitió
`RISKAI_UI_CAPTURE_OK`; el proceso terminó. Se inspeccionaron el retrato con
estadísticas y el catálogo naval con sus dos colas de cinco posiciones.
El fixture usa API de juego y desactiva `RtsController.Update`: verifica
presentación, no entrada física ni reutilización de objetos durante pérdida
de foco. No se observó directamente su código de salida.

En Edge 152.0.4191.66, la primera pasada CDP seleccionó dos ballesteros
reclutados normalmente con área táctil y lápiz, mostró el roster en escritorio,
horizontal y vertical y seleccionó una tarjeta en vertical. Doble toque y
toque de dos contactos movieron ambos; el botón secundario del lápiz inició
movimiento de ambos, con un superviviente al final. No se inyectó estado Unity.

Esa pasada no confirmó la tarjeta de escritorio ni la recuperación del grupo.
El seguimiento limitado a esos recorridos, con teclas solapadas durante varios
fotogramas y contacto táctil de 200 ms, confirmó selección individual por toque
y ratón y recuperación de dos tropas tras limpiar o elegir una tarjeta.
No se modificó el runtime. Esto no demuestra que todo toque físico breve se
registre correctamente. Cambiar de página dejó las dos tropas seleccionadas,
sin certificar pérdida de foco del sistema operativo ni reutilización del pool.
Ese último caso está cubierto por las regresiones Unity, no por estas capturas.

Ambas pasadas terminaron sin excepciones JavaScript. Se conservan favicon 404,
tres diagnósticos de shaders auxiliares y seis avisos `INVALID_ENUM` al consultar
formatos WebGL; no hubo avisos de incompatibilidad de sampler. Se publican
[primera pasada](selection-lifetime-v0.21/web-first-pass.json) y
[seguimiento](selection-lifetime-v0.21/web-followup.json), sin convertir la
finalización del script en un veredicto automático de juego.

Capturas inspeccionadas:

- [Retrato Windows](selection-lifetime-v0.21/windows-portrait.webp) y [puerto](selection-lifetime-v0.21/windows-port.webp).
- [Tarjeta táctil escritorio](selection-lifetime-v0.21/web-desktop-touch.webp), [ratón](selection-lifetime-v0.21/web-mouse-card.webp) y [grupo recuperado](selection-lifetime-v0.21/web-group-recall.webp).
- [Roster vertical](selection-lifetime-v0.21/web-portrait-roster.webp), [tarjeta vertical](selection-lifetime-v0.21/web-portrait-card.webp) y [horizontal](selection-lifetime-v0.21/web-landscape-roster.webp).

Las comprobaciones pendientes de exportación de este parche quedan cerradas
con el alcance anterior. La fase 2 completa, los dispositivos físicos y las
decisiones de costa y tamaño mínimo de ola siguen abiertos. No se hizo merge.
