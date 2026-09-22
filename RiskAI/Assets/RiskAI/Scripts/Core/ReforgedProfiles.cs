using System;
namespace RiskAI.Core
{
    public readonly struct UnitProfile
    {
        public readonly float Health, BaseDamage, Range, Cooldown, Speed, Armor, AttackPoint, Backswing;
        public readonly int Dice, Sides, Cost, Level, PointValue;
        public readonly AttackKind Attack;
        public readonly ArmorKind Defense;
        public readonly string Source;
        public float MinimumDamage=>BaseDamage+Dice;
        public float MaximumDamage=>BaseDamage+Dice*Sides;
        public float AverageDamage=>BaseDamage+Dice*(Sides+1)*.5f;
        public UnitProfile(float hp,float damage,int dice,int sides,float range,float cooldown,float speed,float armor,AttackKind attack,ArmorKind defense,int cost,int level,string source,int pointValue,float attackPoint=.24f,float backswing=0)
        {Health=hp;BaseDamage=damage;Dice=dice;Sides=sides;Range=range;Cooldown=cooldown;Speed=speed;Armor=armor;Attack=attack;Defense=defense;Cost=cost;Level=level;Source=source;PointValue=pointValue;AttackPoint=attackPoint;Backswing=backswing;}
        public float RollDamage(Random random)
        {float result=BaseDamage;for(int i=0;i<Dice;i++)result+=random.Next(1,Sides+1);return result;}
    }
    public static class ReforgedProfiles
    {
        // Custom-map overrides from war3map.w3u. Gold uses the raw ugol values;
        // world distance is adapted by the prototype's existing scale.
        // Inherited dice use the wc3libs historical SLK fixture; see docs/REFORGED-BASE-STATS-v0.9.md.
        // Footman and Mage are local fantasy units. Art is original/KayKit, independent of stats.
        public static readonly UnitProfile[] Units=System.Array.ConvertAll(UnitCatalog.Land,item=>item.Profile);
        // The map's Bunker overrides HP550, base50, cooldown1.5 and armor3.
        public static UnitProfile Tower=>UnitCatalog.Tower;
        // h00N/h00O override base45, one die/five sides, .9 cooldown and
        // 650 native range (13 Unity). Health and armor remain the shared
        // runtime post shell; no local damage/range tuning is retained here.
        public static UnitProfile CapturableTower=>UnitCatalog.CapturableTower;
    }
}
