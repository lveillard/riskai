# Iteración v0.9 — cauce, captura y combate

Se conserva Unity 6000.3.23f1/URP. Esta iteración cambia el comportamiento del prototipo, no importa código del motor ni arte de Blizzard.

## Qué mapa estamos siguiendo

El archivo local es **Risk Reforged v3 de Saran**, abierto, con `war3map.j`, `war3map.w3u` y `war3map.wts` extraídos. Las capturas de `references/visual` corresponden a **Risk Reforged: Rome (Cobalte)**, una variante con otro catálogo, combate y comercio. El hilo del autor de Rome no ofrece el `.w3x`; en esta búsqueda no se obtuvo un archivo verificable de esa variante. No se atribuyen sus valores a Saran ni se presenta el juego como una réplica exacta de Rome.

- [Auditoría de captura y objetos](CAPTURE-SOURCE-v0.9.md).
- [Herencia de estadísticas, fuentes y versiones](REFORGED-BASE-STATS-v0.9.md).
- [Datos extraídos del mapa](../references/maps/reforged-v3-source/reforged-unit-stats.json).

## Combate implementado

Los campos modificados por el mapa mandan sobre la unidad base. Los dados heredados proceden del fixture histórico `UnitWeapons.slk` de wc3libs; no se afirma que su parche sea idéntico al de Saran. Tipos/cadencias/armaduras base se contrastan con las páginas históricas de Blizzard. Perfiles centralizados en `ReforgedProfiles.cs`; tiradas discretas por dado con semilla de partida. Tabla de daño de Saran más fórmula WC3 para armadura positiva y negativa.

| Unidad visible | Referencia | Vida | Daño bruto | Cadencia | Armadura | Coste local |
|---|---|---:|---:|---:|---|---:|
| Espadachín | Adaptación propia | 200 | 18–21 | 1,35 s | Pesada 2 | 20 |
| Ballestero | h00B / Rifleman | 200 | 15 + 2d4 = 17–23 | 1,5 s | Media 0 | 20 |
| Guardia real | h00G / Knight; representación a pie | 650 | 37 + 2d5 = 39–47 | 1,4 s | Pesada 7 | 100 |
| Mago | Adaptación propia | 250 | 30–32 | 1,6 s | Sin coraza 1 | 80 |
| Mortero | h00H / Mortar | 350 | 18 + 1d13 = 19–31 | 3,5 s | Pesada 0 | 60 |
| Sanador | h00E / Medic | 250 | 8–9 | 2 s | Sin coraza 1 | 40 |
| Torre | o000 / Bunker; representación propia | 550 | 50 + 1d8 = 51–58 | 1,5 s | Fortificada 3 | 60 |

Moneda terrestre ×20 respecto al oro del mapa en las unidades referenciadas; distancias a escala de 50 unidades WC3 por metro del prototipo. El mínimo del mortero es 5, máximo 18; intenta separarse si un enemigo entra demasiado cerca. Alcance de torre 8,5. Los puntos de vida no se escalan. No hay bonificaciones ocultas de daño para la IA; se elimina el antiguo multiplicador de daño reducido de neutrales.

Con estos valores una torre necesita **5–6 impactos contra un ballestero sano** de 200 vida y armadura media, según las tiradas. Tres golpes no es una regla universal. La torre sigue siendo autónoma: no se ha portado la ocupación y aceleración de disparo de las madrigueras/búnkeres de WC3.

El sanador cura 15 vida/s al aliado terrestre con mayor herida dentro de 8, con línea de visión; no sobrecura ni resucita ni cura tropas embarcadas. Puede apoyar mientras se desplaza. La curación sin maná es una adaptación propia, no una extracción de la habilidad del mapa. Túnica crema y capucha de equipo para distinguirlo del mago.

## Captura y arranque

Se sustituye la ocupación de radio 5,8 durante siete segundos por un punto de control de 2,2 × 2,2. Un soldado existente ocupa el puesto como defensor; conserva tipo, vida e identidad. Mientras siga vivo y dentro, mantiene la ciudad. Al morir o salir, un aliado dentro puede sustituirlo; si entra un enemigo sin defensor, toma la ciudad inmediatamente. Los soldados fuera del punto no disputan ni bloquean. Barcos y torres no son ocupantes. Esta versión exige estar dentro también para los relevos tras muerte, evitando los grandes radios de búsqueda del JASS.

Los puestos insulares usan la misma regla: desembarcar infantería, sin conquista desde el barco. Una torre construida conserva su equipo tras cambiar la ciudad; hay que destruirla para reconstruir el hueco. Las colas y obras se reembolsan una vez al capturarse la ciudad.

No se añaden tropas a las guarniciones: se reasigna un soldado existente por ciudad antes de mover la dotación de los puertos. Se mantienen los mismos totales iniciales por bando: 24 soldados en Reparto Risk, 16 en Práctica/Países iniciales, 120 oro, una galera y un transporte. Tres soldados de cada ejército se colocan en su muelle. El reparto aleatorio puede dar países completos e ingresos distintos; igualdad de cantidad no implica simetría geográfica.

La IA tranquila es el valor inicial: primera compra a 30 s, como máximo una compra cada 12 s, primera ofensiva a partir de 120 s; conserva reservas y limita las oleadas. Estándar conserva el ritmo previo. Ambas usan los mismos precios y recursos. El modo se elige para la siguiente partida en F1; la barra superior muestra tu población y la de la IA. La IA naval aún no organiza desembarcos.

## Terreno y agua

El río se desplaza a la ladera occidental de la montaña y discurre por tierra hasta alcanzar nivel del mar antes de la costa. Centro Catmull-Rom con elevación Hermite monótona, 57 muestras compartidas por lecho, ribera, exclusión de árboles y material. La malla transversal tiene ocho segmentos; anchura variable, transición de orillas y desembocadura progresiva. Espuma concentrada en pendiente, ondas direccionales y sombras; se elimina la cinta blanca angular.

Terreno continental con muestreo inferior a un metro. Los grandes fragmentos que sobresalían de las crestas se sustituyen por escombros pequeños, empotrados al pie. Textura de roca mezclada progresivamente con la pendiente y el césped. Se reutilizan los atlas originales existentes: no se han generado ni extraído nuevos bitmaps en esta iteración.

## Probar

`Play-RiskAI.cmd` abre la versión publicada localmente. F1: IA tranquila/estándar y nueva partida. Clic derecho sobre una ciudad con tropas: avanzar al punto de control combatiendo. C en ciudad propia: sanador. R en nivel II: mortero. F3: puerto, N: flota, B: embarcar, D: desembarcar. E selecciona también guarniciones: moverlas deja el punto sin defensor.

La evidencia de ejecución final se registra en `VALIDATION-v0.9.md`.
