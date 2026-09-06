using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public sealed class BattleSession : MonoBehaviour
    {
        public enum VictoryMode { Conquest }
        public static ScenarioMap MapForNewMatch;
        // Retained for existing scene tests and integrations selecting the two authored arenas.
        public static bool ExpandedMapForNewMatch { get=>MapForNewMatch==ScenarioMap.Riverlands;set=>MapForNewMatch=value?ScenarioMap.Riverlands:ScenarioMap.Classic; }
        public static VictoryMode ModeForNewMatch = VictoryMode.Conquest;
        public enum StartLayout { RandomCities, RandomCountries, Fixed }
        public static StartLayout LayoutForNewMatch = StartLayout.RandomCities;
        public enum AiDifficulty { Relaxed, Standard }
        public static AiDifficulty DifficultyForNewMatch = AiDifficulty.Relaxed;
        public static int SeedForNewMatch = System.Environment.TickCount & int.MaxValue;
        public static int PlayerCountForNewMatch = PlayerRules.MaxPlayers;
        public int Seed { get; private set; }
        public int PlayerCount { get; private set; }
        public StartLayout Layout { get; private set; }
        public string LayoutName => Layout == StartLayout.RandomCities ? "Reparto Risk" : Layout == StartLayout.RandomCountries ? "Países iniciales" : "Escenario de práctica";
        public AiDifficulty Difficulty { get; private set; }
        public string DifficultyName => Difficulty == AiDifficulty.Relaxed ? "Relajado" : "Estándar";
        public float AiInterval => Difficulty == AiDifficulty.Relaxed ? 12f : 7f;
        public float AiFirstRecruitmentTime => Difficulty == AiDifficulty.Relaxed ? 30f : 14f;
        public float AiFirstOffensiveTime => Difficulty == AiDifficulty.Relaxed ? 120f : 35f;
        public float AiFirstNavalOffensiveTime => Difficulty == AiDifficulty.Relaxed ? 120f : 45f;
        public VictoryMode Mode { get; set; }
        public int VictoryTarget => Towns.Count == 0 ? 0 : RiskReferenceRules.CalculateCityCountWin(Towns.Count, .6);
        public static BattleSession Current { get; private set; }
        public Economy Economy { get; private set; }
        public readonly List<Soldier> Units = new List<Soldier>();
        public readonly List<CombatTarget> Targets = new List<CombatTarget>();
        public readonly List<DefenseTower> Towers = new List<DefenseTower>();
        public readonly List<Settlement> Towns = new List<Settlement>();
        public readonly List<CountryCamp> Camps = new List<CountryCamp>();
        public readonly List<string> Messages = new List<string>();
        public int[] Kills { get; private set; }
        public float[] VictoryProgress { get; private set; }
        public float BattleTime => (float)Clock.Elapsed;
        public SimClock Clock { get; private set; }
        public BattleWorld World { get; private set; }
        public CombatWorld Combat { get; private set; }
        public SpatialTargetIndex Spatial { get; } = new SpatialTargetIndex();
        public CanopyOcclusion Canopies { get; } = new CanopyOcclusion();
        public SoldierPool SoldierPool { get; private set; }
        public ICommander Commander { get; private set; }
        public IReadOnlyList<SkirmishCommander> Commanders { get; private set; }
        public BattleCommands Commands { get; private set; }
        public CountryRecruitment Reinforcements { get; private set; }
        public NavalWorld Naval { get; internal set; }
        readonly Dictionary<int, CombatTarget> entities = new Dictionary<int, CombatTarget>(256);
        // Rebuilt once before the ordered victory pass.  Keeping these buffers avoids
        // rescanning every entity for every potential winner on every simulation tick.
        int[] ownedTownCounts;
        bool[] playerPresence;
        int nextEntityId;
        System.Action<float> tickWorld;
        public int Winner { get; private set; } = -1;
        public bool Paused { get; private set; }
        public bool AiEnabled = true;
        System.Random combatRandom;
        public float RollDamage(UnitProfile profile)=>profile.RollDamage(combatRandom);
        public float RollDamage(ShipProfile profile)=>profile.RollDamage(combatRandom);
        public bool RollMiss(float probability)=>probability>0 && combatRandom.NextDouble()<probability;

        void Awake() => Initialize();
        public void Initialize()
        {
            if (World != null) return;
            Current = this; Mode = ModeForNewMatch; Layout = LayoutForNewMatch; Difficulty = DifficultyForNewMatch; Seed = SeedForNewMatch;
            PlayerCount = Mathf.Clamp(PlayerCountForNewMatch, 2, Mathf.Min(PlayerRules.MaxPlayers, Mathf.Max(2, MapLayout.Towns.Length)));
            Economy = new Economy(PlayerCount); Kills = new int[PlayerCount]; VictoryProgress = new float[PlayerCount];
            ownedTownCounts = new int[PlayerCount]; playerPresence = new bool[PlayerCount];
            combatRandom = new System.Random(Seed ^ 0x2945);
            Clock = new SimClock();
            Combat = new CombatWorld(this);
            SoldierPool = new SoldierPool(this);
            var commanders = new CommanderGroup(this, PlayerCount);
            Commanders = commanders.Commanders;
            // Existing integrations cast Commander to SkirmishCommander in the
            // authored two-player match. Keep that surface while multi-player
            // sessions tick the complete commander collection.
            Commander = PlayerCount == 2 ? Commanders[0] : commanders;
            Commands = new BattleCommands(this);
            Reinforcements = new CountryRecruitment(this);
            World = new BattleWorld(this);
            tickWorld = World.Tick;
        }
        public static void NewSeed() { SeedForNewMatch = System.Environment.TickCount & int.MaxValue; }
        public int[] StartingOwners()
        {
            var countries=MapLayout.Towns.Select(t=>t.Country).ToArray();
            if(Layout == StartLayout.Fixed && PlayerCount<=2)return MapLayout.Towns.Select(t=>t.Owner).ToArray();
            bool fallback=Layout == StartLayout.RandomCountries && PlayerCount>MapLayout.Countries.Length;
            var mode=Layout != StartLayout.RandomCountries || fallback
                ? StartingAllocationMode.IndividualCities : StartingAllocationMode.WholeCountries;
            if(fallback)Message("No hay países suficientes para todos: reparto por ciudades.");
            return StartingAllocation.Generate(Seed,countries,PlayerCount,mode).CityOwners;
        }
        void OnDestroy() { if (Current == this) Current = null; }
        public int Population(int team)
        {
            int count=0;
            for(int i=0;i<Units.Count;i++) if(Units[i] && Units[i].Team==team) count++;
            return count;
        }
        public int RecruitmentPopulation(int team)
        {
            int count=0;foreach(var unit in Units)if(unit&&unit.Team==team&&!unit.IsGarrison)count++;
            return count;
        }
        public int RecruitmentReservations(int team)
        {
            int count=RecruitmentPopulation(team);
            foreach(var town in Towns)if(town)count+=town.PendingRecruits(team);
            if(Naval)foreach(var harbor in Naval.Harbors)if(harbor)count+=harbor.PendingLandRecruits(team);
            return count;
        }
        public void RegisterTarget(CombatTarget target)
        {
            if (!target) return;
            if (target.EntityId == 0 || !entities.TryGetValue(target.EntityId, out var current) || current != target)
            { target.EntityId=++nextEntityId; entities.Add(target.EntityId,target); }
            if(!Targets.Contains(target)){Targets.Add(target);Spatial.Add(target);}
        }
        public void UnregisterTarget(CombatTarget target)
        {
            if(!target) return;
            entities.Remove(target.EntityId); Targets.Remove(target);
        }
        public CombatTarget FindTarget(int id) => entities.TryGetValue(id,out var target) && target ? target : null;
        void SuspendMovement(bool suspended)
        {
            for(int i=0;i<Units.Count;i++) if(Units[i]) Units[i].SetSimulationPaused(suspended);
        }
        public void Message(string message) { Messages.Insert(0, message); if (Messages.Count > 5) Messages.RemoveAt(5); }
        public void TogglePause() { if (Winner >= 0) return; Paused = !Paused; SuspendMovement(Paused); }

        void Update()
        {
            Clock.Advance(Time.deltaTime, Paused || Winner >= 0, tickWorld);
        }
        internal void TickRules(float delta)
        {
            if (Paused || Winner >= 0) return;
            if (Economy.Advance(delta) > 0) { Message($"Ronda {Economy.Round} · +{Economy.Income(0)} de oro"); CountryReinforcements(); }
            Reinforcements.Tick(delta);
            BuildVictorySnapshot();
            for (int team = 0; team < PlayerCount; team++)
            {
                VictoryProgress[team] = Mode == VictoryMode.Conquest && ownedTownCounts[team] >= VictoryTarget
                    ? VictoryProgress[team] + delta : 0;
                if (VictoryProgress[team] >= BattleRules.VictoryHoldSeconds || OtherPlayersEliminated(team))
                {
                    Winner = team; SuspendMovement(true);
                    Message(team == 0 ? "¡Victoria! Las Marcas son tuyas." : VisualFactory.TeamName(team) + " ha ganado.");
                    break;
                }
            }
        }
        void BuildVictorySnapshot()
        {
            System.Array.Clear(ownedTownCounts, 0, ownedTownCounts.Length);
            System.Array.Clear(playerPresence, 0, playerPresence.Length);
            for (int i = 0; i < Towns.Count; i++)
            {
                var town = Towns[i];
                if (!town) continue;
                int owner = town.State.Owner;
                if (owner < 0 || owner >= PlayerCount) continue;
                ownedTownCounts[owner]++;
                playerPresence[owner] = true;
            }
            for (int i = 0; i < Units.Count; i++)
            {
                var unit = Units[i];
                if (!unit || !unit.IsAlive || unit.Team < 0 || unit.Team >= PlayerCount) continue;
                playerPresence[unit.Team] = true;
            }
            if (!Naval) return;
            for (int i = 0; i < Naval.Ships.Count; i++)
            {
                var ship = Naval.Ships[i];
                if (!ship || !ship.IsAlive || ship.Team < 0 || ship.Team >= PlayerCount) continue;
                playerPresence[ship.Team] = true;
            }
            for (int i = 0; i < Naval.Harbors.Count; i++)
            {
                var harbor = Naval.Harbors[i];
                if (!harbor || harbor.Owner < 0 || harbor.Owner >= PlayerCount) continue;
                playerPresence[harbor.Owner] = true;
            }
        }
        bool OtherPlayersEliminated(int winner)
        {
            for(int player=0;player<PlayerCount;player++)
            {
                if(player==winner)continue;
                if (playerPresence[player]) return false;
            }
            return true;
        }

        public Soldier Spawn(int team, UnitKind kind, Vector3 position, int originCountry = -1)
        {
            if (!NavMesh.SamplePosition(position, out var hit, 10, NavMesh.AllAreas)) return null;
            var soldier=SoldierPool.Rent(team,kind,hit.position);
            soldier.OriginCountry=originCountry;
            Units.Add(soldier);
            return soldier;
        }

        void CountryReinforcements() => Reinforcements.CreditRound();

        public static void GiveFormation(IReadOnlyList<Soldier> units, Vector3 point, bool attackMove, bool queue, bool patrol=false)
        {
            if(units.Count==0)return;
            var remaining=units.Where(u=>u&&u.Health>0&&!u.IsGarrison).ToList();
            int count=remaining.Count;if(count==0)return;
            Vector3 center=remaining.Aggregate(Vector3.zero,(sum,u)=>sum+u.transform.position)/remaining.Count;
            Vector3 forward=point-center;forward.y=0;forward=forward.sqrMagnitude>.01f?forward.normalized:Vector3.forward;
            Vector3 right=new Vector3(forward.z,0,-forward.x);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int meleeCount = 0;
            foreach (var unit in remaining) if (!BattleRules.Ranged(unit.Kind)) meleeCount++;
            int meleeRows = Mathf.CeilToInt((float)meleeCount / columns);
            int rows = meleeRows + Mathf.CeilToInt((float)(count - meleeCount) / columns);
            for (int i = 0; i < count; i++)
            {
                // Keep a separate front and rear row even for a two-unit squad.
                // Filling one shared row put melee beside ranged, not ahead.
                int groupIndex = i < meleeCount ? i : i - meleeCount;
                int groupCount = i < meleeCount ? meleeCount : count - meleeCount;
                int row = groupIndex / columns + (i < meleeCount ? 0 : meleeRows);
                int rowWidth = Mathf.Min(columns, groupCount - groupIndex / columns * columns);
                var offset = right*(groupIndex % columns - (rowWidth - 1) * .5f)*1.4f + forward*((rows-1)*.5f-row)*1.4f;
                var slot=point+offset;
                int best=0;
                for(int j=1;j<remaining.Count;j++)
                {
                    var a=remaining[j];var b=remaining[best];
                    int rank=(BattleRules.Ranged(a.Kind)?100:0)+(int)a.Kind;
                    int currentRank=(BattleRules.Ranged(b.Kind)?100:0)+(int)b.Kind;
                    if(rank<currentRank || rank==currentRank && (a.transform.position-slot).sqrMagnitude<(b.transform.position-slot).sqrMagnitude)best=j;
                }
                var unit=remaining[best];remaining.RemoveAt(best);
                unit.session.Commands.Submit(new UnitCommand(unit.Team,unit.EntityId,patrol?UnitCommandKind.Patrol:attackMove?UnitCommandKind.AttackMove:UnitCommandKind.Move,slot.x,slot.y,slot.z,append:queue));
            }
        }
    }
}
