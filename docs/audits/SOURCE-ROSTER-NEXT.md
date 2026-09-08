# Propuesta de altas de unidades: pendiente de decisión

No se ha añadido ninguna unidad al runtime. La identidad es el rawcode W3U,
no el nombre visible ni el objeto base de Blizzard. Coste = `ugol` explícito
del mapa, sin convertirlo a otra economía. HP indicados como explícitos están
fijados por el mapa y prevalecen sobre Reforged.

| Rawcode | Nombre fuente | Rol orientativo | Oro (`ugol`) | HP | Base WC3 |
|---|---|---|---:|---|---|
| h001 | Battleship SS | Acorazado pesado | 45 | 2350 explícitos | hdes |
| h00F | Elite Rifleman | Infantería de élite a distancia | 6 | 450 explícitos | hrif |
| h00I | Roarer | Apoyo; revisar habilidades antes de implementar | 4 | 400 explícitos | hmpr |
| h00J | Army General | General terrestre | 10 | 885 raíz / 800 capas balance; selector pendiente | hkni |
| h00M | Artillery | Artillería | 15 | 900 explícitos | hmtt |
| h00U | Warship A | Buque de guerra | 20 | 1250 explícitos | hdes |
| h01A | Tank | Unidad terrestre pesada | 25 | 1500 explícitos | hfoo |
| n007 | Armoured Transport Ship | Transporte blindado | 6 | 300 explícitos | nzep |

La base `hfoo` de Tank no convierte al Footman local en Tank. Tampoco la base
`nzep` del transporte autoriza a copiar su comportamiento aéreo: mandan las
modificaciones del mapa. Estos roles resumen nombres, no certifican habilidades,
disponibilidad en todos los modos ni equilibrio del runtime.

| Identidades alternativas | Nombre fuente | Oro | HP |
|---|---|---:|---|
| h00R / h012 | Marine / Marine Private | 1 / 1 | 200 / 200 explícitos |
| h00S / h014 | Major / Marine Major | 5 / 5 | 650 / 650 explícitos |
| h00T / h015 | Marine General / Marine General | 10 / 10 | 885 raíz / 800 capas balance; selector pendiente |

Son rawcodes distintos aunque coincidan nombres o estadísticas. Las listas
`utra` y las ramas JASS deciden cuál se recluta: no fusionar las tres parejas
sin conservar identidad y modo. Footman y Mage son perfiles locales y no tienen
equivalencia fuente afirmada; conservarlos, sustituirlos o retirarlos es una
decisión de producto pendiente, separada de las ocho altas.

Evidencia: `references/maps/reforged-v3-source/war3map.w3u`, nombres resueltos
mediante WTS en `data/derived/reforged-source-combat.json`, y su sección `roster`
con las fuentes JASS de disponibilidad. El contraste actual está en
[REFORGED-LATEST-INHERITANCE.md](REFORGED-LATEST-INHERITANCE.md).
