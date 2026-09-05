using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public sealed class BattleSession : MonoBehaviour
    {
        public enum VictoryMode { Conquest, Capitals }
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
        public readonly List<string> Messages = new List<string>();
        public readonly int[] Kills = new int[2];
        public readonly float[] VictoryProgress = new float[2];
        public float BattleTime { get; private set; }
        public int Winner { get; private set; } = -1;
        public bool Paused { get; private set; }
        public bool AiEnabled = true;
        float nextAi;
        System.Random combatRandom;
        public float RollDamage(UnitProfile profile)=>profile.RollDamage(combatRandom);

        void Awake()
        {
            Current = this; Mode = ModeForNewMatch; Layout = LayoutForNewMatch; Difficulty = DifficultyForNewMatch; Seed = SeedForNewMatch;
            combatRandom=new System.Random(Seed ^ 0x2945); nextAi = AiFirstRecruitmentTime; Time.timeScale = 1;
        }
        public static void NewSeed() { SeedForNewMatch = System.Environment.TickCount & int.MaxValue; }
        public int[] StartingOwners()
        {
            if(Layout == StartLayout.Fixed) return MapLayout.Towns.Select(t=>t.Owner).ToArray();
            return StartingAllocation.Generate(Seed,MapLayout.Towns.Select(t=>t.Country).ToArray(),2,
                Layout == StartLayout.RandomCities ? StartingAllocationMode.IndividualCities : StartingAllocationMode.WholeCountries).CityOwners;
        }
        void OnDestroy() { if (Current == this) { Current = null; Time.timeScale = 1; } }
        public int Population(int team) => Units.Count(u => u && u.Team == team);
        public void Message(string message) { Messages.Insert(0, message); if (Messages.Count > 5) Messages.RemoveAt(5); }
        public void TogglePause() { if (Winner >= 0) return; Paused = !Paused; Time.timeScale = Paused ? 0 : 1; }

        void Update()
        {
            if (Paused || Winner >= 0) return;
            BattleTime += Time.deltaTime;
            if (Economy.Advance(Time.deltaTime) > 0) { Message($"Ronda {Economy.Round} · +{Economy.Income(0)} de oro"); CountryReinforcements(); }
            if (AiEnabled && BattleTime >= nextAi) { nextAi = BattleTime + AiInterval; RunAi(); }
            for (int team = 0; team < 2; team++)
            {
                bool capitalWon = Mode == VictoryMode.Capitals && Towns.Any(t => t.IsCapital && t.State.Owner == team && t.FoundingTeam >= 0 && t.FoundingTeam != team);
                if (capitalWon)
                {
                    Winner = team; Time.timeScale = 0;
                    Message(team == 0 ? "¡Victoria! Las Marcas son tuyas." : "La Frontera Carmesí controla las Marcas.");
                    break;
                }
                VictoryProgress[team] = Mode == VictoryMode.Conquest && Towns.Count(t => t.State.Owner == team) >= VictoryTarget
                    ? VictoryProgress[team] + Time.deltaTime : 0;
                int opponent = 1 - team;
                if (VictoryProgress[team] >= BattleRules.VictoryHoldSeconds ||
                    (Population(opponent) == 0 && Towns.All(t => t.State.Owner != opponent)))
                {
                    Winner = team; Time.timeScale = 0;
                    Message(team == 0 ? "¡Victoria! Las Marcas son tuyas." : "La Frontera Carmesí controla las Marcas.");
                    break;
                }
            }
        }

        public Soldier Spawn(int team, UnitKind kind, Vector3 position, int originCountry = -1)
        {
            if (!NavMesh.SamplePosition(position, out var hit, 10, NavMesh.AllAreas)) return null;
            var go = new GameObject(BattleRules.Name(kind)); go.transform.position = hit.position;
            go.transform.rotation=Quaternion.Euler(0,team==0?180:0,0);
            var soldier = go.AddComponent<Soldier>(); soldier.Initialize(this, team, kind); soldier.OriginCountry = originCountry; Units.Add(soldier); Targets.Add(soldier); return soldier;
        }

        void RunAi()
        {
            bool relaxed = Difficulty == AiDifficulty.Relaxed;
            bool recruited = false;
            foreach (var town in Towns.Where(t => t.State.Owner == 1))
            {
                float upgradeTime = relaxed ? 120f : 70f;
                float towerTime = relaxed ? 150f : 90f;
                if(!town.Building && BattleTime>=upgradeTime && Economy.Gold[1]>=125 && town.IsCapital && town.State.Level==1)town.Upgrade(1);
                else if(!town.Building && BattleTime>=towerTime && Economy.Gold[1]>=100 && !town.Defense.IsAlive)town.BuildTower(1);
                if (relaxed && recruited) continue;
                if (town.QueueCount < 2 && Population(1) < 45)
                {
                    var kind = town.State.Level==2 ? (UnitKind)(Mathf.FloorToInt(BattleTime/AiInterval)%4) : Mathf.FloorToInt(BattleTime/AiInterval)%3==0 ? UnitKind.Archer : UnitKind.Footman;
                    if (town.Recruit(kind,1) == null) recruited = true;
                }
            }
            if (BattleTime < AiFirstOffensiveTime) return;
            var active = Units.Where(u => u && u.Team == 1 && u.IsAlive && u.isActiveAndEnabled && u.Agent && u.Agent.enabled && u.IsIdle).ToList();
            if (active.Count < 5) return;
            var available = active;
            if (relaxed)
            {
                var infantry = active.Where(IsInfantry).Take(6).ToList();
                if (infantry.Count < 6) return;
                available = active.Where(u => !infantry.Contains(u)).Take(10).ToList();
                if (available.Count == 0) return;
            }
            Vector3 center = available.Aggregate(Vector3.zero, (sum, u) => sum + u.transform.position) / available.Count;
            bool neutralsRemain=Towns.Any(t=>t.State.Owner<0);
            var target = Towns.Where(t => t.State.Owner != 1 && (BattleTime > 100 || t.State.Owner < 0 || !neutralsRemain))
                .OrderByDescending(t => t.State.Country >= 0 && Towns.Any(x => x.State.Country == t.State.Country && x.State.Owner == 1))
                .ThenBy(t => Vector3.SqrMagnitude(t.transform.position - center)).FirstOrDefault();
            if (target) GiveFormation(available, target.ClaimPoint, true, false);
        }
        static bool IsInfantry(Soldier unit) => unit.Kind == UnitKind.Footman || unit.Kind == UnitKind.Guard;
        void CountryReinforcements()
        {
            for(int team=0;team<2;team++) for(int country=0;country<MapLayout.Countries.Length;country++)
            {
                var config = MapLayout.Countries[country];
                if (Economy.CountryOwner(country) != team) continue;
                var cities=Towns.Where(t=>t.State.Country==country && t.State.Owner==team).ToList();
                int pending = Towns.Where(t => t.State.Owner == team).Sum(t => t.QueueCount);
                int current = Units.Count(u => u && u.Team == team && u.OriginCountry == country);
                int amount = RiskReferenceRules.ComputeSpawnAmount(current, config.PerTurn * 5, config.PerTurn);
                amount = Mathf.Min(amount, Mathf.Max(0, BattleRules.PopulationLimit - Population(team) - pending));
                if(cities.Count==0 || amount<=0) continue;
                for (int i=0; i<amount; i++)
                {
                    var unit=Spawn(team,config.Reinforcement,cities[0].Rally,country);
                    if(unit) unit.MoveTo(cities[0].Rally,true,false);
                }
            }
        }

        public static void GiveFormation(IReadOnlyList<Soldier> units, Vector3 point, bool attackMove, bool queue, bool patrol=false)
        {
            if(units.Count==0)return;
            var remaining=units.Where(u=>u&&u.Health>0).ToList();
            int count=remaining.Count;if(count==0)return;
            Vector3 center=remaining.Aggregate(Vector3.zero,(sum,u)=>sum+u.transform.position)/remaining.Count;
            Vector3 forward=point-center;forward.y=0;forward=forward.sqrMagnitude>.01f?forward.normalized:Vector3.forward;
            Vector3 right=new Vector3(forward.z,0,-forward.x);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            for (int i = 0; i < count; i++)
            {
                var offset = right*(i % columns - (columns - 1) * .5f)*1.4f + forward*((Mathf.CeilToInt((float)count / columns)-1)*.5f-i/columns)*1.4f;
                var slot=point+offset;
                var unit=remaining.OrderBy(u=>BattleRules.Ranged(u.Kind)).ThenBy(u=>u.Kind).ThenBy(u=>Vector3.SqrMagnitude(u.transform.position-slot)).First();remaining.Remove(unit);
                if(patrol)unit.Patrol(slot,queue);else unit.MoveTo(slot,attackMove,queue);
            }
        }
    }
}
