using System;
using System.Collections.Generic;

namespace RiskAI.Core
{
    /// <summary>Tuning for one AI difficulty. No level receives extra gold, vision or combat stats.</summary>
    public readonly struct AiDifficultyProfile
    {
        public readonly int Level;
        public readonly float DecisionInterval, MicroInterval;
        public readonly int PurchasesPerDecision, BonusPurchaseGold;
        public readonly float CounterWeight;
        public readonly float AttackMargin, DefenseMargin;
        public readonly int MinimumWave, MaximumWave, MaximumArmies, MaximumDefenders, PathBudget;
        public readonly float GatherTimeout, DefenseDispatchRadius;
        public readonly float RetreatHealthFraction, ArmyRetreatRatio;
        public readonly bool FocusGuardian, RangedStandoff, NavalEscort;
        public readonly int ExpeditionTroops, FleetTarget;

        AiDifficultyProfile(int level,float decision,float micro,int purchases,int bonusGold,float counter,float attackMargin,float defenseMargin,
            int minimumWave,int maximumWave,int armies,int defenders,int pathBudget,float gatherTimeout,float dispatchRadius,
            float retreatHealth,float armyRetreat,bool focusGuardian,bool rangedStandoff,bool escort,int expeditionTroops,int fleet)
        {
            Level=level;DecisionInterval=decision;MicroInterval=micro;PurchasesPerDecision=purchases;BonusPurchaseGold=bonusGold;CounterWeight=counter;
            AttackMargin=attackMargin;DefenseMargin=defenseMargin;MinimumWave=minimumWave;MaximumWave=maximumWave;MaximumArmies=armies;
            MaximumDefenders=defenders;PathBudget=pathBudget;GatherTimeout=gatherTimeout;DefenseDispatchRadius=dispatchRadius;
            RetreatHealthFraction=retreatHealth;ArmyRetreatRatio=armyRetreat;FocusGuardian=focusGuardian;RangedStandoff=rangedStandoff;
            NavalEscort=escort;ExpeditionTroops=expeditionTroops;FleetTarget=fleet;
        }

        public static readonly AiDifficultyProfile Relaxed=new AiDifficultyProfile(0,12f,3f,1,10,0f,.9f,1f,2,8,1,4,24,25f,32f,.18f,0f,false,false,false,4,2);
        public static readonly AiDifficultyProfile Standard=new AiDifficultyProfile(1,7f,1.5f,3,0,.7f,1.2f,1.25f,3,14,3,8,32,35f,36f,.25f,.4f,true,true,true,6,3);
        public static readonly AiDifficultyProfile Hard=new AiDifficultyProfile(2,5f,.75f,4,0,1f,1.4f,1.35f,3,20,4,12,40,40f,44f,.33f,.55f,true,true,true,8,4);

        /// <summary>0 = relaxed, 1 = standard, 2 = hard. Unknown future levels clamp to the nearest defined one.</summary>
        public static AiDifficultyProfile For(int level) => level<=0?Relaxed:level==1?Standard:Hard;
    }

    public enum AiUnitRole { Frontline, Ranged, Splash, Healer }

    /// <summary>Derived, data-driven AI view of one land unit kind.</summary>
    public readonly struct AiUnitTraits
    {
        public readonly UnitKind Kind;
        public readonly AiUnitRole Role;
        public readonly int Cost, Level;
        public readonly float Dps, EffectiveHealth, Range, Value;
        public readonly AttackKind Attack;
        public readonly ArmorKind Defense;
        public readonly bool Ranged, Splash, Healer;
        public AiUnitTraits(UnitKind kind,AiUnitRole role,int cost,int level,float dps,float effectiveHealth,float range,AttackKind attack,ArmorKind defense,bool ranged,bool splash,bool healer)
        {
            Kind=kind;Role=role;Cost=Math.Max(1,cost);Level=level;Dps=dps;EffectiveHealth=effectiveHealth;Range=range;Attack=attack;Defense=defense;
            Ranged=ranged;Splash=splash;Healer=healer;
            Value=AiUnitAnalysis.RawValue(dps,effectiveHealth,splash,healer);
        }
    }

    /// <summary>Weighted armor/attack census of a force, used for counter picks and threat estimates.</summary>
    public sealed class AiForceMix
    {
        public static readonly int ArmorCount=Enum.GetValues(typeof(ArmorKind)).Length;
        public static readonly int AttackCount=Enum.GetValues(typeof(AttackKind)).Length;
        readonly float[] armor=new float[ArmorCount];
        readonly float[] attack=new float[AttackCount];
        public float Total { get; private set; }
        public void Clear(){Array.Clear(armor,0,armor.Length);Array.Clear(attack,0,attack.Length);Total=0;}
        public void Add(ArmorKind defense,AttackKind offense,float weight)
        {
            if(weight<=0)return;
            int a=(int)defense,k=(int)offense;
            if(a>=0&&a<armor.Length)armor[a]+=weight;
            if(k>=0&&k<attack.Length)attack[k]+=weight;
            Total+=weight;
        }
        public void Add(UnitKind kind,float weight=1)
        {
            var traits=AiUnitAnalysis.For(kind);Add(traits.Defense,traits.Attack,weight);
        }
        // Without information, assume the usual early mix of crossbows and swords.
        static float DefaultArmor(ArmorKind kind) => kind==ArmorKind.Light?.4f:kind==ArmorKind.Heavy?.4f:kind==ArmorKind.Unarmored?.1f:kind==ArmorKind.Medium?.1f:0;
        static float DefaultAttack(AttackKind kind) => kind==AttackKind.Normal?.45f:kind==AttackKind.Piercing?.45f:kind==AttackKind.Magic?.05f:kind==AttackKind.Siege?.05f:0;
        public float ArmorShare(ArmorKind kind)
        {
            int index=(int)kind;if(index<0||index>=armor.Length)return 0;
            return Total>0?armor[index]/Total:DefaultArmor(kind);
        }
        public float AttackShare(AttackKind kind)
        {
            int index=(int)kind;if(index<0||index>=attack.Length)return 0;
            return Total>0?attack[index]/Total:DefaultAttack(kind);
        }
        /// <summary>Expected damage multiplier of an attack type against this force.</summary>
        public float OffenseMultiplier(AttackKind offense)
        {
            float sum=0,weights=0;
            for(int a=0;a<ArmorCount;a++)
            {
                float share=ArmorShare((ArmorKind)a);if(share<=0)continue;
                sum+=share*CombatRules.DamageMultiplier(offense,(ArmorKind)a);weights+=share;
            }
            return weights>0?sum/weights:1;
        }
        /// <summary>Expected damage multiplier this force's attacks deal against an armor type.</summary>
        public float Vulnerability(ArmorKind defense)
        {
            float sum=0,weights=0;
            for(int k=0;k<AttackCount;k++)
            {
                float share=AttackShare((AttackKind)k);if(share<=0)continue;
                sum+=share*CombatRules.DamageMultiplier((AttackKind)k,defense);weights+=share;
            }
            return weights>0?sum/weights:1;
        }
    }

    public static class AiUnitAnalysis
    {
        // Warcraft medic autocast restores 25 health per second-long cast in range.
        public const float HealerSupportDps = 12f;
        public const float SplashBonus = 1.25f;
        static AiUnitTraits[] traits;
        static readonly AiForceMix neutralMix=new AiForceMix();

        static AiUnitTraits[] Table
        {
            get
            {
                if(traits!=null&&traits.Length==UnitCatalog.Land.Length)return traits;
                var table=new AiUnitTraits[UnitCatalog.Land.Length];
                for(int i=0;i<table.Length;i++)table[i]=Build((UnitKind)i,UnitCatalog.Land[i]);
                return traits=table;
            }
        }

        static AiUnitTraits Build(UnitKind kind,LandUnitDefinition definition)
        {
            var profile=definition.Profile;
            bool splash=SourceWeapons.For(kind,profile.Attack).HasSplash;
            bool healer=IsHealer(kind,definition);
            float dps=profile.Cooldown>0?profile.AverageDamage/profile.Cooldown:profile.AverageDamage;
            float health=profile.Health/Math.Max(.05f,CombatRules.ArmorMultiplier(profile.Armor));
            var role=healer?AiUnitRole.Healer:splash?AiUnitRole.Splash:definition.Ranged?AiUnitRole.Ranged:AiUnitRole.Frontline;
            return new AiUnitTraits(kind,role,profile.Cost,profile.Level,dps,health,profile.Range,profile.Attack,profile.Defense,definition.Ranged,splash,healer);
        }

        static bool IsHealer(UnitKind kind,LandUnitDefinition definition)
        {
            if(kind==UnitKind.Medic)return true;
            string role=definition.Role??"";
            return role.IndexOf("Sana",StringComparison.OrdinalIgnoreCase)>=0||role.IndexOf("Cura",StringComparison.OrdinalIgnoreCase)>=0||
                   role.IndexOf("Heal",StringComparison.OrdinalIgnoreCase)>=0;
        }

        /// <summary>Traits for any catalog kind. Kinds the catalog does not know yet fall back to a basic frontline unit.</summary>
        public static AiUnitTraits For(UnitKind kind)
        {
            var table=Table;int index=(int)kind;
            if(index>=0&&index<table.Length)return table[index];
            return new AiUnitTraits(kind,AiUnitRole.Frontline,1,1,12f,200f,1f,AttackKind.Normal,ArmorKind.Heavy,false,false,false);
        }

        public static float RawValue(float dps,float effectiveHealth,bool splash,bool healer)
        {
            float offense=Math.Max(0,dps)+(healer?HealerSupportDps:0);
            float value=(float)Math.Sqrt(offense*Math.Max(1,effectiveHealth));
            return splash?value*SplashBonus:value;
        }

        /// <summary>Lanchester-style value (sqrt of damage rate times durability) against a specific enemy mix.</summary>
        public static float ValueAgainst(in AiUnitTraits unit,AiForceMix enemy)
        {
            enemy=enemy??neutralMix;
            float dps=unit.Dps*enemy.OffenseMultiplier(unit.Attack);
            float health=unit.EffectiveHealth/Math.Max(.2f,enemy.Vulnerability(unit.Defense));
            return RawValue(dps,health,unit.Splash,unit.Healer);
        }

        /// <summary>Threat of a city post tower. It only fires while the guardian lives, so the guardian's health is its effective durability.</summary>
        public static float TowerValue(float guardianHealth,AiForceMix attackers)
        {
            var tower=UnitCatalog.CapturableTower;
            float dps=tower.AverageDamage/Math.Max(.1f,tower.Cooldown);
            float multiplier=0,weights=0;
            for(int a=0;a<AiForceMix.ArmorCount;a++)
            {
                float share=(attackers??neutralMix).ArmorShare((ArmorKind)a);if(share<=0)continue;
                multiplier+=share*CombatRules.DamageMultiplier(tower.Attack,(ArmorKind)a);weights+=share;
            }
            if(weights>0)dps*=multiplier/weights;
            return (float)Math.Sqrt(dps*Math.Max(50f,guardianHealth));
        }

        public static float ShipValue(in ShipProfile ship)
        {
            if(!ship.CanAttack)return 0;
            float dps=ship.Damage/Math.Max(.1f,ship.Cooldown);
            return (float)Math.Sqrt(dps*ship.Health/Math.Max(.05f,CombatRules.ArmorMultiplier(ship.Armor)));
        }
    }

    /// <summary>Current own roster (trained and queued) grouped by role.</summary>
    public sealed class AiArmyCensus
    {
        public static readonly int RoleCount=Enum.GetValues(typeof(AiUnitRole)).Length;
        readonly int[] roles=new int[RoleCount];
        public int Total { get; private set; }
        public int Count(AiUnitRole role) => roles[(int)role];
        public void Clear(){Array.Clear(roles,0,roles.Length);Total=0;}
        public void Add(UnitKind kind){roles[(int)AiUnitAnalysis.For(kind).Role]++;Total++;}
        public void Add(AiUnitRole role,int count=1){if(count<=0)return;roles[(int)role]+=count;Total+=count;}
    }

    public readonly struct AiPurchaseDecision
    {
        public readonly bool Buy, Save;
        public readonly UnitKind Kind;
        public AiPurchaseDecision(bool buy,bool save,UnitKind kind){Buy=buy;Save=save;Kind=kind;}
        public static AiPurchaseDecision None => new AiPurchaseDecision(false,false,default);
    }

    /// <summary>Chooses the next recruit from whatever the building offers, aiming at a role mix and countering the enemy.</summary>
    public static class AiCompositionPlanner
    {
        public const int OpeningSize = 4;

        public static float TargetShare(AiUnitRole role,int total,AiForceMix enemy)
        {
            float healer=total<6?0:.08f;
            float splash=total<5?0:.14f;
            float pierce=enemy!=null?enemy.AttackShare(AttackKind.Piercing):.45f;
            // Heavy frontline takes normal damage from crossbows and the city
            // towers, while light ranged units take double.
            float frontline=Clamp(.42f+(pierce-.45f)*.3f,.3f,.6f);
            switch(role)
            {
                case AiUnitRole.Healer:return healer;
                case AiUnitRole.Splash:return splash;
                case AiUnitRole.Frontline:return frontline;
                default:return Math.Max(0,1-healer-splash-frontline);
            }
        }

        /// <summary>Gold efficiency matters most while the army is small; unit quality matters near the population cap.</summary>
        public static float CostExponent(int total) => total<8?1f:total<40?.7f:.45f;

        public static float Score(UnitKind kind,int total,AiForceMix enemy,float counterWeight)
        {
            var traits=AiUnitAnalysis.For(kind);
            float neutral=AiUnitAnalysis.ValueAgainst(traits,null);
            float value=counterWeight<=0||enemy==null?neutral:neutral+(AiUnitAnalysis.ValueAgainst(traits,enemy)-neutral)*Clamp(counterWeight,0,1);
            return value/(float)Math.Pow(traits.Cost,CostExponent(total));
        }

        public static AiPurchaseDecision Choose(IReadOnlyList<UnitKind> options,AiArmyCensus census,AiForceMix enemy,int gold,int income,int siteLevel,
            bool urgent,float counterWeight,bool allowSaving)
        {
            if(options==null||options.Count==0||census==null)return AiPurchaseDecision.None;
            int total=census.Total;
            Span4 deficits=default;
            for(int r=0;r<AiArmyCensus.RoleCount&&r<4;r++)
                deficits[r]=TargetShare((AiUnitRole)r,total,enemy)*(total+1)-census.Count((AiUnitRole)r);
            int visited=0;
            for(int pass=0;pass<AiArmyCensus.RoleCount&&pass<4;pass++)
            {
                int role=-1;float best=float.NegativeInfinity;
                for(int r=0;r<AiArmyCensus.RoleCount&&r<4;r++)
                {
                    if((visited&(1<<r))!=0)continue;
                    // Roles with a zero target (support in the opening) are never the desired pick.
                    if(TargetShare((AiUnitRole)r,total,enemy)<=0)continue;
                    if(deficits[r]>best){best=deficits[r];role=r;}
                }
                if(role<0)break;
                visited|=1<<role;
                if(!BestOption(options,(AiUnitRole)role,total,enemy,counterWeight,siteLevel,int.MaxValue,out var desired))continue;
                int cost=AiUnitAnalysis.For(desired).Cost;
                if(cost<=gold)return new AiPurchaseDecision(true,false,desired);
                // Save for a better unit when the next income round covers it.
                if(allowSaving&&!urgent&&total>=OpeningSize&&cost<=gold+Math.Max(0,income))
                    return new AiPurchaseDecision(false,true,desired);
                if(BestOption(options,(AiUnitRole)role,total,enemy,counterWeight,siteLevel,gold,out var affordable))
                    return new AiPurchaseDecision(true,false,affordable);
                break;
            }
            // No desired role is affordable here: take the best affordable combat unit.
            UnitKind fallback=default;float fallbackScore=float.NegativeInfinity;bool found=false;
            for(int i=0;i<options.Count;i++)
            {
                var traits=AiUnitAnalysis.For(options[i]);
                if(traits.Cost>gold||traits.Level>siteLevel||traits.Healer&&total<OpeningSize)continue;
                if(TargetShare(traits.Role,total,enemy)<=0&&total<OpeningSize)continue;
                float score=Score(options[i],total,enemy,counterWeight);
                if(score>fallbackScore){fallbackScore=score;fallback=options[i];found=true;}
            }
            return found?new AiPurchaseDecision(true,false,fallback):AiPurchaseDecision.None;
        }

        static bool BestOption(IReadOnlyList<UnitKind> options,AiUnitRole role,int total,AiForceMix enemy,float counterWeight,int siteLevel,int maximumCost,out UnitKind choice)
        {
            choice=default;float best=float.NegativeInfinity;bool found=false;
            for(int i=0;i<options.Count;i++)
            {
                var traits=AiUnitAnalysis.For(options[i]);
                if(traits.Role!=role||traits.Level>siteLevel||traits.Cost>maximumCost)continue;
                float score=Score(options[i],total,enemy,counterWeight);
                if(score>best){best=score;choice=options[i];found=true;}
            }
            return found;
        }

        static float Clamp(float value,float min,float max) => value<min?min:value>max?max:value;

        // Allocation-free fixed buffer for the four roles.
        struct Span4
        {
            float a,b,c,d;
            public float this[int index]
            {
                get=>index==0?a:index==1?b:index==2?c:d;
                set{if(index==0)a=value;else if(index==1)b=value;else if(index==2)c=value;else d=value;}
            }
        }
    }

    public enum AiWaveDecision { Wait, Launch, Abort }

    /// <summary>Pure objective and engagement arithmetic for the skirmish commander.</summary>
    public static class AiTargetScoring
    {
        public const float DistanceScale = 45f;

        /// <summary>Strategic worth of taking one city, dominated by country completion and denial.</summary>
        public static float CityValue(int countryTotal,int ownedByUs,bool breaksEnemyCountry,bool neutral)
        {
            float value=1;
            if(countryTotal>0)
            {
                int remaining=countryTotal-ownedByUs;
                if(remaining<=1)value+=1.5f+.5f*countryTotal;   // completes a country: income + free reinforcements
                else value+=1.2f*ownedByUs/countryTotal;          // progress towards one
                if(breaksEnemyCountry)value+=.8f+.25f*countryTotal; // denies their country income and reinforcements
            }
            if(neutral)value+=.25f;
            return value;
        }

        /// <summary>How attainable a target is for the power that can be committed to it.</summary>
        public static float Feasibility(float availablePower,float defensePower,float margin)
        {
            if(defensePower<=1)return 1.3f;
            float ratio=availablePower/Math.Max(1,defensePower*Math.Max(.1f,margin));
            if(ratio>=1)return 1;
            return .1f+.9f*ratio*ratio;
        }

        public static float Score(float cityValue,float distance,float feasibility) => cityValue*feasibility/(1+Math.Max(0,distance)/DistanceScale);

        public static AiWaveDecision Wave(float gatheredPower,float requiredPower,int gathered,int total,float elapsed,float timeout)
        {
            if(total<=0)return AiWaveDecision.Abort;
            if(requiredPower<=1&&gathered>0)return AiWaveDecision.Launch;
            bool strong=gatheredPower>=requiredPower;
            if(strong&&(gathered*10>=total*7||elapsed>=timeout*.5f))return AiWaveDecision.Launch;
            if(elapsed>=timeout&&gatheredPower>=requiredPower*.6f)return AiWaveDecision.Launch;
            if(elapsed>=timeout*2)return AiWaveDecision.Abort;
            return AiWaveDecision.Wait;
        }

        public static bool ShouldRetreat(float armyPower,float defensePower,float ratio) => ratio>0&&defensePower>1&&armyPower<defensePower*ratio;

        /// <summary>Power still needed at a threatened post. Contested posts always get at least a token response.</summary>
        public static float DefenseShortfall(float enemyPower,float friendlyPower,float margin,bool contested)
        {
            float shortfall=enemyPower*margin-friendlyPower;
            return contested?Math.Max(1,shortfall):shortfall;
        }
    }
}
