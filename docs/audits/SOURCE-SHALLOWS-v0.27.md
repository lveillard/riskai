# Riesgus: puertos someros y edificios integrados

## Fuentes y límites

Europe conserva el mapa local **Risk Reforged v3.0 de Saran**: 212 ciudades,
69 países y 44 puertos. New World conserva 293 ciudades, 100 países y 59
puertos. No se trasplantan coordenadas de otra versión de Europe: la captura
aportada muestra Baleares, pero esa región no aparece con ese nombre en este
catálogo de Saran. Se reutilizan datos numéricos, no modelos ni texturas de Blizzard.

Los edificios `h00N`/`h00T`/`h00O`, círculos `B00R` y puntos de refuerzo mantienen
sus coordenadas fuente, con la conversión existente de 50 unidades WC3 por
metro Unity. La adaptación anterior elevaba los círculos de puerto a Y=0,55,
añadía pasarelas y desplazaba edificio y torre a los lados de la costa.

Los archivos `war3map.wpm` extraídos de ambos `.w3x` aportan una cuadrícula
de paso a 32 unidades nativas (0,64 m), independiente de la malla visual W3E.
Europe tiene 1024×1024 celdas y New World 1536×1536.

El export guarda los bytes WPM en Base64, sin cabecera, con dimensiones,
origen y tamaño de celda. Solo se decodifican una vez en el runtime. También
guarda coordenadas nativas y evidencia W3E/WPM de cada edificio, círculo y
punto de refuerzo. Las sumas y hashes de los archivos fuente quedan en
`metadata`; el formato no incluye imágenes ni modelos originales.

## Interpretación comprobada

Se mantienen por separado tierra visual, paso terrestre y navegación naval.
El bit `0x02` bloquea el paso terrestre y `0x40` el naval. En estos archivos:

| Punto o superficie | WPM observado | Resultado |
| --- | --- | --- |
| Los 103 círculos de puerto | `0x08` | Admite tropas y barcos |
| Los 402 edificios de ciudad terrestre | `0x40` o `0x48` | Admite tropas, no barcos |
| La mayoría del mar profundo | `0x0A` | Admite barcos, no tropas |

El círculo de puerto conserva el fondo W3E real: 0,413–0,753 m bajo el agua
en Europe y 0,273–0,753 m en New World, interpolando los mismos triángulos
del terreno. No hace falta levantar una tabla para situar allí la guarnición.

La interpretación no se deduce únicamente de nombres de constantes externos:
Warsmash utiliza `UNWALKABLE=0x02`/`UNSWIMABLE=0x40` (con una advertencia
histórica en este último); HiveWE actual llama `water` a `0x40`. Se contrastó
la interpretación con las coordenadas y superficies de **nuestros archivos**.
No se afirma reproducir todos los detalles del motor de pathfinding de WC3.

Referencias de contraste:

- [Warsmash PathingGrid](https://github.com/Retera/WarsmashModEngine/blob/master/core/src/com/etheller/warsmash/viewer5/handlers/w3x/environment/PathingGrid.java).
- [wc3libs PathMap](https://github.com/inwc3/wc3libs/blob/master/src/main/java/net/moonlightflower/wc3libs/misc/PathMap.java).
- [HiveWE PathingMap](https://github.com/stijnherfst/HiveWE/blob/main/src/base/pathing_map.ixx).

## Variantes de edificio

`DetachedTown`, `IntegratedTown`, `PierHarbor` e `IntegratedHarbor` son
variantes explícitas; no dependen implícitamente de que un mapa sea importado.
Europe/New World eligen las dos integradas. Classic/Riverlands conservan las
dos originales. Las torres integradas comparten el centro XZ del edificio;
no se amplía su alcance ni su daño ni se crea una segunda guarnición.

La menor distancia entre una torre centrada y un círculo ajeno es 14,411 m
en Europe y 16,406 m en New World. Los círculos propios están a 4,874–6,675 m
del edificio, fuera de su cuerpo y dentro del alcance defensivo.

El círculo mantiene su coordenada exacta. El defensor usa el punto NavMesh
válido más cercano, resuelto una sola vez dentro del radio existente de
0,9 m. En `europe-033`, el círculo está en el borde entre WPM `08` y `0A`;
la erosión del NavMesh para el cuerpo de la unidad desplaza su ancla 0,481 m.
No se mueve el círculo, no se añade suelo sobre agua profunda y no se reduce
el radio global de los agentes. La prueba exige el ancla NavMesh exacta y
que el guardián no se desplace al avanzar la simulación. El JASS original
teletransporta al guardián al círculo ignorando pathing; esa particularidad
no se presenta como una equivalencia exacta del motor Unity.
La comprobación de los dos mapas encuentra tres desplazamientos horizontales
mayores de 0,2 m: `europe-033` (0,481), `newworld-033` (0,509) y
`newworld-237` (0,293); todos respetan el mismo contrato de anclaje.

## Reproducción de datos

```powershell
python scripts/extract_reforged_v3.py
python scripts/extract_map_members.py references/maps/Risk-New-World-v3.0.w3x references/maps/risk-new-world-v3-source
python scripts/export_playable_risk_maps.py
python -m unittest scripts.test_export_playable_risk_maps
```

Los seis tests de exportación pasan: cabeceras/dimensiones, bytes completos,
anclas y profundidades, bits de paso y recuentos. La extracción requiere los
archivos fuente locales, que no se redistribuyen en el repositorio.

## Revisión adversarial

Se ejecutó Grok 4.7 con herramientas/web desactivados y snapshots acotados,
desde dos perspectivas. Ambas respuestas finalizaron con `end_turn` y
reportaron `grok-4.7-build`: edificios (215 s) y terreno (294 s). Prompts,
respuestas y estado quedan en `.tools/grok-shallows-v027/` (evidencia local).

La revisión de edificios no confirmó defectos. La de terreno propuso una
fuga de colisión cuando el origen WPM está desplazado 64 unidades respecto
del terreno. Ese caso no llega a construir la malla: `Validate` ya rechaza
orígenes distintos por más de 0,01 unidades. El extracto revisado no incluía
esa validación; no se aceptó el hallazgo como fallo de los mapas reales.
Estas revisiones limitadas complementan las pruebas Unity, no certifican
ausencia de errores.
