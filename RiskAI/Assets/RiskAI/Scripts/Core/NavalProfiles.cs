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
        public static readonly ShipProfile Galley = new ShipProfile(
            "Fragata", 400f, 30f, 20f, 1.5f, 6.8f, 6f,
            // Naval combat remains source-aligned. RiskAI requires landed troops
            // for conquest, so warships no longer occupy a city guard slot.
            AttackKind.Normal, 5, 1f, 0, 5,1,15,"h00W",false);

        public static ShipProfile Frigate => Galley;

        // n008 (old nzep) overrides HP300, speed340/50 and cost/point2.
        // Its inherited nzep armor is 0 and it has no enabled weapon.
        // n008 uabi@0x3588 attaches Sch3: W3A Car1@0x395 sets capacity 10.
        // The UI's Normal attack token does not enable a weapon.
        public static readonly ShipProfile Transport = new ShipProfile(
            "Transporte", 300f, 0f, 0f, 0f, 6.8f, 0f,
            // W3U n008 ubld@0x3402; JASS17438-17445 excludes n008 from entry.
            AttackKind.Normal, 2, 1f, 10, 2,sourceRawId:"n008",canCapture:false);

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
