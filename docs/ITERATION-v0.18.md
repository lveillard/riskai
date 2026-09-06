# Iteración v0.18

Esta ronda aplica las peticiones de la conversación a cuatro escenarios con reglas comunes. No implementa todavía multijugador, red, WebGL, entrada táctil ni lápiz.

## Presentación y cámara

El zoom lejano cambia a una vista estratégica que dibuja superficies de terreno e indicadores de ciudades y puertos. La máscara de cámara excluye árboles, soldados y edificios detallados; sus objetos de simulación, colisiones y navegación siguen activos. Al acercarse vuelve la presentación táctica. La histéresis evita alternancias al mover ligeramente la rueda junto al umbral.

`TerritoryAtlas` deriva regiones de las ciudades y de ambos puntos de cada puerto —orilla y atraque—. La inspección de una hoguera y la propiedad estratégica utilizan ese mismo atlas, proyectado sobre las mallas existentes del terreno y recortado contra tierra/agua. Sustituye la cuadrícula gruesa independiente que producía rectángulos en costas. Los puertos del país tienen un indicador adicional. Blanco representa un propietario neutral.

Los cuatro escenarios comparten el ángulo, zoom inicial, mínimo, sensibilidad y umbral estratégico. El encuadre máximo se calcula a partir de la extensión geográfica y del área que deja el HUD.

Las diferencias de importación adaptan geografía y representación de puestos;
no seleccionan perfiles de combate. Los cuatro mapas calculan el refuerzo de
un país con `ceil(ciudades / 2)`. Los censos del menú se leen de sus datos sin
generar terreno. [Auditoría de reglas compartidas y límites](audits/MAP-SSOT-v0.18.md).

## Selección y producción

La caja izquierda prioriza tropas móviles; cuando no hay tropas dentro, selecciona edificios. Shift añade; doble clic en una ciudad propia agrupa ciudades propias cercanas y visibles. Los puertos importados tienen una sola identidad de selección.

La selección múltiple presenta las colas por edificio y permite cancelar encargos concretos. Una compra añade **una unidad en total**, a la cola compatible más corta; no multiplica el gasto por el número de edificios. Las hogueras conservan un punto de salida opcional y un marcador al inspeccionarlas; sin salida, sus refuerzos esperan allí.

El ranking coloca primero al jugador con más ciudades, con desempate estable por identificador. El menú previo a la partida es una escena propia: no genera terreno ni una sesión hasta pulsar Iniciar. La ayuda y los ajustes durante la partida se separan de la configuración del próximo mapa.

## Simulación y perfiles

La salida voluntaria de una guarnición exige un aliado elegible dentro de su círculo. La entrega se valida y resuelve antes de liberar al defensor. Los barcos usan su atraque marítimo y la misma prioridad de sucesión; no saltan al círculo terrestre. Una orden inválida conserva al defensor.

El límite de tropas móviles excluye guarniciones en todos los escenarios. Los perfiles compartidos suministran los tiempos de preparación del golpe verificados en las fuentes locales; la cadencia ya coincidía. El backswing queda registrado como metadato, sin inventar un bloqueo de movimiento. [Evidencia y campos aún no resueltos](audits/v0.18-combat-source.md).

El soldado local no se presenta como una unidad extraída de Europe. Su relación de daño contra el ballestero se comprobó con la matriz de armaduras existente; no se añadió una bonificación arbitraria.

## Mapas y arte

Las Marcas tiene 33 ciudades y 11 grupos; Cuatro Riberas, 44 y 11. Sus densidades aproximan las 0,815 ciudades por 1.000 unidades cuadradas terrestres de Europe, manteniendo coordenadas irregulares, pasos, islas y despejes. Los mapas fuente conservan sus coordenadas y cantidades.

La actividad de entrenamiento se representa mediante una entrada iluminada. El caballero embiste con la lanza, el mortero es un artillero a pie con cañón corto y proyectil arqueado, y los impactos distinguen perforación, magia y asedio. Las vistas no deciden daño y siguen usando pools limitados. El arte continúa siendo propio o CC0.

## Rendimiento y revisión

Tres rondas de Grok 4.6 revisaron corrección, límites de presentación y costes
de navegación. La búsqueda naval reutiliza sus colecciones y limita cuántas
celdas prueba al suavizar cada tramo, conservando presupuesto de búsqueda,
comprobaciones de costa y rutas independientes por barco. Se han medido una
partida avanzada y cargas de hasta 660 unidades; siguen existiendo picos y
esperas de navegación. [Resultados y límites](VALIDATION-v0.18.md) ·
[Hallazgos y decisiones de las revisiones](audits/GROK-v0.18.md).

## Límites

La vista estratégica reduce presentación; no reduce frecuencia ni precisión de la simulación. El atlas se construye al cargar y no admite todavía terraformación durante la partida. El HUD sigue usando IMGUI: esta reorganización no supone una migración a UI Toolkit ni garantiza uso táctil. La navegación continúa dependiendo de Unity NavMesh y no ofrece determinismo de red.
