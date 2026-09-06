# Iteración v0.12 — dominios y sucesión

Esta entrega continúa el [corte de arquitectura v0.11](ITERATION-v0.11.md) y adapta captura, mapa y economía al último feedback. La [auditoría de fuentes](RISK-RULES-v0.12.md) distingue Saran, New World, Rome y nuestras decisiones locales.

La [validación](VALIDATION-v0.12.md) recoge 97 pruebas aprobadas, compilación Windows y revisión de ambos escenarios, con los límites visuales todavía pendientes.

## Cambios de juego

- Ciudades, puertos y torres permanentes. El objetivo de ataque es el soldado del círculo; clic sobre torre enemiga redirige hacia su defensor. Sucesión en el siguiente tick: aliado más cercano a 4,43, enemigo más próximo a 6, o neutral. Radio de círculo 1,55, tolerancia vertical 1,25. Los empates usan ID de entidad; el relevo conserva identidad y salud.
- Perfil local de torre de captura: 81–88 perforante, 0,9 s y alcance 13. Conservamos aparte el búnker Saran 51–58 / 1,5 s / 8,5. La preferencia por el sucesor más cercano y las torres permanentemente armadas son decisiones del prototipo, no una reproducción exacta del JASS.
- Oro inicial/base 4, ronda 60 s, un oro por ciudad de grupo completo. Costes terrestres 1/1/5/4/3/2 y navales 5/2. Las bajas usan `PointValue/4` con acarreo fraccionario; precio y valor de puntos son campos independientes. Refuerzos: tope de cinco puntos vivos por ciudad del grupo.
- Puertos con identidad y producción propias; eliminada la renta adicional inventada de los puestos insulares. Los dos perfiles navales implementados siguen siendo un subconjunto del catálogo original.
- IA evalúa amenazas cada 0,75 s y redirige tropas móviles cercanas hacia la defensa, evitando repetir la misma orden. Mantiene reserva cuando hay tropas suficientes. Sigue sin visión limitada ni estrategia naval de desembarco.
- Tiro perforante cuesta arriba: fallo sembrado del 25 % a partir de 2,5 de desnivel. La probabilidad sigue la [referencia oficial de combate de WC3](https://classic.battle.net/war3/basics/combat.shtml); el umbral en metros es local. No se añade daño extra a magia o cuerpo a cuerpo.

## Mapas y presentación

**Las Marcas** conserva sus 12 ciudades. **Cuatro Riberas** añade 20 ciudades en cuatro grupos continentales y un archipiélago, cuatro ciudades por grupo, tres islas y más profundidad de mapa. Río central desde la montaña meridional, puente y cruce norte. Los dos escenarios conservan reparto por semilla y se eligen antes de empezar; su geometría es original, sin importar posiciones del mapa de referencia.

Cada grupo tiene una hoguera: punto real de aparición de refuerzos y entrada de inspección territorial. Seleccionarla genera una malla transparente una sola vez y resalta únicamente sus ciudades. Los postes siguen indicando propietario sin repintar biomas por facción. La producción inicial de cada grupo es un ajuste de diseño; queda pendiente medirla según dificultad de defensa y valor de tropas.

Los shaders personalizados aplican el desvanecimiento de sombras de URP por posición mundial, que faltaba en la variante de `GetMainLight` anterior. Se aleja la niebla para evitar que domine el plano visible. Pradera y agua incorporan variación espacial suave; el agua añade ondulación visual sin subdivisión dinámica ni pases nuevos. Los círculos tienen más segmentos y extremos redondeados. El lecho de río y lagunas es continuo en la malla visible; una malla de colisión aparte excluye el agua. El overlay tiene transparencia propia y transición suave en las fronteras de grupo.

El menú separa partida, controles y ajustes. Se elimina la condición de victoria por capitales; la base inicial solo centra cámara/despliegue. La victoria local exige 60 % de ciudades durante 20 s. Se suprimen compras de mejoras y reconstrucciones del HUD.

## Input y arquitectura

Botón derecho distingue pulsación de arrastre con un reconocedor puro: superar siete píxeles transforma el gesto en desplazamiento de cámara y anula su orden al soltar. Cámara conserva `Drag`, `Pan` y `ZoomAt` como operaciones independientes del dispositivo. El gesto se cancela al perder foco. El protocolo de touch propuesto por el usuario está registrado en [TODO.md](../TODO.md), todavía sin activar.

Seguimos sobre Core sin Unity, simulación 20 Hz, proyectiles independientes de vistas, adquisición espacial, pools y snapshots de HUD de v0.11. No se cambia a ECS. La nueva IA usa IDs de torres para identificar posiciones; el estado de autoridad continúa dentro de la simulación local.

Persisten cortes pendientes: perfiles externos, UI Toolkit, Input Actions completo, mapas/NavMesh horneados, compra/naval por comandos, servidor y 6–12 jugadores, niebla, guardado y habilidades. Los métodos de construcción antiguos permanecen como compatibilidad interna, sin acceso de compra normal; conviene retirarlos al separar datos de asentamientos. NavMesh aún impide afirmar determinismo de replay. No se acreditan plataformas táctiles, Web o Android ni mejoras porcentuales de rendimiento sin medirlas.
