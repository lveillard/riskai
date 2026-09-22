using System;

namespace RiskAI.Core
{
    public readonly struct LandUnitDefinition
    {
        public readonly UnitProfile Profile;
        public readonly string Name,Role,Hotkey,Model;
        public readonly float TrainSeconds,MinimumRange;
        public readonly bool Ranged;
        public LandUnitDefinition(UnitProfile profile,string name,string role,string hotkey,string model,float trainSeconds,bool ranged,float minimumRange=0)
        {Profile=profile;Name=name;Role=role;Hotkey=hotkey;Model=model;TrainSeconds=trainSeconds;Ranged=ranged;MinimumRange=minimumRange;}
    }

    /// <summary>Authoritative registry for land and naval unit identity, stats and capabilities.</summary>
    public static class UnitCatalog
    {
        public static readonly LandUnitDefinition[] Land = {
            new(new UnitProfile(200,17,1,4,.9f,1.35f,5.4f,2,AttackKind.Normal,ArmorKind.Heavy,1,1,"Adaptación de infantería",1,.17f),"Espadachín","Primera línea","Q","Knight",3,false),
            new(new UnitProfile(200,15,2,4,8,1.6f,5.4f,0,AttackKind.Piercing,ArmorKind.Light,1,1,"h00B · Rifleman ← hrif",1,.17f,.7f),"Ballestero","Ataque a distancia","W","RogueHooded",1,true),
            new(new UnitProfile(650,37,2,5,2f,1.36f,7,7,AttackKind.Normal,ArmorKind.Heavy,5,1,"h00G · Knight ← hkni",5,.66f,.44f),"Caballero","Caballería pesada","D","RoyalGuard",1,false),
            new(new UnitProfile(250,29,1,3,10,1.6f,5.4f,1,AttackKind.Magic,ArmorKind.Unarmored,4,1,"Adaptación de mago",4),"Mago","Daño de área","F","Mage",6,true),
            new(new UnitProfile(350,18,1,13,18,3.5f,4.6f,0,AttackKind.Siege,ArmorKind.Medium,3,1,"h00H · Mortar ← hmtm",3,1f,1.1f),"Mortero","Área a larga distancia","R","Mortar",1,true,5),
            new(new UnitProfile(250,7,1,2,8,2,5.4f,1,AttackKind.Piercing,ArmorKind.Light,2,1,"h00E · Medic ← hmpr",2,.59f,.58f),"Sanador","Sana aliados · 25 vida","C","Medic",1,true),
            new(new UnitProfile(200,16,2,4,6,1.6f,5.4f,1,AttackKind.Piercing,ArmorKind.Light,1,1,"h012 Marine Private ← hrif",1,.17f,.7f),"Marine Private","Pistolero de puerto","V","MarinePrivate",1,true),
            new(new UnitProfile(650,37,2,5,2,1.36f,5.6f,6,AttackKind.Normal,ArmorKind.Heavy,5,1,"h014 Marine Major ← hkni",5,.66f,.44f),"Marine Major","Caballería de puerto","B","RoyalGuard",1,false),
            new(new UnitProfile(800,64,2,5,2,1.45f,5.6f,8,AttackKind.Normal,ArmorKind.Heavy,10,1,"h015 Marine General ← hkni",10,.66f,.44f),"Marine General","Caballería veterana de puerto","C","RoyalGuard",1,false)
        };

        public static readonly ShipProfile[] Naval = {
            new ShipProfile("Fragata",400f,30f,20f,1.5f,6.8f,6f,AttackKind.Normal,5,1f,0,5,1,15,"h00W",true),
            new ShipProfile("Transporte",300f,0f,0f,0f,6.8f,0f,AttackKind.Normal,2,1f,10,2,sourceRawId:"n008",canCapture:false)
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
