# Validación v0.11

Comprobación local del 6 de septiembre de 2026, Windows, Unity **6000.3.23f1**, URP y backend Mono. El [alcance de la implementación](ITERATION-v0.11.md) distingue los cambios terminados de las migraciones pendientes.

## Resultados

| Comprobación | Resultado | Evidencia local |
| --- | --- | --- |
| Reglas EditMode | **26/26**, sin fallos | `TestResults/editmode-v11.xml` |
| Partida PlayMode, batería completa final | **64/64**, sin fallos; 234,9 s | `TestResults/playmode-v11-final.xml` |
| Compilación Windows x64 | Correcta; BuildReport: **195.763.014 bytes** | `RiskAI/Logs/build-v11.log`, marcador `RISKAI_BUILD_OK` |
| Ejecución y capturas del Player | Diez capturas a 1600 × 900, semilla 20260905; sin errores ni excepciones registrados | `TestResults/player-capture-v11.log`, marcador `RISKAI_PLAYER_CAPTURE_OK` |

Los informes, logs y ejecutables son locales e ignorados por Git. Se pueden repetir con `scripts/Unity.ps1 -Action Test`, `-Action PlayTests` y `-Action Build`, con el Editor cerrado.

## Qué comprueban las pruebas

- Reloj fijo, pausa y recuperación de tiempo acumulado; la pausa de partida no modifica `Time.timeScale` ni deja avanzar unidades o impactos.
- Eliminar una flecha visual no cancela su daño. Deshabilitar la presentación conserva la resolución del combate. Los pools de flechas e impactos reutilizan sus objetos.
- Una baja sale del registro activo; al reutilizar su objeto recibe una identidad nueva y recupera vida, navegación y estado de órdenes. Los IDs antiguos dejan de resolver.
- Los comandos rechazan otro propietario, coordenadas no finitas y unidades de guarnición. Se aplican al comenzar un tick y se descartan si su unidad muere antes.
- La consulta espacial contiene todos los candidatos dentro del radio preciso y cuenta correctamente la presión de atacantes por equipo.
- Ocupación circular continua, radio de protección, separación por altura, guarnición retenida, conservación de identidad/vida y prohibición de embarcar. La torre no disputa el círculo y cambia con el edificio; se puede reconstruir tras destruirla.
- Regresiones de combate, formaciones, ataque en movimiento, economía, reclutamiento, victoria, rampas, selección y cámara. Transporte, desembarco y captura insular; cola naval, reembolsos y adquisición automática de blancos costeros por galeras.

La primera ejecución descubrió pruebas que asumían destrucción de soldados y torres independientes del edificio, además de interferencias de combatientes ajenos al caso. Se actualizaron esas expectativas a las reglas nuevas y se aislaron los escenarios. El resultado de la tabla procede de una ejecución completa posterior, no de sumar ejecuciones parciales.

## Inspección visual

Se inspeccionaron las capturas reales de ciudad, puerto y ayuda: círculo de guarnición, estado de ocupación, torre, paneles de compra y explicación de la conversión. El HUD de esta semilla muestra 24 soldados por bando. Las diez capturas locales incluyen también vista general, mesetas, flota, río, desembocadura, robles e isla; no se afirma una nueva revisión artística de todos esos entornos.

![Ciudad con guarnición y panel de reclutamiento](images/v0.11-city.png)

## Límites

Estas pruebas no acreditan determinismo entre máquinas, rendimiento de miles de unidades, gestos táctiles, servidor ni builds Web/Android. La navegación aún se integra mediante NavMeshAgent y la interfaz sigue siendo IMGUI. No hay un benchmark comparativo de CPU/GC para publicar porcentajes de mejora. Mono se mantiene para iteración local; las plataformas finales requieren sus propias compilaciones y pruebas.
