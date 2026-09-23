# Reglas Risk Reforged v0.30 · roster fuente completo

Amplía [RISK-RULES-v0.16.md](RISK-RULES-v0.16.md) con las ocho altas propuestas en
[SOURCE-ROSTER-NEXT.md](audits/SOURCE-ROSTER-NEXT.md). No se copia arte, modelos ni tablas de Blizzard.

## Procedencia

- Overrides explícitos: `data/derived/reforged-source-combat.json` (`objects[].all_map_overrides`, extraídos de `war3map.w3u`). Siempre prevalecen.
- Campos heredados: el mismo baseline RoC local que ya usan Ballestero/Caballero/Sanador/Mortero
  (`references/owned-disc-data/resolver/RoC-UnitBalance.slk`, `RoC-UnitWeapons.slk`, `RoC-UnitData.slk`, `RoC-AbilityData.slk`).
  `hdes` (no existe en RoC) usa la fila TFT histórica, como la Fragata `h00W`.
- Disponibilidad: `roster.shops` — `h00N` (ciudad) recluta `h00F h00I h00J h00M h01A`; `h00O` (puerto) `h00U h001 n007`.
- Conversión: 50 unidades WC3 = 1 unidad Unity. Todos los objetos nuevos fijan `ubld=1` (1 s) y `upoi = ugol`.

## Tabla de unidades v0.30

`*` = heredado (RoC / TFT para `hdes`); el resto es override explícito del mapa.

| Runtime (`UnitKind`) | Rawcode ← base | Oro | Vida | Daño | Alcance | Cadencia | Velocidad | Armadura / tipo | Ataque | Tecla |
| --- | --- | ---: | ---: | --- | ---: | ---: | ---: | --- | --- | :-: |
| Fusilero de élite (`EliteRifleman`) | `h00F` ← `hrif` | 6 | 450 | 36 + 2d4* = 38–44 | 7 | 1.0 | 5.4* | 1 / small (Light)* | pierce*, instantáneo* | T |
| Rugidor (`Roarer`) | `h00I` ← `hmpr` | 4 | 400 | 29 + 1d3 = 30–32 | 10 | 2.0* | 5.4* | 1 / small (Light)* | pierce*, misil 18 | X |
| General (`ArmyGeneral`) | `h00J` ← `hkni` | 10 | 800* | 55 + 2d5* = 57–65 | 2* | 1.45 | 7* | 10 / large (Heavy)* | normal* | G |
| Artillería (`Artillery`) | `h00M` ← `hmtt` | 15 | 900 | 55 + 1d13 = 56–68 | 20 | 3.0 | 4 | 3 / none (Unarmored) | pierce, artillería 18 | Z |
| Tanque (`Tank`) | `h01A` ← `hfoo` | 25 | 1500 | 80 + 1d11 = 81–91 | 10 | 1.8 | 5.2 | 9 / fort (Fortified) | siege, msplash 20 | Y |
| Buque de guerra (`Warship`) | `h00U` ← `hdes` | 20 | 1250 | 90 + 1d15* = 91–105 | 30 | 1.5* | 9 | 10 | normal*, msplash 20 | R (puerto) |
| Acorazado (`Battleship`) | `h001` ← `hdes` | 45 | 2350 | 130 + 1d15* = 131–145 | 30 | 1.4 | 6.6 | 20 | normal*, msplash 20 | F (puerto) |
| Transporte blindado (`ArmoredTransport`) | `n007` ← `nzep` | 6 | 300 | — | — | — | 7.4 | 30 | sin arma | X (puerto) |

Otros datos: adquisición Fusilero 12*, Rugidor 8, General 10*, Artillería 20, Tanque 10*; punto de ataque /
backswing Fusilero .17/.7*, Rugidor .59/.58*, General .66/.44*, Artillería .5/.5, Tanque .2/.5*.
Colisión (`ucol`, radio NavMesh): Fusilero 16, Rugidor 16*, General 36, Artillería 48*, Tanque 40.
Splash de Artillería: 25/100/170 nativos → 0.5/2/3.4, factores .35/.1, máscara árbol/suelo/estructura.
Buques `h00U/h001`: splash msplash heredado de `hdes` idéntico a `h00W` (0.5/0.7/1, .3/.1).
`n007` comparte el `uabi` de `n008` (`Sch3 Car1=10`): capacidad 10.

Teclas existentes sin cambios: ciudad Q W D F R C; puerto Q (Fragata) W (Transporte) V B C (Marines).
En puerto, R/F/X compran barcos en lugar de Mortero/Mago/Rugidor (mismo patrón contextual que C).
La tecla T ya no construye torres (ver abajo) y pasa al Fusilero de élite.

## Adaptaciones locales

- **Sanador 220 HP** en lugar del `uhpm=250` explícito de `h00E`: decisión de producto v0.30.
- **Maná del Sanador**: `hmpr` RoC `manaN=200`, `mana0=75`; `h00E` fija `umpr=1.5`. `Ahea` RoC: 25 HP, 5 maná,
  1 s, alcance 250 → 5. Sólo cura con maná suficiente; con 75 iniciales paga 15 curas y luego ~1 cada 3,3 s.
  Respeta el flag `organic` de `Ahea`: no cura Artillería ni Tanque (`utyp=Mechanical`). El maná se muestra
  en la ficha de la unidad seleccionada.
- **Rugido (`Aroa`)** para Rugidor y General (ambos lo llevan en `uabi`): +25 % de daño (se aplica al daño
  tirado, no sólo a la base) durante 45 s a aliados en 700 → 14, coste 100 maná. En WC3 es manual; aquí se
  autolanza cuando la unidad o un aliado cercano combate y algún aliado del área no está bajo el efecto
  (evaluación cada 0,5 s). Maná: Rugidor 300 máx., 75 inicial, 2/s; General 300 máx., 0 inicial, 3/s.
  `Adis` (Rugidor) y `Afzy` (General, Caballero) siguen sin representar.
- **Tanque**: `h01A` fija `ua1w=msplash` pero no hereda áreas de `hfoo`, así que el arma resuelve como misil
  de objetivo único (sin splash). Se representa tal cual, sin inventar radios.
- **Tipo de armadura de barcos**: sigue la fuente. Los buques de guerra `hdes` (Fragata h00W, Buque h00U, Acorazado h001)
  son `small` (Light) y los transportes `nzep` (n008, n007) son `large` (Heavy). Hasta v0.30 todos eran `Heavy`, lo que
  reducía a la mitad el daño perforante contra los buques de guerra.
- **Arte**: modelos originales derivados de KayKit y piezas procedurales (fusilero con penacho y rifle, rugidor
  con yelmo de cuernos y cuerno de guerra, general montado mayor con capa y estandarte, cañón de campaña, tanque
  de vapor, cascos navales escalados ×1.15/×1.3/×1.05). Las alturas son objetivos visuales locales relativos al
  modelo base verificado (no hay MDX propio verificado para estas unidades). Los retratos nuevos se generan con
  `RiskArtSetup.Prepare`; mientras no existan, la UI usa el retrato de la unidad base.

## Torre: sin botón de construcción

Las torres de ciudad/puerto no se pueden atacar (`DefenseTower.CanBeAttacked=false`), así que
«Construir/Reconstruir torre» (tecla T, 60 de oro heredados) era inalcanzable. v0.30 elimina la tecla,
`RtsController.BuildTower`, `PlayerBuildingIntentKind.BuildTower`, `Settlement/Harbor.BuildTower`,
`BattleRules.TowerCost` y la obra de torre del puerto. `DefenseTower.BeginBuild/CompleteBuild` se conservan
porque los usan los fixtures de combate.
