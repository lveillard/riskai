# Auditoría naval de RiskReforged Saran v3

Auditoría de los archivos extraídos de Saran v3. Las líneas citadas son de
[`war3map.j`](../references/maps/reforged-v3-source/war3map.j) y los nombres
y textos de [`war3map.wts`](../references/maps/reforged-v3-source/war3map.wts).
Los valores de `war3map.w3u` están volcados con su offset y literal en
[`reforged-unit-stats.json`](../references/maps/reforged-v3-source/reforged-unit-stats.json).
`INHERITED` significa que el mapa no tiene un override de ese campo; no se
interpreta como cero.

## Hechos comprobados en Saran v3

| Función | Raw ID / datos | Evidencia |
|---|---|---|
| Astillero | `h00O`, nombre `Shipyard`, “Main unit production center” | WTS 196–204; hay instancias colocadas en JASS 3546–3600 y se incluyen como ciudades, por ejemplo 6313–6319. |
| Catálogo del astillero | `h015,h014,h012,h00X,h006,h00L,h00P,h00Q,n009,n008,n007,h00U,h00W,h001,h00R,h00T,h00S` | `w3u@0x14c6`, campo `utra` en el JSON. |
| Transportes | `n008` Transport Ship, `n009` Transport Ship, `n007` Armoured Transport Ship | WTS 208–227, 2811–2829 y 1681–1694; todos aparecen en el catálogo `h00O`. |
| Transportes, overrides | `n008/n009`: 300 HP, 340 velocidad, 2 oro; `n007`: 300 HP, 370 velocidad, 30 armadura, 6 oro | JSON, filas de cada `raw_id`; los demás campos que dicen `INHERITED` no están fijados por `w3u`. |
| Buques de combate | `h00X` 2000 HP/90/1500/15/30 oro; `h001` 2350 HP/130/1500/20/45; `h006` 2500 HP/130/1500/20/40; `h00U` 1250 HP/90/1500/10/20; `h00W` 400 HP/30/1000/6/5; `h00P` 550 HP/50/1200/6/10; `h00Q` 400 HP/30/1000/4/5 | JSON. El orden es HP/daño base/rango/armadura/oro; cooldown, velocidad y tipos no listados quedan `INHERITED` cuando así figura en el JSON. |
| Infantería naval | `h012` Marine Private, `h014` Marine Major, `h015` Marine General; también `h00R/h00S/h00T` | Catálogo `h00O` en `w3u@0x14c6`; disponibilidad explícita en JASS 5026–5029 y 5017, 5006–5007. |

La disponibilidad general activa transportes y buques en JASS 5006–5014 y
5017, y la segunda variante de unidades navales en 5036–5041. Los modos de
barcos son tres: normal, barcos débiles y “Transports only + land anywhere”
(JASS 10055–10060, 12400–12413 y 13637–13648). En el modo 2 solo se vuelve a
habilitar `n008` y se desactivan los triggers de carga propios en 8227–8240.

## Carga, movimiento y desembarco

Está comprobado que existe una interfaz de transporte, pero no una capacidad
numérica documentada en los archivos auditados:

* El tooltip de ambos tipos de transporte solo muestra “Cargo Hold” (WTS
  220–227, 1687–1694, 2823–2829); no contiene un número de plazas.
* `A00V` es `[Q] Load All` y su tooltip ordena a las unidades cercanas entrar
  en el barco (WTS 3835–3849). El trigger `Ship Load Order` repite esa
  condición en JASS 20076–20080, busca unidades en 512 de rango (20153–20159),
  ordena como máximo diez iteraciones (20160–20174) y luego envía la orden
  `smart` (20149–20151, 20175).
* Ese filtro excluye explícitamente `n007`, `n008`, `h000`, `h001`, `h00U`,
  `h00V` y `h00W` (JASS 20083–20105): los transportes y varios buques no son
  carga. No demuestra que el buque pueda capturar una ciudad.
* `A00X` es `[W] Unload All` (WTS 3853–3873); su acción ejecuta
  `unloadall` (JASS 20196–20207).
* Carga y descarga solo se permiten sobre el terreno `Vcbp`: la validación de
  ambos hechizos detiene la orden y muestra el error en JASS 18445–18507, y
  el trigger de la orden `unload` vuelve a bloquear otro terreno en 18521–18542.
  El texto del error es también WTS 490–493.
* Al reiniciar se eliminan transportes `n008` y `n007` en JASS 4885–4905.

Por tanto, “transporta tropas y desembarca” está verificado; **capacidad 6
soldados no está verificada**. El bucle de diez es el límite de una orden de
carga, no una capacidad confirmada del objeto.

## Captura y economía

La fuente implementa captura de ciudades, no una regla separada de islas.
Cuando muere el defensor de una ciudad, el trigger selecciona una unidad
aliada cercana y llama a `City Claim` (JASS 17799–17861). La reclamación cambia
`udg_CitiesOwners`, `udg_PlayerCitiesOwned` y el propietario de la ciudad en
18194–18320. Los grupos de candidatos excluyen estructuras y expresamente
`n007`/`n008` en 17520–17541 y 17548–17566; un transporte no es el reclamante
normal en este flujo. Hay una rama especial para una ciudad cuyo objeto es
`h00O` (17712–17730), pero no constituye una regla de captura naval ni de
captura de islas.

No hay IDs, nombres o triggers de “island”/“isla”/“galley” en los textos
extraídos. No se encontró una condición que permita que un barco capture por
sí mismo. La fuente sí distingue el astillero `h00O` como ciudad y permite
capturarlo dentro del flujo general de ciudad.

El oro periódico se calcula a partir de ciudades poseídas: `Income Give`
reinicia y suma `udg_PlayerCitiesOwned` en JASS 18769–18845, y aplica el ingreso
básico si el jugador posee ciudades en 19064–19075 y 19161–19197. Los modos
Conquest y Timed parten de 4 de primer ingreso, 4 básico y multiplicador 1.0
(14453–14475 y 14508–14530); Russia usa 10 inicial y 5 básico (14559–14582).
No hay un bono de oro por barco, transporte o astillero. El coste en oro de
cada unidad naval es un override de `ugol` en `w3u`, como se detalla arriba.

## Referencia visual Rome

Las capturas de [`BetaTest v1.16p SS-1.png`](../references/visual/BetaTest%20v1.16p%20SS-1.png)
y [`BetaTest3_SS_3.png`](../references/visual/BetaTest3_SS_3.png) muestran una
unidad llamada **Galley** seleccionada con 500/500 HP, daño 10–20 y armadura 2.
Es evidencia visual del prototipo Rome aportado para dirección de producto:
no contiene raw ID, capacidad, coste ni regla de captura y no sustituye a
`war3map.w3u/.j` de Saran v3.

## Adaptación mínima propuesta para el prototipo

Estas tres decisiones son adaptación de producto, no hechos de Saran v3:

1. **Puerto compra Galley.** Representar el puerto con el concepto `h00O` y
   ofrecer una galera de 500 HP, daño 10–20 y armadura 2 según la captura; el
   coste y cooldown quedan por fijar porque la captura no los muestra.
2. **Transporte de seis soldados.** Permitir seis unidades terrestres por
   `n008`/`n007` y rechazar la séptima. Es una regla explícita del prototipo;
   no reutilizar el diez observado en el trigger como dato de Saran.
3. **Isla capturable por tropas.** Un transporte lleva unidades a una playa
   válida, solo una unidad terrestre desembarcada entra en el punto de
   control y esa unidad reclama la isla. El barco nunca captura directamente.
   Al capturar, sumar la isla al mismo contador de ciudades/territorios que
   alimenta el ingreso; el astillero no concede oro adicional.

No se modificó código de Unity ni la fuente extraída; este documento es una
auditoría y una especificación de adaptación.
