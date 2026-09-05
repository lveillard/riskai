# RiskAI — investigación inicial

Revisado el 5 de septiembre de 2026. El proyecto está en fase de elección de motor y estudio de referencias; todavía no hay un juego implementado.

La idea es un RTS de conquista territorial: seleccionar soldados individualmente o en grupos, dar órdenes de movimiento y ataque, tomar ciudades y completar regiones para aumentar los ingresos. La acción continúa mientras un reloj marca las rondas económicas. Dirección visual propuesta: escenario 3D, cámara elevada, unidades estilizadas y colores de facción fáciles de distinguir.

La plataforma de trabajo asumida es PC. El número de jugadores y unidades queda por concretar.

## Referencia elegida

Usaremos **Risk Reforged, de la familia Risk Devolution**, como referencia de diseño, y el código público de **Risk Europe** para estudiar una implementación más reciente.

WC3Maps muestra en sus resultados indexados 71.341 partidas alojadas en la agrupación asociada a [Risk Reforged Beta 1.46b](https://www.wc3maps.com/map/34199), con 154 versiones relacionadas, y 37.012 en la de [Risk Europe 1.1a](https://www.wc3maps.com/map/237990), con 58 versiones relacionadas. Son registros parciales de alojamiento, no jugadores únicos ni partidas necesariamente completadas. Las agrupaciones y las épocas difieren; no deben sumarse ni interpretarse como una clasificación histórica universal. Las cifras aparecieron en resultados de búsqueda; la lectura directa de esas páginas dejó sus paneles estadísticos cargando.

El autor publicó un [mapa editable de Risk Reforged](https://www.hiveworkshop.com/threads/risk-reforged-becomes-open-source.330360/), que ya está descargado. Hay además una copia parcial del [repositorio wc3-risk-system](https://github.com/Warcraft-3-Risk/wc3-risk-system) con código, pruebas y documentación. Procedencia y archivos en [references/SOURCES.md](references/SOURCES.md).

## Motor propuesto

**Primera opción: Unity 6, C# y URP.** Es una recomendación provisional para priorizar control RTS y aprovechar sistemas existentes. La versión concreta se fijará al validar las dependencias.

| Opción | Encaje con este proyecto | Trabajo que sigue siendo nuestro |
| --- | --- | --- |
| Unity + C# | Navegación 3D y posibilidad de incorporar herramientas RTS existentes. Mi primera opción por esa vía de reutilización. | Integrar y ajustar movimiento de grupos, combate, economía, captura y red. |
| Godot 4 + C# | Alternativa con licencia MIT, navegación y soporte de servidor sin gráficos. Encaja si priorizamos una base propia y control del motor. | Implementar o integrar los sistemas RTS y medir su rendimiento. La versión C# no exporta actualmente a web. |
| Unreal | Lo reconsideraría si la prioridad pasa a una presentación visual mucho más ambiciosa. | Para este alcance no veo una ventaja que justifique elegirlo de entrada. Valoración de proyecto, sin prueba comparativa. |

Fuentes técnicas: [Unity AI Navigation](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ai.navigation.html), [URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/urp-introduction.html), [Godot C#](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html), [servidores Godot](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_dedicated_servers.html) y [licencia Godot](https://godotengine.org/license/).

[RTS Engine para Unity](https://docs.gamedevspice.com/rtsengine/manual/index.html) es un candidato a evaluar: documenta selección, unidades, combate y módulos de red. No se ha comprado, instalado ni probado. Su compatibilidad, coste total y encaje con nuestras reglas se deben comprobar antes de adoptarlo. No asumir que usar un framework resuelve el multijugador o el rendimiento automáticamente.

## Primera prueba jugable propuesta

Un mapa pequeño con ciudades agrupadas en regiones, dos bandos, dos tipos de soldados, reclutamiento, captura e ingresos cada 60 segundos. Selección por caja, clic derecho, orden de avanzar atacando y cámara con zoom. Estética provisional legible.

Separar las reglas de economía y propiedad en C# de la cámara, interfaz y representación. Para multijugador, proponer una autoridad que valide las órdenes y resuelva el estado; conectar dos clientes pronto. Evitar mezclar la lógica con animaciones o basar la economía en la frecuencia de dibujo.

La primera validación técnica debe incluir grupos cruzando pasos estrechos, enemigos bloqueando caminos y reclutamiento durante un combate. Probar escalones de 100, 300 y 500 unidades totales, midiendo tiempos de cálculo, respuesta a órdenes y tráfico de red. Son escenarios de prueba propuestos, no capacidades comprobadas. El primer hito es que mover y combatir con los soldados resulte satisfactorio y que la conquista produzca correctamente el siguiente ingreso.
