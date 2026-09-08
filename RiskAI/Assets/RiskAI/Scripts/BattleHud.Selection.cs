using RiskAI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    public sealed partial class BattleHud
    {
        // A polynomial hash can collide for different, valid army selections.
        // Compare ordered identities exactly, without allocating during refresh.
        bool RosterChanged()
        {
            if (retainedSoldierCount != controller.Selection.Count ||
                retainedRosterIds.Count != controller.Selection.Count + controller.Fleet.Count) return true;
            int index = 0;
            foreach (var unit in controller.Selection)
                if (retainedRosterIds[index++] != RosterIdentity(unit)) return true;
            foreach (var ship in controller.Fleet)
                if (retainedRosterIds[index++] != RosterIdentity(ship)) return true;
            return false;
        }

        void RememberRoster()
        {
            retainedSoldierCount = controller.Selection.Count;
            retainedRosterIds.Clear();
            foreach (var unit in controller.Selection) retainedRosterIds.Add(RosterIdentity(unit));
            foreach (var ship in controller.Fleet) retainedRosterIds.Add(RosterIdentity(ship));
        }

        static int RosterIdentity(CombatTarget actor) => actor ? actor.EntityId : 0;

        // The same roster lives in the desktop selection column and compact tab.
        // Its parent ScrollView handles overflow; no selected units are omitted.
        void BuildSelectionRoster(VisualElement root)
        {
            var roster = new VisualElement { name = "HUD selection roster" };
            roster.style.flexDirection = FlexDirection.Row;
            roster.style.flexWrap = Wrap.Wrap;
            roster.style.flexShrink = 0;
            foreach (var unit in controller.Selection) AddSelectionCard(roster, unit);
            foreach (var ship in controller.Fleet) AddSelectionCard(roster, ship);
            root.Add(roster);
        }

        void AddSelectionCard(VisualElement roster, CombatTarget actor)
        {
            if (!actor) return;
            int entityId = actor.EntityId;
            var soldier = actor as Soldier;
            var ship = actor as Ship;
            string name = soldier ? BattleRules.Name(soldier.Kind) : ship.DisplayName;
            bool SameLiveActor() => actor && actor.EntityId == entityId && actor.IsAlive &&
                actor.isActiveAndEnabled && actor.Team == 0;

            var button = RtsUiStyle.Button("", () =>
            {
                // A pooled view may represent a new actor before this UI rebuilds.
                if (!SameLiveActor()) return;
                // Membership is checked only when acting. Refreshing every card's
                // health must stay linear in roster size, without nested scans.
                if (soldier ? !controller.Selection.Contains(soldier) : !controller.Fleet.Contains(ship)) return;
                if (soldier) controller.SelectOnly(soldier);
                else controller.SelectShip(ship);
            });
            button.name = "HUD selected actor " + entityId;
            button.AddToClassList("riskai-selection-card");
            button.style.width = button.style.minWidth = 52;
            button.style.height = button.style.minHeight = 60;
            button.style.flexShrink = 0;
            button.style.paddingLeft = button.style.paddingRight = 4;
            button.style.paddingTop = button.style.paddingBottom = 4;
            button.style.marginRight = button.style.marginBottom = 6;
            if (soldier)
            {
                var portrait = new Image { image = CachedPortrait(PortraitResource(soldier.Kind)), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                portrait.style.width = 40; portrait.style.height = 40;
                button.Add(portrait);
            }
            else
            {
                var icon = new NavalQueueIcon { pickingMode = PickingMode.Ignore };
                icon.SetKind(ship.Kind); icon.style.width = 40; icon.style.height = 40;
                button.Add(icon);
            }
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.width = Length.Percent(100);
            track.style.height = 5; track.style.flexShrink = 0;
            track.style.backgroundColor = new Color(.09f, .06f, .04f);
            var health = new VisualElement { name = "HUD selection health", pickingMode = PickingMode.Ignore };
            health.style.height = Length.Percent(100);
            health.style.backgroundColor = new Color(.48f, .85f, .3f);
            track.Add(health); button.Add(track); roster.Add(button);

            System.Action refresh = () =>
            {
                bool valid = SameLiveActor();
                button.SetEnabled(valid);
                health.style.width = Length.Percent(valid && actor.MaxHealth > 0 ? Mathf.Clamp01(actor.Health / actor.MaxHealth) * 100 : 0);
                button.tooltip = valid ? name + " · " + Mathf.CeilToInt(actor.Health) + " / " + actor.MaxHealth + " vida" : "Unidad retirada";
            };
            liveContext.Add(refresh); refresh();
        }
    }
}
