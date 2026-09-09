# v0.22 · Interfaz, puertos y continuidad del terreno

Revisión del 9 de septiembre de 2026. Código inicial: `134f495`; correcciones
tras las primeras capturas: `f69b7f0`.
La evidencia de esta ronda se conserva en `audits/v0.22-refinements/`.

## Cambios

- Los edificios y barcos comparten geometría, materiales y retratos entre mapas.
  Los puertos importados conservan sus anclas y añaden accesos a tierra; sus
  pasarelas y los muelles de mapas propios utilizan el mismo generador de tablas,
  vigas y pilotes. Las pendientes de esas pasarelas siguen sus extremos reales.
- La costa de los mapas propios varía la anchura de playa y la caída submarina
  según exposición y pendiente. Europa y Nuevo Mundo conservan sus coordenadas
  originales. Las normales del terreno importado usan vecinos globales para
  evitar diferencias de luz entre mallas; el agua comparte vértices y una
  iluminación continua sin bandas de cascadas de sombras.
- Las Marcas tiene ciudades meridionales menos alineadas, relieve y vegetación
  seca. Cuatro Riberas incorpora cordilleras equivalentes en ambas orillas;
  dos ciudades se apartaron de una laguna y del borde para permitir accesos.
  El catálogo de ciudades genera tanto las anclas como las plataformas de suelo.
- Paleta exacta WC3: rojo, azul, turquesa, violeta, amarillo, naranja, verde,
  rosa, gris, azul claro, verde oscuro, marrón, granate, azul marino, turquesa
  claro y violeta claro. Los tejados conservan mejor el color en sombra.
- Sin selección desaparece la franja inferior. En móvil vertical, recursos y
  botones de mapa/menú ocupan filas estables. Nombres de ciudad más pequeños,
  selección anterior desactivada inmediatamente, producción directa y retratos
  centrados con el mismo marco para unidades y barcos.
- Las colas propias siguen al edificio en el mundo y permiten cancelar; las
  ajenas se ocultan. El oro abre su desglose y las ciudades abren el ranking.
  El total incluye tropas, guarniciones y barcos; la reserva de reclutamiento
  se muestra frente al límite 100, incluyendo encargos pendientes.
- Ingresos corregidos siguiendo el llamador JASS: básico 4 más ciudades de
  países completos. El desglose usa el mismo cálculo que el pago efectivo.
- Ataques visuales muestreados con el reloj de batalla y el punto de contacto
  del clip correspondiente. La muestra de contacto ocurre antes del evento de
  daño; las órdenes canceladas restablecen la animación de reposo.
- Las primeras capturas descubrieron un fragmento blanco del contorno del
  minimapa dibujado sobre el mundo. Se recorta ahora cada segmento en coordenadas
  lógicas absolutas, evitando rotar el origen de un grupo IMGUI. La columna de
  compra de edificios en escritorio también aprovecha todo el ancho disponible.

## Revisión visual inicial

Las carpetas de captura `v22-refine-*` corresponden a `134f495`. Las vistas
vertical y horizontal de Europa, Las Marcas en tablet y Cuatro Riberas en tablet
completaron 14 capturas cada una sin errores de juego. Se revisaron las imágenes
de costa, norte y sur: no muestran las antiguas bandas de iluminación del agua;
se conserva el contorno escalonado cerca de anclas importadas del original.

El fragmento blanco del minimapa se detectó en esas imágenes. En Nuevo Mundo,
la revisión inicial alcanzó compra y ranking; el informe de diagnóstico del
ranking excedió el límite de longitud de consola WebGL. La revisión final
reduce el informe a controles y métricas del HUD; no oculta errores de juego.

## Exportación y revisión final

Windows y Web compilados correctamente desde `f69b7f0`. El manifiesto `builds.json`
contiene los hashes de 185 archivos Windows (207243174 bytes) y nueve archivos
Web (57510910 bytes), además del hash de cada log de compilación.

Las capturas finales del mismo WebGL cubren estas cinco combinaciones. Cada una
incluye menú, ausencia de selección, ciudad, cola, ejército, puerto, clic real
en oro y ciudades, sus paneles, vista estratégica, norte, sur y costa.

| Mapa | Viewport CSS / DPR | Capturas |
| --- | --- | --- |
| Las Marcas | 1024×768 / 2 | [Tablet, desde Azure](audits/v0.22-refinements/v22-click-classic-tablet.jpg) |
| Nuevo Mundo | 1280×800 / 1 | [Escritorio](audits/v0.22-refinements/v22-final-world-desktop.jpg) |
| Europa | 390×844 / 2 | [Móvil vertical](audits/v0.22-refinements/v22-final-europe-phone.jpg) |
| Europa | 844×390 / 2 | [Móvil horizontal](audits/v0.22-refinements/v22-final-europe-landscape.jpg) |
| Cuatro Riberas | 768×1024 / 2 | [Tablet vertical](audits/v0.22-refinements/v22-click-riverlands-tablet.jpg) |

Las cinco revisiones finales pasan: **70 capturas** seleccionadas y diez
comparaciones de clic real frente al panel esperado. Se conservan los
rectángulos reales de UI Toolkit y los resultados en JSON.
El contorno de cámara se ve ahora completo dentro del minimapa; el puerto de
escritorio usa todo el ancho de compra. Las capturas de costa y terreno no
muestran las antiguas bandas rectangulares de iluminación.

La primera automatización enviaba pulsación, liberación y movimiento de cursor
en el mismo frame: fallaron un clic en oro de Cuatro Riberas y otro en ciudades
de Las Marcas. Las imágenes originales se conservan y sus informes de revisión
se marcan fallidos. Se repitieron con pulsación de 100 ms y espera antes de mover
el cursor. Una comparación de imagen exige que cada clic muestre el mismo panel
que la ruta programática; no basta con que no haya excepciones en la consola.

## Publicación

URL: https://riskai-demo.spaincentral.cloudapp.azure.com/

Release `20260909T012000Z-f69b7f0`, activada atómicamente; la anterior
`20260908T225132Z-413c435` permanece disponible para rollback y clientes que
conserven URLs antiguas. Los diez archivos servidos por HTTPS coinciden por SHA256
con el manifiesto del despliegue. También se comprobó gzip y MIME de WebAssembly.

La partida de comprobación pública completó 60.03 s: **269 órdenes aplicadas,
cero rechazadas y 6/6 unidades de prueba desplazadas**, sin supervivientes
inmóviles ni timeouts de espera. La población cambió de 293 a 242 durante la
batalla. Media de frame 30.00 ms, máximo 72 ms, nueve frames sobre 50 ms y ninguno
sobre 100 ms, en Edge con RTX 5080 Laptop a 1280×800/DPR 1. No es una comparación
controlada de rendimiento frente a la versión anterior. El informe conserva una
respuesta HTTP 404 en consola, sin excepciones de página; los diez recursos del
manifiesto público se verificaron por separado y la prueba de juego terminó bien.

## Pruebas automatizadas

**93 casos distintos con último resultado aprobado.** `test-summary.json`
conserva el nombre de cada caso y su ejecución más reciente. Incluye economía,
paleta, terreno, accesos, HUD, privacidad de colas, puertos de ambos tipos,
combate, armas, captura y transporte naval. Los últimos ocho casos del minimapa
y catorce del HUD y su ciclo de vida también pasan tras las correcciones visuales.
Son pruebas enfocadas a los cambios,
no la ejecución íntegra de todas las pruebas históricas del repositorio.

Se conserva la ejecución `refinements-edit-r5`, que detectó una ciudad demasiado
cerca del borde. La ejecución posterior `refinements-edit-r6` comprueba los cuatro
casos de distribución tras corregirla. El resumen no oculta ni cuenta como
aprobada aquella ejecución fallida.

## Límites de fidelidad y rendimiento

La auditoría de coordenadas dio error máximo serializado **0.0** en las 212
ciudades de Europa y 293 de Nuevo Mundo, sus círculos, hogueras y límites W3I.
Los atraques derivados y el ajuste de hogueras al NavMesh son adaptaciones de
runtime; no se confunden con coordenadas originales del mapa.

Quedan por resolver las capas efectivas de herencia Reforged, ocho unidades del
catálogo propuesto, las identidades Marine, el flujo completo de victoria y
regiones especiales, y determinadas rutas locales de construcción/balance.
El límite global 100 sigue identificado como adaptación. No se afirma una
réplica computativa completa del mapa.

Las capturas con viewport táctil se ejecutan en Edge de escritorio. No certifican
rendimiento en un teléfono físico ni el presupuesto de navegación de una partida
avanzada con 900 unidades.

Referencias: [economía y reglas](audits/ECONOMY-RULES-v0.22.md),
[coordenadas importadas](audits/IMPORTED-COORDINATES-v0.22.md),
[keypoints de animación](audits/v0.22/attack-animation-contact-keypoints.json),
[herencia Reforged](audits/REFORGED-LATEST-INHERITANCE.md).
