# Reglas Risk Reforged v0.16

## Alcance y procedencia

Esta auditoría usa datos numéricos de los mapas extraídos localmente. No copia ni distribuye arte, modelos, sonidos ni tablas de Blizzard.

| Variante | Objetos locales | Script local | Formato de objetos |
| --- | --- | --- | --- |
| Europa Saran | `references/maps/reforged-v3-source/war3map.w3u` | `war3map.j` | W3U v3 |
| New World | `references/maps/risk-new-world-v3-source/war3map.w3u` | `war3map.j` | W3U v2 |

Los campos que no están en un W3U personalizado se heredan del `oldId`. Para `hrif`, `hkni`, `hmpr`, `hmtm`, `otrb` y `nzep`, esta auditoría resuelve la herencia contra las tablas propias `references/owned-disc-data/RoC-Units-UnitBalance.slk` y `RoC-Units-UnitWeapons.slk`. Es evidencia del baseline RoC, no una afirmación de que una tabla TFT 1.21 aplicada sea idéntica: los ficheros de parche TFT disponibles son BSDIFF, no SLK ya resueltos. Para el Destroyer `hdes`, se leyó además su fila `Units\UnitWeapons.slk` directamente del MPQ RoC interno `File00000155.mpq` del medio propio `references/owned-disc-data/tft-setup.mpq`; no se conservó ni distribuyó ese MPQ ni sus recursos. La conversión espacial es 50 unidades WC3 por unidad Unity.

Los cuatro perfiles jugables principales y `o000` tienen los mismos overrides en los dos mapas. Las columnas que vienen de W3U son exactas para cada mapa; las que indican dados, tipo, defensa o vida heredados requieren el baseline señalado arriba. Los offsets difieren por versión, por lo que el exportador/auditor debe conservar tanto variante como `oldId`; un rawcode no basta en todos los casos.

## Perfiles corregidos

| Runtime | Rawcode / herencia | Vida | Daño fuente | Alcance Unity | Cadencia | Velocidad Unity | Armadura / defensa | Ataque | Oro / puntos |
| --- | --- | ---: | --- | ---: | ---: | ---: | --- | --- | ---: |
| Ballestero | `h00B` ← `hrif` | 200 | 15 + 2d4 = 17–23* | 8 | 1.60* | 5.4* | 0 / small (Light)* | pierce* | 1 / 1 |
| Caballero (`UnitKind.Guard`) | `h00G` ← `hkni` | 650 | 37 + 2d5 = 39–47* | 2* | 1.36* | 7* | 7 / large (Heavy)* | normal* | 5 / 5 |
| Mortero | `h00H` ← `hmtm` | 350 | 18 + 1d13 = 19–31* | 18 (mín. 5*) | 3.50* | 4.6 | 0 / medium (Medium)* | siege* | 3 / 3 |
| Sanador | `h00E` ← `hmpr` | 250 | 7 + 1d2 = 8–9* | 8 | 2.00* | 5.4* | 1 / small (Light)* | pierce* | 2 / 2 |
| Torre | `o000` ← `otrb` | 550 | 50 + 1d8 = 51–58 | 8.5 | 1.50 | — | 3 / fort (Fortified) | pierce | 3 / 3 |

`*` marca un campo heredado, resuelto contra el baseline RoC local. `h00E` además tiene `Ahea`; que el prototipo le dé curación aliada es una adaptación de capacidad, pero su ataque básico y tipo de defensa siguen la herencia disponible. `Footman` y `Mage` no tienen rawcode equivalente de este mapa y se mantienen explícitamente como perfiles adaptados.

Las correcciones están en `Core/ReforgedProfiles.cs`: cooldown y Light del ballestero; alcance/cooldown del caballero; Medium del mortero; Piercing/Light del sanador. `BattleRules` muestra a `UnitKind.Guard` como **Caballero / Caballería pesada**, sin cambiar el ID ni el nombre de modelo de compatibilidad.

## Oro e ingresos

Los defaults de ambos JASS contienen `ModesTurnTime=60`, `ModesFirstIncome=4`, `ModesBasicIncome=4`, `ModesIncomeMultiplier=1`, `ModesSpawnLimit=5`, `ModesBounty=4` y `ModesIncomeType=0`.

- El primer ingreso es 4 de oro y se ejecuta en `Post_Start_Init`, antes del primer temporizador de 60 s.
- Cada ronda siguiente, un jugador con una o más ciudades cobra `4 + ciudades propias`; una ciudad propia aporta 1 aunque su país esté fragmentado.
- Con cero ciudades, el ingreso de ronda es 0. No existe un bono fijo por completar país en el modo por defecto.
- La recompensa acumula el valor de puntos del muerto y paga cada cuarto de punto entero (`pointValue / 4`), conservando el resto fraccional.

En Europa, esto se ve en `war3map.j` líneas 4484–4488 (primer ingreso), 5568–5587 (defaults), 19230–19296 (income) y 18689+ (bounty). New World conserva los defaults en 1669–1671 y el flujo de ingresos en 5656–5676. `Core/BattleRules.cs` y `Economy.CalculateIncome` reflejan esta regla; `CountryOwner` se conserva para la lógica de refuerzos, no para bloquear el oro de las ciudades.

## Refuerzos de país

Al acabar una ronda, el JASS marca cada región para reclutar y ejecuta `Begin_Recruit`. En FFA, una región completa y bajo su máximo recibe crédito persistente de:

```
creditPerRound = ceil(cityCount / 2) puntos
pointCap = cityCount * 5 puntos
unit = h00B (1 punto)
```

`Recruit_Step` corre cada 500 ms y crea un `h00B` por punto hasta consumir crédito o alcanzar el máximo. El crédito usa `+=`; no se resetea entre rondas, y el JASS no lo borra por un cambio de dueño. Si no hay propietario jugador, simplemente deja de producir hasta que pueda hacerlo de nuevo. El prototipo mantiene su tope global local de 100 móviles como una decisión separada: no es un límite de la fuente.

Europa: `war3map.j` 4523–4533, 4770–4783, 19352–19414 y 19488–19547. New World tiene el mismo orden en 1434, 1494 y 5695–5734. `BattleRules.CountryReinforcementPointsPerRound`, `CountryReinforcementPointCapPerCity` y `CountryReinforcementStepSeconds` exponen el contrato numérico para el goteo de campamentos.

## Puertos y navales

`h00O` es el Shipyard. Su `utra` incluye reclutas de tierra y barcos; la condición de reclutamiento del JASS trata expresamente a `h00O`, así que no hay base fuente para vetar tropas terrestres en un puerto.

| Recruit de puerto | Nombre WTS en New World | Base | Datos efectivos |
| --- | --- | --- | --- |
| `h012` | Marine Private | `hrif` | 200 HP, 16+2d4, alcance 6, 1.6 s, 5.4, armadura 1 Light/Pierce, coste/puntos 1 |
| `h014` | Marine Major | `hkni` | 650 HP, 37+2d5, alcance 2, 1.36 s, 5.6, armadura 6 Heavy/Normal, 5/5 |
| `h015` | Marine General | `hkni` | 800 HP, 64+2d5, alcance 2, 1.45 s, 5.6, armadura 8 Heavy/Normal, 10/10 |

No aparecen los nombres “Sailor” o “Admiral” en WTS: los nombres verificables son Marine Private, Major y General. `h00R`/`h00S` duplican Private/Major. Hay una divergencia esencial: **Europa usa `h00T` como otro Marine General; New World usa `h00T` como estructura `hbar` especial**. Nunca se debe mapear ese rawcode sin conocer la variante.

La separación física usa `ucol` heredado: Private = 16/50 = 0.32 Unity; Major y General = 32/50 = 0.64 Unity. New World sí contiene variantes artísticas directas: `h014` usa `units\human\ArthaswithSword\ArthaswithSword.mdl` con `usca=0.8`, y `h015` usa `units\other\Proudmoore\Proudmoore.mdl` con `usca=0.9`. Europa no sobreescribe `umdl` ni `usca` en esos rawcodes y hereda el Knight (`units\human\Knight\Knight`, escala UI 1.4). Es procedencia de modelo, no una medida de bounds: no se asigna `StandingHeight` ni anchura visual a los marines.

La fragata de runtime representa `h00W` ← `hdes`: los dos mapas verifican 400 HP, base 30, alcance 20, velocidad 6.8, armadura 6 y coste/puntos 5. La fila `hdes` de `Units\UnitWeapons.slk` del MPQ propio citado arriba aporta tipo **normal**, cooldown **1.5 s**, **1 dado** y **15 caras**. Al combinarla con el override `ua1b=30` de ambos W3U, el ataque efectivo es **31–45 Normal cada 1.5 s**.

El transporte `n008` ← `nzep` verifica 300 HP, velocidad 6.8, defensa large y coste/puntos 2. `nzep` heredado tiene armadura 0 y ninguna arma habilitada. `n007` es su transporte blindado: 300 HP, velocidad 7.4, armadura 30, coste/puntos 6. La capacidad de **seis** plazas del runtime es una adaptación local explícita: `Aloa` en el `AbilityData.slk` propio sólo aporta alcance 80 y no contiene `DataA`–`DataE` de capacidad; el `A00D` del mapa hereda `Aloa` sin modificaciones. Por eso no se atribuyen capacidad, tiempo de construcción ni token de UI a la fuente.

La orden de carga personalizada `A00V` busca unidades a 512 unidades nativas (10.24 Unity) del barco y ordena hasta 10 candidatas; esa cifra es un límite del bucle, no la capacidad. `A00X` ordena `unloadall` en la posición actual. Ninguna de las dos exige una zona de muelle. Europa: `war3map.j` 20076–20214; mismo flujo en New World.

## Límites conocidos

- No se afirma que los valores de arte, tamaño visual o selección de Unity reproduzcan modelos WC3.
- No se infiere capacidad ni otros campos heredados de TFT cuando no están presentes en W3U ni en las tablas locales disponibles; el caso resuelto de `hdes` se documenta arriba con su procedencia concreta.
- La salud, armadura y defensa de la carcasa de torre runtime siguen siendo decisiones separadas; su daño, dados, cadencia y alcance sí usan `h00N`/`h00O` (45+1d5, 0.9 s, 13 Unity).
- No se ha ejecutado Unity para esta auditoría.
