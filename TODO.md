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

## Apertura y siguiente revisión (v0.13)

- [x] Un ballestero por ciudad y puerto, también neutral; sin ejércitos o flotas regalados.
- [x] Primer recluta asequible para la IA; priorizar refuerzo de ciudades amenazadas y frontera. Reserva compatible con una apertura sin ejército móvil; ahorro y compra de la primera fragata por cola.
- [x] Ampliar las islas al norte y recolocar dos puertos clásicos para separar puestos y evitar fuego entre guarniciones al empezar.
- [x] Ballesteros de hoguera en proporción `ceil(ciudades / 2)`, conservando límite de puntos vivos.
- [x] Reservar claros de bosque considerando la copa proyectada ante la cámara; barras solo durante captura real.
- [x] Frenada de unidades a distancia cerca de su alcance útil, para evitar sobrepasarlo entre ticks.
- [x] Prueba táctica del ballestero que se aproxima por el lado opuesto a la torre, mata una guarnición cuerpo a cuerpo y ocupa su círculo.
- [x] Regresión de aproximación a distancia con fotogramas largos (10 FPS, velocidad ×4); ruta hasta la posición de tiro. Suite final: 101 casos aprobados, 32 EditMode + 69 PlayMode.
- [ ] Emitir los refuerzos de país de uno en uno según el temporizador de reclutamiento del JASS; ahora salen juntos al cambiar de ronda.
- [ ] Completar adquisición, alcance mínimo y activación de armas heredados desde las tablas apropiadas; no presentar ajustes locales de torre como estadísticas exactas del mapa.
- [ ] Sustituir las suposiciones de IA sobre navegación y visión por consultas de ruta/visión. Mejorar concentración de expediciones y desembarcos.

## Importación de Europe y World

- [x] Importar Saran Europe/Mediterráneo: 212 ciudades y 69 grupos, conservando XY de ciudad, círculo y punto de refuerzo. Las dos arenas pequeñas siguen siendo mapas originales.
- [x] Mantener puerto como una clase de ciudad del mapa importado, incluida en reparto, grupos, ingresos y victoria; los puertos adicionales actuales aún tienen reglas separadas.
- [x] Incorporar **New World v3.0**, 293 ciudades y 100 grupos, como escenario seleccionable. No equivale a todo el planeta ni a haber identificado la versión histórica más popular.
- [ ] Leer altura, rampas, agua, pasos, biomas y límites desde datos propios de autoría; reconstruir el arte con materiales/modelos originales. Verificar rutas, distancia al círculo y ángulos de tiro por ciudad.
- [x] Separar guarniciones del presupuesto de 100 tropas móviles en los mapas importados para permitir compras desde el inicio.
- [x] Generalizar propietarios, economía, IA, combate, captura y reparto a 2–16 jugadores. Neutral separado del jugador 2.
- [ ] Alianzas/diplomacia y participantes remotos; hoy todos los oponentes son IA locales.

## Héroes y bucle de partida — fase posterior

- [ ] Selección previa de héroe, aparición en una ciudad aliada y progresión de niveles durante la partida, como ha propuesto el usuario. Diseñar muerte/reaparición y reparto de experiencia antes de implementarlo.
- [ ] Mantener la conquista por soldados, territorios y hogueras como núcleo: los héroes deben abrir decisiones, no reemplazar la guarnición o volver irrelevante el control del mapa.
- [ ] Auditar las decisiones que sostienen Risk (frentes, completar grupos, ventanas de ingreso, alianzas, riesgo de extenderse) y las útiles de DotA/LoL (identidad de rol, progresión, respuesta al rival). Distinguir ideas de reglas extraídas.
- [ ] Validar con partidas cortas: tiempo hasta primera conquista, posibilidad de remontar, claridad del siguiente objetivo y diferencias entre mapas. No inferir diversión o popularidad del código por sí solo.

## Revisiones asistidas

- [x] Medir Qwen3.8 por Grok CLI en tandas de 3 a 10: 51/52 respuestas completas en tareas pequeñas. [Medición y límites](docs/audits/QWEN-CONCURRENCY-v0.13.md). Una respuesta completa no implica que el hallazgo sea correcto.
- [ ] Dar paquetes pequeños por tema; contrastar contra código y tests antes de aplicar sugerencias. Terra/Luna pueden preparar pruebas y verificar hallazgos; decisiones de diseño e integración a cargo del agente principal.
- [ ] Revisión adversarial Grok4.6 sobre cambios concretos, con evidencias y limitaciones.

## Expansión v0.14

- [x] Anclar cada defensor a un punto navegable estable y excluirlo de la evasión que lo desplazaba del círculo. Restaurar movimiento al liberarlo.
- [x] Las Marcas: 18 ciudades, nueve grupos y secano con vegetación baja al suroeste.
- [x] Menú con cuatro mapas; listas de grupos y ciudades paginadas, zoom estratégico y foco de cámara adaptados a los mapas grandes.
- [x] Importar alturas, agua, tiles y posiciones numéricas; crear terreno y muelles con materiales propios.
- [ ] Refinar navegación y decoración de los cientos de puestos importados mediante partidas; medir carga y renderizado, y hornear NavMesh en editor.
- [ ] Incorporar datos fuente de pathing, puentes y destructibles cuando sean necesarios; la cuadrícula de altura no reproduce por sí sola todo el World Editor.

- [x] Leer límites jugables y de cámara W3I para recortar el espacio exterior de los imports sin mover las coordenadas de ciudades.

## Fidelidad y 16 jugadores · v0.15

- [x] Mantener XY de ciudades, círculos y hogueras; aplicar la misma conversión nativa /50 a alcance, movimiento y colisiones verificadas, sin comprimir Europe/New World.
- [x] Extraer árboles DOO, transformar ejes/rotación/escala correctamente, seleccionar meshes propios por especie y agrupar su renderizado. Dejar claros los puestos.
- [x] Menú 2–16 participantes, paleta de 16 colores, identificación por IA y panel de jugadores.
- [x] Cordilleras opcionales: relieve suave y transición a roca/nieve que preserva claros, costa y agua.
- [x] Obtener bounds efectivos de Rifleman, Priest, Knight, MortarTeam y HumanBarracks; calibrar la altura de espera de nuestros cuatro tipos de unidad correspondientes y comprobar la selección. No confundir ucol con anchura/altura de un mesh.
- [ ] Calibrar edificios, torre y árboles propios; revisar siluetas, anchuras y animaciones. La altura del guardia corresponde a Knight, pero el arte propio aún es infantería sin montura.
- [ ] Resolver pathing fuente de árboles y edificios: los árboles importados siguen siendo decoración sin nuevos bloqueadores NavMesh.
- [ ] Diseñar un Rin continuo desde los Alpes al mar con lecho, riberas y cruces. El prototipo de traza chocó con exclusiones de ciudades/círculos y no se incorpora.
- [ ] Medir partidas prolongadas con 16 ejércitos, optimizar decisiones de IA y hornear navegación en editor. No equiparar 15 IA locales con multiplayer autoritativo.
- [x] Usar inclinación, orientación y distancia inicial de cámara del JASS para Europe/New World; mantener FOV explícitamente adaptado y extender el zoom estratégico.
- [x] Decodificar el índice de suelo W3E desde el nibble correcto, independientemente de variación y flags de agua/acantilado.
