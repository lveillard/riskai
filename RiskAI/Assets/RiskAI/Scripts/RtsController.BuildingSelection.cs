using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public sealed partial class RtsController
    {
        const float NearbyBuildingSelectionRadius = 90f;
        const float DoubleBuildingClickSeconds = .3f;
        readonly List<Settlement> selectedTowns = new List<Settlement>();
        readonly List<Harbor> selectedHarbors = new List<Harbor>();
        Component lastBuildingClick;
        float lastBuildingClickTime;

        /// <summary>Selected city buildings, excluding imported port cities represented by their Harbor.</summary>
        public IReadOnlyList<Settlement> SelectedTowns => selectedTowns;
        /// <summary>Selected harbor buildings. An imported port appears here once, never as a duplicate town.</summary>
        public IReadOnlyList<Harbor> SelectedHarbors => selectedHarbors;

        public void SelectTown(Settlement town) => SelectTown(town, false);
        public void SelectTown(Settlement town, bool append)
        {
            if (!town) { if (!append) Clear(); return; }
            if (town.Port) { SelectHarbor(town.Port, append); return; }
            SelectBuildings(new[] { town }, null, append);
            SelectedTown = town;
            // Preserve the clicked town as the legacy primary even when Shift kept ports in the group.
            SelectedHarbor = null;
        }

        public void SelectHarbor(Harbor harbor) => SelectHarbor(harbor, false);
        public void SelectHarbor(Harbor harbor, bool append)
        {
            if (!harbor) { if (!append) Clear(); return; }
            SelectBuildings(null, new[] { harbor }, append);
            SelectedHarbor = harbor;
            SelectedTown = harbor.IsImportedPort && harbor.LinkedTown ? harbor.LinkedTown : selectedTowns.FirstOrDefault();
        }

        public void ToggleTown(Settlement town)
        {
            if (!town) return;
            if (town.Port) { ToggleHarbor(town.Port); return; }
            if (selectedTowns.Contains(town)) RemoveTown(town);
            else SelectTown(town, true);
        }

        public void ToggleHarbor(Harbor harbor)
        {
            if (!harbor) return;
            if (selectedHarbors.Contains(harbor)) RemoveHarbor(harbor);
            else SelectHarbor(harbor, true);
        }

        /// <summary>Selects buildings as one group. Imported ports are represented by their Harbor only.</summary>
        public void SelectBuildings(IEnumerable<Settlement> towns, IEnumerable<Harbor> harbors, bool append = false)
        {
            var townList = towns == null ? new List<Settlement>() : towns.Where(t => t && !t.Port).Distinct().ToList();
            var harborList = harbors == null ? new List<Harbor>() : harbors.Where(h => h).Distinct().ToList();
            if (!append) Clear();
            else ClearMobileAndCamp();
            foreach (var town in townList) AddTown(town);
            foreach (var harbor in harborList) AddHarbor(harbor);
            UpdatePrimaryBuilding();
        }

        public void ClearSelectedBuildings()
        {
            foreach (var town in selectedTowns) if (town) town.Selected = false;
            foreach (var harbor in selectedHarbors)
            {
                if (!harbor) continue;
                harbor.Select(false);
                if (harbor.IsImportedPort && harbor.LinkedTown) harbor.LinkedTown.Selected = false;
            }
            if (SelectedTown) SelectedTown.Selected = false;
            if (SelectedHarbor) SelectedHarbor.Select(false);
            selectedTowns.Clear(); selectedHarbors.Clear();
            SelectedTown = null; SelectedHarbor = null;
        }

        void ClearMobileAndCamp()
        {
            if (SelectedCamp) SelectedCamp.Select(false);
            SelectedCamp = null;
            foreach (var unit in Selection) if (unit) unit.Select(false);
            Selection.Clear();
            foreach (var ship in Fleet) if (ship) ship.Select(false);
            Fleet.Clear();
            InspectedTarget = null;
        }

        void AddTown(Settlement town)
        {
            if (!town) return;
            if (town.Port) { AddHarbor(town.Port); return; }
            if (selectedTowns.Contains(town)) return;
            selectedTowns.Add(town); town.Selected = true;
        }

        void AddHarbor(Harbor harbor)
        {
            if (!harbor || selectedHarbors.Contains(harbor)) return;
            selectedHarbors.Add(harbor); harbor.Select(true);
            if (harbor.IsImportedPort && harbor.LinkedTown) harbor.LinkedTown.Selected = true;
        }

        void RemoveTown(Settlement town)
        {
            if (!selectedTowns.Remove(town)) return;
            if (town) town.Selected = false;
            UpdatePrimaryBuilding();
        }

        void RemoveHarbor(Harbor harbor)
        {
            if (!selectedHarbors.Remove(harbor)) return;
            if (harbor)
            {
                harbor.Select(false);
                if (harbor.IsImportedPort && harbor.LinkedTown) harbor.LinkedTown.Selected = false;
            }
            UpdatePrimaryBuilding();
        }

        void UpdatePrimaryBuilding()
        {
            SelectedHarbor = selectedHarbors.FirstOrDefault();
            SelectedTown = selectedTowns.FirstOrDefault();
            if (!SelectedTown && SelectedHarbor && SelectedHarbor.IsImportedPort) SelectedTown = SelectedHarbor.LinkedTown;
        }

        void SelectBuildingsIn(Rect rect, bool append)
        {
            var towns = session.Towns.Where(t => t && !t.Port && InSelection(t, rect)).ToList();
            var harbors = (NavalWorld.Current
                ? NavalWorld.Current.Harbors.Where(h => h && InSelection(h, rect))
                : Enumerable.Empty<Harbor>()).ToList();
            // Shift-dragging empty terrain must leave the prior selection intact.
            if (towns.Count == 0 && harbors.Count == 0)
            {
                if (!append) Clear();
                return;
            }
            SelectBuildings(towns, harbors, append);
        }

        public void SelectNearbyTowns(Settlement pivot, bool append = false)
        {
            var radiusSquared = NearbyBuildingSelectionRadius * NearbyBuildingSelectionRadius;
            var nearby = session.Towns.Where(t => t && t.State.Owner == 0 && InViewport(t) && FlatDistance(t.transform.position, pivot.transform.position) <= radiusSquared).ToList();
            SelectBuildings(nearby.Where(t => !t.Port), nearby.Where(t => t.Port).Select(t => t.Port), append);
            if (pivot.Port) { SelectedHarbor = pivot.Port; SelectedTown = pivot; }
            else { SelectedTown = pivot; SelectedHarbor = null; }
        }

        public void SelectNearbyHarbors(Harbor pivot, bool append = false)
        {
            var radiusSquared = NearbyBuildingSelectionRadius * NearbyBuildingSelectionRadius;
            var harbors = NavalWorld.Current == null ? Enumerable.Empty<Harbor>() : NavalWorld.Current.Harbors
                .Where(h => h && h.Owner == 0 && InViewport(h) && FlatDistance(h.transform.position, pivot.transform.position) <= radiusSquared);
            SelectBuildings(null, harbors, append);
            SelectedHarbor = pivot;
            SelectedTown = pivot.IsImportedPort ? pivot.LinkedTown : selectedTowns.FirstOrDefault();
        }

        void HandleTownClick(Settlement town, bool append)
        {
            if (!town) return;
            bool doubleTown = town.State.Owner == 0 && IsDoubleBuildingClick(town);
            if (doubleTown) SelectNearbyTowns(town, append); else SelectTown(town, append);
            RememberBuildingClick(town);
        }
        void HandleHarborClick(Harbor harbor, bool append)
        {
            if (!harbor) return;
            bool doubleHarbor = harbor.Owner == 0 && IsDoubleBuildingClick(harbor);
            if (doubleHarbor) SelectNearbyHarbors(harbor, append); else SelectHarbor(harbor, append);
            RememberBuildingClick(harbor);
        }
        bool IsDoubleBuildingClick(Component building) => building && lastBuildingClick == building && Time.unscaledTime - lastBuildingClickTime < DoubleBuildingClickSeconds;
        void RememberBuildingClick(Component building) { lastBuildingClick = building; lastBuildingClickTime = Time.unscaledTime; }

        /// <summary>Queues exactly one land unit at the selected, allied compatible building with the shortest queue.</summary>
        public string TryRecruitSelected(UnitKind kind)
        {
            string firstError = null;
            if (kind >= UnitKind.MarinePrivate)
            {
                foreach (var harbor in selectedHarbors.Where(h => h && h.Owner == 0).OrderBy(h => h.LandQueueCount).ThenBy(StableHarborIndex))
                {
                    string error = harbor.RecruitLand(kind);
                    if (error == null) return null;
                    if (firstError == null) firstError = error;
                }
                return firstError ?? "Selecciona un puerto de tu bando para reclutar Marines.";
            }
            foreach (var town in selectedTowns.Where(t => t && t.State.Owner == 0).OrderBy(t => t.QueueCount).ThenBy(t => t.State.Id, System.StringComparer.Ordinal))
            {
                string error = town.Recruit(kind);
                if (error == null) return null;
                if (firstError == null) firstError = error;
            }
            return firstError ?? "Selecciona una ciudad de tu bando para reclutar.";
        }

        /// <summary>Queues exactly one ship at the selected allied harbor with the shortest naval queue.</summary>
        public string TryBuySelected(ShipKind kind)
        {
            string firstError = null;
            foreach (var harbor in selectedHarbors.Where(h => h && h.Owner == 0).OrderBy(h => h.QueueCount).ThenBy(StableHarborIndex))
            {
                string error = harbor.Buy(kind);
                if (error == null) return null;
                if (firstError == null) firstError = error;
            }
            return firstError ?? "Selecciona un puerto de tu bando para comprar barcos.";
        }

        // NavalWorld builds this list in source order; the index gives deterministic ties without treating a display name as identity.
        int StableHarborIndex(Harbor harbor)
        {
            var naval = NavalWorld.Current;
            return naval ? naval.Harbors.IndexOf(harbor) : int.MaxValue;
        }

        bool SetSelectedBuildingRallies(Vector3 point)
        {
            bool changed = false;
            foreach (var town in selectedTowns)
            {
                if (!town || town.State.Owner != 0) continue;
                town.SetRally(point); changed = true;
            }
            foreach (var harbor in selectedHarbors)
            {
                if (!harbor || harbor.Owner != 0) continue;
                var town = harbor.IsImportedPort ? harbor.LinkedTown : null;
                if (town)
                {
                    if (selectedTowns.Contains(town)) continue;
                    town.SetRally(point); changed = true;
                }
                else if (harbor.SetRally(point)) changed = true;
            }
            return changed;
        }

        // An imported port is one command identity, but its city house and berth
        // are deliberately separated by source waterfront geometry. A drag over
        // either visible footprint selects the same Harbor.
        bool InSelection(Harbor harbor, Rect rect)
        {
            return InSelection((Component)harbor, rect) ||
                (harbor.IsImportedPort && harbor.LinkedTown && InSelection(harbor.LinkedTown, rect));
        }

        bool InSelection(Component building, Rect rect)
        {
            var center = BuildingSelection.Bounds(building).center;
            var screen = cam.WorldToScreenPoint(center);
            return screen.z > 0 && rect.Contains(new Vector2(screen.x, Screen.height - screen.y));
        }

        bool InViewport(Component building)
        {
            var point = cam.WorldToViewportPoint(BuildingSelection.Bounds(building).center);
            return point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
        }
    }
}
