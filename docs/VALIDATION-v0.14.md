# Validación v0.14

Unity 6000.3.23f1, URP, Windows. Verificación local del 6 de septiembre de 2026. Logs, XML, capturas completas y ejecutables quedan fuera de Git.

## Pruebas

**107 casos distintos aprobados: 34 EditMode y 73 PlayMode**, mediante una pasada completa y la repetición del único caso corregido. No es un informe único con 107 casos.

| Ejecución | Resultado | Evidencia local |
| --- | --- | --- |
| EditMode | 34/34, 0,091 s | `TestResults/editmode-v14.xml` |
| Imports, tras corregir altura de NavMesh | 3/3, 9,565 s | `TestResults/playmode-v14-imports.xml` |
| PlayMode completo, incluidas nuevas aserciones de hogueras y puertos | 72/73, 222,609 s | `TestResults/playmode-v14.xml` |
| Caso de órdenes con fixture corregido | 1/1, 4,533 s | `TestResults/playmode-v14-orders.xml` |

La prueba fallida usaba dos coordenadas históricas a altura cero y exigía un corredor directo. El terreno ampliado ya no cumplía esa premisa. El fixture final utiliza puntos del NavMesh del claro de despliegue de una ciudad, verifica cinco unidades de separación y una línea transitable, y aísla el duelo de guarniciones y captura. Conserva las comprobaciones de mantener posición, detenerse y entrar en combate, y retirarse mediante una orden explícita. No se modificó el movimiento del juego para conseguir ese resultado.

La nueva prueba de guarnición sustituye al defensor, introduce aglomeración y combate, y exige menos de 2 mm de deriva durante 120 fotogramas. Verifica que liberar al anterior restaura su representación como agente móvil.

Los imports montan escenas reales de Europe y New World: 212/293 defensores únicos de tipo ballestero, posiciones XY fuente, anclas próximas al círculo, 69/100 hogueras utilizables, 44/59 puertos que comparten estado, torre y defensor con su ciudad, y reclutamiento permitido con más de cien guarniciones. Los 505 defensores conservan vida completa durante la observación; ambas escenas registran cero disparos de torre. Distancia mínima de una torre añadida a un círculo ajeno: 14,012 unidades, superior al alcance 13. Las pruebas navales comprueban cada embarcadero y una ruta costera con holgura por mapa.

La primera pasada de importación detectó 22,3 cm de diferencia entre un círculo y el NavMesh. La interpolación ahora coincide con los triángulos del terreno y el voxel importado se redujo de 0,24 a 0,12; se mantuvieron las tolerancias de la prueba.

## Ejecutable

Compilación final Windows v0.14.0, Mono para iteración: **203.368.998 bytes**, `RISKAI_BUILD_OK` en `RiskAI/Logs/build-v14-final.log`. Ejecutable: `Builds/Windows-v0.14/RiskAI.exe`.

Después de las pruebas, la revisión visual ajustó únicamente el agua/material de costa y el texto del puerto importado. El agua comparte la óptica del río, con profundidad real de escena y sin usar la costa sintética de los escenarios pequeños. La compilación final y las capturas corresponden a esos cambios.

Capturas del ejecutable a 1600×900, semilla 701, reparto por ciudades:

| Escenario | Ciudades / puertos | Defensores | Azul / rojo / neutral | Móviles / barcos |
| --- | --- | ---: | --- | --- |
| Las Marcas | 18 + 7 puertos independientes | 25 | 12 / 12 / 1 | 0 / 0 |
| Cuatro Riberas | 20 + 8 puertos independientes | 28 | 14 / 14 / 0 | 0 / 0 |
| Europe | 212, incluidos 44 puertos | 212 | 106 / 106 / 0 | 0 / 0 |
| New World | 293, incluidos 59 puertos | 293 | 146 / 146 / 1 | 0 / 0 |

Las cuatro secuencias registran cero heridos y cero disparos iniciales. Evidencia: `RiskAI/Logs/player-v14-final-<mapa>.log` y `Captures/v14-final-<mapa>/`. Se revisaron el menú de cuatro escenarios, la vista general de ambos imports, puertos, círculos y el secano del suroeste. Las capturas pausan la simulación tras verificar el inicio; no representan partidas completas.

[Europe](images/v0.14-europe.png) · [New World](images/v0.14-newworld.png) · [Secano de Las Marcas](images/v0.14-southwest.png).

## Límites

No hay benchmark de rendimiento ni validación Web, Android o multijugador. Solo existen dos bandos y neutrales. La cuadrícula W3E conserva una costa aún angular; faltan límites jugables W3I, pathing especializado y elementos del editor como puentes/destructibles. Las áreas exteriores del lienzo fuente siguen visibles al alejar mucho la cámara. Las plataformas originales adaptan los puertos sobre agua, pero no se ha probado manualmente cada desembarco ni el equilibrio de todas las semillas. New World cubre Europa y América, no todo el planeta.
