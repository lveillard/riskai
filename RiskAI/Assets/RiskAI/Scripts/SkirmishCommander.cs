using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
namespace RiskAI
{
    public interface ICommander { void Tick(float delta); }
    public sealed class CommanderGroup : ICommander
    {
        readonly List<SkirmishCommander> commanders = new List<SkirmishCommander>();
        public IReadOnlyList<SkirmishCommander> Commanders => commanders;
        public CommanderGroup(BattleSession session,int playerCount)
        {
            for(int player=1;player<playerCount;player++)commanders.Add(new SkirmishCommander(session,player));
        }
        public void Tick(float delta){for(int i=0;i<commanders.Count;i++)commanders[i].Tick(delta);}
    }
    /// <summary>
    /// Skirmish AI with the same information and economy as a human player (the map has no fog).
    /// Three cadences: a quick proportional defense pass, a slower strategic pass (recruitment,
    /// army formation, staging and waves) and a difficulty-scaled micro pass for attacking armies.
    /// Pure scoring lives in <see cref="AiCompositionPlanner"/> and <see cref="AiTargetScoring"/>.
    /// </summary>
    public sealed class SkirmishCommander : ICommander
    {
        const float DefenseDecisionSeconds = .75f;
        const float DefenseRadius = 12f;
        const float NavalThreatRadius = 22f;
        const float OpeningRecruitmentWindow = .15f;
        const float OpeningOffensiveWindow = 5f;
        // Stage just outside the 13 m post tower so a wave is assembled before it is fired upon.
        const float StageDistance = 19f;
        const float DirectAttackDistance = StageDistance + 8f;
        const float GatherRadius = 9f;
        const float ClusterRadius = 45f;
        const float ReinforceRadius = 70f;
        const float TargetDefenseRadius = 14f;
        const float AttackTimeout = 100f;
        const float RecoverSeconds = 14f;
        const float AvoidSeconds = 40f;
        const float UnreachableSeconds = 60f;
        const float ReachabilityCell = 30f;
        const int TargetShortlist = 8;
        const float NeutralOpeningSeconds = 100f;
        readonly BattleSession session;
        readonly PlayerBuildingCommands buildingCommands;
        readonly int team;
        readonly Dictionary<int, int> defenseAssignments = new Dictionary<int, int>(32);
        readonly List<DefenseSite> threats = new List<DefenseSite>(32);
        readonly List<CombatTarget> nearby = new List<CombatTarget>(64);
        readonly List<Soldier> defenders = new List<Soldier>(8);
        readonly List<int> staleAssignments = new List<int>(16);
        readonly List<RecruitmentCandidate> recruitmentSites = new List<RecruitmentCandidate>(16);
        readonly List<Soldier> own = new List<Soldier>(64);
        readonly List<Soldier> pool = new List<Soldier>(32);
        readonly List<Soldier> cluster = new List<Soldier>(32);
        readonly List<Soldier> wave = new List<Soldier>(24);
        readonly List<Soldier> melee = new List<Soldier>(16);
        readonly List<Soldier> ranged = new List<Soldier>(16);
        readonly List<TargetCandidate> candidates = new List<TargetCandidate>(TargetShortlist + 1);
        readonly List<Army> armies = new List<Army>(4);
        readonly Stack<Army> spareArmies = new Stack<Army>(4);
        readonly Dictionary<int, Army> armyOf = new Dictionary<int, Army>(64);
        readonly Dictionary<int, float> recovering = new Dictionary<int, float>(16);
        readonly Dictionary<int, float> avoidUntil = new Dictionary<int, float>(16);
        readonly Dictionary<long, float> unreachableUntil = new Dictionary<long, float>(64);
        readonly List<int> scratchKeys = new List<int>(16);
        readonly Dictionary<int,int> countryTotals = new Dictionary<int,int>();
        readonly Dictionary<int,int> countryOwned = new Dictionary<int,int>();
        // Country -> sole owner, or -2 when split/neutral.
        readonly Dictionary<int,int> countryHolder = new Dictionary<int,int>();
        readonly AiForceMix enemyMix = new AiForceMix();
        readonly AiForceMix ownMix = new AiForceMix();
        readonly AiArmyCensus census = new AiArmyCensus();
        readonly NavMeshPath offensivePath = new NavMeshPath();
        readonly float decisionPhase;
        float nextDecision;
        float nextDefenseDecision;
        float nextMicro;
        bool openingDecision = true;
        bool openingOffensivePending = true;
        bool neutralsRemain;
        bool countryMapsFresh;
        int pathBudget;
        public int Team => team;
        public int ArmyCount => armies.Count;
        /// <summary>True while a land wave or a defense assignment owns this soldier.</summary>
        public bool IsCommitted(Soldier unit) => unit && (armyOf.ContainsKey(unit.EntityId) || defenseAssignments.ContainsKey(unit.EntityId));

        public SkirmishCommander(BattleSession battle,int player=1)
        {
            session=battle;
            buildingCommands=new PlayerBuildingCommands(session);
            team=player;
            // Let every AI spend its opening gold immediately, then distribute its
            // recurring work through a deterministic seed/team phase.
            nextDecision=battle.AiFirstRecruitmentTime+SeededPhase(battle.Seed,team,OpeningRecruitmentWindow,0xA341316Cu);
            decisionPhase=SeededPhase(battle.Seed,team,battle.AiInterval,0xC8013EA4u);
            nextDefenseDecision=SeededPhase(battle.Seed,team,DefenseDecisionSeconds,0xAD90777Du);
            nextMicro=SeededPhase(battle.Seed,team,1f,0x5BD1E995u);
        }

        static float SeededPhase(int seed,int team,float interval,uint salt)
        {
            unchecked
            {
                uint state=(uint)seed^salt^(uint)team*0x9E3779B9u;
                state^=state>>16;state*=0x7FEB352Du;state^=state>>15;state*=0x846CA68Bu;state^=state>>16;
                return (state&0x00FFFFFFu)*(interval/16777216f);
            }
        }

        static float AdvanceSchedule(float scheduled,float interval,float now)
        {
            do scheduled+=interval; while(scheduled<=now);
            return scheduled;
        }

        public void Tick(float delta)
        {
            if (!session.AiEnabled || session.Paused || session.Winner >= 0 || session.IsPlayerEliminated(team)) return;
            var profile=session.AiProfile;
            if (session.BattleTime >= nextDefenseDecision)
            {
                nextDefenseDecision=AdvanceSchedule(nextDefenseDecision,DefenseDecisionSeconds,session.BattleTime);
                DecideDefense();
                // Give the first mobile recruit a prompt departure, then return
                // to the normal decision cadence. Bound retries on isolated land.
                if (openingOffensivePending && !openingDecision &&
                    session.BattleTime <= session.AiFirstRecruitmentTime + OpeningOffensiveWindow &&
                    session.BattleTime >= session.AiFirstOffensiveTime)
                    IssueOffensiveOrders(profile.Level==0);
            }
            if (session.BattleTime >= nextMicro)
            {
                nextMicro=AdvanceSchedule(nextMicro,Mathf.Max(.25f,profile.MicroInterval),session.BattleTime);
                Micro(profile);
            }
            if (session.BattleTime < nextDecision) return;
            Decide();
            if (openingDecision)
            {
                openingDecision=false;
                nextDecision=session.AiFirstRecruitmentTime+session.AiInterval+decisionPhase;
                if(nextDecision<=session.BattleTime)nextDecision=session.BattleTime+session.AiInterval;
            }
            else nextDecision=AdvanceSchedule(nextDecision,session.AiInterval,session.BattleTime);
        }

        // ---------------------------------------------------------------- defense

        void DecideDefense()
        {
            var profile=session.AiProfile;
            threats.Clear();
            countryMapsFresh = false;
            foreach (var town in session.Towns)
            {
                if (!town || town.State.Owner != team) continue;
                AddThreat(town.Defense.EntityId, town.ClaimPoint, town.State.Capture, town.State.Contested, town.Defense,
                    town.ClaimZone != null ? town.ClaimZone.Guardian : null, town.State.Country, session.Naval && town.IsPort, profile);
            }
            if (session.Naval)
                foreach (var harbor in session.Naval.Harbors)
                {
                    if (!harbor || harbor.Owner != team) continue;
                    AddThreat(harbor.Defense.EntityId, harbor.Landing, harbor.State.Capture, harbor.State.Contested, harbor.Defense,
                        harbor.ClaimZone != null ? harbor.ClaimZone.Guardian : null, -1, true, profile);
                }
            if (threats.Count == 0) { defenseAssignments.Clear(); return; }
            threats.Sort((a, b) => a.Priority == b.Priority ? a.Key.CompareTo(b.Key) : b.Priority.CompareTo(a.Priority));
            RemoveStaleAssignments();
            RefreshOwn();

            for (int i = 0; i < threats.Count; i++)
            {
                var threat = threats[i];
                float needed = AiTargetScoring.DefenseShortfall(threat.EnemyPower, threat.FriendlyPower, profile.DefenseMargin, threat.Contested || threat.Capture > 0)
                    - AssignedPower(threat.Key);
                float dispatch = profile.DefenseDispatchRadius;
                // Do not feed units one by one into a fight that cannot be won; the
                // strategic pass will retake the post with a staged wave instead.
                if (needed <= 0 || profile.Level > 0 && ReachablePower(threat.Point, dispatch, threat.NavalOnly) + threat.FriendlyPower < threat.EnemyPower * .5f)
                {
                    threat.Covered = true; threats[i] = threat; continue;
                }
                defenders.Clear();
                int already = CountAssignments(threat.Key);
                while (needed > 0 && already < profile.MaximumDefenders)
                {
                    var unit = NearestUnassigned(threat.Point, dispatch, threat.NavalOnly);
                    if (!unit) break;
                    defenseAssignments[unit.EntityId] = threat.Key;
                    RemoveFromArmy(unit);
                    defenders.Add(unit);
                    needed -= Power(unit);
                    already++;
                }
                if (defenders.Count > 0) BattleSession.GiveFormation(defenders, threat.Point, true, false);
                threat.Covered = needed <= 0;
                threats[i] = threat;
            }
        }

        void AddThreat(int key, Vector3 point, float capture, bool contested, DefenseTower tower, CombatTarget guardian, int country, bool coastal, AiDifficultyProfile profile)
        {
            float radius = coastal ? NavalThreatRadius : DefenseRadius;
            session.Spatial.Query(point, radius, nearby);
            float enemyPower = 0, friendlyPower = 0, navalPower = 0, friendlyShips = 0, friendlyRanged = 0;
            int enemies = 0;
            for (int i = 0; i < nearby.Count; i++)
            {
                var target = nearby[i];
                if (!target || !target.IsAlive) continue;
                float distance = FlatDistanceSquared(target.transform.position, point);
                if (target is Soldier unit)
                {
                    if (distance > DefenseRadius * DefenseRadius) continue;
                    if (unit.Team == team)
                    {
                        // Assigned defenders are counted by AssignedPower, not twice once they arrive.
                        if (defenseAssignments.TryGetValue(unit.EntityId, out int assigned) && assigned == key) continue;
                        float value = Power(unit); friendlyPower += value; if (BattleRules.Ranged(unit.Kind)) friendlyRanged += value;
                    }
                    else if (PlayerRules.IsPlayer(unit.Team)) { enemyPower += Power(unit); enemies++; }
                }
                else if (target is Ship ship)
                {
                    if (distance > NavalThreatRadius * NavalThreatRadius || !ship.Profile.CanAttack) continue;
                    float value = AiUnitAnalysis.ShipValue(ship.Profile) * ship.Health / Mathf.Max(1, ship.MaxHealth);
                    if (ship.Team == team) { friendlyPower += value; friendlyShips += value; }
                    else if (PlayerRules.IsPlayer(ship.Team)) { enemyPower += value; navalPower += value; enemies++; }
                }
            }
            bool navalOnly = navalPower > 0 && navalPower >= enemyPower - .01f;
            // A galley outranges both the 13 m post tower and a crossbow garrison:
            // against a pure naval threat only ships and part of the ranged force count.
            if (navalOnly) friendlyPower = friendlyShips + friendlyRanged * .5f;
            else if (TowerActive(tower, guardian)) friendlyPower += AiUnitAnalysis.TowerValue(guardian.Health, null);
            if (enemies == 0 && !contested && capture <= 0) return;
            if (!contested && capture <= 0 && enemyPower * profile.DefenseMargin <= friendlyPower) return;
            float weight = 1 + CountryWeight(country);
            float priority = ((enemyPower - friendlyPower) / 50f + (contested ? 5f : 0) + capture * 4f) * weight;
            threats.Add(new DefenseSite { Key=key, Point=point, EnemyPower=enemyPower, FriendlyPower=friendlyPower, Priority=priority,
                Contested=contested, Capture=capture, NavalOnly=navalOnly });
        }

        // Complete or nearly complete own countries are worth defending first.
        float CountryWeight(int country)
        {
            if (country >= 0 && !countryMapsFresh) BuildCountryMaps();
            if (country < 0 || !countryTotals.TryGetValue(country, out int total) || total <= 0) return 0;
            countryOwned.TryGetValue(country, out int owned);
            if (owned >= total) return 1f + .15f * total;
            return owned >= total - 1 ? .5f : 0;
        }

        void RemoveStaleAssignments()
        {
            staleAssignments.Clear();
            foreach (var assignment in defenseAssignments)
            {
                var unit = session.FindTarget(assignment.Key) as Soldier;
                if (!IsMobileDefender(unit) || !HasThreat(assignment.Value)) staleAssignments.Add(assignment.Key);
            }
            for (int i = 0; i < staleAssignments.Count; i++) defenseAssignments.Remove(staleAssignments[i]);
        }

        bool HasThreat(int key)
        {
            for (int i = 0; i < threats.Count; i++) if (threats[i].Key == key) return true;
            return false;
        }

        int CountAssignments(int key)
        {
            int count=0;
            foreach (var assignment in defenseAssignments) if (assignment.Value == key) count++;
            return count;
        }

        float AssignedPower(int key)
        {
            float power=0;
            foreach (var assignment in defenseAssignments)
                if (assignment.Value == key && session.FindTarget(assignment.Key) is Soldier unit) power += Power(unit);
            return power;
        }

        float ReachablePower(Vector3 point, float radius, bool rangedOnly)
        {
            float power=0;
            for (int i = 0; i < own.Count; i++)
            {
                var unit = own[i];
                if (!IsMobileDefender(unit) || defenseAssignments.ContainsKey(unit.EntityId) || rangedOnly && !BattleRules.Ranged(unit.Kind)) continue;
                if (FlatDistanceSquared(unit.transform.position, point) <= radius * radius) power += Power(unit);
            }
            return power;
        }

        Soldier NearestUnassigned(Vector3 point, float radius, bool rangedOnly)
        {
            Soldier best=null;float distance=float.MaxValue;
            for (int i = 0; i < own.Count; i++)
            {
                var unit = own[i];
                if (!IsMobileDefender(unit) || defenseAssignments.ContainsKey(unit.EntityId)) continue;
                // Melee soldiers cannot reach a galley bombarding from the water.
                if (rangedOnly && !BattleRules.Ranged(unit.Kind)) continue;
                float next=Vector3.SqrMagnitude(unit.transform.position-point);
                if (next > radius * radius) continue;
                if (next<distance || next==distance && (!best || unit.EntityId<best.EntityId)) { best=unit;distance=next; }
            }
            return best;
        }

        bool IsMobileDefender(Soldier unit) => unit && unit.Team == team && unit.IsAlive && !unit.IsGarrison && unit.isActiveAndEnabled && unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh && (!session.Naval || !session.Naval.IsReserved(unit));

        struct DefenseSite
        {
            public int Key;
            public Vector3 Point;
            public float EnemyPower;
            public float FriendlyPower;
            public float Priority;
            public float Capture;
            public bool Contested;
            public bool NavalOnly;
            public bool Covered;
        }

        struct RecruitmentCandidate
        {
            public Settlement Town;
            public Harbor Harbor;
            public float Score;
            public bool Exhausted;
        }

        struct TargetCandidate
        {
            public Settlement Town;
            public int Index;
            public float Value;
            public float Distance;
            public float Defense;
            public float Score;
        }

        sealed class Army
        {
            public readonly List<Soldier> Units = new List<Soldier>(24);
            public Settlement Target;
            public Vector3 Stage;
            public bool Attacking;
            public float StartedAt;
            public float RequiredPower;
        }

        // ---------------------------------------------------------------- strategy

        void Decide()
        {
            var profile = session.AiProfile;
            RefreshOwn();
            BuildCensus();
            Recruit(profile);
            if (session.BattleTime < session.AiFirstOffensiveTime) return;
            IssueOffensiveOrders(profile.Level == 0);
        }

        void RefreshOwn()
        {
            own.Clear();
            for (int i = 0; i < session.Units.Count; i++)
            {
                var unit = session.Units[i];
                if (unit && unit.Team == team && unit.IsAlive && !unit.IsGarrison) own.Add(unit);
            }
        }

        int MobileCount()
        {
            int count = 0;
            for (int i = 0; i < own.Count; i++) if (IsMobileDefender(own[i])) count++;
            return count;
        }

        void BuildCensus()
        {
            census.Clear(); ownMix.Clear(); enemyMix.Clear();
            Vector3 center = Vector3.zero; int owned = 0;
            for (int i = 0; i < own.Count; i++) { census.Add(own[i].Kind); ownMix.Add(own[i].Kind); }
            foreach (var town in session.Towns)
            {
                if (!town || town.State.Owner != team) continue;
                center += town.ClaimPoint; owned++;
                for (int q = 0; q < town.QueueCount; q++) census.Add(town.QueuedKind(q));
            }
            if (session.Naval)
                foreach (var harbor in session.Naval.Harbors)
                    if (harbor && harbor.Owner == team && !harbor.IsImportedPort)
                        for (int q = 0; q < harbor.LandQueueCount; q++) census.Add(harbor.QueuedLandKind(q));
            if (owned > 0) center /= owned;
            // The whole battlefield is visible. Weight nearby armies more, so a
            // 16-player match counters its neighbours rather than the far side.
            for (int i = 0; i < session.Units.Count; i++)
            {
                var unit = session.Units[i];
                if (!unit || !unit.IsAlive || unit.Team == team || !PlayerRules.IsPlayer(unit.Team)) continue;
                float distance = Mathf.Sqrt(FlatDistanceSquared(unit.transform.position, center));
                enemyMix.Add(unit.Kind, (unit.IsGarrison ? .3f : 1f) / (1 + distance / 60f));
            }
        }

        void BuildCountryMaps()
        {
            countryMapsFresh = true;
            neutralsRemain = false;
            countryTotals.Clear(); countryOwned.Clear(); countryHolder.Clear();
            foreach (var town in session.Towns)
            {
                if (!town) continue;
                int owner = town.State.Owner;
                if (owner < 0) neutralsRemain = true;
                int country = town.State.Country;
                if (country < 0) continue;
                countryTotals[country] = countryTotals.TryGetValue(country, out int total) ? total + 1 : 1;
                if (owner == team) countryOwned[country] = countryOwned.TryGetValue(country, out int mine) ? mine + 1 : 1;
                if (!countryHolder.TryGetValue(country, out int holder)) countryHolder[country] = owner >= 0 ? owner : -2;
                else if (holder != owner) countryHolder[country] = -2;
            }
        }

        // ---------------------------------------------------------------- recruitment

        void Recruit(AiDifficultyProfile profile)
        {
            int reservations = session.RecruitmentReservations(team);
            if (reservations >= BattleRules.PopulationLimit) return;
            int mobile = MobileCount();
            int navalBudget = threats.Count == 0 && mobile >= 2 && session.Naval
                ? session.Naval.FirstFleetSavingsTargetFor(team) : 0;
            int gold = session.Economy.Gold[team];
            int purchases = profile.PurchasesPerDecision + (profile.BonusPurchaseGold > 0 && gold >= profile.BonusPurchaseGold ? 1 : 0);
            int income = session.Economy.Income(team);
            bool urgent = false;
            for (int i = 0; i < threats.Count; i++) if (!threats[i].Covered) urgent = true;
            RefreshRecruitmentSites();
            for (int i = 0; i < purchases && reservations < BattleRules.PopulationLimit; i++)
            {
                int site = RecruitmentSite();
                if (site < 0) break;
                var candidate = recruitmentSites[site];
                var options = candidate.Harbor ? ProductionCatalog.HarborUnits : ProductionCatalog.SettlementUnits;
                int level = candidate.Harbor ? candidate.Harbor.State.Level : candidate.Town.State.Level;
                var decision = AiCompositionPlanner.Choose(options, census, enemyMix, session.Economy.Gold[team] - navalBudget, income, level,
                    urgent, profile.CounterWeight, true);
                if (decision.Save) break;
                if (!decision.Buy) { candidate.Exhausted = true; recruitmentSites[site] = candidate; i--; continue; }
                var building = candidate.Harbor ? candidate.Harbor.BuildingId : candidate.Town.BuildingId;
                if (buildingCommands.Execute(team, PlayerBuildingIntent.Recruit(building, decision.Kind)) != null)
                {
                    candidate.Exhausted = true; recruitmentSites[site] = candidate; i--; continue;
                }
                census.Add(decision.Kind);
                reservations++;
            }
        }

        void RefreshRecruitmentSites()
        {
            recruitmentSites.Clear();
            Vector3 stage = default; bool hasStage = false;
            for (int i = 0; i < armies.Count; i++) if (!armies[i].Attacking) { stage = armies[i].Stage; hasStage = true; break; }
            foreach(var town in session.Towns)
            {
                if(!town || town.IsPort || town.State.Owner!=team)continue;
                recruitmentSites.Add(new RecruitmentCandidate { Town=town, Score=SiteScore(town.ClaimPoint, town.Defense ? town.Defense.EntityId : 0, stage, hasStage) });
            }
            if (session.Naval)
                foreach (var harbor in session.Naval.Harbors)
                {
                    if (!harbor || harbor.Owner != team) continue;
                    // Marines are a supplementary harbor roster; bias towards cities
                    // so that the regular composition planner drives most purchases.
                    recruitmentSites.Add(new RecruitmentCandidate { Harbor=harbor, Score=SiteScore(harbor.Landing, harbor.Defense ? harbor.Defense.EntityId : 0, stage, hasStage) + 15f });
                }
        }

        float SiteScore(Vector3 point, int defenseKey, Vector3 stage, bool hasStage)
        {
            float frontier = 10000;
            foreach (var other in session.Towns)
                if (other && other.State.Owner != team)
                    frontier = Mathf.Min(frontier, Mathf.Sqrt(FlatDistanceSquared(point, other.ClaimPoint)));
            float score = frontier;
            if (hasStage) score = frontier * .6f + Mathf.Sqrt(FlatDistanceSquared(point, stage)) * .4f;
            // Replenish threatened posts first.
            return HasThreat(defenseKey) ? score - 1000 : score;
        }

        int RecruitmentSite()
        {
            int best = -1;
            float bestScore = float.PositiveInfinity;
            for(int i=0;i<recruitmentSites.Count;i++)
            {
                var candidate=recruitmentSites[i];
                if (candidate.Exhausted) continue;
                int queue = candidate.Harbor ? (candidate.Harbor ? candidate.Harbor.LandQueueCount : 99) : (candidate.Town ? candidate.Town.QueueCount : 99);
                if (queue >= 2) continue;
                // Stable site order breaks ties without consuming combat RNG.
                float score = candidate.Score + queue * 20;
                if (score < bestScore) { best = i; bestScore = score; }
            }
            return best;
        }

        // ---------------------------------------------------------------- offense

        void IssueOffensiveOrders(bool relaxed)
        {
            var profile = session.AiProfile;
            RefreshOwn();
            BuildCountryMaps();
            pathBudget = profile.PathBudget;
            UpdateArmies(profile);
            BuildPool();
            ReinforceArmies();
            FormArmies(profile, relaxed);
        }

        void BuildPool()
        {
            pool.Clear();
            scratchKeys.Clear();
            foreach (var entry in recovering)
            {
                var unit = session.FindTarget(entry.Key) as Soldier;
                if (!unit || !unit.IsAlive || session.BattleTime >= entry.Value || unit.Health >= unit.MaxHealth * .7f) scratchKeys.Add(entry.Key);
            }
            for (int i = 0; i < scratchKeys.Count; i++) recovering.Remove(scratchKeys[i]);
            for (int i = 0; i < own.Count; i++)
            {
                var unit = own[i];
                if (!IsMobileDefender(unit) || (!unit.IsIdle && !unit.IsHolding)) continue;
                if (defenseAssignments.ContainsKey(unit.EntityId) || armyOf.ContainsKey(unit.EntityId) || recovering.ContainsKey(unit.EntityId)) continue;
                // Keep local troops beside a post whose defense is still short.
                if (NearUncoveredThreat(unit.transform.position)) continue;
                pool.Add(unit);
            }
        }

        bool NearUncoveredThreat(Vector3 point)
        {
            for (int i = 0; i < threats.Count; i++)
                if (!threats[i].Covered && FlatDistanceSquared(threats[i].Point, point) <= DefenseRadius * DefenseRadius * 4) return true;
            return false;
        }

        void UpdateArmies(AiDifficultyProfile profile)
        {
            for (int i = armies.Count - 1; i >= 0; i--)
            {
                var army = armies[i];
                Prune(army);
                if (army.Units.Count == 0 || !army.Target) { ReleaseArmy(i); continue; }
                if (army.Target.State.Owner == team)
                {
                    if (!Retarget(army, profile)) ReleaseArmy(i);
                    continue;
                }
                float power = ArmyPower(army);
                if (!army.Attacking)
                {
                    float gatheredPower = 0; int gathered = 0;
                    wave.Clear();
                    for (int u = 0; u < army.Units.Count; u++)
                    {
                        var unit = army.Units[u];
                        if (FlatDistanceSquared(unit.transform.position, army.Stage) <= GatherRadius * GatherRadius * 2.25f) { gathered++; gatheredPower += Power(unit); }
                        else if (unit.IsIdle || unit.IsHolding) wave.Add(unit);
                    }
                    float defense = TargetDefense(army.Target);
                    army.RequiredPower = defense * profile.AttackMargin;
                    var decision = AiTargetScoring.Wave(gatheredPower, army.RequiredPower, gathered, army.Units.Count, session.BattleTime - army.StartedAt, profile.GatherTimeout);
                    if (decision == AiWaveDecision.Launch) Launch(army, profile);
                    else if (decision == AiWaveDecision.Abort) { avoidUntil[army.Target.GetInstanceID()] = session.BattleTime + AvoidSeconds; ReleaseArmy(i); }
                    else if (wave.Count > 0) BattleSession.GiveFormation(wave, army.Stage, true, false);
                }
                else
                {
                    float defense = TargetDefense(army.Target);
                    if (AiTargetScoring.ShouldRetreat(power, defense, profile.ArmyRetreatRatio) &&
                        FlatDistanceSquared(ArmyCenter(army), army.Target.ClaimPoint) <= 25f * 25f)
                    {
                        avoidUntil[army.Target.GetInstanceID()] = session.BattleTime + AvoidSeconds;
                        Retreat(army);
                        ReleaseArmy(i);
                        continue;
                    }
                    if (session.BattleTime - army.StartedAt > AttackTimeout) { ReleaseArmy(i); continue; }
                    // Members that finished their attack-move without taking the
                    // post (for example, the guardian moved) resume the assault.
                    wave.Clear();
                    for (int u = 0; u < army.Units.Count; u++) if (army.Units[u].IsIdle || army.Units[u].IsHolding) wave.Add(army.Units[u]);
                    if (wave.Count > 0) BattleSession.GiveFormation(wave, army.Target.ClaimPoint, true, false);
                }
            }
        }

        void Prune(Army army)
        {
            for (int u = army.Units.Count - 1; u >= 0; u--)
            {
                var unit = army.Units[u];
                if (IsMobileDefender(unit) && !defenseAssignments.ContainsKey(unit.EntityId)) continue;
                if (unit) armyOf.Remove(unit.EntityId);
                army.Units.RemoveAt(u);
            }
        }

        bool Retarget(Army army, AiDifficultyProfile profile)
        {
            // Momentum: a victorious wave picks its next objective from where it stands.
            Vector3 center = ArmyCenter(army);
            float power = ArmyPower(army);
            RankTargets(center, power, power, profile);
            var lead = army.Units[0];
            for (int c = 0; c < candidates.Count && pathBudget > 0; c++)
            {
                var candidate = candidates[c];
                if (IsUnreachable(candidate.Index, center)) continue;
                pathBudget--;
                if (!CanReachOffensiveTarget(lead, candidate.Town.ClaimPoint)) { MarkUnreachable(candidate.Index, center); continue; }
                army.Target = candidate.Town;
                army.RequiredPower = candidate.Defense * profile.AttackMargin;
                BeginApproach(army, center, candidate.Defense, profile);
                return true;
            }
            return false;
        }

        void BeginApproach(Army army, Vector3 center, float defense, AiDifficultyProfile profile)
        {
            army.StartedAt = session.BattleTime;
            var claim = army.Target.ClaimPoint;
            bool direct = defense <= 1 || FlatDistanceSquared(center, claim) <= DirectAttackDistance * DirectAttackDistance;
            if (direct) { Launch(army, profile); return; }
            army.Attacking = false;
            army.Stage = StagePoint(army.Target, center);
            BattleSession.GiveFormation(army.Units, army.Stage, true, false);
        }

        Vector3 StagePoint(Settlement town, Vector3 from)
        {
            var claim = town.ClaimPoint;
            var direction = from - claim; direction.y = 0;
            if (direction.sqrMagnitude < .01f) { direction = town.Rally - claim; direction.y = 0; }
            if (direction.sqrMagnitude < .01f) direction = Vector3.back;
            var stage = claim + direction.normalized * StageDistance;
            return NavMesh.SamplePosition(stage, out var hit, 6f, NavMesh.AllAreas) ? hit.position : Vector3.Lerp(from, claim, .5f);
        }

        void Launch(Army army, AiDifficultyProfile profile)
        {
            army.Attacking = true;
            army.StartedAt = session.BattleTime;
            var claim = army.Target.ClaimPoint;
            melee.Clear(); ranged.Clear();
            float range = 0;
            for (int u = 0; u < army.Units.Count; u++)
            {
                var unit = army.Units[u];
                if (BattleRules.Ranged(unit.Kind)) { ranged.Add(unit); range += BattleRules.Range(unit.Kind); }
                else melee.Add(unit);
            }
            if (profile.RangedStandoff && melee.Count > 0 && ranged.Count > 0)
            {
                // Swordsmen lead into the post; ranged units stop short so the tower
                // (which fires at the nearest attacker) targets the heavy frontline.
                var back = ArmyCenter(army) - claim; back.y = 0;
                back = back.sqrMagnitude > .01f ? back.normalized : Vector3.back;
                float standoff = Mathf.Clamp(range / ranged.Count * .7f, 2f, 6f);
                BattleSession.GiveFormation(melee, claim, true, false);
                BattleSession.GiveFormation(ranged, claim + back * standoff, true, false);
            }
            else BattleSession.GiveFormation(army.Units, claim, true, false);
            openingOffensivePending = false;
        }

        void Retreat(Army army)
        {
            var center = ArmyCenter(army);
            var home = NearestOwnRally(center);
            if (!home.HasValue) return;
            for (int u = 0; u < army.Units.Count; u++)
            {
                var unit = army.Units[u];
                session.Commands.Submit(new UnitCommand(team, unit.EntityId, UnitCommandKind.Move, home.Value.x, home.Value.y, home.Value.z));
                recovering[unit.EntityId] = session.BattleTime + RecoverSeconds;
            }
        }

        Vector3? NearestOwnRally(Vector3 point)
        {
            Vector3? best = null; float distance = float.MaxValue;
            foreach (var town in session.Towns)
            {
                if (!town || town.State.Owner != team) continue;
                float next = FlatDistanceSquared(town.Rally, point);
                if (next < distance) { distance = next; best = town.Rally; }
            }
            return best;
        }

        void ReinforceArmies()
        {
            if (armies.Count == 0) return;
            for (int a = 0; a < armies.Count; a++)
            {
                var army = armies[a];
                wave.Clear();
                var point = army.Attacking ? army.Target.ClaimPoint : army.Stage;
                // Attacking waves only accept fresh troops that are already close,
                // otherwise late recruits would trickle into the tower one by one.
                float radius = army.Attacking ? 30f : ReinforceRadius;
                for (int i = pool.Count - 1; i >= 0 && pathBudget > 0; i--)
                {
                    var unit = pool[i];
                    if (FlatDistanceSquared(unit.transform.position, point) > radius * radius) continue;
                    pathBudget--;
                    if (!CanReachOffensiveTarget(unit, point)) continue;
                    wave.Add(unit); army.Units.Add(unit); armyOf[unit.EntityId] = army; pool.RemoveAt(i);
                }
                if (wave.Count > 0) BattleSession.GiveFormation(wave, point, true, false);
            }
        }

        void FormArmies(AiDifficultyProfile profile, bool relaxed)
        {
            int guard = 0;
            while (armies.Count < profile.MaximumArmies && pool.Count > 0 && pathBudget > 0 && guard++ < 8)
            {
                int minimumWave = openingOffensivePending ? 1 : relaxed ? Mathf.Min(2, profile.MinimumWave) : profile.MinimumWave;
                if (pool.Count < minimumWave) return;
                BuildCluster();
                // The seed is the densest group: if it is too small, so is every other.
                if (cluster.Count < minimumWave) return;
                Vector3 center = Centroid(cluster);
                float clusterPower = 0, poolPower = 0;
                for (int i = 0; i < cluster.Count; i++) clusterPower += Power(cluster[i]);
                for (int i = 0; i < pool.Count; i++) poolPower += Power(pool[i]);
                RankTargets(center, clusterPower, clusterPower + poolPower * .5f, profile);
                bool formed = false;
                for (int c = 0; c < candidates.Count && pathBudget > 0 && !formed; c++)
                {
                    var candidate = candidates[c];
                    // A second army spreads to another objective instead of queueing behind the first.
                    if (IsUnreachable(candidate.Index, center) || IsTargeted(candidate.Town)) continue;
                    var claim = candidate.Town.ClaimPoint;
                    bool direct = candidate.Defense <= 1 || FlatDistanceSquared(center, claim) <= DirectAttackDistance * DirectAttackDistance;
                    var destination = direct ? claim : StagePoint(candidate.Town, center);
                    SortByDistance(cluster, destination);
                    wave.Clear();
                    float required = candidate.Defense * profile.AttackMargin, wavePower = 0;
                    int failures = 0;
                    bool sizeWave = armies.Count + 1 < profile.MaximumArmies;
                    for (int i = 0; i < cluster.Count && wave.Count < profile.MaximumWave && pathBudget > 0; i++)
                    {
                        // Size the wave to the objective; the remainder stays free for another army.
                        if (sizeWave && wave.Count >= minimumWave && required > 1 && wavePower >= required * 1.6f) break;
                        pathBudget--;
                        var unit = cluster[i];
                        if (!CanReachOffensiveTarget(unit, claim)) { if (++failures >= 2 && wave.Count == 0) break; continue; }
                        wave.Add(unit); wavePower += Power(unit);
                    }
                    if (wave.Count == 0 && failures >= 2) MarkUnreachable(candidate.Index, center);
                    if (wave.Count < minimumWave) continue;
                    var army = RentArmy();
                    army.Target = candidate.Town;
                    army.RequiredPower = required;
                    for (int i = 0; i < wave.Count; i++) { army.Units.Add(wave[i]); armyOf[wave[i].EntityId] = army; pool.Remove(wave[i]); }
                    armies.Add(army);
                    BeginApproach(army, center, candidate.Defense, profile);
                    openingOffensivePending = false;
                    formed = true;
                }
                if (!formed) for (int i = 0; i < cluster.Count; i++) pool.Remove(cluster[i]);
            }
        }

        bool IsTargeted(Settlement town)
        {
            for (int a = 0; a < armies.Count; a++) if (armies[a].Target == town) return true;
            return false;
        }

        void BuildCluster()
        {
            cluster.Clear();
            // Seed the densest group first so the main force, not a straggler,
            // claims the first wave. Sampling bounds the quadratic work.
            Soldier seed = null; int best = -1;
            int step = Mathf.Max(1, pool.Count / 24);
            for (int i = 0; i < pool.Count; i += step)
            {
                var probe = pool[i].transform.position; int neighbours = 0;
                for (int j = 0; j < pool.Count; j++)
                    if (FlatDistanceSquared(pool[j].transform.position, probe) <= ClusterRadius * ClusterRadius) neighbours++;
                if (neighbours > best) { best = neighbours; seed = pool[i]; }
            }
            if (!seed) return;
            var origin = seed.transform.position;
            for (int i = 0; i < pool.Count; i++)
                if (FlatDistanceSquared(pool[i].transform.position, origin) <= ClusterRadius * ClusterRadius) cluster.Add(pool[i]);
        }

        void RankTargets(Vector3 center, float committedPower, float availablePower, AiDifficultyProfile profile)
        {
            candidates.Clear();
            bool early = session.BattleTime <= NeutralOpeningSeconds && neutralsRemain;
            for (int index = 0; index < session.Towns.Count; index++)
            {
                var town = session.Towns[index];
                if (!town || town.State.Owner == team) continue;
                if (avoidUntil.TryGetValue(town.GetInstanceID(), out float until) && until > session.BattleTime) continue;
                int owner = town.State.Owner, country = town.State.Country;
                int total = country >= 0 && countryTotals.TryGetValue(country, out int foundTotal) ? foundTotal : 0;
                int owned = country >= 0 && countryOwned.TryGetValue(country, out int foundOwned) ? foundOwned : 0;
                bool breaks = owner >= 0 && country >= 0 && countryHolder.TryGetValue(country, out int holder) && holder == owner;
                float value = AiTargetScoring.CityValue(total, owned, breaks, owner < 0);
                // The opening grabs free neutral posts before provoking a neighbour.
                if (early && owner >= 0) value *= .25f;
                float distance = Mathf.Sqrt(FlatDistanceSquared(center, town.ClaimPoint));
                float score = AiTargetScoring.Score(value, distance, 1);
                if (candidates.Count >= TargetShortlist && score <= candidates[candidates.Count - 1].Score) continue;
                int insert = candidates.Count;
                while (insert > 0 && candidates[insert - 1].Score < score) insert--;
                candidates.Insert(insert, new TargetCandidate { Town=town, Index=index, Value=value, Distance=distance, Score=score });
                if (candidates.Count > TargetShortlist) candidates.RemoveAt(candidates.Count - 1);
            }
            // Only the shortlist pays for a spatial defense estimate.
            for (int c = 0; c < candidates.Count; c++)
            {
                var candidate = candidates[c];
                candidate.Defense = TargetDefense(candidate.Town);
                candidate.Score = AiTargetScoring.Score(candidate.Value, candidate.Distance,
                    AiTargetScoring.Feasibility(Mathf.Max(committedPower, availablePower), candidate.Defense, profile.AttackMargin));
                candidates[c] = candidate;
            }
            candidates.Sort((a, b) => a.Score == b.Score ? a.Index.CompareTo(b.Index) : b.Score.CompareTo(a.Score));
        }

        float TargetDefense(Settlement town)
        {
            if (!town) return 0;
            var claim = town.ClaimPoint;
            session.Spatial.Query(claim, NavalThreatRadius, nearby);
            float power = 0;
            for (int i = 0; i < nearby.Count; i++)
            {
                var target = nearby[i];
                if (!target || !target.IsAlive || target.Team == team) continue;
                float distance = FlatDistanceSquared(target.transform.position, claim);
                if (target is Soldier unit)
                {
                    if (distance <= TargetDefenseRadius * TargetDefenseRadius) power += Power(unit);
                }
                else if (target is Ship ship && ship.Profile.CanAttack && distance <= NavalThreatRadius * NavalThreatRadius)
                    power += AiUnitAnalysis.ShipValue(ship.Profile) * ship.Health / Mathf.Max(1, ship.MaxHealth);
            }
            var guardian = town.ClaimZone != null ? town.ClaimZone.Guardian : null;
            if (TowerActive(town.Defense, guardian)) power += AiUnitAnalysis.TowerValue(guardian.Health, ownMix);
            return power;
        }

        static bool TowerActive(DefenseTower tower, CombatTarget guardian) =>
            tower && tower.isActiveAndEnabled && tower.IsAlive && !tower.UnderConstruction && guardian && guardian.IsAlive;

        // ---------------------------------------------------------------- micro

        void Micro(AiDifficultyProfile profile)
        {
            for (int a = 0; a < armies.Count; a++)
            {
                var army = armies[a];
                if (!army.Attacking || !army.Target) continue;
                var claim = army.Target.ClaimPoint;
                var guardian = army.Target.ClaimZone != null ? army.Target.ClaimZone.Guardian : null;
                bool tower = TowerActive(army.Target.Defense, guardian);
                if (profile.RetreatHealthFraction > 0 && army.Units.Count > 2)
                    for (int u = army.Units.Count - 1; u >= 0 && army.Units.Count > 2; u--)
                    {
                        var unit = army.Units[u];
                        if (!unit || !unit.IsAlive || unit.Health >= unit.MaxHealth * profile.RetreatHealthFraction) continue;
                        if (!tower && !unit.CurrentTarget) continue;
                        if (FlatDistanceSquared(unit.transform.position, claim) > 22f * 22f) continue;
                        var point = RetreatPoint(army, unit, claim);
                        session.Commands.Submit(new UnitCommand(team, unit.EntityId, UnitCommandKind.Move, point.x, point.y, point.z));
                        recovering[unit.EntityId] = session.BattleTime + RecoverSeconds;
                        armyOf.Remove(unit.EntityId);
                        army.Units.RemoveAt(u);
                    }
                if (!(guardian is Soldier keeper) || keeper.Team == team) continue;
                // Troops that stopped after a guardian kill resume the assault at once
                // while the post still has a living defender.
                wave.Clear();
                for (int u = 0; u < army.Units.Count; u++) if (army.Units[u].IsIdle) wave.Add(army.Units[u]);
                if (wave.Count > 0) BattleSession.GiveFormation(wave, claim, true, false);
                // Killing the guardian silences the tower and opens the post.
                if (!profile.FocusGuardian) continue;
                if (FlatDistanceSquared(ArmyCenter(army), claim) > 18f * 18f) continue;
                float armyPower = ArmyPower(army), others = 0;
                session.Spatial.Query(claim, TargetDefenseRadius, nearby);
                for (int i = 0; i < nearby.Count; i++)
                    if (nearby[i] is Soldier enemy && enemy != keeper && enemy.IsAlive && enemy.Team != team &&
                        FlatDistanceSquared(enemy.transform.position, claim) <= TargetDefenseRadius * TargetDefenseRadius) others += Power(enemy);
                if (others > armyPower * .35f) continue;
                for (int u = 0; u < army.Units.Count; u++)
                {
                    var unit = army.Units[u];
                    if (unit.CurrentTarget == keeper || AiUnitAnalysis.For(unit.Kind).Healer) continue;
                    bool rangedUnit = BattleRules.Ranged(unit.Kind);
                    if (!rangedUnit && (profile.Level < 2 || unit.CurrentTarget)) continue;
                    if (FlatDistanceSquared(unit.transform.position, claim) > 18f * 18f) continue;
                    session.Commands.Submit(new UnitCommand(team, unit.EntityId, UnitCommandKind.Attack, targetId: keeper.EntityId));
                }
            }
        }

        Vector3 RetreatPoint(Army army, Soldier unit, Vector3 claim)
        {
            // Prefer a medic of the same wave standing outside the tower's reach.
            Soldier medic = null; float best = float.MaxValue;
            for (int u = 0; u < army.Units.Count; u++)
            {
                var other = army.Units[u];
                if (other == unit || !AiUnitAnalysis.For(other.Kind).Healer) continue;
                if (FlatDistanceSquared(other.transform.position, claim) < 14f * 14f) continue;
                float next = FlatDistanceSquared(other.transform.position, unit.transform.position);
                if (next < best) { best = next; medic = other; }
            }
            if (medic) return medic.transform.position;
            var back = unit.transform.position - claim; back.y = 0;
            back = back.sqrMagnitude > .01f ? back.normalized : Vector3.back;
            var point = claim + back * StageDistance;
            if (NavMesh.SamplePosition(point, out var hit, 6f, NavMesh.AllAreas)) return hit.position;
            var home = NearestOwnRally(unit.transform.position);
            return home ?? unit.transform.position;
        }

        // ---------------------------------------------------------------- helpers

        Army RentArmy()
        {
            var army = spareArmies.Count > 0 ? spareArmies.Pop() : new Army();
            army.Units.Clear(); army.Target = null; army.Attacking = false; army.StartedAt = session.BattleTime; army.RequiredPower = 0;
            return army;
        }

        void ReleaseArmy(int index)
        {
            var army = armies[index];
            for (int u = 0; u < army.Units.Count; u++) if (army.Units[u]) armyOf.Remove(army.Units[u].EntityId);
            army.Units.Clear(); army.Target = null;
            armies.RemoveAt(index);
            spareArmies.Push(army);
        }

        void RemoveFromArmy(Soldier unit)
        {
            if (!unit || !armyOf.TryGetValue(unit.EntityId, out var army)) return;
            army.Units.Remove(unit);
            armyOf.Remove(unit.EntityId);
        }

        float ArmyPower(Army army)
        {
            float power = 0;
            for (int u = 0; u < army.Units.Count; u++) power += Power(army.Units[u]);
            return power;
        }

        static Vector3 ArmyCenter(Army army) => Centroid(army.Units);

        static Vector3 Centroid(List<Soldier> units)
        {
            if (units.Count == 0) return Vector3.zero;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < units.Count; i++) center += units[i].transform.position;
            return center / units.Count;
        }

        static float Power(Soldier unit)
        {
            if (!unit || !unit.IsAlive) return 0;
            return AiUnitAnalysis.For(unit.Kind).Value * Mathf.Clamp01(unit.Health / Mathf.Max(1, unit.MaxHealth));
        }

        static void SortByDistance(List<Soldier> units, Vector3 point)
        {
            // Insertion sort: clusters are small and this avoids a comparer allocation.
            for (int i = 1; i < units.Count; i++)
            {
                var unit = units[i]; float distance = FlatDistanceSquared(unit.transform.position, point);
                int j = i - 1;
                while (j >= 0 && FlatDistanceSquared(units[j].transform.position, point) > distance) { units[j + 1] = units[j]; j--; }
                units[j + 1] = unit;
            }
        }

        long ReachabilityKey(int townIndex, Vector3 from)
        {
            int x = Mathf.FloorToInt(from.x / ReachabilityCell), z = Mathf.FloorToInt(from.z / ReachabilityCell);
            unchecked { return ((long)townIndex << 32) ^ (uint)(x * 73856093 ^ z * 19349663); }
        }

        bool IsUnreachable(int townIndex, Vector3 from) =>
            unreachableUntil.TryGetValue(ReachabilityKey(townIndex, from), out float until) && until > session.BattleTime;

        void MarkUnreachable(int townIndex, Vector3 from)
        {
            if (unreachableUntil.Count > 512) unreachableUntil.Clear();
            unreachableUntil[ReachabilityKey(townIndex, from)] = session.BattleTime + UnreachableSeconds;
        }

        public static int CountryCompletionTier(int total,int owned)
        {
            if(total<=0||owned<=0)return 2;
            return total-owned<=1?0:1;
        }

        bool CanReachOffensiveTarget(Soldier unit,Vector3 point)
        {
            if(!unit||!unit.Agent||!unit.Agent.enabled||!unit.Agent.isOnNavMesh)return false;
            return NavMesh.CalculatePath(unit.transform.position,point,NavMesh.AllAreas,offensivePath)&&
                offensivePath.status==NavMeshPathStatus.PathComplete;
        }

        static float FlatDistanceSquared(Vector3 a, Vector3 b) { float x = a.x - b.x, z = a.z - b.z; return x * x + z * z; }
    }
}
