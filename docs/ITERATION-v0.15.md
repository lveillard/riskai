# v0.15 · Escala fuente y 16 jugadores

La misma conversión `/50` conserva las proporciones entre coordenadas, alcances, velocidades y radios físicos verificados. Los límites jugables W3I gobiernan cámara y minimapa. La cámara importada usa los valores jugables del JASS: inclinación 70°, norte arriba y distancia 80. La vista de mapa completo calcula el encuadre en perspectiva reservando el espacio del HUD.

Se pueden elegir entre 2 y 16 jugadores. Cada IA tiene comandante, presupuesto, tropas y flota propios; todos empiezan con cuatro de oro y defensores por puesto, sin unidades móviles gratuitas. Se identifica cada bando en tejados, unidades, postes, minimapa y panel de jugadores. La neutralidad usa un identificador distinto de cualquiera de los 16 jugadores. La victoria por territorio y la eliminación consideran a todos los participantes.

Europe incorpora 4.635 registros de árbol; New World, 5.992. El render usa candidatos vivos sobre tierra y fuera de los claros de los puestos. Mantiene coordenadas, escala relativa y orientación DOO con meshes propios; agrupa exclusivamente los árboles con static batching. Se corrige además la decodificación del índice de suelo W3E, que estaba confundida con su byte de variación. Los nuevos relieves opcionales de Alpes/Pirineos protegen los puntos jugables y añaden roca/nieve sin retocar la costa.

Las alturas de espera de ballestero, sanador, guardia y mortero se calibran mediante bounds numéricos de los modelos originales: 1,637 / 2,447 / 3,058 / 1,605 unidades Unity. Se escala cada modelo propio uniformemente y se apoya en el suelo; la medida se calcula una vez por tipo. Los anillos consideran el radio físico y la selección cubre la altura nueva. Se distribuyen sólo los números y su procedencia, no los recursos de Warcraft.

## Límites

- Se verifica la altura de espera de cuatro tipos, no su silueta, anchura ni todas las animaciones. El guardia propio sigue siendo infantería, aunque su referencia sea Knight. Ciudades, torres y copas de árboles aún conservan tamaños de arte local.
- Los árboles no importan todavía obstáculos por textura de pathing; la navegación sigue siendo una adaptación.
- La traza de un río nuevo no pasó la comprobación de separación de ciudades. Se conserva el agua W3E y se documenta la siguiente iteración.
- Multiplayer remoto, touch, niebla, diplomacia y desembarcos de IA siguen pendientes. Los 15 oponentes de esta entrega corren localmente.
- Las Marcas y Cuatro Riberas son mapas originales, no conversiones 1:1 de mapas históricos.

Ver [auditoría de escala](MAP-SCALE-v0.15.md), [validación](VALIDATION-v0.15.md) y [pendientes](../TODO.md).
