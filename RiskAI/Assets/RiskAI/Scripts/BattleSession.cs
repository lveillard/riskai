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
        public static bool ExpandedMapForNewMatch;
        public static VictoryMode ModeForNewMatch = VictoryMode.Conquest;
        public enum StartLayout { RandomCities, RandomCountries, Fixed }
        public static StartLayout LayoutForNewMatch = StartLayout.RandomCities;
        public enum AiDifficulty { Relaxed, Standard }
        public static AiDifficulty DifficultyForNewMatch = AiDifficulty.Relaxed;
        public static int SeedForNewMatch = System.Environment.TickCount & int.MaxValue;
        public int Seed { get; private set; }
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
        public readonly Economy Economy = new Economy();
        public readonly List<Soldier> Units = new List<Soldier>();
        public readonly List<CombatTarget> Targets = new List<CombatTarget>();
        public readonly List<DefenseTower> Towers = new List<DefenseTower>();
        public readonly List<Settlement> Towns = new List<Settlement>();
        public readonly List<CountryCamp> Camps = new List<CountryCamp>();
        public readonly List<string> Messages = new List<string>();
        public readonly int[] Kills = new int[2];
        public readonly float[] VictoryProgress = new float[2];
        public float BattleTime => (float)Clock.Elapsed;
        public SimClock Clock { get; private set; }
        public BattleWorld World { get; private set; }
        public CombatWorld Combat { get; private set; }
        public SpatialTargetIndex Spatial { get; } = new SpatialTargetIndex();
        public SoldierPool SoldierPool { get; private set; }
        public ICommander Commander { get; private set; }
        public BattleCommands Commands { get; private set; }
        public NavalWorld Naval { get; internal set; }
        readonly Dictionary<int, CombatTarget> entities = new Dictionary<int, CombatTarget>(256);
        int nextEntityId;
        System.Action<float> tickWorld;
        public int Winner { get; private set; } = -1;
        public bool Paused { get; private set; }
        public bool AiEnabled = true;
        System.Random combatRandom;
        public float RollDamage(UnitProfile profile)=>profile.RollDamage(combatRandom);
        public bool RollMiss(float probability)=>probability>0 && combatRandom.NextDouble()<probability;

        void Awake() => Initialize();
        public void Initialize()
        {
            if (World != null) return;
            Current = this; Mode = ModeForNewMatch; Layout = LayoutForNewMatch; Difficulty = DifficultyForNewMatch; Seed = SeedForNewMatch;
            combatRandom = new System.Random(Seed ^ 0x2945);
            Clock = new SimClock();
            Combat = new CombatWorld(this);
            SoldierPool = new SoldierPool(this);
            Commander = new SkirmishCommander(this);
            Commands = new BattleCommands(this);
            World = new BattleWorld(this);
            tickWorld = World.Tick;
        }
        public static void NewSeed() { SeedForNewMatch = System.Environment.TickCount & int.MaxValue; }
        public int[] StartingOwners()
        {
            if(Layout == StartLayout.Fixed) return MapLayout.Towns.Select(t=>t.Owner).ToArray();
            return StartingAllocation.Generate(Seed,MapLayout.Towns.Select(t=>t.Country).ToArray(),2,
                Layout == StartLayout.RandomCities ? StartingAllocationMode.IndividualCities : StartingAllocationMode.WholeCountries).CityOwners;
        }
        void OnDestroy() { if (Current == this) Current = null; }
        public int Population(int team)
        {
            int count=0;
            for(int i=0;i<Units.Count;i++) if(Units[i] && Units[i].Team==team) count++;
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
            for (int team = 0; team < 2; team++)
            {
                VictoryProgress[team] = Mode == VictoryMode.Conquest && Towns.Count(t => t.State.Owner == team) >= VictoryTarget
                    ? VictoryProgress[team] + delta : 0;
                int opponent = 1 - team;
                if (VictoryProgress[team] >= BattleRules.VictoryHoldSeconds ||
                    (Population(opponent) == 0 && Towns.All(t => t.State.Owner != opponent)))
                {
                    Winner = team; SuspendMovement(true);
                    Message(team == 0 ? "¡Victoria! Las Marcas son tuyas." : "La Frontera Carmesí controla las Marcas.");
                    break;
                }
            }
        }

        public Soldier Spawn(int team, UnitKind kind, Vector3 position, int originCountry = -1)
        {
            if (!NavMesh.SamplePosition(position, out var hit, 10, NavMesh.AllAreas)) return null;
            var soldier=SoldierPool.Rent(team,kind,hit.position);
            soldier.OriginCountry=originCountry;
            Units.Add(soldier);
            return soldier;
        }

        void CountryReinforcements()
        {
            for(int team=0;team<2;team++) for(int country=0;country<MapLayout.Countries.Length;country++)
            {
                var config = MapLayout.Countries[country];
                if (Economy.CountryOwner(country) != team) continue;
                var cities=Towns.Where(t=>t.State.Country==country && t.State.Owner==team).ToList();
                int pending = Towns.Where(t => t.State.Owner == team).Sum(t => t.QueueCount);
                int current = 0; foreach(var unit in Units)if(unit && unit.Team==team && unit.OriginCountry==country)current+=BattleRules.PointValue(unit.Kind);
                int pointCap=cities.Count*5;
                int amount=Mathf.Min(config.PerTurn,Mathf.Max(0,pointCap-current)/Mathf.Max(1,BattleRules.PointValue(config.Reinforcement)));
                amount = Mathf.Min(amount, Mathf.Max(0, BattleRules.PopulationLimit - Population(team) - pending));
                if(cities.Count==0 || amount<=0) continue;
                for (int i=0; i<amount; i++)
                {
                    var point=country<Camps.Count && Camps[country] ? Camps[country].SpawnPoint : cities[0].Rally;
                    var unit=Spawn(team,config.Reinforcement,point+new Vector3(i%3*1.2f,0,i/3*1.2f),country);
                    if(unit) unit.MoveTo(cities[0].Rally,true,false);
                }
            }
        }

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
