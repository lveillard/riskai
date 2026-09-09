using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public sealed class Settlement : MonoBehaviour
    {
        public TownState State { get; private set; }
        public BuildingId BuildingId => new BuildingId(BuildingKind.Settlement,State?.Id);
        public string DisplayName { get; private set; }
        public Vector3 Rally { get; private set; }
        public bool IsCapital { get; private set; }
        public int FoundingTeam { get; private set; }
        public DefenseTower Defense { get; private set; }
        public bool IsPort { get; private set; }
        public Harbor Port { get; internal set; }
        public CityClaimZone ClaimZone { get; private set; }
        public Soldier Defender => ClaimZone != null ? ClaimZone.Defender : null;
        public Vector3 ClaimPoint { get; private set; }
        public Vector3 PortBuildingPoint { get; private set; }
        public Vector3 PortSeaward { get; private set; }
        public Vector3 PortLandEntry { get; private set; }
        // Town architecture faces south; this is beyond its entrance steps and
        // outside the solid hall, independent of the owning team.
        public Vector3 DefaultLandEntry => transform.position + Vector3.back * 4f;
        public int QueueCount => queue.Count;
        public float TrainingProgress => queue.Count == 0 ? 0 : 1 - queue[0].Remaining / BattleRules.TrainTime(queue[0].Kind);
        public UnitKind TrainingKind => queue.Count == 0 ? UnitKind.Footman : queue[0].Kind;
        public LineRenderer Ring { get; private set; }
        bool navalClaimVisual;
        bool selected;
        public bool Selected
        {
            get=>selected;
            set
            {
                selected=value;
                if(SelectionRing)SelectionRing.enabled=value;
                if(rallyRing)rallyRing.enabled=value&&State!=null&&State.Owner==0;
                if(Ring)Ring.widthMultiplier=value?.10f:CityClaimZone.RingWidth;
            }
        }
        public bool Building => project != BuildingProject.None;
        public string ProjectName => project == BuildingProject.Tower ? "Torre de guardia" : "Mejora de ciudad";
        public float ProjectProgress => Building ? 1 - projectRemaining / BattleRules.ConstructionSeconds : 0;
        public int PotentialIncome => BattleRules.TownIncome + (State.Level - 1) * BattleRules.UpgradeIncome;
        public int Income => State.Owner >= 0 ? PotentialIncome : 0;
        BattleSession session;
        Renderer flag;
        readonly List<Training> queue = new List<Training>();
        float projectRemaining;
        int projectOwner;
        BuildingProject project;
        LineRenderer rallyRing;
        BuildingTrainingView trainingView;
        bool portNavalTraining;
        public LineRenderer SelectionRing { get; private set; }
        enum BuildingProject { None, Tower, Upgrade }
        sealed class Training { public int Team; public UnitKind Kind; public float Remaining; }

        public void Initialize(BattleSession battle, string id, string displayName, int owner, int region, bool capital, int country = -1, Vector3? sourceClaim = null, bool isPort = false)
        {
            session = battle; State = new TownState(id, owner, region, country); DisplayName = displayName; IsCapital = capital; FoundingTeam = owner;IsPort=isPort;
            Rally = DefaultLandEntry;
            Vector3 claimProbe = transform.position + new Vector3(0, 0, -4.2f);
            ClaimPoint = sourceClaim ?? MapLayout.Point(claimProbe.x, claimProbe.z);
            PortBuildingPoint=transform.position;PortSeaward=Vector3.forward;PortLandEntry=DefaultLandEntry;
            if(IsPort&&MapLayout.IsImported)
            {
                var anchors=ImportedPortLayout.Resolve(transform.position,ClaimPoint);
                // ClaimPoint stays at the exact source B00R coordinate. Only the
                // presentation and land access adapt to the coastline.
                PortBuildingPoint=anchors.Building;PortLandEntry=anchors.Shore;PortSeaward=anchors.Seaward;
            }
            ClaimZone = new CityClaimZone(ClaimPoint);
            session.Towns.Add(this); session.Economy.Towns.Add(State);
            BuildingEntranceAnchor entrance;
            if(IsPort)
            {
                var visual=NavalArt.CreateHarborBuildingCentered(transform,owner,PortBuildingPoint,PortSeaward,true);
                flag=visual.Flag;entrance=visual.Entrance;
            }
            else
            {
                flag = VisualFactory.Town(transform, owner, capital && !MapLayout.IsImported);
                entrance=BuildingEntranceAnchor.Find(transform);
            }
            Ring = VisualFactory.Ring(transform, CityClaimZone.DefaultHalfExtent, CityClaimZone.RingWidth, CityClaimZone.RingColor);
            Ring.transform.position = ClaimPoint; Ring.enabled=false;
            var towerObject = new GameObject("Torre de " + displayName);
            towerObject.transform.SetParent(transform, false);
            towerObject.transform.localPosition = new Vector3(transform.position.x < 0 ? 3.8f : -3.8f, 0, 0);
            if(sourceClaim.HasValue) towerObject.transform.position=ImportedTowerPoint();
            if(IsPort&&MapLayout.IsImported)towerObject.transform.rotation=Quaternion.LookRotation(PortSeaward);
            Defense = towerObject.AddComponent<DefenseTower>(); Defense.Initialize(session, this, true);
            SelectionRing=BuildingSelection.CreateRing(this);
            var rallyObject = new GameObject("Punto de reunión"); rallyObject.transform.SetParent(transform, false);
            rallyRing = VisualFactory.Ring(rallyObject.transform, .6f, .09f, new Color(.8f, 1, .5f));
            rallyObject.transform.position = Rally; rallyRing.enabled = false;
            trainingView=BuildingTrainingView.Create(transform,entrance);
        }

        Vector3 ImportedTowerPoint()
        {
            if(IsPort)return ImportedPortLayout.Resolve(transform.position,ClaimPoint).Tower;
            Vector3 away=transform.position-ClaimPoint;away.y=0;away.Normalize();
            Vector3 fallback=transform.position+away*3.8f;
            // Rotate our added tower, keeping both source city and circle XY intact.
            // Independent starting posts must not bombard each other's defenders.
            float clearance=ReforgedProfiles.CapturableTower.Range+1;
            for(int attempt=0;attempt<25;attempt++)
            {
                float angle=attempt==0?0:((attempt+1)/2)*15*(attempt%2==0?-1:1);
                Vector3 spot=transform.position+Quaternion.Euler(0,angle,0)*away*3.8f;
                if(!IsPort&&!MapLayout.IsLand(spot.x,spot.z))continue;
                bool clear=true;
                foreach(var city in MapLayout.Towns)
                {
                    if(city.Id==State.Id)continue;
                    var difference=city.ClaimPoint-spot;difference.y=0;
                    if(difference.sqrMagnitude<clearance*clearance){clear=false;break;}
                }
                if(clear){fallback=spot;break;}
            }
            fallback.y=IsPort?.55f:MapLayout.Height(fallback.x,fallback.z);
            return fallback;
        }

        public string Recruit(UnitKind kind, int team = 0)
        {
            if(IsPort)return "Este puerto sólo recluta Marines.";
            if(!ProductionCatalog.AllowsSettlementUnit(kind))return "Esta ciudad sólo recluta tropas regulares.";
            return QueueRecruit(kind,team);
        }
        // Imported port cities retain one shared land queue. Only their Harbor
        // may enter the Marine catalog through this narrow domain path.
        internal string RecruitPortMarine(UnitKind kind,int team)
        {
            if(!IsPort||!ProductionCatalog.AllowsHarborUnit(kind))return "Este puerto sólo recluta Marines.";
            return QueueRecruit(kind,team);
        }
        string QueueRecruit(UnitKind kind,int team)
        {
            string error=CanManage(team);if(error!=null)return error;
            if(State.Level<BattleRules.RequiredLevel(kind))return "Mejora la ciudad a nivel II para reclutar esta unidad.";
            if(queue.Count>=5)return "La cola está llena. Pulsa un encargo para cancelarlo.";
            if(session.RecruitmentReservations(team)>=BattleRules.PopulationLimit)return "Límite de 100 soldados móviles alcanzado.";
            if(!session.Economy.Spend(team,BattleRules.Cost(kind)))return "Oro insuficiente. Recibirás ingresos al terminar la ronda.";
            queue.Add(new Training{Team=team,Kind=kind,Remaining=BattleRules.TrainTime(kind)});return null;
        }
        string CanManage(int team)
        {
            if (!PlayerRules.IsPlayer(team) || team >= session.PlayerCount) return "Bando inválido.";
            if (session.Winner >= 0) return "La batalla ha terminado.";
            if (session.Paused) return "Reanuda la partida para dar esta orden.";
            if (State.Owner != team) return "Selecciona una ciudad de tu bando.";
            return null;
        }
        internal int PendingRecruits(int team){int count=0;foreach(var order in queue)if(order.Team==team)count++;return count;}
        public UnitKind QueuedKind(int index) => queue[index].Kind;
        public string CancelTraining(int index, int team = 0)
        {
            string error = CanManage(team); if (error != null) return error;
            if (index < 0 || index >= queue.Count) return "Este encargo ya no está en la cola.";
            var item = queue[index]; session.Economy.Refund(item.Team, BattleRules.Cost(item.Kind)); queue.RemoveAt(index);
            return null;
        }
        public string BuildTower(int team = 0)
        {
            string error = CanManage(team); if (error != null) return error;
            if (Defense.IsAlive) return "Esta ciudad ya tiene una torre.";
            return BeginProject(BuildingProject.Tower, team, BattleRules.TowerCost);
        }
        public string Upgrade(int team = 0)
        {
            return "Las ciudades conservan su nivel: compra las unidades directamente.";
        }
        string BeginProject(BuildingProject next, int team, int cost)
        {
            if (Building) return "Ya hay una obra en marcha en esta ciudad.";
            if (!session.Economy.Spend(team, cost)) return "Oro insuficiente para esta obra.";
            project = next; projectOwner = team; projectRemaining = BattleRules.ConstructionSeconds;
            if (next == BuildingProject.Tower) Defense.BeginBuild();
            return null;
        }
        public bool SetRally(Vector3 target)
        {
            if(float.IsNaN(target.x)||float.IsNaN(target.y)||float.IsNaN(target.z)||float.IsInfinity(target.x)||float.IsInfinity(target.y)||float.IsInfinity(target.z)||!NavMesh.SamplePosition(target,out var hit,8,NavMesh.AllAreas))return false;
            Rally=hit.position;rallyRing.transform.parent.position=Rally;return true;
        }
        internal void SetNavalClaimVisual(bool active) => navalClaimVisual=active;
        internal void SetPortNavalTraining(bool active)
        {
            portNavalTraining=active;
            RefreshTrainingView();
        }
        internal void BindImportedPortEntry(Vector3 landEntry, Vector3 outward)
        {
            // Port entry remains a gameplay spawn/rally coordinate. The cue stays on the town art entrance.
        }
        void RefreshTrainingView()
        {
            if(trainingView)trainingView.SetActivity(queue.Count>0,portNavalTraining,session.BattleTime);
        }

        public bool InitializeGarrison(IEnumerable<Soldier> soldiers, ISet<Soldier> assigned)
        {
            if (Defender) return false;
            if (assigned == null) assigned = new HashSet<Soldier>();
            int team = PlayerRules.ToCombatTeam(State.Owner);
            var candidate = soldiers == null ? null : soldiers
                .Where(unit => unit && !unit.IsGarrison && !assigned.Contains(unit) && unit.IsAlive && unit.isActiveAndEnabled && unit.Team == team &&
                               unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh)
                .Where(unit => Vector3.SqrMagnitude(unit.transform.position - transform.position) <= 12f * 12f)
                .OrderBy(unit => Vector3.SqrMagnitude(unit.transform.position - ClaimPoint))
                .FirstOrDefault();
            if (!candidate || !NavMesh.SamplePosition(ClaimPoint, out var hit, .9f, NavMesh.AllAreas)) return false;
            if (!candidate.Agent.Warp(hit.position)) return false;
            candidate.HoldPosition();
            ClaimZone.SetDefender(candidate);
            assigned.Add(candidate);
            return true;
        }

        void Captured()
        {
            foreach (var item in queue) session.Economy.Refund(item.Team, BattleRules.Cost(item.Kind));
            queue.Clear();
            if (Building)
            {
                session.Economy.Refund(projectOwner, project == BuildingProject.Tower ? BattleRules.TowerCost : BattleRules.UpgradeCost);
                project = BuildingProject.None; Defense.CancelBuild();
            }
            flag.sharedMaterial = VisualFactory.Mat(VisualFactory.TeamMaterialColor(State.Owner)); Defense.ChangeOwner();
            foreach(var roof in GetComponentsInChildren<Renderer>())if(roof.name=="Faction roof"&&!roof.GetComponentInParent<DefenseTower>())roof.sharedMaterial=WorldArt.RoofMaterial(State.Owner);
            session.Message((State.Owner < 0 ? "Queda neutral " : State.Owner == 0 ? "Has conquistado " : VisualFactory.TeamName(State.Owner) + " ha conquistado ") + DisplayName);
            if (State.Owner >= 0 && State.Country >= 0 && session.Economy.CountryOwner(State.Country) == State.Owner)
                session.Message((State.Owner==0?"País completado: ":VisualFactory.TeamName(State.Owner)+" completa ") + MapLayout.Countries[State.Country].Name + ". Refuerzos activos.");
        }
        void Update()
        {
            SelectionRing.enabled=Selected;
            rallyRing.enabled = Selected && State.Owner == 0;
            Ring.enabled = !navalClaimVisual;
            Ring.startColor = Ring.endColor = CityClaimZone.VisibleRingColor(State.Contested);
            Ring.widthMultiplier = Selected ? .10f : CityClaimZone.RingWidth;
        }
        public void SimTick(float delta)
        {
            if (session.Paused || session.Winner >= 0) return;
            int previousOwner = State.Owner;
            Soldier previousDefender = Defender;
            int nextOwner = IsPort && Port ? Port.StepClaim(delta) : ClaimZone.Step(session, State.Owner, delta);
            State.Capture = ClaimZone.Progress; State.Capturing = ClaimZone.CapturingTeam; State.Contested = ClaimZone.Contested;
            if (Defender && Defender != previousDefender) Defender.HoldPosition();
            if (nextOwner != previousOwner) { State.Owner = nextOwner; Captured(); }
            if (Building)
            {
                projectRemaining -= delta;
                if (project == BuildingProject.Tower) Defense.SetBuildProgress(ProjectProgress);
                if (projectRemaining <= 0)
                {
                    if (project == BuildingProject.Tower) Defense.CompleteBuild();
                    else { State.Level = 2; VisualFactory.TownUpgrade(transform); }
                    session.Message(DisplayName + ": " + ProjectName + " completada.");
                    project = BuildingProject.None;
                }
            }
            if (queue.Count == 0) { RefreshTrainingView(); return; }
            var first = queue[0]; first.Remaining -= delta;
            if (first.Remaining <= 0)
            {
                // A full population is a temporary cap: retain this paid order until a slot opens.
                if (session.RecruitmentPopulation(first.Team) < BattleRules.PopulationLimit)
                {
                    Vector3 spawn = IsPort && Port ? Port.LandEntry : DefaultLandEntry;
                    var unit = session.SpawnSeparated(first.Team, first.Kind, spawn);
                    if (unit) { queue.RemoveAt(0); unit.MoveTo(Rally, true, false); }
                    else { queue.RemoveAt(0); session.Economy.Refund(first.Team, BattleRules.Cost(first.Kind)); }
                }
            }
            RefreshTrainingView();
        }
    }
}
