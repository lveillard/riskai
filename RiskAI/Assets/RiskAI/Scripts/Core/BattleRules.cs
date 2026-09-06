using System;
using System.Collections.Generic;

namespace RiskAI.Core
{
    public enum UnitKind { Footman, Archer, Guard, Mage, Mortar, Medic }

    public static class BattleRules
    {
        public const int StartingGold = 120;
        public const int BaseIncome = 12;
        public const int TownIncome = 8;
        public const int PopulationLimit = 100;
        public const float RoundSeconds = 60f;
        public const float VictoryHoldSeconds = 20f;

        public const int TowerCost = 60;
        public const int UpgradeCost = 90;
        public const int UpgradeIncome = 6;
        public const float ConstructionSeconds = 7;
        public const float TowerHealth = 550;
        public const float TowerRange = 8.5f;
        static readonly float[] Training = { 3f, 4f, 5.5f, 6f, 6f, 4f };
        static readonly string[] Names = { "Espadachín", "Ballestero", "Guardia real", "Mago", "Mortero", "Sanador" }, Roles = { "Primera línea", "Ataque a distancia", "Infantería pesada", "Daño de área", "Asedio · contra fortificaciones", "Sana aliados · 15 vida/s" }, Keys = { "Q", "W", "D", "F", "R", "C" }, Models = { "Knight", "RogueHooded", "RoyalGuard", "Mage", "Mortar", "Medic" };
        public static UnitProfile Profile(UnitKind kind)=>ReforgedProfiles.Units[(int)kind];
        public static int Cost(UnitKind kind) => Profile(kind).Cost;
        public static float TrainTime(UnitKind kind) => Training[(int)kind];
        public static float Health(UnitKind kind) => Profile(kind).Health;
        public static float Damage(UnitKind kind) => Profile(kind).AverageDamage;
        public static string DamageRange(UnitKind kind) => Profile(kind).MinimumDamage+"–"+Profile(kind).MaximumDamage;
        public static float Range(UnitKind kind) => Profile(kind).Range;
        public static float MinimumRange(UnitKind kind)=>kind==UnitKind.Mortar?5:0;
        public static float AttackInterval(UnitKind kind) => Profile(kind).Cooldown;
        public static float Speed(UnitKind kind) => Profile(kind).Speed;
        public static bool Ranged(UnitKind kind) => kind != UnitKind.Footman && kind != UnitKind.Guard;
        public static int RequiredLevel(UnitKind kind) => Profile(kind).Level;
        public static string Name(UnitKind kind) => Names[(int)kind];
        public static string Role(UnitKind kind) => Roles[(int)kind];
        public static string Hotkey(UnitKind kind) => Keys[(int)kind];
        public static string Model(UnitKind kind) => Models[(int)kind];
    }

    [Serializable]
    public sealed class TownState
    {
        public string Id;
        public int Owner;
        public int Region;
        public int Country;
        public int Level = 1;
        public float Capture;
        public int Capturing = -1;
        public bool Contested;

        public TownState(string id, int owner, int region, int country = -1) { Id = id; Owner = owner; Region = region; Country = country; }

    }

    public sealed class Economy
    {
        public readonly int[] Gold = { BattleRules.StartingGold, BattleRules.StartingGold };
        public readonly List<TownState> Towns = new List<TownState>();
        public readonly int[] RegionBonuses = { 8, 12, 8 };
        // TownState is intentionally mutable and Towns is publicly editable for the
        // prototype. Rebuild this reusable map for each query instead of caching a
        // result that could become stale after an ownership or topology change.
        readonly Dictionary<int, int> countryOwners = new Dictionary<int, int>();
        public float ElapsedInRound { get; private set; }
        public int Round { get; private set; } = 1;

        public int CountryOwner(int country)
        {
            RebuildCountryOwners();
            return countryOwners.TryGetValue(country, out var owner) ? owner : -1;
        }

        void RebuildCountryOwners()
        {
            countryOwners.Clear();
            foreach (var town in Towns)
            {
                if (!countryOwners.TryGetValue(town.Country, out var owner))
                {
                    countryOwners.Add(town.Country, town.Owner >= 0 ? town.Owner : -1);
                    continue;
                }

                if (owner < 0 || town.Owner != owner)
                    countryOwners[town.Country] = -1;
            }
        }

        public int Income(int team)
        {
            RebuildCountryOwners();
            return CalculateIncome(team);
        }

        int CalculateIncome(int team)
        {
            int income = BattleRules.BaseIncome;
            foreach (var town in Towns)
            {
                var countryComplete = town.Country < 0 ||
                    (countryOwners.TryGetValue(town.Country, out var owner) && owner == team);
                if (town.Owner == team && countryComplete)
                    income += BattleRules.TownIncome + (town.Level - 1) * BattleRules.UpgradeIncome;
            }
            for (int region = 0; region < RegionBonuses.Length; region++)
            {
                bool found = false, owned = true;
                foreach (var town in Towns) if (town.Region == region)
                { found = true; if (town.Owner != team) owned = false; }
                if (found && owned) income += RegionBonuses[region];
            }
            return income;
        }

        public bool Spend(int team, int amount)
        {
            if (team < 0 || team > 1 || amount < 0 || Gold[team] < amount) return false;
            Gold[team] -= amount; return true;
        }

        /// <summary>Adds earned or otherwise awarded gold to a team's account.</summary>
        public bool Grant(int team, int amount)
        {
            if (team < 0 || team > 1 || amount < 0 ||
                (amount > 0 && Gold[team] > int.MaxValue - amount)) return false;
            Gold[team] += amount;
            return true;
        }

        /// <summary>Returns previously spent gold to a team's account.</summary>
        public bool Refund(int team, int amount) => Grant(team, amount);

        public int Advance(float dt)
        {
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return 0;
            ElapsedInRound += dt;
            int paid = 0;
            while (ElapsedInRound >= BattleRules.RoundSeconds)
            {
                ElapsedInRound -= BattleRules.RoundSeconds; Round++; paid++;
                RebuildCountryOwners();
                Gold[0] += CalculateIncome(0); Gold[1] += CalculateIncome(1);
            }
            return paid;
        }
    }
}
