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
        public readonly string SourceRawId;
        public readonly bool CanCapture;
        public readonly float Health;
        public readonly float Damage;
        public readonly float BaseDamage;
        public readonly int Dice, Sides;
        public float MinimumDamage=>BaseDamage+Dice;
        public float MaximumDamage=>BaseDamage+Dice*Sides;
        public string DamageText=>Dice>0?MinimumDamage+"–"+MaximumDamage:Damage.ToString();
        public float RollDamage(Random random){float value=BaseDamage;for(int i=0;i<Dice;i++)value+=random.Next(1,Sides+1);return value;}
        public readonly float Range;
        public readonly float Cooldown;
        public readonly float Speed;
        public readonly float Armor;
        public readonly AttackKind Attack;
        public readonly int Cost;
        public readonly int PointValue;
        public readonly float TrainSeconds;
        public readonly int Capacity;
        public bool CanAttack=>Damage>0&&Range>0;
        public bool CanTransport=>Capacity>0;

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
            int pointValue,int dice=0,int sides=0,string sourceRawId=null,bool canCapture=false)
        {
            Name = name;
            SourceRawId = sourceRawId;
            CanCapture = canCapture;
            Health = health;
            BaseDamage=damage;Dice=dice;Sides=sides;Damage=damage+dice*(sides+1)*.5f;
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
        public static ShipProfile Galley=>UnitCatalog.Profile(NavalUnitKind.Galley);

        public static ShipProfile Frigate => Galley;

        // n008 (old nzep) overrides HP300, speed340/50 and cost/point2.
        // Its inherited nzep armor is 0 and it has no enabled weapon.
        // n008 uabi@0x3588 attaches Sch3: W3A Car1@0x395 sets capacity 10.
        // The UI's Normal attack token does not enable a weapon.
        public static ShipProfile Transport=>UnitCatalog.Profile(NavalUnitKind.Transport);

        public static ShipProfile Profile(NavalUnitKind kind)
        {
            return UnitCatalog.Profile(kind);
        }
    }
}
