# Riesgus · audio clips and ElevenLabs prompts

The game plays nothing until these files exist; every missing clip is skipped silently.

- Folder: `RiskAI/Assets/RiskAI/Resources/Audio/` (Unity `Resources/Audio/<file name without extension>`).
- Format: `.mp3`, `.ogg` or `.wav` (Unity imports all three; keep the file names below, the extension is free).
- Variants: a clip with several files is named `<id>_1`, `<id>_2`… (up to `_4`); one is picked at random per play. A single clip is just `<id>`.
- The same data is machine-readable in `RiskAI/Assets/RiskAI/Resources/Audio/clips.json`.
- Generation (ElevenLabs Sound Effects): use the listed duration, prompt influence around 0.5, and append the shared style line.
- 3D clips are positioned in the world (panned and attenuated around the camera focus); 2D clips are interface feedback.
- Mix: normalise to about -14 LUFS short-term, trim leading silence (the game adds its own pitch/volume jitter).

Shared style line appended to every prompt: *Medieval fantasy RTS game sound effect, dry and close, no reverb tail, no music, no speech or words, clean start, short.*

| Id | Files | Length | Space | When it plays | Prompt(s) |
|---|---|---|---|---|---|
| `hit_sword` | `hit_sword_1.mp3`, `hit_sword_2.mp3`, `hit_sword_3.mp3` | 0.5 s | 3D | Melee hit by swordsmen and foot soldiers | `hit_sword_1.mp3`: Steel sword striking chainmail and a wooden shield, single sharp metallic clang with a dull thud<br>`hit_sword_2.mp3`: Short sword slash hitting leather armor, quick swoosh ending in a meaty impact<br>`hit_sword_3.mp3`: Iron blade clashing against a steel helmet, bright ring with a short scrape |
| `hit_lance` | `hit_lance_1.mp3`, `hit_lance_2.mp3` | 0.6 s | 3D | Melee hit by mounted knights and marine cavalry | `hit_lance_1.mp3`: Heavy cavalry lance punching into a wooden shield, deep crack and splinters<br>`hit_lance_2.mp3`: Mounted knight lance hitting plate armor, heavy metallic thump with a horse snort |
| `shot_crossbow` | `shot_crossbow_1.mp3`, `shot_crossbow_2.mp3`, `shot_crossbow_3.mp3` | 0.5 s | 3D | Crossbowman, healer and tower bolt release | `shot_crossbow_1.mp3`: Crossbow firing, tight string twang with a wooden thunk of the stock<br>`shot_crossbow_2.mp3`: Crossbow bolt release, sharp snap of the string and a short whoosh<br>`shot_crossbow_3.mp3`: Heavy arbalest shot, deep twang and mechanical click |
| `shot_rifle` | `shot_rifle_1.mp3`, `shot_rifle_2.mp3` | 0.6 s | 3D | Harbor pistolier (flintlock) shot | `shot_rifle_1.mp3`: Old flintlock pistol shot, flint click then a dry black-powder pop<br>`shot_rifle_2.mp3`: Short musket shot, crisp powder crack with a tiny puff, outdoors |
| `hit_arrow` | `hit_arrow_1.mp3`, `hit_arrow_2.mp3` | 0.5 s | 3D | Bolt/shot landing on a unit | `hit_arrow_1.mp3`: Crossbow bolt thudding into a wooden shield, short dull thunk<br>`hit_arrow_2.mp3`: Arrow hitting leather armor, quick soft impact with a light rattle |
| `magic_bolt` | `magic_bolt_1.mp3`, `magic_bolt_2.mp3` | 0.7 s | 3D | Mage casting a magic missile | `magic_bolt_1.mp3`: Wizard casting an arcane bolt, shimmering magical whoosh rising in pitch<br>`magic_bolt_2.mp3`: Blue magic missile launched, crackling energy zap with a sparkling tail |
| `magic_impact` | `magic_impact_1.mp3`, `magic_impact_2.mp3` | 0.6 s | 3D | Magic missile exploding on target | `magic_impact_1.mp3`: Arcane energy burst on impact, glassy crystalline pop with a soft electric crackle<br>`magic_impact_2.mp3`: Magic orb exploding, bright shimmering thump with sparkles |
| `mortar_fire` | `mortar_fire_1.mp3`, `mortar_fire_2.mp3` | 1.0 s | 3D | Mortar launching a shell | `mortar_fire_1.mp3`: Medieval mortar firing, deep hollow boom with a whistle of the shell leaving<br>`mortar_fire_2.mp3`: Siege mortar launch, heavy thump of black powder and metallic ring of the barrel |
| `explosion` | `explosion_1.mp3`, `explosion_2.mp3`, `explosion_3.mp3` | 1.5 s | 3D | Mortar shell landing (siege impact) | `explosion_1.mp3`: Cannonball explosion on dirt, heavy boom with debris and earth raining down<br>`explosion_2.mp3`: Black powder shell exploding, sharp blast and crumbling stones<br>`explosion_3.mp3`: Siege shell impact, deep explosion with wooden debris scattering |
| `ship_cannon` | `ship_cannon_1.mp3`, `ship_cannon_2.mp3` | 1.2 s | 3D | Frigate cannon fire | `ship_cannon_1.mp3`: Wooden warship cannon firing over water, big booming shot with a creak of the hull<br>`ship_cannon_2.mp3`: Naval cannon broadside single shot, heavy boom and splash in the distance |
| `death` | `death_1.mp3`, `death_2.mp3`, `death_3.mp3` | 0.8 s | 3D | A soldier dies | `death_1.mp3`: Medieval soldier falling down in armor, short pained grunt and metal clatter on the ground<br>`death_2.mp3`: Warrior collapsing, exhaled groan and armor plates hitting dirt<br>`death_3.mp3`: Soldier death, brief cry and a body falling with chainmail rattle |
| `order_move` | `order_move_1.mp3`, `order_move_2.mp3` | 0.5 s | 2D | Move order acknowledged | `order_move_1.mp3`: Soft leather boots marching two steps and a short metal jingle, confirmation click<br>`order_move_2.mp3`: Quick banner flap and a light armor jingle, short confirmation |
| `order_attack` | `order_attack_1.mp3`, `order_attack_2.mp3` | 0.5 s | 2D | Attack order acknowledged | `order_attack_1.mp3`: Sword drawn from a scabbard, short metallic shing<br>`order_attack_2.mp3`: War drum single hit with a quick blade ring, aggressive confirmation |
| `ui_click` | `ui_click.mp3` | 0.5 s | 2D | Any HUD button press | `ui_click.mp3`: Tiny wooden button click on a parchment interface, very short and soft |
| `purchase` | `purchase.mp3` | 0.6 s | 2D | Recruit/ship purchase accepted | `purchase.mp3`: A few gold coins dropped into a leather pouch, short satisfying jingle |
| `no_gold` | `no_gold.mp3` | 0.5 s | 2D | Purchase rejected for lack of gold | `no_gold.mp3`: Empty coin purse shaken, dull thud of a closed wooden chest, short negative feedback |
| `unit_trained` | `unit_trained.mp3` | 0.8 s | 2D | One of your units finished training | `unit_trained.mp3`: Soldier stepping out ready, armor clank and a short brass horn blip |
| `city_captured` | `city_captured.mp3` | 1.5 s | 2D | You captured a city or harbor | `city_captured.mp3`: Castle banner raised, cloth flap and a short triumphant war horn call |
| `city_lost` | `city_lost.mp3` | 1.5 s | 2D | You lost a city or harbor | `city_lost.mp3`: Low mournful war horn with a heavy wooden gate slamming shut |
| `country_completed` | `country_completed.mp3` | 2.5 s | 2D | You completed a whole country | `country_completed.mp3`: Two war horns calling together, distant crowd cheer and a bell ring, triumphant |
| `income` | `income.mp3` | 0.8 s | 2D | Round income received | `income.mp3`: Handful of gold coins pouring onto a wooden table, bright short cascade |
| `under_attack` | `under_attack.mp3` | 1.2 s | 2D | Your units or cities are under attack | `under_attack.mp3`: Urgent alarm bell ringing three quick strikes in a castle tower |
| `victory` | `victory.mp3` | 3.0 s | 2D | Match won | `victory.mp3`: Victorious army cheering, war horns blowing and banners flapping, triumphant ending |
| `defeat` | `defeat.mp3` | 3.0 s | 2D | Match lost / you were eliminated | `defeat.mp3`: Defeated army retreating, low sorrowful horn, distant wind and a dropped sword |
| `chat` | `chat.mp3` | 0.5 s | 2D | Chat message posted | `chat.mp3`: Quill scratching parchment briefly, short soft notification |

## Runtime behaviour

- 16 pooled voices for world sounds plus 2 interface voices; the quietest/oldest voice is stolen when all are busy.
- Each clip id has a small budget (at most 1-3 plays per 60 ms) so a 900-unit battle does not stack hundreds of hits.
- Pitch varies ±6 % and volume ±10 % per play.
- World sounds are heard around the camera's focus point; the audible radius grows with zoom and far-away events are culled before they take a voice.
- Master and effects volume, and mute, are in the in-game menu (Menú → Partida) and persist between sessions. Audio mutes while the tab/app is unfocused.
