# Revisión del Mortero v0.30

Fuente: `h00H` (overrides explícitos de `war3map.w3u` en `data/derived/reforged-source-combat.json`) sobre la base
`hmtm` resuelta con el baseline RoC local (`references/owned-disc-data/resolver/RoC-UnitBalance.slk`,
`RoC-UnitWeapons.slk`). 50 unidades WC3 = 1 unidad Unity.

| Campo | Fuente | Antes (v0.29.1) | Después (v0.30) | Nota |
| --- | --- | --- | --- | --- |
| Vida `uhpm` | 350 explícito | 350 | 350 | OK |
| Daño `ua1b` + dados | 18 + 1d13 (dados heredados) | 19–31 | 19–31 | OK |
| Tipo de ataque `ua1t` | siege (heredado) | Siege | Siege | OK |
| Armadura `udef` / `udty` | 0 / `medium` RoC | 0 / Medium | 0 / Medium | Capas Reforged actuales: `large` en root/custom_v1, `medium` en custom_v0/melee_v0; se mantiene el baseline RoC común a todo el roster |
| Velocidad `umvs` | 230 explícito → 4.6 | 4.6 | 4.6 | OK |
| Alcance `ua1r` | 900 explícito → 18 | 18 | 18 | OK |
| Alcance mínimo `uamn` | 250 heredado → 5 | 5 | 5 | OK |
| Adquisición `uacq` | 900 explícito → 18 | 18 | 18 | OK |
| Cadencia `ua1c` | 3.5 heredado | 3.5 | 3.5 | OK |
| Punto de ataque / backswing | 1 / 1.1 heredados | 1 / 1.1 | 1 / 1.1 | OK |
| Arma `ua1w` | artillery (heredado) | Artillery, LaunchPoint | Artillery, LaunchPoint | OK |
| Velocidad de proyectil `ua1z` | 900 → 18/s | 18 | 18 | OK |
| Áreas `ua1f/ua1h/ua1q` | 25/150/250 → 0.5/3/5 | 0.5/3/5 | 0.5/3/5 | OK |
| Factores `uhd1/uqd1` | 0.35 explícito / 0.1 heredado | .35/.1 | .35/.1 | RoC tiene 0.4 para `uhd1`; manda el override |
| Máscara de splash `ua1p` | tree, ground, structure explícito | Tree/Ground/Structure | Igual | Sin flags de bando: el splash también daña aliados (como en WC3) |
| Arco del proyectil (`uma1` 0.35) | presentación | Arco alto sólo si `AttackKind.Siege` | Arco alto para toda entrega `Artillery` | Afecta a la nueva Artillería `h00M` (pierce) |

## Splash realmente aplicado

`Soldier` dispara con `CombatWorld.FireWeapon(..., SourceWeapons.For(Mortar))`, es decir la ruta `WeaponProfile`
con las áreas y factores de la tabla. El radio heredado de 1.5 para Siege (y su factor 0.35 plano) sólo existe en
`CombatWorld.FireProjectile`, la ruta de compatibilidad para capturas de presentación (`VisualFactory.Arrow`,
`RuntimeVisualCapture`); ninguna unidad jugable la usa. No se ha cambiado.

## Cambios

1. `CombatWorld.TryGetProjectile` y la vista del proyectil usan el arco/obús de asedio para cualquier entrega
   `Artillery`, no sólo para daño Siege (el Mortero no cambia; la Artillería `h00M` de daño pierce ya no vuela
   como un virote plano).
2. Tests EditMode `SourceRosterV030Tests.MortarMatchesH00HAndHmtmSource` fijan todos los valores de la tabla.

Conclusión: el perfil del Mortero ya coincidía con `h00H`/`hmtm`; no hacía falta ningún cambio numérico.
