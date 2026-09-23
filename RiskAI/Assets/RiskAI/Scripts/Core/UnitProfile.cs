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
}
