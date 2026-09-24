using RiskAI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>
    /// WC3-style command card: production is a 4×3 grid of square portrait cells whose
    /// positions are the grid hotkeys (ProductionHotkeys). Compact layouts reuse the same
    /// grid with touch-sized cells, no key letters, and a drawer handle that shrinks the
    /// footer to a single scrollable row.
    /// </summary>
    public sealed partial class BattleHud
    {
        bool footerCollapsed;
        /// <summary>Compact footers can collapse to one row; wide footers always stay open.</summary>
        bool FooterCollapsed => footerCollapsed && UiViewport.IsCompact;
        static float CollapsedFooterHeight => UiViewport.MinimumTouchTarget + 16;
        /// <summary>Key letters are for keyboard players: hidden on phones and tablets.</summary>
        static bool ShowGridHotkeys => !UiViewport.IsTouchLayout;
        static float CommandCellSize => !UiViewport.IsCompact ? 54 : UiViewport.IsPortrait ? 56 : 46;
        static readonly Color CellEmpty = new Color(.05f, .055f, .05f, .55f);
        static readonly Color BadgeBack = new Color(0, 0, 0, .72f);
        static readonly Color CostShort = new Color(1f, .38f, .32f, 1);

        void BuildCommandCard(VisualElement root)
        {
            var card = new VisualElement { name = "HUD command card" };
            RtsUiStyle.Row(card); card.style.alignItems = Align.FlexStart; card.style.flexShrink = 0;
            var grids = new VisualElement { name = "HUD production grids" }; grids.style.flexShrink = 0;
            var info = new VisualElement { name = "HUD command info" };
            info.style.flexGrow = 1; info.style.flexShrink = 1; info.style.minWidth = 0; info.style.marginLeft = 10;
            BuildSelection(info);
            BuildProduction(grids, info);
            card.Add(grids); card.Add(info); root.Add(card);
            // Desktop uses the space right of the grid; compact stacks the details under it.
            BuildBuildingDetails(UiViewport.IsCompact ? root : info);
        }

        /// <summary>Selected building facts: country, garrison, tower, queues with cancel and rally hint.</summary>
        void BuildBuildingDetails(VisualElement root)
        {
            if (controller.SelectedTowns.Count + controller.SelectedHarbors.Count != 1) return;
            var harbor = controller.SelectedHarbor;
            var town = harbor ? (harbor.IsImportedPort ? harbor.LinkedTown : null) : controller.SelectedTown;
            var state = town ? town.State : harbor ? harbor.State : null;
            if (state == null) return;
            var details = new VisualElement { name = "HUD building details" }; details.style.flexShrink = 0; details.style.marginTop = 2;
            int country = state.Country;
            if (country >= 0 && country < MapLayout.Countries.Length)
                DetailLine(details, () =>
                {
                    var group = hud.Countries[country];
                    return "País · " + MapLayout.Countries[country].Name + " · " + group.Owned + " / " + group.CityCount + " ciudades" + (group.Owner == state.Owner && PlayerRules.IsPlayer(state.Owner) ? " · completo" : "");
                });
            DetailLine(details, () =>
            {
                var defender = town ? town.Defender : harbor ? harbor.Defender : null;
                return defender && defender.IsAlive
                    ? "Guarnición · " + UnitCatalog.Get(defender.Kind).Name + " · " + Mathf.CeilToInt(defender.Health) + " / " + Mathf.CeilToInt(defender.MaxHealth) + " vida"
                    : "Sin guarnición · un enemigo en el círculo la conquista";
            });
            var defense = town ? town.Defense : harbor ? harbor.Defense : null;
            if (defense)
                DetailLine(details, () => defense.UnderConstruction ? "Torre en construcción" : defense.IsAlive
                    ? "Torre · " + Mathf.CeilToInt(defense.Health) + " / " + Mathf.CeilToInt(defense.MaxHealth) + " vida" + (defense.Guardian && defense.Guardian.IsAlive ? "" : " · sin guarnición no dispara")
                    : "Torre destruida");
            if (harbor)
            {
                DetailLine(details, () => harbor.HasNavalDefender ? "Barco guardia · " + harbor.NavalDefender.DisplayName : "Sin barco guardia");
                if (!harbor.CanLaunch && !string.IsNullOrEmpty(harbor.LaunchBlockReason)) DetailLine(details, () => harbor.LaunchBlockReason);
            }
            if (state.Owner == 0)
            {
                if (town && town.QueueCount > 0) QueueStrip(details, "Cola", town.QueueCount, i => PortraitResource(town.QueuedKind(i)), i => UnitCatalog.Get(town.QueuedKind(i)).Name,
                    () => town.TrainingProgress, i => controller.CancelTraining(town, i));
                else if (harbor && !town && harbor.LandQueueCount > 0) QueueStrip(details, "Cola", harbor.LandQueueCount, i => PortraitResource(harbor.QueuedLandKind(i)), i => UnitCatalog.Get(harbor.QueuedLandKind(i)).Name,
                    () => harbor.LandTrainingProgress, i => controller.CancelTraining(harbor, i, false));
                if (harbor && harbor.QueueCount > 0) QueueStrip(details, "Astillero", harbor.QueueCount, i => UnitVariantViews.PortraitResource(harbor.QueuedKind(i)), i => UnitCatalog.Get(harbor.QueuedKind(i)).Name,
                    () => harbor.TrainingProgress, i => controller.CancelTraining(harbor, i, true));
                if (!UiViewport.IsTouchLayout) DetailLine(details, () => "Clic derecho en el mapa fija la salida de las nuevas unidades.", true);
            }
            root.Add(details);
        }

        void DetailLine(VisualElement root, System.Func<string> value, bool hint = false)
        {
            var label = RtsUiStyle.Label(value(), null, UiViewport.IsCompact ? 12 : 13);
            label.style.color = hint ? new Color(.6f, .62f, .58f) : RtsUiStyle.Text;
            label.style.whiteSpace = WhiteSpace.NoWrap; label.style.overflow = Overflow.Hidden; label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.marginTop = 0; label.style.marginBottom = 2;
            root.Add(label); liveContext.Add(() => label.text = GameText.Localize(value()));
        }

        /// <summary>Queued orders as small portraits; the first shows training progress; a click cancels (refund).</summary>
        void QueueStrip(VisualElement root, string title, int count, System.Func<int, string> portrait, System.Func<int, string> name, System.Func<float> progress, System.Action<int> cancel)
        {
            var row = new VisualElement { name = "HUD selection queue " + title }; RtsUiStyle.Row(row); row.style.marginTop = 3; row.style.marginBottom = 3;
            var caption = RtsUiStyle.Label(title, null, 12); caption.style.color = RtsUiStyle.Bronze; caption.style.width = 64; caption.style.flexShrink = 0; row.Add(caption);
            float size = UiViewport.IsTouchLayout ? UiViewport.MinimumTouchTarget : 36;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var button = RtsUiStyle.Button("", () => cancel(index), "HUD selection queue item " + i);
                SquareCell(button, size);
                button.tooltip = GameText.Localize(name(i) + " · cancelar encargo (devuelve el oro)");
                button.Add(PortraitFrame(portrait(i), size - 8));
                if (i == 0)
                {
                    var track = new VisualElement { pickingMode = PickingMode.Ignore }; track.style.position = Position.Absolute;
                    track.style.left = 2; track.style.right = 2; track.style.bottom = 2; track.style.height = 4; track.style.backgroundColor = RtsUiStyle.Slate;
                    var fill = new VisualElement { pickingMode = PickingMode.Ignore }; fill.style.height = 4; fill.style.backgroundColor = RtsUiStyle.Gold;
                    track.Add(fill); button.Add(track);
                    liveContext.Add(() => fill.style.width = Length.Percent(Mathf.Clamp01(progress()) * 100));
                }
                row.Add(button);
            }
            root.Add(row);
        }

        void BuildProduction(VisualElement grids, VisualElement info)
        {
            bool ownTown = false, ownHarbor = false;
            foreach (var town in controller.SelectedTowns) if (town && town.State.Owner == 0) { ownTown = true; break; }
            foreach (var harbor in controller.SelectedHarbors) if (harbor && harbor.Owner == 0) { ownHarbor = true; break; }
            bool hasCard = controller.TryGetProductionCard(out var active);
            if (controller.SelectedTowns.Count + controller.SelectedHarbors.Count > 1)
            {
                const string grouped = "Cada compra encarga una unidad por edificio compatible. La cantidad y el oro muestran el máximo disponible ahora; las colas no disponibles se omiten.";
                if (UiViewport.IsCompact) grids.tooltip = GameText.Localize(grouped); else AddInfo(info, grouped);
            }
            if (ownTown) ProductionGrid(grids, ProductionBuilding.City, hasCard && active == ProductionBuilding.City, CommandCellSize);
            if (ownHarbor) ProductionGrid(grids, ProductionBuilding.Harbor, hasCard && active == ProductionBuilding.Harbor, CommandCellSize);
        }

        void ProductionGrid(VisualElement parent, ProductionBuilding card, bool keysActive, float size)
        {
            var layout = ProductionHotkeys.Layout(card);
            int pages = ProductionHotkeys.PageCount(card), page = controller.ProductionPage(card);
            bool keys = keysActive && ShowGridHotkeys;
            int used = pages > 1 ? ProductionHotkeys.CellsPerPage : 0;
            foreach (var slot in layout) if (slot.Page == page) used = Mathf.Max(used, slot.Cell + 1);
            // Desktop keeps the full 4×3 card like WC3; compact drops empty trailing rows.
            int rows = UiViewport.IsCompact ? Mathf.Max(1, (used + ProductionHotkeys.Columns - 1) / ProductionHotkeys.Columns) : ProductionHotkeys.Rows;
            var grid = new VisualElement { name = "HUD production grid " + card };
            grid.style.flexShrink = 0; grid.style.marginBottom = 2;
            for (int row = 0; row < rows; row++)
            {
                var line = new VisualElement { name = "HUD production row " + row }; RtsUiStyle.Row(line); line.style.flexShrink = 0;
                for (int column = 0; column < ProductionHotkeys.Columns; column++)
                {
                    int cell = row * ProductionHotkeys.Columns + column;
                    if (pages > 1 && cell == ProductionHotkeys.PageCell) line.Add(PageCell(card, page, pages, keys, size));
                    else if (ProductionHotkeys.TryFind(card, page, cell, out var slot)) line.Add(ProductCell(slot, keys, size));
                    else line.Add(EmptyCell(size));
                }
                grid.Add(line);
            }
            parent.Add(grid);
        }

        /// <summary>Every product of the active cards in grid order, for the collapsed one-row strip.</summary>
        void ProductionStrip(VisualElement strip, float size)
        {
            bool ownTown = false, ownHarbor = false;
            foreach (var town in controller.SelectedTowns) if (town && town.State.Owner == 0) { ownTown = true; break; }
            foreach (var harbor in controller.SelectedHarbors) if (harbor && harbor.Owner == 0) { ownHarbor = true; break; }
            if (ownTown) foreach (var slot in ProductionHotkeys.Layout(ProductionBuilding.City)) strip.Add(ProductCell(slot, false, size));
            if (ownHarbor) foreach (var slot in ProductionHotkeys.Layout(ProductionBuilding.Harbor)) strip.Add(ProductCell(slot, false, size));
        }

        Button ProductCell(ProductionSlot slot, bool showKey, float size)
        {
            var option = slot.Option;
            string key = showKey ? slot.Key : null;
            if (option.IsShip)
            {
                var kind = option.Kind; var preview = controller.PreviewShipPurchase(kind); var profile = UnitCatalog.Get(kind);
                string detail = profile.MaxHealth + " vida · armadura " + profile.Armor + (profile.CanAttack ? " · " + profile.Weapon.DamageText + " daño · alcance " + profile.Weapon.Range : "") + (profile.CanTransport ? " · carga " + profile.Transport.Capacity : "");
                return CommandCell("Build ship " + kind, UnitVariantViews.PortraitResource(kind), PurchaseTitle(profile.Name, preview) + " · " + PurchaseCost(preview, profile.Cost, key) + " · " + detail,
                    () => controller.Produce(option), preview, profile.Cost, key, QueuedCount(option), size);
            }
            var unit = option.Kind; var unitPreview = controller.PreviewRecruitSelected(unit);
            return CommandCell("Recruit " + unit, PortraitResource(unit), PurchaseTitle(UnitCatalog.Get(unit).Name, unitPreview) + " · " + PurchaseCost(unitPreview, UnitCatalog.Get(unit).Cost, key) + " · " + UnitTooltip(unit),
                () => controller.Produce(option), unitPreview, UnitCatalog.Get(unit).Cost, key, QueuedCount(option), size);
        }

        static Button CommandCell(string name, string resource, string tooltip, System.Action action, ProductionBatchPreview preview, int unitCost, string key, int queued, float size)
        {
            var button = RtsUiStyle.Button("", action, name);
            button.AddToClassList("riskai-purchase-card"); button.AddToClassList("riskai-command-cell");
            button.tooltip = GameText.Localize(tooltip);
            button.SetEnabled(preview.CanPurchase);
            SquareCell(button, size);
            var frame = PortraitFrame(resource, size - 6); frame.style.alignSelf = Align.Center;
            if (!preview.CanPurchase) { var portrait = frame.Q<Image>(); if (portrait != null) portrait.tintColor = new Color(.42f, .42f, .42f, 1); }
            button.Add(frame);
            if (!string.IsNullOrEmpty(key)) button.Add(Badge(key, RtsUiStyle.Gold, true, true, 11));
            if (queued > 0) button.Add(Badge(queued.ToString(), Color.white, false, true, 10, new Color(.36f, .25f, .08f, .95f)));
            string cost = preview.IsGrouped && preview.PlannedCount > 1 ? preview.PlannedCost.ToString() : unitCost.ToString();
            button.Add(Badge(cost, !preview.CanPurchase ? CostShort : preview.UnfundedCount > 0 ? new Color(1f, .7f, .35f, 1) : RtsUiStyle.Gold, false, false, 11));
            if (preview.IsGrouped) button.Add(Badge("×" + preview.CandidateCount, RtsUiStyle.Text, true, false, 9));
            return button;
        }

        Button PageCell(ProductionBuilding card, int page, int pages, bool showKey, float size)
        {
            var button = RtsUiStyle.Button("", () => { controller.NextProductionPage(card); RebuildContext(); }, "HUD production page");
            button.tooltip = GameText.Localize("Más opciones · página " + (page + 1) + " / " + pages);
            SquareCell(button, size); button.style.justifyContent = Justify.Center; button.style.alignItems = Align.Center;
            var label = RtsUiStyle.Label((page + 1) + "/" + pages, null, 12); label.pickingMode = PickingMode.Ignore; label.style.color = RtsUiStyle.Gold;
            button.Add(new RtsChevron(false)); button.Add(label);
            if (showKey) button.Add(Badge(ProductionHotkeys.PageKey, RtsUiStyle.Gold, true, true, 11));
            return button;
        }

        static VisualElement EmptyCell(float size)
        {
            var cell = new VisualElement { name = "HUD empty command cell", pickingMode = PickingMode.Ignore };
            cell.style.width = cell.style.height = size; cell.style.flexShrink = 0;
            cell.style.marginRight = cell.style.marginBottom = 4;
            cell.style.backgroundColor = CellEmpty;
            cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = 1;
            cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = new Color(.3f, .24f, .13f, .5f);
            return cell;
        }

        static void SquareCell(VisualElement button, float size)
        {
            button.style.width = button.style.minWidth = button.style.maxWidth = size;
            button.style.height = button.style.minHeight = button.style.maxHeight = size;
            button.style.flexShrink = 0; button.style.flexGrow = 0;
            button.style.marginLeft = button.style.marginTop = 0; button.style.marginRight = button.style.marginBottom = 4;
            button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 2;
            button.style.justifyContent = Justify.Center;
        }

        /// <summary>A corner mark on a command cell: hotkey (top-left), queue (top-right), cost (bottom-right), group (bottom-left).</summary>
        static Label Badge(string text, Color color, bool left, bool top, int size, Color? background = null)
        {
            var badge = new Label(text) { name = "HUD cell badge", pickingMode = PickingMode.Ignore };
            badge.style.position = Position.Absolute; badge.style.fontSize = size; badge.style.color = color;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold; badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.backgroundColor = background ?? BadgeBack;
            badge.style.paddingLeft = badge.style.paddingRight = 3; badge.style.paddingTop = badge.style.paddingBottom = 0;
            badge.style.marginLeft = badge.style.marginRight = badge.style.marginTop = badge.style.marginBottom = 0;
            if (left) badge.style.left = 2; else badge.style.right = 2;
            if (top) badge.style.top = 2; else badge.style.bottom = 2;
            return badge;
        }

        /// <summary>Queued orders of this product across the selected own buildings.</summary>
        int QueuedCount(ProductionOption option)
        {
            int count = 0;
            if (option.IsShip)
            {
                var kind = option.Kind;
                foreach (var harbor in controller.SelectedHarbors)
                    if (harbor && harbor.Owner == 0) for (int i = 0; i < harbor.QueueCount; i++) if (harbor.QueuedKind(i) == kind) count++;
            }
            else if ((UnitCatalog.Get(option.Kind).Building==UnitBuilding.Harbor))
            {
                foreach (var harbor in controller.SelectedHarbors)
                    if (harbor && harbor.Owner == 0) for (int i = 0; i < harbor.LandQueueCount; i++) if (harbor.QueuedLandKind(i) == option.Kind) count++;
            }
            else
            {
                foreach (var town in controller.SelectedTowns)
                    if (town && town.State.Owner == 0) for (int i = 0; i < town.QueueCount; i++) if (town.QueuedKind(i) == option.Kind) count++;
            }
            return count;
        }

        /// <summary>Drawer tab just above the compact footer: collapse to one row or expand.</summary>
        void BuildFooterHandle(VisualElement root)
        {
            if (!UiViewport.IsCompact || !FooterVisible || MinimapVisible && UiViewport.IsPortrait) return;
            bool collapsed = FooterCollapsed;
            var handle = RtsUiStyle.Button("", ToggleFooterCollapsed, "HUD footer collapse");
            handle.tooltip = GameText.Localize(collapsed ? "Ampliar panel" : "Reducir panel");
            handle.style.position = Position.Absolute; handle.style.right = 8; handle.style.bottom = FooterHeight - 1;
            handle.style.width = handle.style.minWidth = 60; handle.style.height = handle.style.minHeight = 36;
            handle.style.marginRight = handle.style.marginBottom = 0;
            handle.style.paddingLeft = handle.style.paddingRight = handle.style.paddingTop = handle.style.paddingBottom = 0;
            handle.style.alignItems = Align.Center; handle.style.justifyContent = Justify.Center;
            handle.style.backgroundColor = RtsUiStyle.PanelColor;
            handle.Add(new RtsChevron(collapsed));
            root.Add(handle);
        }

        void ToggleFooterCollapsed()
        {
            footerCollapsed = !FooterCollapsed;
            BuildRetainedUi(false);
        }

        /// <summary>Collapsed compact footer: one horizontally scrollable row of the same cells.</summary>
        void BuildCollapsedFooter(VisualElement footerPanel)
        {
            var strip = new ScrollView(ScrollViewMode.Horizontal) { name = "HUD collapsed strip" };
            strip.style.flexGrow = 1; strip.style.minHeight = 0;
            strip.horizontalScrollerVisibility = ScrollerVisibility.Hidden; strip.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.contentContainer.style.flexDirection = FlexDirection.Row; strip.contentContainer.style.alignItems = Align.Center;
            float size = UiViewport.MinimumTouchTarget;
            if (BuildingSelection) ProductionStrip(strip, size);
            else if (controller.Selection.Count + controller.Fleet.Count > 0) BuildOrderCells(strip, size);
            if (strip.contentContainer.childCount == 0)
            {
                var title = RtsUiStyle.Label(CollapsedTitle(), "HUD collapsed title", 13); title.style.color = RtsUiStyle.Gold;
                title.style.whiteSpace = WhiteSpace.NoWrap; title.style.overflow = Overflow.Hidden; title.style.textOverflow = TextOverflow.Ellipsis;
                strip.Add(title);
            }
            footerPanel.Add(strip);
        }

        // Mirrors docs/CONTROLS-v0.30.md. Spanish source; GameText translates each cell.
        static readonly string[,] ControlsTable =
        {
            { "Q W E R · A S D F · Z X C V", "Con una ciudad o puerto propio seleccionado: produce la unidad de esa casilla de la cuadrícula" },
            { "V", "Si la cuadrícula tiene más de 12 opciones: cambia de página" },
            { "A · M · P · S · H", "Con tropas: atacar, mover, patrullar, detener, mantener posición" },
            { "B · D", "Embarcar tropas cercanas · desembarcar la flota" },
            { "E", "Seleccionar todo el ejército" },
            { "N", "Seleccionar la flota" },
            { "1–9 · Ctrl+1–9", "Recuperar o guardar un grupo; doble pulsación centra la cámara" },
            { "Espacio", "Centrar en la selección, la flota o la última alerta" },
            { "F1 · F2 · F3", "Menú · ir a tu base · ir a tu puerto" },
            { "F7 · F8 · F9", "Efectos de sonido · música · minimapa (también en la barra rápida)" },
            { "F10", "Pausa" },
            { "Tab", "Mantener para ver la clasificación" },
            { "Intro · Mayús+Intro", "Chat al destinatario elegido · enviar a todos" },
            { "Tab en el chat · /w color", "Cambiar de destinatario · mensaje privado (p. ej. /w azul hola, /azul hola)" },
            { "Esc", "Cancelar la orden o deseleccionar" },
            { "Alt", "Mostrar vida y nombres" },
            { "Mayús", "Con una tropa ocupada añade la orden: mover, atacar, capturar, seguir, embarcar y desembarcar. Sin Mayús, la orden sustituye la cola. El conmutador de la barra rápida hace lo mismo." },
            { "F4", "Mantener junto a las flechas: la cámara se mueve más rápido" },
            { "Flechas · Retroceso", "Mover la cámara · restablecer la cámara" },
        };

        void BuildControlsTable(VisualElement panel)
        {
            AddInfo(panel, "Con un edificio seleccionado, su cuadrícula tiene prioridad: Q W E R / A S D F / Z X C V producen y E, A, S, D no dan órdenes de tropa.");
            for (int i = 0; i < ControlsTable.GetLength(0); i++)
            {
                var row = new VisualElement { name = "HUD controls row " + i }; RtsUiStyle.Row(row);
                row.style.alignItems = Align.FlexStart; row.style.marginBottom = 4;
                var keys = RtsUiStyle.Label(ControlsTable[i, 0], null, 13); keys.style.color = RtsUiStyle.Gold;
                keys.style.width = UiViewport.IsCompact ? 120 : 210; keys.style.flexShrink = 0; keys.style.whiteSpace = WhiteSpace.Normal;
                var action = RtsUiStyle.Label(ControlsTable[i, 1], null, 13); action.style.color = RtsUiStyle.Muted;
                action.style.flexGrow = 1; action.style.flexShrink = 1; action.style.minWidth = 0; action.style.whiteSpace = WhiteSpace.Normal;
                row.Add(keys); row.Add(action); panel.Add(row);
            }
        }

        string CollapsedTitle()
        {
            if (controller.SelectedCamp) return controller.SelectedCamp.DisplayName;
            if (controller.SelectedHarbor) return controller.SelectedHarbor.DisplayName;
            if (controller.SelectedTown) return controller.SelectedTown.DisplayName;
            if (controller.InspectedTarget is Soldier soldier) return UnitCatalog.Get(soldier.Kind).Name;
            return "";
        }
    }
}
