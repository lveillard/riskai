# Revisiones Grok 4.6 · v0.18

Dos rondas independientes por la CLI local, con paquetes de código limitados a
presentación estratégica, selección, producción y menú. No se les proporcionó
el repositorio completo ni se trataron sus conclusiones como pruebas ejecutadas.
Salidas locales ignoradas: `.tools/grok-v18/round1/` y `round2/`.

## Primera ronda

Completada en 639 s. Se confirmó una pestaña que mostraba la cola naval cuando
estaba seleccionada la pestaña de ejército: corregida comparando explícitamente
la pestaña naval. Se reforzó la construcción del atlas para resolver por
proximidad los raros píxeles que no cubriera el rasterizador. Deseleccionar un
país usa un identificador negativo, sin confundirse con el byte reservado cero.

La observación sobre copiar escala global como local sólo fallaría con un
padre transformado; el padre actual es identidad. Se endureció igualmente la
copia de superficies colocando su transformación global antes de asignar padre.

Dos afirmaciones no quedaron confirmadas: la cámara recibe ya el ángulo y FOV
comunes desde `RiskBootstrap`, fuera del paquete revisado; un pass sin LightMode
no implica por sí solo que URP no lo dibuje. Se explicitó `SRPDefaultUnlit`, pero
la prueba de renderizado se realiza con capturas del ejecutable.

## Segunda ronda

Completada en 806 s. Identificó tres rutas concretas para verificar y corregir:

- Caja sobre una ciudad portuaria importada: debe seleccionar su mismo Harbor
  aunque el rectángulo no incluya el centro de selección del muelle.
- Censos del menú: los cuatro mapas deben consultar `MapLayout.ScenarioDetail`,
  sin cantidades importadas escritas de nuevo en el HUD.
- Desactivar `StrategicMapView` debe restaurar máscara y fondo de cámara,
  además de hacerlo al destruirlo. Liberar materiales y atlas sigue siendo una
  operación de destrucción, no de desactivación temporal.

Los resultados de ejecución y las regresiones correspondientes se registran en
[Validación v0.18](../VALIDATION-v0.18.md).

## Tercera ronda: partida larga y rendimiento

Completada en 226 s con un paquete de 151 KB: navegación terrestre y naval,
IA, simulación, picking, diagnóstico y sonda, más registros de calentamiento
v0.18 y la validación v0.17. Archivos locales en
`.tools/grok-v18/performance/`. El primer calentamiento no produjo una cohorte
azul; se indicó expresamente que no era una medición válida de respuesta.

Se confirmó que las búsquedas navales síncronas concentran varios picos de IA.
El A* recreaba heap y diccionario en cada búsqueda, y su suavizado buscaba hacia
el final de toda la ruta. Se reutilizan buffers por cuadrícula y las ocho
direcciones de holgura; el suavizado mira hasta 32 celdas por tramo. Los caminos
devueltos siguen teniendo almacenamiento independiente para cada barco.

Se conservaron el máximo de estados explorados y las comprobaciones geométricas
de todos los segmentos. Reducir arbitrariamente el límite de A* podía volver
inaccesibles rutas válidas. Tampoco se aplicó la propuesta de ignorar una nueva
orden mientras `pathPending`: una orden explícita del jugador debe poder
sustituir la anterior. Las peticiones autónomas ya evitan reemplazar una ruta
pendiente. El coste del picking no está aislado en los registros; no se añadió
una caché de selección potencialmente desfasada sin esa evidencia.

La comparación con 6 y 32 refuerzos de prueba por bando detectó otra limitación
del medidor: la primera muestra a veces arrastraba `unscaledDeltaTime` de la
carga. Se añadió estabilización después de crear la cohorte y se reinicia su
posición de referencia antes de medir. Los máximos de unos 2,6 s de esas
primeras pruebas de carga no se atribuyen a una partida estable.
