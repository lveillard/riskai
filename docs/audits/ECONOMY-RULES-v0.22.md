# Auditoría de economía y reglas v0.22

Fecha: 2026-09-09. Fuente principal: `references/maps/reforged-v3-source/war3map.j`, extraído de Saran Risk Reforged v3.0. Contraste: `references/maps/risk-new-world-v3-source/war3map.j`. Las líneas de New World aquí usan `splitlines()` (incluye CR sueltos); por ello se identifica también cada función, evitando depender del contador de una herramienta concreta.

## Corrección aplicada: ingresos de países fragmentados

El runtime cobraba **4 + todas las ciudades propias** por minuto. El modo Conquest FFA de la fuente cobra **4 + ciudades de países completamente propios**, si queda al menos una ciudad. Con cero ciudades, cobra cero.

| Situación | Antes | Fuente y corrección |
| --- | ---: | ---: |
| Tres ciudades dispersas, sin país completo | 7/min | 4/min |
| Un país completo de tres ciudades | 7/min | 7/min |
| País de tres ciudades pierde una (quedan dos propias) | 6/min | 4/min |
| Cero ciudades, aún quedan tropas | 0/min | 0/min |

La condición no está en la pequeña función que suma ciudades: está en su llamador. La auditoría v0.16 había omitido ese nivel del flujo.

1. `Trig_Setup_default_modes_Actions`, Europa 5563–5587: modo 0 Conquest, alianza 0 FFA, ingreso inicial 4, básico 4, multiplicador 1, turno 60 segundos.
2. `Trig_Assign_bases_Func006Func009Func001Func005C`, 8889–8894: exige que las ciudades propias del país igualen todas sus ciudades. Sólo entonces `Trig_Assign_bases_Func006Func009Func001A`, 8902–8904, introduce al jugador en `RegionOwnersGroup`.
3. `Trig_City_Claim_Func019Func017C`, 18081–18086, repite la igualdad al capturar; la acción actualiza ese grupo en 18243–18244.
4. `Trig_Income_Give_Func003Func001Func002Func001Func001C`, 18954–18959, exige un único propietario completo. `Trig_Income_Give_Actions`, 19230–19256, ejecuta la suma sólo sobre ese grupo en la rama FFA.
5. `Trig_Income_Give_Func012Func001Func003C`, 19071–19076, exige al menos una ciudad para el básico. `Trig_Income_Give_Func012A`, 19161–19184, fija 4 al empezar y luego abona la cantidad calculada.
6. New World conserva las mismas funciones y condición: `Trig_Income_Give_Func003Func001Func002Func001Func001C` 19295–19300 y `Trig_Income_Give_Actions` 19542–19565.

`Economy.CalculateIncome` ahora comprueba el propietario completo antes de sumar cada ciudad. Las ciudades sin identificador de país válido sólo habilitan el básico; no forman artificialmente un país. Los tests cubren fragmentación neutral/enemiga, completar/perder/recuperar país, pago real al minuto, tres ciudades dispersas frente a país completo, y ausencia total de ciudades.

## Valores comprobados que se conservan

- Oro inicial 4, básico 4, turno 60 s: defaults arriba. El básico puede parecer generoso con pocas ciudades, pero reducirlo sería una decisión de balance distinta de restaurar la fuente.
- Bounty de un cuarto del point value con resto acumulado: `Trig_Count_kills_deaths_and_other_bounty_Actions`, 18689–18699. `Economy.GrantBounty` conserva ese comportamiento.
- Refuerzo FFA: `ceil(ciudades/2)` puntos por ronda, tope vivo `5 * ciudades`, goteo cada 0.5 s: `Trig_Begin_Recruit_Actions` y `Trig_Recruit_Step_Actions`, 19374–19533. La economía de oro y esos refuerzos son sistemas separados.
- Victoria territorial del 60%: defaults 5565–5566 y cálculo 8348. `RiskReferenceRules.CalculateCityCountWin` proviene de la referencia MIT distinta `wc3-risk-system`; no debe confundirse con una transcripción íntegra del JASS de Saran.

## Divergencias pendientes, sin cambio de balance en esta revisión

| Tema | Evidencia fuente / estado Unity | Trabajo pendiente |
| --- | --- | --- |
| Momento de victoria | `Trig_Turn_End_Actions`, 4789–4790, activa la comprobación de victoria al terminar turno; Unity exige mantener el umbral 20 s. | Decidir y trasladar el flujo completo de victoria, incluyendo otros triggers que la invocan; no reemplazar sólo una constante. |
| Regiones especiales | `Trig_Set_Regional_Spawns_Actions`, 5423–5440, configura British Isles con centros 59/58/47/48, +2 oro y `h00W`; `Income_Give` aplica el bonus y la ruta especial. Unity deja RegionBonuses en cero. | Mapear por identidad de países en cada variante, comprobar condiciones del spawn; no aplicar un +2 genérico a todos los países ni añadir unidades sin encargo. |
| Límite global móvil | Unity `PopulationLimit=100` además del tope por país. La evidencia JASS citada demuestra el tope regional, no ese límite global. | Mantener explícito como adaptación hasta revisar coste/rendimiento y límite efectivo de fuente. |
| Edificios y unidades adaptados | Footman/Mage son perfiles locales; sus tiempos 3/6 s son locales. Construcción de torre usa coste 60 y 7 s, mientras el perfil bunker lleva coste fuente 3. | Auditar la ruta concreta de construcción y catálogo antes de sustituirla. No se han incorporado unidades nuevas. |
| Herencia de estadísticas | Los overrides W3U prevalecen; las capas actuales de balance difieren y el selector del motor sigue sin prueba. | Resolver la procedencia efectiva según `REFORGED-LATEST-INHERITANCE.md`; no adoptar tablas actuales arbitrariamente. |
| Modos opcionales | Alianzas, capitales, ingreso alternativo agotable, multiplicadores y otras variantes existen en JASS; Unity expone Conquest FFA. | Tratar cada modo como alcance propio, sin mezclar sus fórmulas con los defaults. |

No se ejecutó Warcraft III ni Unity durante esta auditoría. Se comprobó el flujo de los scripts locales y se prepararon pruebas C# para la validación centralizada. Los informes previos de `.tools/qwen-source-audits` son pistas de búsqueda, no sustituyen la lectura del llamador y sus condiciones.
