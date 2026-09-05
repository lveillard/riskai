# RTS interaction reference · RiskAI v0.7

Design reference: Blizzard's Warcraft III manual, https://classic.battle.net/war3/basics/unitcommands.shtml and https://classic.battle.net/war3/basics/specialcommands.shtml (consulted 2026-09-05).

The prototype implements distinct orders:

- **Move (M or ground right-click)**: explicit travel, usable for retreating through danger. On arrival, return to autonomous combat readiness.
- **Attack (A + enemy, or enemy right-click)**: pursue the explicit target.
- **Attack-move (A + ground)**: acquire enemies along the route, fight, then resume the original destination. Short pursuit limit prevents unrelated fights dragging the group across the map.
- **Stop (S)**: cancel orders, become ready, acquire and approach nearby enemies.
- **Hold (H)**: cancel orders and fire only within range, without pursuing.
- **Patrol (P + ground)**: travel between two positions, engaging and returning to the route.
- **Follow (friendly right-click)**: follow an ally, joining its nearby fight.

Neutral guardians defend a small area. Automatic acquisition favors nearby opponents and spreads melee pressure across available targets. Damage occurs after an attack windup; ranged damage arrives with the projectile. This is a small Unity NavMesh prototype, not a reproduction of every Warcraft mechanic.

The camera is perspective (44° FOV, 49° pitch, 30° yaw). Virtual zoom starts at 34 and clamps to 17–44. One normalized wheel unit changes the target by exp(±0.24); damping takes about 0.1 s. The ground under the cursor stays anchored, including elevated terrain, except when constrained by the map boundary. Focus is centered in the playable area above the HUD. Middle mouse grabs the ground; leaving the window or losing focus cancels that drag. Backspace restores the zoom and initial capital, including random starts. Arrow keys pan at equal cardinal/diagonal speed; Shift accelerates. Screen edges pan after 0.14 s dwell. F1 exposes pan speed and an edge-pan toggle and blocks game orders while open.

Regression tests cover economy, capture, all city routes and elevation ramps, recruitment/pause, stop versus hold/move, attack-move resumption, and camera anchoring/drag distance.

The v0.3 layer also supports clearer target picking, queue cancellation with a full cost refund, tower construction (70 gold, 320 health, range 11), level-II upgrades (90 gold), and complete-country reinforcements capped at five living waves (casualties reopen capacity). Level II unlocks Guard (D, 55 gold) and Mage (F, 65 gold); Mortar (R, 75 gold, range 14) provides siege damage against fortifications. Footman and Archer remain Q/W. Towers fire 24 piercing damage every 1.1 s and require terrain line of sight, including against neutral guardians.

## Lectura de la interfaz

La franja inferior ocupa 208 píxeles a 1600 × 900 y se escala proporcionalmente. Minimapa a la izquierda, retrato/estado en el centro, cuadrícula de órdenes a la derecha. Los iconos de órdenes son dibujos propios generados por `RtsGlyphs`, sin recursos de Warcraft incorporados al juego.

El panel de dominio regional muestra las ciudades controladas y el bonus pendiente o activo; permite localizar una ciudad de la región. F2 selecciona una ciudad propia, prioriza la capital y centra la cámara. Las formaciones asignan los puestos delanteros a unidades cuerpo a cuerpo y los posteriores a tiradores.

Referencia visual consultada: https://classic.battle.net/war3/basics/daynight.shtml y su captura oficial `screen-day.jpg`, guardada únicamente en `references/warcraft-ui-day.jpg`.
