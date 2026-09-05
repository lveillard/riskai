# v0.8 — costa, relieve y navegación

## Dirección visual

Referencias locales revisadas: las seis imágenes de `references/visual`, en particular Rome v1.13 (islas), v1.15 (relieve) y v1.16 (flotas). Se conserva el encuadre RTS y se amplía el norte del mapa para dos islas, canales y puertos. Hay relieve continuo entre las terrazas, pradera, bosque de coníferas, robles otoñales, costa arenosa y palmeras. Los colores políticos están en postes y estandartes; el suelo depende de bioma/altura/humedad.

El río comparte una definición de cauce con el terreno: nace bajo la cumbre oriental, baja por ocho puntos de altura decreciente, talla el lecho y desemboca en el mar. Se elimina el manantial circular y la cascada corta de v0.7. Agua con orilla común a costa/islas, variación de profundidad aparente, reflejo del cielo, brillo, ondulación y espuma animada. No es una simulación hidráulica.

## Primera adaptación naval jugable

- Dos puestos insulares inicialmente neutrales. Infantería desembarcada captura y da +8 oro/ronda. No cuentan para el objetivo de 12 ciudades continentales de esta versión.
- Puertos continentales vinculados al propietario de su ciudad. Cada bando tiene puerto y recibe una galera y un transporte iniciales. Tres soldados existentes se sitúan en su muelle; no aumenta la población inicial.
- Galera: 75 oro, 4 s, 500 vida, 20 daño de asedio cada 1.5 s, alcance 17, armadura pesada 2.
- Transporte: 45 oro, 6 s, 300 vida, armadura pesada 1, seis plazas, sin ataque.
- Cola de tres encargos, cancelación y reembolso; al cambiar de dueño el puerto reembolsa encargos pendientes. Límite naval de doce barcos por equipo.
- Tropas embarcadas conservan identidad, salud y población, y no participan en combate/captura/selección. Si se hunde el transporte, se pierden.
- Navegación sobre océano, con margen de casco y comprobación de segmentos. El río y las lagunas no son navegables.
- Galeras enemigas defienden y patrullan hacia puertos rivales. La IA todavía no organiza invasiones en transportes.

Balance adaptado, no réplica completa de Saran ni del Rome de las capturas. La auditoría de reglas verificadas está en [REFORGED-NAVAL.md](REFORGED-NAVAL.md).

## Controles de prueba

F3: puerto propio. Q/W en un puerto: galera/transporte. N: seleccionar flota. B con transporte seleccionado: embarcar tropas cercanas al muelle. D: desembarcar en puerto próximo. Clic derecho sobre un muelle con transporte: navegar y desembarcar al llegar. A + clic: atacar. E selecciona infantería; N selecciona barcos. Minimap incluye puertos, islas y flotas.

## Arte original generado

Herramienta integrada ImageGen (no CLI). Archivo final: [FoliageAtlas-v08.png](../RiskAI/Assets/RiskAI/Resources/Painted/FoliageAtlas-v08.png).

Origen: `C:\Users\lveil\.codex\generated_images\01a06f0f-278e-7ae1-8117-3f140f998561\exec-ff85b3e1-53bf-4835-9232-5dcffb73d5c2.png`.

Prompt final:

> Use case: stylized-concept. Asset type: original hand-painted foliage texture atlas for a modern 3D fantasy real-time strategy game, sampled onto foliage mesh cards. Square 1024x1024, exactly 2 by 2 equal tiles, pure solid BLACK RGB 0 0 0 background in every tile and every margin (deliberate chroma mask, not transparency, not checkerboard). Upper left: one chunky rounded oak leaf cluster, emerald green with yellow-green upper light, dense overlapping large oak leaves. Upper right: same broad chunky leaf cluster in burnt orange, ochre and autumn amber, bright warm tips. Lower left: one wide palm frond with stem at bottom center, dense olive green chunky long blades spreading upwards and sideways, readable clean silhouette. Lower right: one rounded silver sage and muted olive shrub leaf cluster. All subjects fit fully in their own quadrant with a 24px black gutter at tile edges. Dense graphic masses, broad painterly brush strokes, shaded dark interiors with luminous leaf edges, strong game readability at tiny size, Warcraft III inspired stylization with richer modern painterly detail, no photorealism, no noise, no wispy hair or fern needles. Light from upper left. No trunks, no ground, no shadow outside leaves, no borders, no text, no symbols. Black visible between clusters but no black holes through the central mass.

La textura se integra mediante mallas originales de copas y un shader de recorte sobre negro. La ropa de unidades KayKit se recolorea por tono conservando piel, metal y textura original. Barcos y muelles usan geometría original y el atlas de arquitectura propio existente. No se importaron texturas ni modelos de Blizzard.
