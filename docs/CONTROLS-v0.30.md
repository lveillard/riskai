# Riesgus v0.30 controls

This replaces the README controls table for v0.30. The in-game **Menu → Controls** tab shows the same table (`BattleHud.CommandCard.cs`, `ControlsTable`).

## Command card and grid hotkeys (WC3 style)

When you select one of your cities or harbors, the footer shows a 4×3 command card. Each product sits in a fixed cell, and the cell's position is its hotkey:

```
Q W E R
A S D F
Z X C V
```

Products are placed by `RiskAI.Core.ProductionHotkeys`. Land units come first, then hulls, each sorted from cheapest to most expensive; for equal costs, `ProductionCatalog` order decides. A new catalog entry takes the next free cell automatically. If a card ever has more than 12 products, it pages: the **V** cell switches pages, and 11 products fit on each page.

City card (11 units):

| Key | Unit | Gold |
|---|---|---|
| Q | Swordsman (Espadachín) | 1 |
| W | Crossbowman (Ballestero) | 1 |
| E | Healer (Sanador) | 2 |
| R | Mortar (Mortero) | 3 |
| A | Mage (Mago) | 4 |
| S | Roarer (Rugidor) | 4 |
| D | Knight (Caballero) | 5 |
| F | Elite rifleman (Fusilero de élite) | 6 |
| Z | General | 10 |
| X | Artillery (Artillería) | 15 |
| C | Tank (Tanque) | 25 |
| V | — | |

Harbor card (3 marines + 5 ships):

| Key | Product | Gold |
|---|---|---|
| Q | Marine Private | 1 |
| W | Marine Major | 5 |
| E | Marine General | 10 |
| R | Transport (Transporte) | 2 |
| A | Frigate (Fragata) | 5 |
| S | Armoured transport (Transporte blindado) | 6 |
| D | Warship (Buque de guerra) | 20 |
| F | Battleship (Acorazado) | 45 |

Each cell shows the portrait, the cost (bottom right, red when you cannot afford it) and how many are queued (top right). On desktop only, the hotkey letter appears in the top-left corner. Hover a cell on desktop, or long-press it on touch, to see the full name, stats and role.

If you select a city and a harbor together, both cards are shown. The hotkeys drive the primary building's card: the harbor's if a harbor is primary, otherwise the city's.

## Key priority

Grid hotkeys only work while one of your own buildings is selected. **While a building is selected, the grid wins**: Q W E R / A S D F / Z X C V produce, and E, A, S, D do not issue their unit or army commands. Press Esc or select units to get the unit keys back.

| Key | Action | When |
|---|---|---|
| Q W E R · A S D F · Z X C V | Produce the unit in that grid cell | Own city/harbor selected |
| V | Switch command-card page (only if more than 12 products) | Own city/harbor selected |
| A · M · P · S · H | Attack · move · patrol · stop · hold position | Units selected (no building) |
| B · D | Board nearby troops · unload the fleet | Transport / fleet selected |
| E | Select the whole army | No building selected |
| N | Select the fleet | Always |
| 1–9 · Ctrl+1–9 | Recall / store a group (double-press centers the camera) | Always |
| Space | Center on the selection, fleet or latest alert | Always |
| F1 · F2 · F3 | Menu · go to your base · go to your harbor | Always |
| F7 · F8 · F9 | Sound effects on/off · music on/off · minimap on/off (also in the quick bar) | Always |
| F10 | Pause | Always |
| F8 | Music on/off | Always (not while typing in chat) |
| Tab (hold) | Ranking board (the quick bar button keeps it open) | Always |
| Enter · Shift+Enter | Open chat / send to the chosen recipient · send to everyone | Always |
| Tab / Shift+Tab (while typing) | Cycle the chat recipient (All, then each living player) | Chat open |
| Esc | Cancel the armed order or deselect | Always |
| Alt (hold) | Show health bars and names | Always |
| Arrows · Backspace | Pan the camera · reset the camera | Always |

The unit command row (Move, Attack, Patrol, Stop, Hold, Focus) uses the same square-cell style on desktop and shows its keys (M, A, P, S, H) in the corner.

## Quick bar, minimap and ranking

- **Desktop:** the minimap is always visible in the bottom-right console. The quick bar sits along its top edge. The selection panel and command grid use the rest of the bottom edge.
- **Phones and tablets:** the quick bar sits at the bottom-left, above the footer, where the old Chat button was.
- **Quick bar buttons, left to right:** sound effects (F7), music (F8), ranking (Tab), minimap (F9), chat (Enter). A muted feature's icon is struck through, and an open panel's icon is highlighted.
- **Ranking board:** shown above the minimap on desktop and below the top bar on compact layouts. Columns: colour, player, cities, units, income per round, completed countries. Rows are sorted by cities, and eliminated players are greyed out. The cities counter in the top bar also opens it.
- **Top bar:** the Ranking and Map buttons are gone because the quick bar has them. Pause and Menu stay.

## Chat recipients

- A chip at the left of the chat field ("To: All") opens a list of the living players with their colour chips. Tab and Shift+Tab cycle through the recipients while typing.
- Shift+Enter always sends to everyone.
- **Shortcuts:** `/w blue hi`, `/w 3 hi`, `/blue hi`, `/azul hola` (Spanish or English colour names, accents optional) and `/all hi` or `/todos hola`. A whisper to an unknown name is reported instead of being sent.
- **Log format:** `You → All: text` and `You → AI 2 · Teal: text`. Private lines between two other players are never shown.
- **WebGL touch:** the browser text field stays above the on-screen keyboard. The recipient chip appears in Unity near the top. Tapping it keeps the field open and refocuses it afterwards.

## Menu

- The window has a close X in the top-right corner, and Esc also closes it.
- **Sections:** Match, Sound, Camera, Controls, Language.
- **Sound:** a sound switch, and sliders for master, effects and music volume.
- **Camera:** a speed slider, and switches for edge panning, camera shake and the minimap.
- **Language:** Spanish and English buttons.
- Settings persist: Sfx and Music keep their own preferences. Camera speed, edge panning and the desktop minimap choice are saved in PlayerPrefs (`riskai.camera.speed`, `riskai.camera.edgepan`, `riskai.hud.minimap`).
- While the menu is open, it draws above the message log and toasts, and receives pointer events first.

## Touch (phones and tablets)

- The command card uses the same grid with larger cells (at least 44 px) and no hotkey letters.
- Long-press a cell to see its tooltip. A quick tap only buys; it never leaves a tooltip on screen.
- A drawer tab above the footer collapses the selection/production panel to one scrollable row, and taps it back open. In phone portrait, the expanded footer is capped at 30% of the screen height.
- The top bar is a single row of icon+number items (gold and income, next-income countdown, cities, recruited units out of 100, Menu). Long-press an item for details.

## Top bar

- Gold shows the balance and the income per round, for example `12 GOLD · +4`.
- The next-income countdown shows a dial beside the gold. On desktop it reads `R1 · Income in 58 s` (Spanish: `R1 · Ingreso en 58 s`), on compact layouts just `58 s`. Its tooltip gives the round number and how much gold the next income pays.

## Language

The first launch follows the browser language (`navigator.languages`) on Web, or the OS language elsewhere: `es-*` gives Spanish, anything else gives English. The ES/EN toggle is stored in PlayerPrefs (`riskai.language`) and takes priority over detection on later launches. Editor and batch-mode runs always start in English so tests stay deterministic.
