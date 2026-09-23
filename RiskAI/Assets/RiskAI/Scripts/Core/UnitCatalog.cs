using System;

namespace RiskAI.Core
{
    public readonly struct LandUnitDefinition
    {
        public readonly UnitProfile Profile;
        public readonly string Name,Role,Hotkey,Model,SourceRawId;
        public readonly float TrainSeconds,MinimumRange;
        public readonly bool Ranged,Mechanical;
        public LandUnitDefinition(UnitProfile profile,string name,string role,string hotkey,string model,float trainSeconds,bool ranged,float minimumRange=0,bool mechanical=false,string sourceRawId=null)
        {Profile=profile;Name=name;Role=role;Hotkey=hotkey;Model=model;TrainSeconds=trainSeconds;Ranged=ranged;MinimumRange=minimumRange;Mechanical=mechanical;SourceRawId=sourceRawId;}
    }

    /// <summary>Authoritative registry for land and naval unit identity, stats and capabilities.</summary>
    public static class UnitCatalog
    {
        public static readonly LandUnitDefinition[] Land = {
            new(new UnitProfile(200,17,1,4,.9f,1.35f,5.4f,2,AttackKind.Normal,ArmorKind.Heavy,1,1,"Adaptación de infantería",1,.17f),"Espadachín","Primera línea","Q","Knight",3,false),
            new(new UnitProfile(200,15,2,4,8,1.6f,5.4f,0,AttackKind.Piercing,ArmorKind.Light,1,1,"h00B · Rifleman ← hrif",1,.17f,.7f),"Ballestero","Ataque a distancia","W","RogueHooded",1,true,sourceRawId:"h00B"),
            new(new UnitProfile(650,37,2,5,2f,1.36f,7,7,AttackKind.Normal,ArmorKind.Heavy,5,1,"h00G · Knight ← hkni",5,.66f,.44f),"Caballero","Caballería pesada","D","RoyalGuard",1,false,sourceRawId:"h00G"),
            new(new UnitProfile(250,29,1,3,10,1.6f,5.4f,1,AttackKind.Magic,ArmorKind.Unarmored,4,1,"Adaptación de mago",4),"Mago","Daño de área","F","Mage",6,true),
            new(new UnitProfile(350,18,1,13,18,3.5f,4.6f,0,AttackKind.Siege,ArmorKind.Medium,3,1,"h00H · Mortar ← hmtm",3,1f,1.1f),"Mortero","Área a larga distancia","R","Mortar",1,true,5,sourceRawId:"h00H"),
            // Local product decision (v0.30): 220 HP instead of the explicit h00E uhpm=250.
            new(new UnitProfile(220,7,1,2,8,2,5.4f,1,AttackKind.Piercing,ArmorKind.Light,2,1,"h00E · Medic ← hmpr (220 HP local)",2,.59f,.58f),"Sanador","Sana aliados · 25 vida · 5 maná","C","Medic",1,true,sourceRawId:"h00E"),
            new(new UnitProfile(200,16,2,4,6,1.6f,5.4f,1,AttackKind.Piercing,ArmorKind.Light,1,1,"h012 Marine Private ← hrif",1,.17f,.7f),"Marine Private","Pistolero de puerto","V","MarinePrivate",1,true,sourceRawId:"h012"),
            new(new UnitProfile(650,37,2,5,2,1.36f,5.6f,6,AttackKind.Normal,ArmorKind.Heavy,5,1,"h014 Marine Major ← hkni",5,.66f,.44f),"Marine Major","Caballería de puerto","B","RoyalGuard",1,false,sourceRawId:"h014"),
            new(new UnitProfile(800,64,2,5,2,1.45f,5.6f,8,AttackKind.Normal,ArmorKind.Heavy,10,1,"h015 Marine General ← hkni",10,.66f,.44f),"Marine General","Caballería veterana de puerto","C","RoyalGuard",1,false,sourceRawId:"h015"),
            // v0.30 source roster (h00N). Explicit W3U overrides win; inherited fields use the
            // same RoC baseline as the existing hrif/hkni/hmpr profiles (see docs/RISK-RULES-v0.30.md).
            new(new UnitProfile(450,36,2,4,7,1f,5.4f,1,AttackKind.Piercing,ArmorKind.Light,6,1,"h00F · Elite Rifleman ← hrif",6,.17f,.7f),"Fusilero de élite","Fusilería de élite","T","RogueHooded",1,true,sourceRawId:"h00F"),
            new(new UnitProfile(400,29,1,3,10,2,5.4f,1,AttackKind.Piercing,ArmorKind.Light,4,1,"h00I · Roarer ← hmpr",4,.59f,.58f),"Rugidor","Rugido · +25% daño aliado","X","RogueHooded",1,true,sourceRawId:"h00I"),
            new(new UnitProfile(800,55,2,5,2,1.45f,7,10,AttackKind.Normal,ArmorKind.Heavy,10,1,"h00J · Army General ← hkni",10,.66f,.44f),"General","Caballería de mando · Rugido","G","RoyalGuard",1,false,sourceRawId:"h00J"),
            new(new UnitProfile(900,55,1,13,20,3,4,3,AttackKind.Piercing,ArmorKind.Unarmored,15,1,"h00M · Artillery ← hmtt",15,.5f,.5f),"Artillería","Asedio de área a gran distancia","Z","Mortar",1,true,mechanical:true,sourceRawId:"h00M"),
            new(new UnitProfile(1500,80,1,11,10,1.8f,5.2f,9,AttackKind.Siege,ArmorKind.Fortified,25,1,"h01A · Tank ← hfoo",25,.2f,.5f),"Tanque","Blindado de asedio","Y","Tank",1,true,mechanical:true,sourceRawId:"h01A")
        };

        public static readonly ShipProfile[] Naval = {
            new ShipProfile("Fragata",400f,30f,20f,1.5f,6.8f,6f,AttackKind.Normal,5,1f,0,5,1,15,"h00W",true,"Q"),
            new ShipProfile("Transporte",300f,0f,0f,0f,6.8f,0f,AttackKind.Normal,2,1f,10,2,sourceRawId:"n008",canCapture:false,hotkey:"W"),
            // v0.30 source roster (h00O). hdes inherited dice/cooldown/splash follow h00W's TFT candidate.
            new ShipProfile("Buque de guerra",1250f,90f,30f,1.5f,9f,10f,AttackKind.Normal,20,1f,0,20,1,15,"h00U",true,"R"),
            new ShipProfile("Acorazado",2350f,130f,30f,1.4f,6.6f,20f,AttackKind.Normal,45,1f,0,45,1,15,"h001",true,"F"),
            // n007 shares n008's uabi (Sch3 Car1=10) and overrides armor 30 and speed 370.
            new ShipProfile("Transporte blindado",300f,0f,0f,0f,7.4f,30f,AttackKind.Normal,6,1f,10,6,sourceRawId:"n007",canCapture:false,hotkey:"X")
        };

        public static readonly UnitProfile Tower=new UnitProfile(550,50,1,8,8.5f,1.5f,0,3,AttackKind.Piercing,ArmorKind.Fortified,3,1,"o000 · Bunker",3);
        public static readonly UnitProfile CapturableTower=new UnitProfile(550,45,1,5,13f,.9f,0,3,AttackKind.Piercing,ArmorKind.Divine,3,1,"h00N/h00O City Post",3,.3f,.3f);

        public static LandUnitDefinition Definition(UnitKind kind)
        {
            int index=(int)kind;if(index<0||index>=Land.Length)throw new ArgumentOutOfRangeException(nameof(kind));return Land[index];
        }
        public static ShipProfile Profile(NavalUnitKind kind)
        {
            int index=(int)kind;if(index<0||index>=Naval.Length)throw new ArgumentOutOfRangeException(nameof(kind));return Naval[index];
        }
    }
}
