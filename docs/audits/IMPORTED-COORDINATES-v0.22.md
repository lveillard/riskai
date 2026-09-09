# Revisión de coordenadas importadas · 9 septiembre 2026

Revisión independiente con Terra de los JASS, W3E, W3I y círculos B00R locales,
los importadores y los recursos serializados de Europa y Nuevo Mundo.
No se han desplazado anclas para mejorar su apariencia.

La conversión conserva los ejes: `x=(nativeX-centerX)/50`,
`z=(nativeY-centerY)/50`. Error máximo de las coordenadas serializadas frente a
la transformación de fuente: **0.0 unidades Unity**, también en límites W3I.

| Mapa | Ciudades | Círculos | Hogueras | Puertos |
| --- | ---: | ---: | ---: | ---: |
| Europa | 212 | 212 | 69 | 44 |
| Nuevo Mundo | 293 | 293 | 100 | 59 |

Cada círculo conserva un doodad B00R único. La distancia original ciudad–círculo
es de 243.705–320 unidades nativas en Europa y 243.705–333.755 en Nuevo Mundo.
Todas las hogueras se encuentran en nodos W3E de tierra (69/69 y 100/100).

Las advertencias de agua del validador pertenecen a puertos: 87 anclas en 44
puertos de Europa y 117 en 59 de Nuevo Mundo. Son posiciones originales sobre
agua, no errores de orientación. Se resuelven con pasarelas, sin mover los datos.

Límites de esta comprobación:

- Los atraques navales son adaptaciones de `NavalWorld.AddImportedHarbor`:
  dirección ciudad→círculo, búsqueda inicial seis unidades más allá del círculo
  y agua navegable cercana en un radio de 30. La fuente no aporta esa coordenada
  de atraque y no se afirma equivalencia posicional con WC3 para ella.
- Las hogueras utilizan `NavMesh.SamplePosition` con radio 8 al crear la vista.
  Se ha comprobado su ancla original; la posición efectiva depende del NavMesh.
- El redondeo visual de costa protege las cercanías de ciudades (9 unidades) y
  puertos (24 unidades). Las mejoras de normales y arquitectura no modifican
  las coordenadas de ciudad, círculo o hoguera.

La revisión no ejecutó Warcraft III. Las capturas y pruebas del runtime se
registran por separado; esta comprobación no certifica todas las reglas de juego.
