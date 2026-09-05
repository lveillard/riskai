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
        public const int TownsToWin = 8;
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
        public float ElapsedInRound { get; private set; }
        public int Round { get; private set; } = 1;

        public int CountryOwner(int country)
        {
            int owner = -1; bool found = false;
            foreach (var town in Towns)
            {
                if (town.Country != country) continue;
                if (!found) { owner = town.Owner; found = true; }
                if (town.Owner < 0 || town.Owner != owner) return -1;
            }
            return found && owner >= 0 ? owner : -1;
        }

        public int Income(int team)
        {
            int income = BattleRules.BaseIncome;
            foreach (var town in Towns)
                if (town.Owner == team && (town.Country < 0 || CountryOwner(town.Country) == team))
                    income += BattleRules.TownIncome + (town.Level - 1) * BattleRules.UpgradeIncome;
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

        public int Advance(float dt)
        {
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return 0;
            ElapsedInRound += dt;
            int paid = 0;
            while (ElapsedInRound >= BattleRules.RoundSeconds)
            {
                ElapsedInRound -= BattleRules.RoundSeconds; Round++; paid++;
                Gold[0] += Income(0); Gold[1] += Income(1);
            }
            return paid;
        }
    }
}
