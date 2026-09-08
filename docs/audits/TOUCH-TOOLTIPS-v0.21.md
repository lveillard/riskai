# Ayuda por pulsación larga · v0.21

Fuente y exportaciones `376d588`. El mismo `RtsRuntimeTooltip` muestra la ayuda
existente al mantener un dedo o la punta del lápiz durante 500 ms. El gesto
tolera 12 puntos lógicos de movimiento; un desplazamiento mayor cancela la
ayuda y deja que el control nativo gestione el arrastre o scroll. El hover de
ratón/lápiz conserva su espera de 350 ms.

Reconocer la ayuda cancela el clic nativo: soltar no compra, cancela encargos
ni cambia la selección. El toque corto posterior sigue activando una vez.
La etiqueta ignora picking y usa el mismo texto y panel en escritorio y
compacto. No se cambian reglas, mapas ni órdenes de simulación.

## Captura y cancelación

Los botones de UI Toolkit capturan el contacto al pulsar. En Unity 6000.3
los eventos capturados no recorren los callbacks de sus antecesores; por eso
el componente observa temporalmente movimiento, liberación y cancelación
en el elemento que tiene la captura, sin apropiarse de ella. Al terminar
retira esos callbacks.

Una cancelación nativa al reconocer el hold desarma tanto `Clickable` como
la síntesis independiente de `ClickEvent`. Multicontacto, cancelación de
entrada, cambio de contenido, desconexión del dispositivo, detach, disposición
y pérdida de foco limpian el seguimiento. Los cuatro botones secundarios del
lápiz se consultan directamente: los bindings UI predeterminados no exponen
todos esos botones. Un tip modificado no se interpreta como petición de ayuda.

## Validación y revisiones

El [resultado publicado](touch-tooltips-v0.21/tests.xml) de la tercera
ejecución dirigida aprobó **35/35 casos en 89,395 s**, incluidos
los **15 nuevos** de `RtsRuntimeTooltipTests`. Se verificaron explícitamente
descubrimiento, nombres, resultados y hashes de fuente. Las otras veinte
regresiones cubren HUD, selección de edificios, entrada directa y rueda.

Los casos nuevos usan dispositivos InputSystem y un panel adjunto real:
toque corto, hold y siguiente toque, botón que conserva captura con 6/20 puntos
de movimiento, scroll, segundo contacto, cancelación, reconstrucción,
detach/reattach, disposición, botón deshabilitado, hover y combinaciones
físicas de controles de lápiz. Comprueban acciones de botón y `ClickEvent`,
no sólo el estado interno del reconocedor.

Los intentos anteriores se conservan. En r1 pasaron veinte casos existentes,
pero Unity ignoró el fixture nuevo por un GUID `.meta` inválido: no cuenta
como aceptación de la nueva ayuda. r2 corrigió ese archivo, pero no ejecutó
pruebas por la ambigüedad de `PenButton` entre InputSystem y UIElements.
r3 sólo añadió un alias explícito en el test respecto al runtime revisado r2.

Tres contextos independientes de Codex API, verificados como Azure /
`gpt-6-astra` / esfuerzo medium, revisaron el cambio y sus seguimientos.
Identificaron dos P2: movimiento perdido bajo captura y observación del evento
de cancelación en el antecesor equivocado. Ambos están corregidos; no quedan
hallazgos concretos abiertos en sus revisiones proporcionales. No participó
Grok. La revisión estática no sustituye las ejecuciones anteriores.

## Exportaciones

Windows y WebGL terminaron correctamente con targets explícitos y Unity
6000.3.23f1. Se verificaron los hashes de 194 archivos: Windows, 185 archivos
y 207.094.688 bytes; Web, nueve archivos y 57.438.558 bytes. Son tamaños del
directorio exportado. La versión anterior sigue archivada y verificada en
`Builds/Archive-v0.21-f05c586`.

El [recibo publicado](TOUCH-TOOLTIPS-v0.21.json) incluye hashes, manifiesto,
nombres y resultados de pruebas, informes completos de los seis dictámenes,
identidades verificadas de revisores y límites de cada comprobación. La
procedencia se apoya en las invocaciones de build posteriores al commit y
fuente congelada; el reproductor no lleva un sello Git interno.

En Edge 152.0.4191.66, mediante CDP, dos ballesteros reclutados normalmente
conservaron su selección después de mantener y soltar una tarjeta. La ayuda
se mostró con dedo y punta del lápiz; el siguiente toque corto seleccionó
una unidad. Se comprobó lo mismo en vertical, con la etiqueta dentro del
viewport. El hover de ratón se conserva y el arrastre cancela la ayuda.

La comprobación de producción se hizo sin pausa, en un puerto propio y con
oro suficiente: mantener y soltar Transporte dejó **4 de oro y 0 barcos en
cola**. El toque corto siguiente dejó **2 de oro y 1 barco en cola**. No se
inyectó estado Unity ni se usó una compra bloqueada como prueba de cancelación.

Ambas sesiones Web terminaron sin excepciones JavaScript. Persisten favicon
404, tres diagnósticos auxiliares de shader y seis consultas WebGL con
`INVALID_ENUM`; no hubo avisos de sampler incompatible. Windows completó
siete capturas con `RISKAI_UI_CAPTURE_OK`; se inspeccionaron retrato y colas
navales. Ese fixture desactiva `RtsController.Update` y comprueba presentación,
no entrada táctil física. No se observó directamente su código de salida.

- Escritorio: [ayuda](touch-tooltips-v0.21/desktop-help.webp), [soltar](touch-tooltips-v0.21/desktop-release.webp), [toque corto](touch-tooltips-v0.21/desktop-tap.webp) y [lápiz](touch-tooltips-v0.21/pen-help.webp).
- Vertical: [ayuda](touch-tooltips-v0.21/portrait-help.webp), [soltar](touch-tooltips-v0.21/portrait-release.webp) y [toque corto](touch-tooltips-v0.21/portrait-tap.webp).
- Compra real: [ayuda](touch-tooltips-v0.21/production-help.webp), [soltar sin comprar](touch-tooltips-v0.21/production-release.webp) y [toque que compra](touch-tooltips-v0.21/production-tap.webp).
- Windows: [retrato](touch-tooltips-v0.21/windows-portrait.webp) y [puerto](touch-tooltips-v0.21/windows-port.webp).

## Límites

La entrada sintética no prueba una tablet ARM, un lápiz físico ni los eventos
de foco del sistema operativo. Los hooks de foco y desconexión existen, pero
el fixture no certifica esos recorridos físicos, múltiples raíces UI vivas
ni todos los órdenes posibles de desmontaje de paneles. No hay nueva medición
de rendimiento. La fase 2 completa y sus decisiones pendientes siguen abiertas.
