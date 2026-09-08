# Seguimiento v0.22: costa, Web y navegación

Geometría y sonda en `61f386d`; optimización del minimapa en `2a9192b`, posteriores
a la [primera validación](VALIDATION-v0.22.md). Las builds anteriores se
conservan localmente en `Windows-v0.22-before-coast` y
`Web-v0.22-before-coast`. La versión de costa anterior al cambio del minimapa
queda en las carpetas `Windows-v0.22-coast-baseline` y
`Web-v0.22-coast-baseline`; las rutas habituales apuntan a la versión nueva.

## Costa y pruebas

Europe y NewWorld redondean las esquinas de su costa con desplazamientos
máximos de 0,2 celda (0,512 m). Se protegen las anclas de ciudades en 9 m y
de puertos en 24 m. Terreno, agua y collider comparten vértices; altura,
agua y clasificación terrestre resuelven los mismos triángulos, incluidos
los puntos que cruzan una celda fuente. No se añaden triángulos ni draws.
El campo compartido de arena/roca conserva sus coordenadas de mundo.

Se aprobaron **10 pruebas EditMode y 10 PlayMode**, con siete casos nuevos
de geometría y tres de preparación de la sonda. Incluyen límites, orientación,
inversión, islas, protección de anclas, muelles, desembarco real y coincidencia
de render/agua/collider con las consultas CPU. Las 12 pruebas Python del
comparador también pasan. La revisión se repartió entre los dos agentes
Astra y el coordinador; cada agente revisó el ámbito del otro.

## Minimapa Web

La prueba Web con 900 soldados completó 90 s y 27.000 órdenes sin rechazos,
pero observó 85,92 ms por fotograma. Los tiempos medidos de simulación no
explicaban la mayor parte del intervalo. Suprimir sólo los draws WebGL dejó
74,31 ms; ocultar el minimapa mediante su control normal dejó 37,18 ms con
el dibujo de la escena activo. Las tres pasadas usaron la misma semilla,
cohorte y órdenes. Hubo compilación externa variable: estos valores orientan
el diagnóstico y no son una comparación limpia de hardware.

El minimapa emitía miles de primitivas IMGUI por repaint para las tropas,
ciudades y puertos. Ahora rasteriza sus marcadores en una textura reutilizada
a 10 Hz y dibuja esa capa una vez. Conserva colores, selección, orden de
superposición y posiciones; el terreno, marco y encuadre de cámara permanecen,
y la cámara y la interacción se actualizan por fotograma. El minimapa sigue
con su visibilidad habitual: visible en escritorio y accesible mediante la
pestaña Mapa en vertical. Los marcadores tienen un intervalo nominal de refresco
de 100 ms, además de la espera hasta el siguiente fotograma.

Cinco pruebas EditMode comprueban orientación, transparencia y composición,
formas, escalado y recorte. Dos PlayMode comprueban selección, cadencia,
reutilización, redimensionado y destrucción. **27 casos Unity aprobados en
esta continuación**; las suites sin cambios no se repitieron.

## Preparación y aceptación de mediciones

La sonda avanzada registra ciudades, puntos navegables, reclutas y móviles
por jugador. Si no consigue reclutas azules, puede seguir hasta seis móviles
azules supervivientes; nunca crea una facción sin ciudades ni cambia dueños.
Distingue eliminación de ausencia de tropas terrestres elegibles.

El éxito exige completar la duración real solicitada, observar movimiento,
cero rechazos y cola vacía. El comparador exige observaciones de ruta y
velocidad y excluye el calentamiento de las estadísticas. `--advanced-only`
permite repetir esa fase sin volver a ejecutar una comparación ya válida.
Las negativas de preflight conservan el estado de memoria y procesos.

El escenario de navegación sostiene 900 soldados reales y conserva las
mismas posiciones y órdenes entre presupuestos. Congela actores originales
y refuerzos: prueba navegación sostenida, no combate sostenido con 900.
Las ventanas Windows ocultas miden latencia y tiempos del proceso; no son
una certificación de FPS de una partida visible o de entrada física.

## Resultados

Windows y Web están exportados en `2a9192b`. La evidencia y los hashes de
las builds están en el [recibo](audits/v0.22-followup/receipt.json).

| Web, 900 unidades y 90 segundos | ms/fotograma, media | Máximo | Frames >100 ms |
| --- | ---: | ---: | ---: |
| Inicial, minimapa visible (`61f386d`) | 85,92 | 162 | 131 |
| Diagnóstico sin draws WebGL | 74,31 | 172 | 11 |
| Diagnóstico con minimapa oculto | 37,18 | 72 | 0 |
| Final, minimapa visible (`2a9192b`) | 56,63 | 84 | 0 |

Todos mantuvieron 900 unidades, movieron las 900 y aplicaron 27.000 órdenes
sin rechazos. Comparten semilla `19031` y fixture `74F57FD4`; hubo carga
externa variable y no se acepta esta tabla como A/B limpio ni como garantía
de FPS. La pasada final conserva 13.500 observaciones de ruta y velocidad;
envío→velocidad promedia 381,51 ms, máximo 774 ms. La fluidez con esa carga
todavía queda por debajo de 30 FPS: **no se da por resuelto todo el rendimiento
Web**. [Resultados y métricas](audits/v0.22-followup/web-measurements.json).

La prueba de interfaz Web final inicia una batalla mediante entrada de lápiz
CDP y comprueba cámara en pausa, gesto de dos dedos, menú y rueda del ranking,
sin errores del juego. Se conserva evidencia visual de esas acciones.
[Resultado de interfaz](audits/v0.22-followup/web-minimap-ui-r1.json).
Son pruebas en Edge de escritorio, no en hardware ARM físico.
NewWorld también carga a 768×1024 con el HUD compacto y sin errores de página
o shaders. [Resultado vertical](audits/v0.22-followup/web-minimap-world-portrait-r1.json).
Sólo se observa el 404 conocido del favicon.

La comparación Windows anterior a la costa usa `8de052e`, semilla `160212`,
fixture `F2CBE9FE` y 900 unidades durante 90 s. Los brazos 500 y 1000 completan
27.000 órdenes cada uno sin rechazos y sin compiladores ni presión de RAM
detectados. El brazo 2000 tuvo compilación Rust externa y se rechaza para
comparación; las repeticiones no arrancaron por el mismo preflight.

| Presupuesto | Aplicación→ruta, media/máximo | Envío→velocidad, media/máximo | Frame máximo |
| --- | --- | --- | --- |
| 500 | 69,80 / 151,68 ms | 92,86 / 170,82 ms | 30,36 ms |
| 1000 | 51,94 / 102,37 ms | 89,21 / 154,27 ms | 47,81 ms |

La pequeña diferencia de latencia total entre estos dos brazos no justifica
cambiar el valor compartido. **Se mantiene 500**. Las medias de etapas con
distintos conteos no se suman; envío→velocidad es una observación directa.
[Registros nativos y entorno](audits/v0.22-followup/native-measurements.json).

La preparación avanzada corregida completó 900 s simulados y otros 90 s
de medición en `61f386d`: 942→454 unidades, seis identidades movidas,
1.291 órdenes aplicadas, cero rechazos y cola vacía. Esta pasada confirma
funcionamiento, pero tuvo compilación externa y tampoco es una validación
limpia de latencia. [Registro](audits/v0.22-followup/advanced-functional.json).

Siguen pendientes la repetición limpia de 2000, la confirmación avanzada
sin contención, optimización adicional para alcanzar mayor fluidez con
900 unidades en Web y comprobación física ARM. El combate real con 800
unidades sostenidas no queda demostrado por una cohorte de navegación.
