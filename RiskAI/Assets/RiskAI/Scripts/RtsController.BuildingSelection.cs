using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public readonly struct ProductionBatchPreview
    {
        public readonly int CandidateCount, PlannedCount, PlannedCost;
        public bool IsGrouped => CandidateCount > 1;
        public bool CanPurchase => PlannedCount > 0;
        public int UnfundedCount => CandidateCount - PlannedCount;
        public ProductionBatchPreview(int candidates,int planned,int cost)
        { CandidateCount=candidates;PlannedCount=planned;PlannedCost=cost; }
    }

    public readonly struct ProductionBatchResult
    {
        public readonly int RequestedCount, AcceptedCount, SpentGold;
        public readonly string FirstFailure;
        public int RejectedCount => RequestedCount-AcceptedCount;
        public string Error => AcceptedCount>0?null:FirstFailure;
        public ProductionBatchResult(int requested,int accepted,int spent,string failure)
        { RequestedCount=requested;AcceptedCount=accepted;SpentGold=spent;FirstFailure=failure; }
        public string Feedback(string product)
        {
            if(AcceptedCount<=0)return FirstFailure;
            string text="×"+AcceptedCount+" "+product+" encargados · "+SpentGold+" oro";
            return RejectedCount>0?text+" · "+RejectedCount+" sin encargo":text;
        }
    }

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
            // Purging first leaves only actors this selection still owns, so every
            // remaining ring is cleared and a replaced actor keeps its own state.
            PurgeStaleSelection();
            foreach (var unit in Selection) unit.Select(false);
            foreach (var ship in Fleet) ship.Select(false);
            ClearSelectionLists();
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
            var towns = session.Towns.Where(t => t && !t.Port && t.State.Owner == 0 && InSelection(t, rect)).ToList();
            var harbors = (NavalWorld.Current
                ? NavalWorld.Current.Harbors.Where(h => h && h.Owner == 0 && InSelection(h, rect))
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

        public ProductionBatchResult LastProductionResult { get; private set; }

        public ProductionBatchPreview PreviewRecruitSelected(UnitKind kind)
        {
            int cost=BattleRules.Cost(kind);
            return ProductionCatalog.AllowsHarborUnit(kind)
                ? PreviewSelectedBuildings(OwnSelectedHarbors(),cost)
                : PreviewSelectedBuildings(OwnSelectedTowns(),cost);
        }

        public ProductionBatchPreview PreviewShipPurchase(ShipKind kind) =>
            PreviewSelectedBuildings(OwnSelectedHarbors(),Harbor.Cost(kind));

        IEnumerable<Settlement> OwnSelectedTowns() => selectedTowns.Where(t=>t&&t.State.Owner==0);
        IEnumerable<Harbor> OwnSelectedHarbors() => selectedHarbors.Where(h=>h&&h.Owner==0);

        ProductionBatchPreview PreviewSelectedBuildings<T>(IEnumerable<T> buildings,int unitCost) where T : class
        {
            int candidates=buildings.Count();
            int gold=session&&unitCost>0?Mathf.Max(0,session.Economy.Gold[0]):0;
            int planned=unitCost>0?Mathf.Min(candidates,gold/unitCost):candidates;
            return new ProductionBatchPreview(candidates,planned,planned*unitCost);
        }

        /// <summary>Queues one land unit at every selected, allied compatible building, shortest queues first.</summary>
        public string TryRecruitSelected(UnitKind kind)
        {
            if (ProductionCatalog.AllowsHarborUnit(kind))
            {
                LastProductionResult=QueueAtSelectedBuildings(
                    OwnSelectedHarbors(), h => h.LandQueueCount, StableHarborIndex,
                    h => ExecuteBuilding(PlayerBuildingIntent.Recruit(h.BuildingId,kind)),
                    BattleRules.Cost(kind),"Selecciona un puerto de tu bando para reclutar Marines.");
                return LastProductionResult.Error;
            }
            LastProductionResult=QueueAtSelectedBuildings(
                OwnSelectedTowns(), t => t.QueueCount, t => t.State.Id,
                t => ExecuteBuilding(PlayerBuildingIntent.Recruit(t.BuildingId,kind)),
                BattleRules.Cost(kind),"Selecciona una ciudad de tu bando para reclutar.");
            return LastProductionResult.Error;
        }

        /// <summary>Queues one ship at every selected allied harbor, shortest naval queues first.</summary>
        public string TryBuySelected(ShipKind kind)
        {
            LastProductionResult=QueueAtSelectedBuildings(
                OwnSelectedHarbors(), h => h.QueueCount, StableHarborIndex,
                h => ExecuteBuilding(PlayerBuildingIntent.BuyShip(h.BuildingId,(NavalUnitKind)kind)),
                Harbor.Cost(kind),"Selecciona un puerto de tu bando para comprar barcos.");
            return LastProductionResult.Error;
        }

        // A grouped purchase is best-effort: a full or unavailable queue must not
        // prevent other selected compatible buildings from receiving one order.
        ProductionBatchResult QueueAtSelectedBuildings<T,TOrder>(IEnumerable<T> buildings, System.Func<T,int> queueLength,
            System.Func<T,TOrder> stableOrder, System.Func<T,string> enqueue, int unitCost,string emptyMessage) where T : class
        {
            string firstError = null;
            int queued = 0,requested=0;
            int goldBefore=session?session.Economy.Gold[0]:0;
            foreach (var building in buildings.OrderBy(queueLength).ThenBy(stableOrder))
            {
                requested++;
                string error = enqueue(building);
                if (error == null) { queued++; continue; }
                if (firstError == null) firstError = error;
            }
            int spent=session?Mathf.Max(0,goldBefore-session.Economy.Gold[0]):queued*unitCost;
            return new ProductionBatchResult(requested,queued,spent,firstError??emptyMessage);
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
                if(ExecuteBuilding(PlayerBuildingIntent.SetLandRally(town.BuildingId,point.x,point.y,point.z))==null)changed=true;
            }
            foreach (var harbor in selectedHarbors)
            {
                if (!harbor || harbor.Owner != 0) continue;
                var town = harbor.IsImportedPort ? harbor.LinkedTown : null;
                if (town)
                {
                    if (selectedTowns.Contains(town)) continue;
                    if(ExecuteBuilding(PlayerBuildingIntent.SetLandRally(harbor.BuildingId,point.x,point.y,point.z))==null)changed=true;
                }
                else if(ExecuteBuilding(PlayerBuildingIntent.SetLandRally(harbor.BuildingId,point.x,point.y,point.z))==null)changed=true;
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
