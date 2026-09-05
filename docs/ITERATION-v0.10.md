# Iteración v0.10

## Cámara

Cursor visible confinado en Windows, desplazamiento inmediato en los cuatro bordes y esquinas, con la misma velocidad que las flechas. Escape libera el cursor y detiene el movimiento; un clic lo recupera. F1, pausa, pérdida de foco y cierre del controlador lo liberan. Al volver con Alt+Tab se espera un clic. El zoom sigue anclado al punto bajo el cursor. Detalles: [CAMERA-v0.10.md](CAMERA-v0.10.md).

## Terreno y vegetación

Río y mar comparten ahora el cálculo de color, profundidad, reflejos y espuma. El shader reconstruye el fondo desde la textura de profundidad de URP y combina el color opaco del lecho con la absorción del agua. Se añadió una plataforma submarina visual sin colisiones y pasadas DepthNormals para que URP lea la profundidad del terreno y las construcciones. El tramo plano de la desembocadura utiliza la superficie del mar, sin una segunda malla fluvial superpuesta. La desembocadura se ensancha progresivamente y pierde la corriente al llegar al mar; desaparecen la franja de color independiente y la máscara circular que borraba la espuma de la costa anterior.

El cauce continúa siguiendo una curva de pendiente monótona. El agua tiene margen geométrico fuera del canal para encontrar los taludes por profundidad, en vez de mostrar los cantos rectos de una cinta. Los tramos sobre valles bajos forman un lecho y riberas de contención antes de regresar a la altura del terreno: la excepción que conserva fondos marinos profundos ya no deja ríos interiores suspendidos. Las rocas se concentran en el tramo alto. El shader conserva un aspecto pintado, con agua azul verdosa en poca profundidad y azul más profundo mar adentro.

Las copas de robles verdes y otoñales usan lóbulos anchos y desplazados, con ramas bifurcadas visibles. Se eliminaron los tres pisos concéntricos y se moderó el amarillo del follaje. Los abetos y palmeras conservan sus modelos anteriores.

Fuentes técnicas consultadas: [superficies y transiciones fluviales de Epic](https://dev.epicgames.com/documentation/en-us/unreal-engine/water-meshing-system-and-surface-rendering-in-unreal-engine), [reconstrucción de posiciones mediante profundidad en URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/writing-shaders-urp-reconstruct-world-position.html). Implementación propia en Unity; no se incorporó código ni arte de esos ejemplos.

## Defensa de ciudades y puertos

Todas las ciudades y todos los puertos, incluidos los puestos insulares, empiezan con torre. Las neutrales pertenecen al equipo neutral y no disparan a sus propios defensores. Los dos bandos mantienen la misma población inicial. Las torres portuarias añaden un obstáculo de navegación que deja libre el desembarco; se pueden reconstruir por 60 oro y 7 segundos. Seleccionar una torre portuaria abre su astillero y T reconstruye una torre destruida. Las obras y encargos se reembolsan al perder el puerto. El arranque garantiza un puerto por bando reasignando uno de los cinco emplazamientos continentales separados; se eliminó el muelle extra que podía quedar al alcance de una torre neutral y provocar bajas antes de la primera compra de la IA.

**Adaptación deliberada pendiente de un modo fiel:** las torres siguen siendo destructibles (550 vida, 51–58 daño perforante / 1,5 s, alcance 8,5, fortificada 3), y conservan su bando cuando se captura el edificio vivo. Es el perfil del búnker de Saran adaptado como defensa universal. La auditoría de esta entrega identificó que el original distingue edificios de ciudad invulnerables, defensores y búnkeres opcionales. Los edificios tienen campos de ataque configurados (45 + 1d5 / 0,9 s, alcance 650 WC3), pero no sobreescriben la activación del arma: el Barracks base de la referencia histórica tiene weapsOn=0. Esos números no prueban que las ciudades disparen en partida. No presentamos la defensa universal actual como una reproducción exacta de ese sistema.

## Datos del mapa y repositorio

Se extrajeron 241 colocaciones de Saran v3: 212 ciudades (168 terrestres y 44 puertos), con coordenadas nativas, orientación, propietario inicial y 69 regiones con sus nombres. El parser valida el final del binario y cruza las ciudades con las asignaciones de JASS. [Informe de mapas y fuentes](RISK-MAPS-v0.10.md), [CSV](../data/derived/risk-reforged-v3-placements.csv).

También se descargó Risk - New World v3.0. Sus colocaciones editoriales están reducidas a un punto de inicio, pero se recuperaron 293 ciudades y 100 regiones desde las llamadas literales de JASS. Los dos juegos de coordenadas están en data/derived y sus scripts de extracción se incluyen en el repositorio.

Estos datos preparan la reconstrucción de Europa/Mediterráneo y del escenario ampliado a América y el Caribe. La partida jugable sigue siendo Las Marcas, de doce ciudades y dos islas; el mapa completo de 212 ciudades todavía no se ha importado como escenario jugable. Las cifras de descargas de versiones distintas no permiten proclamar un Risk más popular.

Repositorio público: https://github.com/lveillard/riskai. Incluye fuentes, arte original/CC0, perfiles y herramientas de extracción. Los mapas de referencia, capturas de Warcraft, ejecutables, cachés de Unity y registros se quedan fuera del historial publicado.
