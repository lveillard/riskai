# Validación v0.20 · identidad RTS y primer reproductor Web

## Interfaz compartida

Se recuperan los marcos de madera, hierro y latón, los títulos Cinzel y los
retratos existentes. Una textura de marco compartida, importada a un máximo
de 1024 px, usa nueve secciones para mantener las esquinas al adaptar los
paneles; los botones conservan decoración vectorial retenida. No añade lógica
de mapa ni un bucle de animación por control. La fuente
Cinzel Decorative se distribuye sin modificar con su licencia SIL OFL 1.1.

En escritorio, selección y órdenes/producción aparecen simultáneamente,
junto al minimapa. En ancho compacto se mantienen las pestañas y los mismos
constructores de controles y ejecutores de comandos.

El marco `Resources/UI/WarTableFrame-v20.png` se generó con la herramienta
integrada de imágenes. El prompt completo está en
`Art/UI/WarTableFrame-v20.prompt.txt`: roble oscuro, hierro ennegrecido, latón
y centro de cuero oscuro vacío, preparado para dividirse en nueve secciones.
No reutiliza una textura de Warcraft. La primera inspección visual detectó
la fuente genérica y la segunda fila de reclutas recortada; se corrigieron
la asignación de fuente y la altura/disposición de esos botones.

## Pruebas dirigidas

`TestResults/v20-ui-first.xml`: 23/24 aprobadas (HUD, frontend, cámara,
lápiz Web y entrada directa). El fallo restante pertenecía al fixture de
vista estratégica: `SendMessage("LateUpdate")` también ejecutaba la cámara
del mismo GameObject y cambiaba su zoom antes de comprobar el umbral.

La presentación expone ahora `RefreshPresentation`, utilizada tanto por
`LateUpdate` como directamente por el fixture. La prueba comprueba además
que refrescar la representación no mueve la cámara.
`TestResults/v20-camera-second.xml`: **10/10 aprobadas**, incluida la
sustitución exacta del fallo. No se presenta como una nueva ejecución
completa de las 213 pruebas de v0.19.

La revisión Grok 4.6 produjo cinco avisos; al contrastar el controlador
completo se descartaron tres. Se distingue el tipo de modal para sustituir
ayuda por victoria y se acota explícitamente el scroll compacto. La prueba
nueva alcanza una victoria por la regla normal de conquista con ayuda ya
abierta y comprueba el resultado retenido. `TestResults/v20-hud-final.xml`:
**2/2 aprobadas**. La unión histórica añade un caso (214 distintos), sin
afirmar que todos se hayan repetido completos.
[Revisión y descartes](audits/GROK-v0.20.md).

`TestResults/v20-harbor-shared.xml`: **4/4 aprobadas** en una primera corrección
que ocultaba el radio de embarque hasta seleccionar el puerto. La inspección
visual posterior confirmó que ese radio adicional seguía ensuciando la selección.
La corrección final elimina su representación: cada puerto conserva el círculo
compartido de selección de edificio y el pequeño de guarnición. La zona de
embarque mantiene su radio y consultas; no hay una política diferente por mapa
ni un barrido de todas las zonas desde cada Harbor.Update.

La inspección no reprodujo un círculo grande persistente en un puerto
deseleccionado después de su primer Update. No se presenta como una reversión
demostrada ni como un fallo específico de Europe. La nueva regresión comprueba
los cuatro escenarios, la ausencia del radio adicional al seleccionar, el
círculo común y la conservación del alcance de embarque.

`v20-harbor-no-radius.xml` aprobó la regresión de los cuatro mapas y dos
casos de presentación. El cuarto falló porque todavía exigía el radio azul
de la política anterior. Se actualizó esa expectativa conservando las
comprobaciones de la guarnición y las entradas NavMesh;
`v20-harbor-presentation-final.xml`: **3/3 aprobadas**. Los cuatro casos
afectados tienen resultado final aprobado. La suite histórica añade dos
regresiones v0.20 (modal y cuatro mapas); no se presenta como una repetición
completa de todos los casos anteriores.

La segunda compilación Windows terminó en `v20-build-second.log`:
206.855.032 bytes. Sus capturas `Captures/v20-desktop-final` (1600×900) y
`Captures/v20-compact` (360×800) verificaron tipografía, retrato, seis reclutas
visibles a la vez en escritorio y presentación compacta. La compilación de
entrega incorpora después el cambio de tipo de modal y el ajuste de ancho
de tarjetas verticales. Esa compilación terminó en
`RiskAI/Logs/v20-build-release.log`: **206.856.120 bytes**. Es la build local
de `Builds/Windows-v0.20/RiskAI.exe` y el destino de `Play-RiskAI.cmd`.

La build posterior al arreglo definitivo de muelles es
`RiskAI/Logs/v20-build-no-radius.log`: **206.855.640 bytes**. Reemplaza el
ejecutable de esa misma carpeta. Sus capturas automáticas completas emitieron
`RISKAI_UI_CAPTURE_OK` en 1600×900 (`Captures/v20-no-radius-desktop`) y
1024×768 (`Captures/v20-no-radius-tablet`). Se inspeccionaron el puerto
seleccionado sin radio azul y el panel horizontal compacto con los seis
reclutas visibles. [Puerto](images/v0.20-port.png) · [Horizontal compacto](images/v0.20-tablet.png).

## Exportación y navegador

Web Build Support está instalado. La primera exportación real, todavía
v0.19, terminó en `RiskAI/Logs/v19-web-first.log`: 56.971.673 bytes.
En Edge 152.0.4191.66 el menú arrancó; la batalla falló porque el stripping
había eliminado SphereCollider, usado por GameObject.CreatePrimitive.

`scripts/check_web_player.py` conserva consola, capturas y un resultado
JSON del reproductor real. Exige el marcador de batalla preparada y trata
los errores C# de consola como fallo, además de las excepciones JavaScript.
La repetición `Captures/web-v19-classic-confirmed/result.json` confirma el
fallo. La ejecución anterior `web-v19-classic` no es evidencia de batalla
correcta: sólo había esperado al loader.

La corrección v0.20 conserva los componentes geométricos usados por los
primitivos en `Assets/RiskAI/link.xml`, conforme a la
[documentación de CreatePrimitive](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GameObject.CreatePrimitive.html).
`RiskAI/Logs/v20-web-first.log` terminó con **57.363.845 bytes**. En
`Captures/web-v20-classic/result.json`, Edge cargó el loader en 6,553 s y
emitió batalla preparada en 14,757 s; sin errores C# ni JavaScript. Heap
WASM observado: 278.396.928 bytes (no es toda la memoria del navegador).
El único error de recurso fue un favicon ausente.

Una primera sonda Europe se rechazó por pasar cero reclutas desde la
utilidad. Se corrigió su valor por defecto a los seis de RuntimeCommandProbe
y se acotó 1–100. La siguiente ejecución duró sólo 30 s, mientras que el
contrato de esa sonda exige más de 30 segundos simulados: 29,90 s no es
una medición aprobada. Se conserva su resultado fallido y se repite con
60 s; la utilidad ya rechaza duraciones incompatibles. Ninguno de esos
dos fallos se presenta como una regresión del combate.

`Captures/web-v20-europe-60/result.json` completó la sonda de 60 s:
293→321 unidades, 118 órdenes aplicadas, cero rechazadas o pendientes,
seis de seis tropas seguidas desplazadas. Carga lista en 17,707 s;
heap WASM observado de 400.949.248 bytes. **La aprobación es funcional,
no de rendimiento:** 1.078 fotogramas, 55,90 ms de media, 741 ms máximo,
137 por encima de 50 ms y 70 por encima de 250 ms. La simulación avanzó
58,20 s durante la ventana de 60 s reales.

No había una operación Unity simultánea; sí seguía una compilación externa
en el equipo. La prueba usó Edge headless con GPU NVIDIA y no permite
separar aún renderizado, sobrecarga del navegador y carga ajena. No cumple
el rendimiento deseado; no es válido deducir una mejora frente a Windows
ni atribuir los picos sólo al número de unidades. Queda revisión Web y
medición con causas acotadas antes de presentar una versión tablet lista.

La primera build Web precede al ajuste posterior del indicador de embarque
y a las tres columnas de reclutas en horizontal compacto. Sus resultados
se atribuyen a esa build concreta, no a un artefacto aún sin exportar.

La exportación final que incluye la retirada completa del radio azul terminó
en `RiskAI/Logs/v20-web-no-radius.log`: **57.361.621 bytes**. Se genera desde
el mismo proyecto y componentes de la build Windows final. La exportación
intermedia `v20-web-release.log` (57.365.390 bytes) todavía conservaba el
radio azul al seleccionar y queda superada por ésta.

La sonda final `Captures/web-v20-final-europe/result.json` vuelve a aprobar
funcionalmente: 293→319 unidades, 85 órdenes aplicadas, cero rechazadas o
pendientes y seis de seis tropas desplazadas. Persiste el mal rendimiento:
64,95 ms medios, 562 ms máximo, 166 fotogramas por encima de 50 ms y 61
por encima de 250 ms en 60 s reales. Antes de esta pasada quedaban unos
4,9 GB libres y seguía un rustc ajeno con unos 4,4 GB; no había Unity ni un
player nativo ejecutándose. La retirada del aro no se presenta como una
solución de estos tirones.

Al terminar el compilador ajeno se repitió la vista táctica, sin cambiar
la build: `Captures/web-v20-final-europe-idle-host`. Con unos 9,7 GB libres
antes de arrancar, midió **37,20 ms** de media, 62 ms máximo, 33 frames
por encima de 50 ms y **cero por encima de 100 ms**. Aplicó 123 órdenes,
sin rechazos/pendientes, y desplazó las seis tropas. Esta diferencia obliga
a separar carga del equipo y coste del juego; no se atribuye a un parche
de rendimiento. La media todavía no alcanza el objetivo de 60 FPS.

La primera ejecución etiquetada `web-v20-final-europe-overview` no activó
la vista estratégica: la utilidad enviaba FrameMap al GameObject Camera
y el rig vive en Bootstrap. Consola y captura permitieron detectarlo;
se marcó ese resultado como experimento fallido y se corrigió el destinatario.
El script ahora también rechaza mensajes sin receptor. Sus 28,13 ms no
son evidencia de rendimiento de la vista estratégica.

La repetición corregida `web-v20-final-europe-overview-confirmed` sí mostró
el mapa estratégico en la captura inspeccionada. En 60 s: **28,35 ms**
medios, 56 ms máximo, un frame por encima de 50 ms y ninguno por encima
de 100 ms; 127 órdenes aplicadas sin rechazos/pendientes, seis tropas
desplazadas. Es una medición de esa vista existente, no una nueva optimización
ni una certificación de 60 FPS. Se conserva la pasada táctica anterior para
comparar sin mezclarla con el experimento fallido.

`Captures/web-v20-final-world-restart` completó tres ciclos NewWorld → menú
sin errores C#/JavaScript. Entre el primer y último menú tras limpieza:
cero crecimiento en conteos de mallas, materiales, renderers y NavMeshData;
1.687.910 bytes más asignados por Unity, 3.293.568 reservados y 606.208
administrados. Heap WASM observado al inicio: 577.437.696 bytes. Estos
conteos estables no prueban ausencia de fugas ni que los bytes se estabilicen
en sesiones más largas.

Europe también completó tres ciclos en
`Captures/web-v20-final-europe-restart`: los cuatro conteos quedaron estables;
delta asignado Unity de 58.810 bytes, reservado de 845.603 y administrado
de 253.952. Es una comprobación funcional de reinicios con sus contadores,
no un benchmark de velocidad; se ejecutó junto a la comprobación de UI.

`Captures/web-v20-input/result.json` comprueba el enlace real navegador →
plugin → Pen virtual → UI Unity: dos eventos CDP de lápiz pulsaron el botón
de comenzar y el juego emitió batalla preparada. Después, un toque CDP
abrió el menú de pausa; se inspeccionó `touch-menu.png`. No hubo errores C#
ni JavaScript. El fixture reutilizable es `scripts/check_web_ui.py`.
Esta comprobación supera la prueba DOM aislada anterior, pero no verifica
un lápiz físico ni todos los gestos u órdenes sobre el mapa.

La utilidad final, revisada para guardar errores y cerrar Playwright en
el orden correcto, repitió la comprobación sobre la exportación definitiva:
`Captures/web-v20-final-input/result.json`, **success=true**. Dos eventos
de lápiz llegaron a Unity e iniciaron una batalla; se inspeccionó de nuevo
la captura del menú abierto mediante toque. No hubo errores ni fallos de
cierre del navegador.

`Captures/web-v20-final-riverlands` comprobó también la carga de Cuatro
Riberas: batalla preparada, sin errores C#/JavaScript y heap WASM observado
de 278.396.928 bytes. Junto a Classic en la prueba de UI, Europe en las
sondas y NewWorld en reinicios, los cuatro mapas se han abierto en la
exportación final; esto no sustituye una partida larga en cada escenario.

Las comprobaciones de navegador de este equipo usan una GPU NVIDIA en
Windows. No representan hardware ARM ni validación de lápiz físico. El
modelo de tablet de referencia aún no está confirmado.
