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
        public readonly int PointValue;
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
            int capacity,
            int pointValue)
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
            PointValue = pointValue;
        }
    }

    public static class NavalProfiles
    {
        // The prototype keeps the Galley identity for public compatibility,
        // while its source-aligned profile is the documented h00W Warship B.
        public static readonly ShipProfile Galley = new ShipProfile(
            "Fragata", 400f, 30f, 20f, 1.5f, 6.8f, 6f,
            AttackKind.Normal, 5, 4f, 0, 5);

        public static ShipProfile Frigate => Galley;

        public static readonly ShipProfile Transport = new ShipProfile(
            "Transporte", 300f, 0f, 0f, 0f, 5f, 1f,
            AttackKind.Normal, 2, 6f, 6, 2);

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
