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
        public static readonly UnitProfile[] Units={
            new(200,17,1,4,.9f,1.35f,5.4f,2,AttackKind.Normal,ArmorKind.Heavy,1,1,"Adaptación de infantería",1,.17f),
            new(200,15,2,4,8,1.6f,5.4f,0,AttackKind.Piercing,ArmorKind.Light,1,1,"h00B · Rifleman ← hrif",1,.17f,.7f),
            new(650,37,2,5,2f,1.36f,7,7,AttackKind.Normal,ArmorKind.Heavy,5,1,"h00G · Knight ← hkni",5,.66f,.44f),
            new(250,29,1,3,10,1.6f,5.4f,1,AttackKind.Magic,ArmorKind.Unarmored,4,1,"Adaptación de mago",4),
            new(350,18,1,13,18,3.5f,4.6f,0,AttackKind.Siege,ArmorKind.Medium,3,1,"h00H · Mortar ← hmtm",3,1f,1.1f),
            new(250,7,1,2,8,2,5.4f,1,AttackKind.Piercing,ArmorKind.Light,2,1,"h00E · Medic ← hmpr",2,.59f,.58f),
            new(200,16,2,4,6,1.6f,5.4f,1,AttackKind.Piercing,ArmorKind.Light,1,1,"h012 Marine Private ← hrif",1,.17f,.7f),
            new(650,37,2,5,2,1.36f,5.6f,6,AttackKind.Normal,ArmorKind.Heavy,5,1,"h014 Marine Major ← hkni",5,.66f,.44f),
            new(800,64,2,5,2,1.45f,5.6f,8,AttackKind.Normal,ArmorKind.Heavy,10,1,"h015 Marine General ← hkni",10,.66f,.44f)
        };
        // The map's Bunker overrides HP550, base50, cooldown1.5 and armor3.
        public static readonly UnitProfile Tower=new(550,50,1,8,8.5f,1.5f,0,3,AttackKind.Piercing,ArmorKind.Fortified,3,1,"o000 · Bunker",3);
        // h00N/h00O override base45, one die/five sides, .9 cooldown and
        // 650 native range (13 Unity). Health and armor remain the shared
        // runtime post shell; no local damage/range tuning is retained here.
        public static readonly UnitProfile CapturableTower=new(550,45,1,5,13f,.9f,0,3,AttackKind.Piercing,ArmorKind.Divine,3,1,"h00N/h00O City Post",3,.3f,.3f);
    }
}
