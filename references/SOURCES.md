# Referencias descargadas

Fecha de consulta: 2026-09-05.

## Risk Reforged editable

- Archivo: [Risk_Reforged_v3.0_by_Saran_OPEN_SOURCE.w3x](maps/Risk_Reforged_v3.0_by_Saran_OPEN_SOURCE.w3x).
- Publicación del autor: https://www.hiveworkshop.com/threads/risk-reforged-becomes-open-source.330360/
- Descarga enlazada en esa publicación: https://www.hiveworkshop.com/attachments/risk_reforged_v3-0_by_saran-open-source-w3x.562086/
- Tamaño descargado: 4.763.120 bytes.
- SHA-256: `ACFA7048A5C48D61CB80FB42222D87B0F0CE9204FF4517A256F8502A68907F26`.
- Verificación realizada: SHA-256 y extracción de reglas/metadatos con StormLib 9.40 mediante `scripts/extract_reforged_v3.py`. El JASS, textos, triggers y metadatos están en `maps/reforged-v3-source/`; no se extrajeron texturas ni modelos. No se ha ejecutado en Warcraft III ni abierto en World Editor. Véase [auditoría del script](../docs/RISK-REFORGED-RULES.md).
- El autor invita a estudiarlo y crear versiones. En diciembre de 2025 describe una corrección relacionada con la barra de progreso del multiboard. No se ha comprobado su funcionamiento en los parches actuales.

## Código de Risk Europe / wc3-risk-system

- Repositorio: https://github.com/Warcraft-3-Risk/wc3-risk-system
- Copia local: [wc3-risk-system](wc3-risk-system).
- Commit: `12822eb3d9ff37b1514028cff831f12c762a0d67`.
- Descarga mediante git con historial de un commit y checkout parcial de `src`, `tests` y `docs`, además de archivos de raíz. Los mapas binarios de este repositorio no se han descargado.
- Licencia publicada: [MIT](wc3-risk-system/LICENSE), copyright 2019 trigger. Conservar el aviso si se reutiliza código cubierto por esa licencia. La presencia de recursos de Warcraft en un mapa no demuestra permiso para incorporarlos a nuestro juego; la dirección artística propuesta utiliza recursos propios o con licencia adecuada.

Observaciones obtenidas leyendo este código, que no deben atribuirse automáticamente a todas las versiones históricas de Risk:

| Sistema | Archivo | Observación |
| --- | --- | --- |
| Rondas | `src/configs/game-settings.ts` | Duración configurada de 60 segundos y contador con tick de un segundo. |
| Cobro | `src/app/game/game-mode/base-game-mode/game-loop-state.ts` | Al inicio de ronda reparte ingresos a jugadores activos y avanza la generación de unidades de los países. |
| Propiedad e ingreso | `src/app/triggers/ownership-change-event.ts` | Completar o perder el control de un país modifica el ingreso según su número de ciudades. |
| Economía aislada | `src/app/managers/income-logic.ts` | Funciones independientes de las API de Warcraft para estudiar y trasladar cálculos. Parte del código de ejecución conserva cálculos equivalentes directamente en triggers. |
| Captura | `src/app/triggers/enter-region-event.ts` | Usa presencia de unidades y selección de guardia; comprueba propietario, aliados y enemigos. |
| Guardia | `src/app/city/components/guard.ts` | Gestiona la unidad que guarda una ciudad y su sustitución. |
| Agrupación regional | `src/app/region/region.ts` | Comprueba propiedad de todos los países de una región. |
| Configuración regional | `src/configs/region-setup.ts` | El ejemplo de bonus regional está comentado. Que exista el sistema no implica que esos bonus estén activos en este mapa. |

El código se compila de TypeScript a Lua para ejecutarse dentro de Warcraft III. Las reglas se pueden estudiar y portar; llamadas como `CreateUnit` y `SetUnitPosition` muestran que el motor de Warcraft aporta servicios esenciales. Descargar el mapa no proporciona una implementación independiente de selección, movimiento, combate o sincronización. No se ejecutaron las dependencias ni las pruebas del repositorio de referencia. En v0.6 se adaptaron dos cálculos puros MIT a `RiskReferenceRules.cs` y se probaron en C#; véase [atribución](../THIRD_PARTY_NOTICES.md).
