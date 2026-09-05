using System;
namespace RiskAI.Core
{
    public readonly struct UnitProfile
    {
        public readonly float Health, BaseDamage, Range, Cooldown, Speed, Armor;
        public readonly int Dice, Sides, Cost, Level;
        public readonly AttackKind Attack;
        public readonly ArmorKind Defense;
        public readonly string Source;
        public float MinimumDamage=>BaseDamage+Dice;
        public float MaximumDamage=>BaseDamage+Dice*Sides;
        public float AverageDamage=>BaseDamage+Dice*(Sides+1)*.5f;
        public UnitProfile(float hp,float damage,int dice,int sides,float range,float cooldown,float speed,float armor,AttackKind attack,ArmorKind defense,int cost,int level,string source)
        {Health=hp;BaseDamage=damage;Dice=dice;Sides=sides;Range=range;Cooldown=cooldown;Speed=speed;Armor=armor;Attack=attack;Defense=defense;Cost=cost;Level=level;Source=source;}
        public float RollDamage(Random random)
        {float result=BaseDamage;for(int i=0;i<Dice;i++)result+=random.Next(1,Sides+1);return result;}
    }
    public static class ReforgedProfiles
    {
        // Custom-map overrides from war3map.w3u. Currency x20; world distance /50.
        // Inherited dice use the wc3libs historical SLK fixture; see docs/REFORGED-BASE-STATS-v0.9.md.
        // Footman and Mage are local fantasy units. Art is original/KayKit, independent of stats.
        public static readonly UnitProfile[] Units={
            new(200,17,1,4,.9f,1.35f,5.4f,2,AttackKind.Normal,ArmorKind.Heavy,20,1,"Adaptación de infantería"),
            new(200,15,2,4,8,1.5f,5.4f,0,AttackKind.Piercing,ArmorKind.Medium,20,1,"h00B · Rifleman"),
            new(650,37,2,5,1.05f,1.4f,7,7,AttackKind.Normal,ArmorKind.Heavy,100,2,"h00G · Knight"),
            new(250,29,1,3,10,1.6f,5.4f,1,AttackKind.Magic,ArmorKind.Unarmored,80,2,"Adaptación de mago"),
            new(350,18,1,13,18,3.5f,4.6f,0,AttackKind.Siege,ArmorKind.Heavy,60,2,"h00H · Mortar"),
            new(250,7,1,2,8,2,5.4f,1,AttackKind.Magic,ArmorKind.Unarmored,40,1,"h00E · Medic")
        };
        // The map's Bunker overrides HP550, base50, cooldown1.5 and armor3.
        public static readonly UnitProfile Tower=new(550,50,1,8,8.5f,1.5f,0,3,AttackKind.Piercing,ArmorKind.Fortified,60,1,"o000 · Bunker");
    }
}
