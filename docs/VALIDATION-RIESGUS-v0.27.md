# Riesgus v0.27.0 — validación

22 de septiembre de 2026. [Datos fuente y decisiones de adaptación](audits/SOURCE-SHALLOWS-v0.27.md).

## Cambios

- Europe/New World: zonas someras compartidas por tropas y barcos, derivadas
  del WPM original y sobre el fondo W3E, sin las pasarelas artificiales.
- Ciudades y astilleros integrados con torre central. Mismos círculos,
  propiedad, guardián, daño y alcance; ninguna ciudad ni país añadido.
- Classic/Riverlands: se conservan ciudades y muelles separados.

Comparación estructural contra los JSON anteriores: alturas, agua, máscaras
visuales, árboles, IDs y todas las coordenadas/cantidades de ciudades y
países permanecen iguales. Solo se añade información fuente y pathing.

## Pruebas automatizadas

| Grupo | Casos aprobados |
| --- | ---: |
| EditMode completo | 170 |
| Partidas importadas, navegación naval y captura por tropas/fragata | 5 |
| Selección de edificios y variantes de mapa | 13 |
| Anclaje, relevo de guarnición y defensa de torres | 10 |
| Óptica del agua y embarque/desembarque | 3 |
| Exportador Python | 6 |

Son 201 casos Unity de esta validación, no una ejecución de todas las clases
PlayMode del repositorio. Los 13 de selección/variantes combinan los 12
aprobados de `v027-legacy-layout.xml` con el caso reencuadrado y aprobado en
`v027-selection-final.xml`. La corrección fue de la cámara del test; no se
relajaron las comprobaciones de selección ni de propiedad.

La partida importada recorre los dos mapas: 505 guarniciones y 103 puertos.
Todos los círculos tienen NavMesh, todas las rutas tierra→círculo están
conectadas y todos los atraques alcanzan un destino naval real a 10 m. El
agua profunda no recibe NavMesh. Tras avanzar la simulación, los guardianes
mantienen identidad, posición y salud completa; las torres no disparan a
los puestos iniciales vecinos. La fragata captura el puerto vacío; el
transporte no, y una tropa terrestre conserva su vía de captura.

Informes locales: `TestResults/v027-editmode-optics.xml`,
`v027-source-opening-final.xml`, `v027-imported-diagnostic.xml`,
`v027-legacy-layout.xml`, `v027-selection-final.xml` y
`v027-garrison-defense.xml` y `v027-water-optics.xml`. El diagnóstico naval contiene cuatro casos
aprobados y el antiguo límite visual del ancla, sustituido por la prueba
física de suelo/anclaje aprobada en `v027-source-opening-final.xml`.

Grok 4.7 completó dos revisiones adversariales de snapshots limitados:
edificios y terreno. Resultado y límites documentados en la auditoría fuente.

La revisión visual añadió una señal de aguas someras a los estantes
transitables WPM que quedan alejados de la costa seca. Se comprueba esa
señal en los 103 círculos de puerto; no cambia el pathing. Las pruebas de
óptica y acceso verifican también playa, costa no apta y embarque/desembarque.
También se extendió la base visual de la torre hasta el suelo del edificio,
manteniendo la altura de galería y disparo: los muelles no tienen un salón
debajo que oculte una base flotante. Se comprueba en los 505 edificios.

## Navegador y presentación

Edge/WebGL: Europe a 1600×900, New World a 390×844 (DPR 2, touch emulado)
y Classic a 1024×768. Sin errores de consola; controles reales de oro y
clasificación, colas, selección, mapa y encuadres responden correctamente.
Los informes completos están en `Captures/v027-europe-final`,
`Captures/v027-world-final` y `Captures/v027-classic-tablet`.

La vista de tablet se comprobó antes de los dos últimos cambios visuales,
que afectan únicamente a las variantes importadas. Las pruebas no
certifican rendimiento de móviles/tablets físicos.

Capturas recortadas del cliente real, con IA desactivada para inspección:

- [Europe: muelle en aguas someras](audits/ports-v0.27/europe-shallows.webp).
- [World: muelle con torre central](audits/ports-v0.27/world-harbor.webp).
- [Ciudad integrada](audits/ports-v0.27/integrated-town.webp) y
  [ciudad separada de Classic](audits/ports-v0.27/detached-town.webp).

El puente de pen/rueda conserva sus 17 pruebas JavaScript aprobadas, y los
checks del Worker de Cloudflare siguen pasando.

## Recursos

Inicio normal de Europe, 16 jugadores, semilla 19031, Edge 153 a
1600×900/DPR 1; muestra de 30 s, sin otros navegadores de QA ni Unity
compilando. Mismo equipo de la [medición anterior](audits/RESOURCES-v0.26.md),
pero no es una comparación A/B controlada.

| Medida | Resultado |
| --- | ---: |
| Cadencia `requestAnimationFrame` | 57,9 Hz |
| Intervalo medio / p95 | 17,27 / 16,8 ms |
| Picos >50 ms / máximo | 5 / 350 ms |
| Hilo principal del navegador ocupado | 81,5% |
| Heap WASM reservado | 491,9 MiB |
| Memoria privada comprometida del navegador completo | 1621,8 MiB |

Sin errores. La cadencia del navegador no equivale a FPS medidos por Unity;
la telemetría del juego en el primer tramo registró 16,70 ms de media y
33 ms máximo. El heap WASM no es RAM residente ni VRAM; las lecturas GPU
incluyen toda la máquina. [Informe completo](audits/ports-v0.27/resources-europe.json).
Los picos y el consumo siguen siendo trabajo de rendimiento, especialmente
para partidas avanzadas y móviles físicos. No se repitió aquí el stress de
900 unidades de v0.26.

## Publicación

Windows: `Builds/Windows-v0.27.0/RiskAI.exe`, accesible desde
`Play-Riesgus.cmd`. Web: `Builds/Web-v0.27.0`.

Publicada en **https://riesgus.com** mediante el origen Azure existente,
manteniendo el Worker/dominio Cloudflare. Release:
`20260922T134715Z-shallows-v027`. Los 10 archivos públicos coinciden con
los SHA-256 del manifiesto local, incluido el HTML de entrada:
[verificación pública](audits/ports-v0.27/public-verification.json).
La partida WebGL abierta desde el dominio también supera la revisión de
interfaz, compras, mapa y muelle a 390×844/DPR 2, sin errores:
[prueba pública móvil emulada](audits/ports-v0.27/public-phone.json).
`https://www.riesgus.com/` devuelve 301 hacia el dominio principal.

Se conserva la release anterior `20260922T121400Z-zoom-v0261`; el recibo
local `.deploy/20260922T134715Z-shallows-v027/receipt.json` contiene la
orden de rollback. No se recrearon ni reutilizaron los tokens Cloudflare
ya revocados en la migración anterior. Sin commit ni push.
