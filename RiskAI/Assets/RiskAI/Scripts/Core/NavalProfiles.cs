using System;

namespace RiskAI.Core
{
    /// <summary>Engine-independent identity for the prototype's naval units.</summary>
    public enum NavalUnitKind
    {
        Galley,
        Transport
    }

    /// <summary>Immutable combat and economy values for a naval unit.</summary>
    public readonly struct ShipProfile
    {
        public readonly string Name;
        public readonly float Health;
        public readonly float Damage;
        public readonly float Range;
        public readonly float Cooldown;
        public readonly float Speed;
        public readonly float Armor;
        public readonly AttackKind Attack;
        public readonly int Cost;
        public readonly float TrainSeconds;
        public readonly int Capacity;

        public ShipProfile(
            string name,
            float health,
            float damage,
            float range,
            float cooldown,
            float speed,
            float armor,
            AttackKind attack,
            int cost,
            float trainSeconds,
            int capacity)
        {
            Name = name;
            Health = health;
            Damage = damage;
            Range = range;
            Cooldown = cooldown;
            Speed = speed;
            Armor = armor;
            Attack = attack;
            Cost = cost;
            TrainSeconds = trainSeconds;
            Capacity = capacity;
        }
    }

    public static class NavalProfiles
    {
        public static readonly ShipProfile Galley = new ShipProfile(
            "Galera", 500f, 20f, 17f, 1.5f, 7f, 2f,
            AttackKind.Siege, 75, 4f, 0);

        public static readonly ShipProfile Transport = new ShipProfile(
            "Transporte", 300f, 0f, 0f, 0f, 5f, 1f,
            AttackKind.Normal, 45, 6f, 6);

        public static ShipProfile Profile(NavalUnitKind kind)
        {
            switch (kind)
            {
                case NavalUnitKind.Galley: return Galley;
                case NavalUnitKind.Transport: return Transport;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
