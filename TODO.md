# RiskAI — decisiones y siguientes pasos

Feedback del 6 de septiembre de 2026. Las reglas verificadas y las adaptaciones se distinguen en [la auditoría de Risk](docs/RISK-RULES-v0.12.md). Esta lista conserva también las propuestas que todavía no están implementadas.

## Implementado en v0.12

- [x] Edificios y torres permanentes; el objetivo de combate es su guarnición. Cambio de dueño conjunto.
- [x] Sucesión inmediata: aliado cercano primero, después enemigo más próximo, neutral si no hay candidato. Mantener vida e identidad del soldado.
- [x] Anillos inspirados en WC3 bajo las unidades y alrededor de la ocupación; color marfil para neutrales.
- [x] IA que interrumpe expediciones para defender posiciones amenazadas y conserva una reserva.
- [x] Oro en la escala del mapa, recompensa fraccionaria por bajas, perfiles navales comunes y refuerzos limitados por valor vivo.
- [x] Hogueras como puntos de refuerzo; selección con superposición territorial y ciudades resaltadas. Suelo sin grandes tintes permanentes por propietario.
- [x] Dos escenarios seleccionables: Las Marcas y Cuatro Riberas, con cuatro dominios continentales y archipiélago.
- [x] Río más largo por el centro del segundo mapa, montaña de origen, más espacio al norte y cruces navegables por infantería.
- [x] Desvanecimiento correcto de sombras por distancia y variación moderada de pradera y agua, anclada al mundo y sin pases adicionales.
- [x] Menú dividido en partida, controles y ajustes. Eliminar el modo Capitales de esta adaptación.
- [x] Botón derecho: pulsación y suelta dan una orden; arrastre mueve cámara y no emite una orden accidental.
- [x] Desventaja de tiro perforante cuesta arriba: 25 % de fallo con desnivel de al menos 2,5 unidades. No aplicar a pequeñas ondulaciones, cuerpo a cuerpo ni hechizos.
- [x] Compilación Windows y 97 pruebas (32 EditMode + 65 PlayMode).
- [x] Inspección del ejecutable: ciudades, puertos, islas, río, hogueras, menú y zoom en ambos escenarios.
- [ ] Recoger feedback jugado de esta versión.

## Entrada táctil y lápiz — todavía pendiente

Intenciones compartidas con ratón, sin mutar directamente salud, propiedad u oro:

- [ ] Un dedo: toque selecciona; arrastre crea selección en área.
- [ ] Dos dedos: desplazamiento conjunto mueve cámara; pinza cambia zoom manteniendo el centro del gesto sobre el terreno.
- [ ] Toque de dos dedos sin movimiento: orden contextual, equivalente a clic derecho.
- [ ] Doble toque con un dedo: orden contextual. Resolver la espera del toque simple y su conflicto con seleccionar todas las unidades del mismo tipo; conservar el doble clic tradicional para ratón.
- [ ] Lápiz: selección y arrastre equivalentes al dedo; acción secundaria o doble toque para orden contextual según dispositivo.
- [ ] Arbitrar cuándo el segundo dedo cancela una selección pendiente; cancelar gestos al perder foco, salir de ventana o abrir menús. Una pinza/arrastre nunca termina enviando una orden.
- [ ] Separar Input Actions y gestos del adaptador de órdenes. `Core/PointerGesture` y `RtsCameraRig.Drag/ZoomAt/Pan` son los primeros límites, no soporte táctil completo.
- [ ] HUD adaptable y botones aptos para dedos: evitar simplemente encoger el HUD de escritorio. Probar tablet real, trackpad y stylus.

## Servidor y plataformas — todavía pendiente

- [ ] Servidor autoritativo con conexión autenticada, validación de órdenes y snapshots. La identidad del jugador la decide el servidor.
- [ ] Incorporar compras, barcos, embarque y desembarque al protocolo de comandos; actualmente la cola por ID cubre infantería.
- [ ] Separar vistas y arranque de arte para Dedicated Server; navegación autoritativa sin exigir que NavMesh coincida entre clientes.
- [ ] Transporte compatible con navegador, reconexión, latencia, visión por jugador y niebla de guerra.
- [ ] Build Web y Android, medir memoria/descarga/renderizado e input en dispositivos. No hay validación de esas plataformas todavía.

## Mapas, reglas y presentación

La [investigación de World Editor](docs/WORLD-EDITOR-TERRAIN.md) orienta el siguiente flujo de autoría.

- [ ] Convertir alturas, biomas, ciudades y cruces en datos editables con vista previa.
- [ ] Añadir pinceles propios de subir, bajar, aplanar y suavizar; visualizar navegación y huellas de edificios. Mantener materiales separados de las reglas de paso.

- [ ] Comparar el mapa original jugándolo en una instalación propiedad del usuario y en World Editor. Esto permite comprobar valores heredados; no proporciona el código fuente del motor Warcraft.
- [ ] Obtener una copia verificable de Reforged: Rome antes de atribuirle estadísticas. Saran, New World y Rome no son la misma variante.
- [ ] Exportar datos y perfiles desde una fuente externa, conservando procedencia, campos heredados y ajustes locales. Completar tipos de unidades y barcos de forma gradual.
- [ ] Refinar puntos de refuerzo y rendimientos según número de ciudades, pasos de entrada y dificultad de defensa. Comparar grupos pequeños de islas con grandes dominios; medir por valor de unidades además de cantidad.
- [ ] Refinar acantilados, encuentros de terreno con puentes, estuario y distribución de árboles con capturas y partidas. Mantener el agua eficiente y sin ondulaciones geométricas exageradas.
- [ ] Añadir más biomas y variación local de tiles sin perder legibilidad de unidades y fronteras.
- [ ] Migrar menú/HUD a UI Toolkit, perfiles a datos, mapas/NavMesh a horneado en editor y carga de arte a un sistema adecuado cuando se mida su necesidad.
- [ ] Medir CPU y GC a 100/200 unidades antes de Jobs/Burst o ECS. El tick fijo no hace determinista por sí solo la navegación.
