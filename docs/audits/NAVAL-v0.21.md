# Puertos, playa y expediciones · v0.21

## Evidencia revisada

Se ha vuelto a leer el JASS extraído localmente de `references/maps/reforged-v3-source/war3map.j` y las tablas W3U del mismo archivo de Europe/Reforged. No se incorpora arte de Blizzard.

- El script guarda un único `udg_CityDefenders[index]`, también para el astillero. `Trig_City_Claim_Actions` sustituye ese defensor, lo coloca en la posición del círculo y cambia el dueño del edificio (líneas 18198–18212).
- La sucesión contiene una rama particular para `h00O` (astillero), además de las ramas terrestre/aérea y de artillería. No basta con leer el filtro de entrada para afirmar que cualquier barco puede ocupar cualquier ciudad.
- `n007` y `n008` están excluidos por los filtros del círculo. El transporte local `n008` no captura; la fragata sí puede ocupar un puerto vacante.
- El catálogo fuente del astillero incluye barcos y Marines. Mantener tres Marines terrestres (`h012`, `h014`, `h015`) en el puerto local es coherente con ese catálogo. Los cinco tipos locales son un subconjunto: no representan todas las variantes o modos del W3U.
- El control de carga/descarga comprueba el tile `Vcbp` (funciones `Trig_Ships_Load_Unload_Kopiuj_*`, desde línea 18445). Los dos recursos de geografía importada conservan ese nombre de tile.
- La configuración inicial usa `h00B` tanto para defensor normal como para astillero (5456–5457). La ronda no añade ejércitos iniciales gratuitos.

Estas comprobaciones actualizan las limitaciones de las primeras auditorías navales; las notas históricas que decían que no había evidencia de captura naval no describen la implementación actual.

## Adaptación compartida

`CityClaimZone` guarda un solo `CombatTarget` guardián. Tierra y mar resuelven sucesión y relevo mediante la misma prioridad aliada, distancia e ID. El mismo estado de propietario alimenta edificio, torre y comandos de producción. Un guardián vivo impide que un enemigo cercano lo sustituya.

Las anclas físicas terrestre y marítima siguen separadas por las restricciones de movimiento de Unity. El círculo del barco se fija al atraque; el modelo puede girar y disparar allí. Esto no es una reproducción exacta del círculo único de WC3. La selección naval tiene un anillo interior y no añade otro hover cuando ya está seleccionada.

El relevo voluntario admite 2,0 unidades alrededor del ancla frente al radio pintado de 1,55: es la pequeña ampliación solicitada por el usuario, no una nueva cifra atribuida al mapa original.

`ShoreAccess` valida tanto embarque como desembarco. En mapas importados exige playa `Vcbp` y una posición transitable suave, o la pasarela transitable de un puerto. Las plataformas de puerto pueden estar sobre agua W3E: no deben rechazarse sólo por el flag de terreno. En mapas propios, pesos de arena/roca calculados una vez para el dibujo se consultan también al ordenar desembarco. El shader interpola los pesos para evitar cortes de material; en el borde importado manda el tile fuente para la regla.

La clasificación de materiales no suaviza por sí sola la geometría escalonada de la costa. Esa tarea sigue separada y pendiente en TODO.

## Límites de atraque y recuperación

El atraque de la guarnición y el punto seguro de carga son anclas distintas. `Harbor.TryTransportLanding` toma una playa validada por `ShoreAccess` y busca el punto de océano más cercano dentro de `Ship.LoadRadius - .45` (9,79 frente al radio de carga de 10,24). El margen cubre la tolerancia de llegada del barco de 0,4 y evita que una ruta que termina ligeramente antes deje el barco fuera del alcance. Este punto se usa para reunir, embarcar, desembarcar y calcular las rutas de la expedición; `Harbor.Berth` conserva la función de guardia y producción.

Una expedición no se bloquea esperando que el transporte coincida con un atraque ocupado por una fragata: `Gather` comprueba la distancia al punto de carga y vuelve a ordenar el viaje a ese punto cuando sigue fuera del radio. Los barcos guardia permanecen anclados en su atraque y no se teletransportan para satisfacer la misión.

El transporte cargado se intenta recuperar antes de planificar otra expedición. `NearestRecoveryHarbor` prueba como máximo seis puertos por decisión, prioriza los del bando y exige playa válida y ruta marítima; `ReturnCargo` mantiene un plazo de 40–240 segundos según la distancia. Si no hay puerto seguro o falla el desembarco, la misión entra en enfriamiento y reintenta tras 10 segundos. El cargamento puede permanecer a bordo mientras no exista un destino válido: no se destruye ni se recoloca para fabricar un éxito. La misión conserva una sola expedición por IA y reserva 2–4 tropas, con presupuestos de sondeo 4/8/6.

## IA y límites de verificación

La primera expedición usa compras pagadas y las mismas órdenes del jugador. Reserva de dos a cuatro tropas móviles, un transporte por IA, búsqueda acotada, decisiones escalonadas y plazos según longitud de ruta. El comandante terrestre excluye tropas reservadas y filtra objetivos inaccesibles por tierra. Si la ola de desembarco pierde todas sus tropas elegibles, la fase de ataque se libera inmediatamente; no conserva el único hueco naval hasta agotar el plazo.

El destino inicial de expediciones se limita a puertos con acceso marítimo y camino completo hasta una ciudad enemiga; no busca todavía cabezas de playa en cualquier tramo arenoso. Esto es una limitación de estrategia, no otra regla de embarque por mapa.

La tanda consolidada de esta ronda registra 72 casos PlayMode distintos aprobados en los XML `RiskAI/Logs/v21-*.xml`, incluyendo cámara/entrada, guardia naval, carga por radio, cancelación y reembolso, expedición real, rutas bloqueadas y recuperación de rutas. Esto es evidencia de ejecución de pruebas, no una validación visual del jugador. Las builds Windows/Web, la inspección visual de puertos y costa y la medición de rendimiento siguen pendientes en [VALIDATION-v0.21](../VALIDATION-v0.21.md).
