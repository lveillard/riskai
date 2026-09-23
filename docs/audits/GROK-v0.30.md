Voy a revisar solo el diff de `feat/gameplay-v030` contra `main`, en solo lectura, y contrastar los hallazgos con el c├│digo y los valores de combate de origen.El diff es grande (~12.5k l├¡neas). Empiezo por habilidades, IA, pool de soldados, hotkeys y est├íticos, que es donde suelen estar los fallos reales.## Veredicto

No hay un P0 de crash o de partida corrupta en el camino de simulaci├│n. El roster nuevo, el man├í, el pool de cad├íveres y el recorte del ├írea jugable aguantan la lectura. El fallo que s├¡ cambia una partida es de entrada: tras pulsar una celda del comando, Enter y Espacio vuelven a comprar y adem├ís ejecutan el atajo global. Hay varios P2 de curaci├│n, presentaci├│n, armadura naval, audio WebGL y del compilador r├ípido.

## Hallazgos

| Sev. | D├│nde | Escenario | Arreglo |
|---|---|---|---|
| P1 | `RtsUiOrnaments.cs:125`, `BattleHud.CommandCard.cs:107`, `RtsController.cs:576`, `BattleHud.Feedback.cs:213` | `RtsOrnamentButton` hereda `Button` y sigue siendo enfocable. Un clic deja el foco en la celda. UI Toolkit dispara el `clicked` con Enter y Espacio, y el Input System ve la misma pulsaci├│n: Espacio compra otra unidad y centra la c├ímara; Enter compra y abre el chat. En una ciudad seleccionada, recentrar gasta el oro. | `focusable = false` en las celdas de producci├│n (y en el resto de botones del HUD con atajo global), o ignorar Enter/Espacio mientras `focusController.focusedElement` sea un `Button`. |
| P2 | `MedicSupport.cs:45-47` | El cooldown de 1 s se adelanta antes de buscar aliado. Un barrido en vac├¡o lo consume. Si alguien cae herido justo despu├⌐s, la cura espera casi otro segundo. En WC3 el cooldown empieza al lanzar. | Avanzar `nextCastTime` solo despu├⌐s de un `Heal` con vida real. |
| P2 | `MedicSupport.cs:58` frente a `RoarSupport.cs:62` | Cada cura llama a `VisualFactory.Impact` aunque `PresentationEnabled` sea falso. El rugido s├¡ est├í tapado. Con presentaci├│n apagada (tests, sim headless) cada cura crea pulsos hasta el tope de 192. | El mismo `if (session.Combat.PresentationEnabled)` que el rugido. |
| P2 | `Ship.cs:68`, `UnitCatalog.cs:42-44` | `ArmorType` es `Heavy` para todos los cascos. `h00U` y `h001` heredan `hdes`, que en la fuente es `small` (Light). Perforante contra Heavy es ├ù1 y contra Light ├ù2: ballestas y fusiles hacen la mitad de da├▒o a buque y acorazado. `n007` s├¡ es `large` en el mapa, y ese mapeo a Heavy cuadra. El hueco est├í escrito en `docs/RISK-RULES-v0.30.md` (l├¡nea 55) y esta rama no lo cierra. | Tipo de armadura por perfil, no una constante del `Ship`. |
| P2 | `Music.cs:85-86` | Si el navegador bloquea el autoplay y `isPlaying` queda en falso, al llegar el fundido a 1 se llama `PlayNext` cada 2 s. La lista rota en silencio y, al desbloquear el `AudioContext`, pueden arrancar varios `Play` a la vez. Los 7 OGG s├¡ est├ín en streaming (`loadType: 2`, sin preload). | No avanzar de pista si `Play()` no lleg├│ a sonar; reintentar la pista actual tras el primer gesto. |
| P2 | `scripts/quick_compile.py:136-141` | Pedir `Editor` recompila tambi├⌐n `Tests` y `PlayTests`, porque cualquier ensamblado anterior a uno pedido se trata como dependencia. El `for dep in []` de la l├¡nea 137 no hace nada. Un error de tests tumba un compile que solo quer├¡a el editor. Si falta `<DefineConstants>`, el `.group(1)` de la l├¡nea 103 lanza y no sale con c├│digo 2. | Compilar solo el pedido y sus `references` reales; si falta la constante, salir 2 con mensaje. |
| P3 | `SoldierPool.cs:28` | La clave es `team * 32 + (int)kind`. Hoy hay 14 tipos (`Tank` = 13) y no colisiona. El tipo 32 del equipo 0 pisa al tipo 0 del equipo 1 y reutiliza el componente equivocado (m├⌐dico con l├│gica de espadach├¡n). | Clave `team * UnitKindCount + kind`, o un par, y un test que falle si el enum llega a 32. |
| P3 | `SkirmishCommander.cs:173` y `:892` | `List.Sort` con lambda reserva un delegado en cada pasada de defensa (0,75 s) y en cada pasada estrat├⌐gica, por cada IA. Con 16 IAs es basura de generacional, no un O(n┬▓) por tick. El trabajo cuadr├ítico de `BuildCluster` est├í muestreado (paso `pool/24`) y `CalculatePath` cabe en `PathBudget` (24ΓÇô40). | `Comparison<T>` est├ítico. |

## Revisado y correcto

- **Man├í y rugido.** M├⌐dico 200/75/1,5 y cura 25 HP, 5 de man├í, 1 s, alcance 5. Rugidor 300/75/2; general 300/0/3; rugido 100 de man├í, 45 s, +25 % sobre el da├▒o ya tirado (`Soldier.cs:338`), sin apilar (`ApplyRoar` se queda con el `until` mayor). No cura mec├ínicos (artiller├¡a y tanque). El general puede sostener el aura (3/s cubre 100 en 33 s, menos que 45): es el autocast documentado, no un multiplicador extra. Los overrides expl├¡citos de `reforged-source-combat.json` (vida, oro, da├▒o, alcance, cadencia) coinciden con el cat├ílogo. La vida 220 del m├⌐dico es la decisi├│n local frente al `uhpm=250`. El ataque perforante de m├⌐dico y rugidor sigue el RoC (`pierce`/`small`); el JSON marca el candidato TFT (`magic`/`none`) como hist├│rico, no como override del mapa.
- **Ordinales.** `EliteRifleman`ΓÇª`Tank` van del 9 al 13, a├▒adidos al final. `NavalUnitKind` y `ShipKind` tienen el mismo orden y el cast de `Produce` / `BuyShip` es v├ílido. La rejilla de 11 y 8 productos no pagina; V queda vac├¡o en la ciudad.
- **Atajos.** Con edificio propio, QΓÇôV compran y no disparan parar/ataque (`RtsController.cs:555-572`). El chat anula el teclado y `IsTyping` aguanta el frame de cierre, as├¡ que Enter no reabre el chat. Los nombres del jslib coinciden con el `DllImport`; el foco va dentro de `touchend`/`pointerup` y el input a 16 px evita el zoom de iOS. `_malloc` del string es el patr├│n que Unity libera.
- **Cad├íveres.** `TakeOldCorpse` solo recicla a partir de 1,2 s y saca la entrada de `retiring`, as├¡ que `Tick` no la devuelve dos veces. `ResetForReuse` limpia `dead`. `GameFeel.UpdateCorpses` (`GameFeel.cs:443`) abandona el hundimiento si el `EntityId` cambi├│ o la unidad est├í viva. Los est├íticos de sesi├│n (`BattleSession`, `NavalWorld`, `Sfx`, `Music`, `GameFeel`, `ChatInput`) se sueltan en `OnDestroy` o en `SubsystemRegistration`. `GameFeel`, `Sfx` y `BattleHud` se desuscriben.
- **Mapa e IA.** La malla andable exige `InPlayable`; la falda visual no tiene collider. Los anclajes de 12 m son el disco documentado, no un pasillo de borde. La IA gasta el oro de su equipo, sin visi├│n extra. Los puertos importados solo entrenan marines, y el censo cuenta esa cola en la ciudad, no dos veces. No hay divisi├│n por cero en centroide, oleada a distancia, vida de barco ni regeneraci├│n de man├í (rechaza NaN e infinito).
EXIT 0

---

## Resolución (lead)

| Hallazgo | Estado |
|---|---|
| P1 foco en botones: Enter/Espacio recompraban y disparaban el atajo global | Corregido: todos los botones del HUD son `focusable=false` (`RtsUiOrnaments.cs`, `BattleHud.Resources.cs`). |
| P2 cooldown del Sanador gastado en barridos vacíos | Corregido: el cooldown empieza al curar de verdad; barrido limitado a ~4/s (`MedicSupport.cs`). Test ajustado a una ventana de un cooldown. |
| P2 impacto de cura sin `PresentationEnabled` | Corregido. |
| P2 armadura de barcos siempre Heavy | Corregido: `ShipProfile.Defense` — hdes (Fragata, Buque, Acorazado) Light; nzep (transportes) Heavy, como en la fuente. |
| P2 música con autoplay bloqueado rotaba la lista en silencio | Corregido: reintenta la misma pista cada 2 s hasta que suena (`Music.cs`). |
| P2 `quick_compile.py` compilaba ensamblados no pedidos | Corregido: solo el pedido y sus dependencias internas; error claro si faltan constantes. |
| P3 clave del pool con `team*32` | Corregido: `team*UnitKindCount+kind`. |
| P3 `List.Sort` con lambda por pasada | Corregido: `Comparison<T>` estáticos. |

Verificación: EditMode 233/233; PlayMode filtrado (Medic, Naval, Harbor, HUD, Tooltip, IA, Commander, TowerCombat, SixteenPlayer, UIWheel, BuildingSelection) 122/124 con los dos fallos resueltos y re-ejecutados (18/18). `HoldingMortarKeepsHullRangeTargetBeyondShipPivotLeash` falló una vez bajo carga y pasa aislado: margen de 3 s frente a un cooldown de 3,5 s, marcado como sensible a tiempos.
