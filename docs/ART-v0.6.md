# Arte y diseño v0.6

Referencia: captura de RiskReforged Rome aportada por el usuario. Se conserva la escala de edificios y soldados de v0.5 y la cámara perspectiva (FOV 44°, inclinación 52°, orientación 35°), y se amplía el mundo un 22 % en X/Z: 175,68 × 136,64 unidades. La navegación reconoce la costa y las lagunas.

Hay llanura a cota 0, meseta occidental a 3,8 y oriental a 6,2, con bordes irregulares y rampas al sur y al este. Las paredes rocosas usan proyección vertical de un material pintado propio y proyectan sombras. El collider del terreno está en la capa 8: cámara, zoom y órdenes lo consultan. El terreno y los edificios se incluyen en la geometría de NavMesh; efectos, agua y detalles ambientales no incorporan obstáculos.

El atlas nuevo contiene arena, barro, pradera seca y roca estratificada. Se mezcla con la hierba y el granito existentes según altura, pendiente, costa, lagunas y proximidad a ciudades. Véase [prompt y procedencia](IMAGEGEN-v0.6.md). El paisaje añade juncos y piedras de orilla, una cascada con manantial, humo de chimeneas, hogueras en capitales, tres aves y estandartes animados. Los modelos de personajes siguen siendo KayKit CC0.

Las ciudades se agrupan en seis países de dos y tres regiones de cuatro. Los dos bandos empiezan con un país completo cada uno y 16 unidades; las ocho ciudades restantes tienen dos defensores neutrales cada una. El panel de países muestra propiedad, ingresos y refuerzos; las alturas aparecen en el minimapa. Ayuda permite reiniciar en modo Conquista o Capitales.

El diseño adopta países completos, refuerzos limitados por tropas vivas y el umbral del 60 % como base de esta adaptación. Reforged cuenta point value de tropas vivas y configura el tope según ciudades del país; esta v0 usa tipos propios por país y un límite de cinco tandas vivas, mediante el cálculo MIT de Risk Europe. Los costes, siete segundos de captura, dos bandos y veinte segundos de dominio final son decisiones del prototipo; no se presentan como un port fiel de todos los presets.
