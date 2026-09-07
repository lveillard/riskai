# Fase 2 · tablet, móvil y navegador

Base cerrada: v0.18, commit cd3ff21. Objetivo activo desde el 7 de septiembre
de 2026. Primera plataforma de referencia: Chrome en Android; un dispositivo
concreto todavía no ha sido confirmado.

## Contrato de producto

Un solo proyecto Unity y un solo juego. Las exportaciones Windows y Web son
artefactos del mismo código, mapas, perfiles y reglas. El dispositivo puede
cambiar interfaz, representación y entrada; no cambia daños, economía,
capturas, población, cámara por mapa ni frecuencia de simulación.

| Entrada | Acción |
| --- | --- |
| Un dedo o punta del lápiz | Toque selecciona; arrastre selecciona un área. |
| Dos dedos | Mueven la cámara por su centro y hacen zoom por su separación. |
| Toque de dos dedos | Una orden contextual al soltar, sin desplazamiento previo. |
| Doble toque de un dedo/lápiz | Orden contextual conservando la selección previa. |
| Botón secundario del lápiz, si se expone | Orden contextual. |
| Ratón | Mantiene clics, doble selección, rueda, arrastre derecho y teclado. |
| Trackpad | Usa los eventos de ratón/rueda/botón que entregue el sistema operativo. |

El toque simple espera 240 ms para distinguirlo del doble: seleccionar o
limpiar en el primer toque eliminaría el ejército antes de ordenar con el
segundo. Las acciones explícitas armadas en la barra no necesitan esa espera.
Un gesto que empieza en la UI le pertenece hasta terminar. Un segundo dedo
cancela una selección pendiente; un gesto de cámara nunca acaba dando una
orden. Se cancela el estado pendiente al perder foco o abrir un modal.

## Entregas y comprobación

1. Entrada semántica compartida, reconocedor puro, pruebas de ratón sin
   teclado, touch y lápiz sintéticos; mantener regresiones de guarnición,
   embarque y selección.
2. UI Toolkit para menú y controles, paneles adaptables y áreas seguras
   compartidas con la cámara; controles táctiles de 44 unidades lógicas.
   Etiquetas sobre el mundo pueden conservar IMGUI sólo en Repaint durante
   esta migración, sin una segunda implementación interactiva para móvil.
3. Catálogo de producción común y ejecutor local por identificadores para
   compras, cancelaciones y salidas. Preparar autoridad no significa que ya
   haya protocolo, deduplicación de red ni servidor.
4. Exportación Web con plantilla propia, densidad de canvas acotada,
   diagnóstico por fases de arranque y pruebas de partidas/reinicios. Medir
   en navegador real antes de reducir geometría o presupuesto de navegación.
5. Revisiones adversariales Grok 4.6, corrección, pruebas Windows/Web y
   documentación de las limitaciones de hardware realmente disponible.

## Producción y comandos locales

ProductionCatalog es la fuente compartida de la oferta regular: seis
reclutas de ciudad y tres marines de puerto, además de fragata y transporte.
Los puertos importados e independientes ofrecen el mismo catálogo, con colas
terrestre y naval diferenciadas. Settlement y Harbor validan el catálogo en el dominio, por lo
que una llamada desde IA o presentación no puede colar un tipo de unidad ajeno
a ese edificio.

PlayerBuildingIntent describe una acción por BuildingId, canal y valores
escalares. PlayerBuildingCommands es el ejecutor local de estas
intenciones: comprueba que la partida no esté pausada ni terminada, que el
propietario y el edificio sigan siendo válidos, que la cola y el índice de
cancelación existan y que el destino de reunión sea finito antes de delegar
en la regla de dominio. Incluye reclutamiento, compra de nave, cancelación y
punto de reunión de tierra. El DTO reserva una variante naval, pero ningún
edificio la acepta todavía: no se presenta esa reserva como funcionalidad.

Este límite mejora la consistencia entre UI e IA y deja una superficie
concreta para una autoridad futura. Sigue siendo ejecución local: no hay
transporte de red, servidor, secuencias distribuidas ni deduplicación entre
clientes.

## Recursos generados y reinicios

GeneratedResourceOwner es propietario de recursos de ejecución creados por
instancia y los destruye al descargarse su objeto raíz. Cubre las mallas y
materiales generados de arte naval y terreno estratégico, detalles de
acantilado y vida decorativa; no barre recursos compartidos, de Resources ni
cachés globales. Si se intenta registrar un recurso después de desechar el
propietario, ese recurso se destruye de inmediato.

Tras construir la navegación, RiskBootstrap registra el NavMeshData runtime
con el propietario de recursos. La prueba de escena comprueba que ese dato se
destruye al descargarla. Esto da una vida explícita a ese dato; no demuestra
por sí solo una mejora de memoria global.

RuntimeRestartProbe es opcional: --riskai-restart-probe repite de dos a cinco
cargas (tres por defecto, ajustable con --riskai-restart-cycles), deja tres
segundos reales de partida quieta, vuelve al menú y sólo entonces ejecuta
UnloadUnusedAssets y GC. Informa unityalloc, reserva, administrada, mallas,
materiales, renderers y NavMeshData con RISKAI_RESTART_METRICS. Su resultado
compara el primer y el último menú tras limpieza, no el menú con la primera
batalla. En nativo sale al final; en Web vuelve al menú. Es incompatible con
las sondas y capturas automáticas para conservar una medición interpretable.

Los argumentos de reinicio se admiten también mediante la lista blanca de
parámetros Web. La sonda no se activa en lanzamientos normales ni altera
reglas de partida.

## Plataforma y fuentes

El paquete instalado (`com.unity.inputsystem` 1.14.2) marca expresamente
**Pen: WebGL no**, aunque admite lápiz en Android nativo. No se puede trasladar
la validación de `Pen.current` de Windows a un navegador. El adaptador toma
sólo `PointerEvent.pointerType === 'pen'` del canvas y
alimenta un `Pen` virtual de Unity. La interfaz y las órdenes siguen usando
los consumidores existentes. [Matriz oficial de dispositivos](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/SupportedDevices.html).

El navegador debe identificar el dispositivo como lápiz; no se adivina que
un Mouse/Touch anónimo sea un stylus. Los eventos de compatibilidad pueden
variar, por lo que las pruebas de Chromium sintéticas no certifican un lápiz
físico. [Pointer Events](https://www.w3.org/TR/pointerevents/).

La cola DOM está acotada a 64 muestras y combina sólo movimientos que no
cambian botones. Unity lee una muestra por fotograma para conservar los
flancos de pulsación y liberación; desbordamiento, pérdida de captura o foco
cancelan el gesto antes de retirar el dispositivo virtual. Las coordenadas
se normalizan respecto al canvas y se convierten al espacio de pantalla de
Unity. El adaptador no emite órdenes de juego ni contiene reglas del mapa.

`node scripts/test_browser_pen.cjs` verifica el contrato DOM; `python
scripts/check_browser_pen.py` usa Playwright y un Edge instalado para enviar
eventos CDP al canvas (sin reproductor Unity). La v0.20 añade una prueba con
el reproductor exportado: el lápiz CDP comienza una batalla y el toque abre
el menú. Esta última sí recorre el enlace IL2CPP/WebGL, pero no certifica un
stylus físico ni todos los gestos y órdenes sobre el mapa.

Unity 6.3 admite navegadores móviles seleccionados, incluido Chrome Android
y Safari iOS. Se usarán versiones recientes. Esto no demuestra que RiskAI
ya cumpla un presupuesto de memoria o fotograma en esos dispositivos.
[Compatibilidad oficial](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-browsercompatibility.html).

El adaptador táctil usa EnhancedTouch; leer directamente Touchscreen en
Update puede perder cambios de estado. El hardware puede no exponer todas
las funciones de un lápiz, de modo que el doble toque sirve de alternativa.
[Entrada táctil](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Touch.html),
[lápiz](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Pen.html).

Web no ofrece hilos administrados de C#; no se basa esta adaptación en
Task.Run ni en trasladar la simulación a un hilo administrado. La memoria
del navegador incluye más que el heap WASM; los contadores se informarán
con su alcance.
[Limitaciones](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-technical-overview.html),
[memoria](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-memory.html).

## Estado

La base de comandos de producción, la propiedad explícita de recursos runtime
y la sonda opt-in de reinicio están implementadas y cubiertas por las
evidencias v0.19 documentadas en
[VALIDATION-v0.19.md](VALIDATION-v0.19.md). La tercera compilación Windows
terminó; el parche posterior de entrada está generado y reúne 213 casos Unity
distintos aprobados más ocho Python. El adaptador DOM añade 12 pruebas Node y
siete comprobaciones CDP en Edge sobre el adaptador DOM. La v0.20 añade
batallas y entrada sintética en el reproductor Web exportado, con los límites
descritos abajo. Los controles habituales usan 44 unidades lógicas; los botones del
encabezado compacto usan 40 para mantener visible el campo de batalla.

Web Build Support ya está instalado. La primera exportación real cargó el
menú en Edge, pero falló al crear el campo de batalla por un componente
eliminado durante stripping. La corrección v0.20 está verificada en batallas
Classic y Europe: la sonda Europe aplicó 118 órdenes, sin rechazos y con
movimiento de las seis unidades seguidas. Su media de 55,90 ms/fotograma
es insuficiente; no se presenta como una versión tablet lista.
Una repetición final sin ese compilador externo midió 37,20 ms medios y
62 ms máximo en vista táctica; la vista estratégica verificada dio 28,35
ms medios y 56 ms máximo. Ninguna prueba certifica 60 FPS ni ARM.
Europe y NewWorld completaron tres reinicios Web cada uno, con conteos
estables de mallas/materiales/renderers/NavMeshData; los bytes se informan
sin inferir ausencia de fugas.
Las verificaciones y la revisión Grok se siguen en
[VALIDATION-v0.20.md](VALIDATION-v0.20.md). No hay todavía medición en hardware ARM físico.
Las dos sondas nativas de reinicio no muestran crecimiento de conteos
de malla/material/renderers, pero sus bytes asignados no mejoraron; quedan
como datos de diagnóstico, no como una conclusión de ausencia de fugas.

La revisión visual v0.20 recupera el carácter de RTS: marcos ornamentados,
títulos dorados, retratos y selección junto a órdenes/producción en escritorio.
La distribución compacta conserva las pestañas y comparte los controles y
las reglas con escritorio. Adaptar el espacio no implica eliminar la
identidad visual ni reducir la información disponible en pantallas amplias.

La v0.20 Windows se genera en Builds/Windows-v0.20. Las builds anteriores
se conservan como referencia local. No se implementan servidor, multijugador, niebla ni héroes en esta
fase.
