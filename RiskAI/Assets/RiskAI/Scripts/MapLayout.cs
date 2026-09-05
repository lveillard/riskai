using RiskAI.Core;
using UnityEngine;
namespace RiskAI
{
    public static class MapLayout
    {
        public const float Spacing=1.4f,HalfWidth=72*Spacing,HalfDepth=84*Spacing;
        public const int TerrainLayer=8;
        public readonly struct City
        {
            public readonly string Id,Name;public readonly Vector3 Position;public readonly int Owner,Region,Country;public readonly bool Capital;
            public City(string id,string name,float x,float z,int owner,int region,int country,bool capital=false)
            {Id=id;Name=name;Position=Point(x*Spacing,z*Spacing);Owner=owner;Region=region;Country=country;Capital=capital;}
        }
        public readonly struct Country
        {
            public readonly string Name;public readonly int Region,PerTurn;public readonly UnitKind Reinforcement;
            public Country(string name,int region,UnitKind unit,int perTurn){Name=name;Region=region;Reinforcement=unit;PerTurn=perTurn;}
        }
        public static readonly Country[] Countries={
            new Country("Marca del Alba",0,UnitKind.Footman,2),new Country("Valdeluz",0,UnitKind.Archer,1),
            new Country("Paso del Rey",1,UnitKind.Footman,2),new Country("Ribera Gris",1,UnitKind.Mage,1),
            new Country("Las Atalayas",2,UnitKind.Guard,1),new Country("Ceniza",2,UnitKind.Footman,2)
        };
        // Authored angular shelves: each contour has recesses and promontories, rather than a radial wall.
        public static readonly Vector2[][] Cliffs={
            new[]{new Vector2(-58,3),new Vector2(-53,-4),new Vector2(-40,-6),new Vector2(-32,-3),new Vector2(-24,-7),new Vector2(-17,-1),new Vector2(-17,9),new Vector2(-10,14),new Vector2(-13,21),new Vector2(-18,24),new Vector2(-18,33),new Vector2(-29,34),new Vector2(-34,27),new Vector2(-45,26),new Vector2(-52,22),new Vector2(-59,16)},
            new[]{new Vector2(16,8),new Vector2(19,0),new Vector2(27,-4),new Vector2(35,-5),new Vector2(43,-1),new Vector2(45,6),new Vector2(56,8),new Vector2(59,15),new Vector2(54,19),new Vector2(56,30),new Vector2(46,36),new Vector2(34,36),new Vector2(30,40),new Vector2(23,34),new Vector2(18,26),new Vector2(21,18)}
        };
        // Flat building pads within otherwise continuously sculpted hills. Defined before Towns.
        static readonly Vector2[] Pads={new(-38,-12),new(-47,12),new(-21,5),new(-23,30),new(-2,24),new(8,3),new(1,-18),new(28,30),new(43,9),new(38,-25),new(20,-39),new(-24,-34)};
        public static readonly Vector4[] Islands={new(-47,53,12,8),new(-8,69,13,9)};
        public static readonly City[] Towns={
            new City("dawn","Bastión del Alba",-38,-12,0,0,0,true),new City("pine","Pinar Alto",-47,12,0,0,0),
            new City("mill","Molino Viejo",-21,5,-1,0,1),new City("meadow","Valdeluz",-23,30,-1,0,1),
            new City("gate","Puerta de Piedra",-2,24,-1,1,2),new City("ford","Valle del Fresno",8,3,-1,1,2),
            new City("stone","Piedra Vieja",1,-18,-1,1,3),new City("ash","Torre del Roble",28,30,-1,2,4),
            new City("watch","Vigía del Este",43,9,-1,2,4),new City("red","Fortaleza Carmesí",38,-25,1,2,5,true),
            new City("highland","Altos de Ceniza",20,-39,1,2,5),new City("west","Marca del Sur",-24,-34,-1,1,3)
        };
        public static float Coast(float x){x/=Spacing;return (40+.26f*x+4*Mathf.Sin(x*.09f)+3*Mathf.Sin(x*.2f))*Spacing;}
        public static float IslandDistance(float x,float z,int index)
        {
            var island=Islands[index];float px=x/Spacing-island.x,pz=z/Spacing-island.y;
            float angle=Mathf.Atan2(pz/island.w,px/island.z);
            float shape=1+.075f*Mathf.Sin(angle*3+index)+.045f*Mathf.Cos(angle*5);
            return (1-new Vector2(px/island.z,pz/island.w).magnitude/shape)*Mathf.Min(island.z,island.w)*Spacing;
        }
        public static bool IsLand(float x,float z)=>Mathf.Abs(x)<=HalfWidth&&Mathf.Abs(z)<=HalfDepth&&((z<=Coast(x)&&!IsPond(x,z))||IslandDistance(x,z,0)>=0||IslandDistance(x,z,1)>=0);
        public static bool IsOcean(float x,float z)=>Mathf.Abs(x)<HalfWidth&&Mathf.Abs(z)<HalfDepth&&z>Coast(x)&&IslandDistance(x,z,0)<0&&IslandDistance(x,z,1)<0;
        public static bool IsPond(float x,float z)
        {
            x/=Spacing;z/=Spacing;
            return Ellipse(x,z,-11,-30,8,5)<.87f||Ellipse(x,z,16,-4,3,2)<.86f;
        }
        static float Ellipse(float x,float z,float cx,float cz,float rx,float rz)=>new Vector2((x-cx)/rx,(z-cz)/rz).magnitude;
        public static float CliffDistance(Vector2 point,int index)
        {
            var polygon=Cliffs[index];float distance=float.MaxValue;bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
            {
                var a=polygon[j];var b=polygon[i];var edge=b-a;
                distance=Mathf.Min(distance,(point-a-edge*Mathf.Clamp01(Vector2.Dot(point-a,edge)/edge.sqrMagnitude)).magnitude);
                if((a.y>point.y)!=(b.y>point.y)&&point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return inside?distance:-distance;
        }
        static float Terrace(float x,float z,int index,float height)
        {
            float distance=CliffDistance(new Vector2(x,z),index);
            float fracture=.16f*Mathf.Sin(x*1.4f+z*.8f)+.08f*Mathf.Sin(z*2.7f-x*.5f);
            float d=distance+fracture;
            float wall=d<-.65f?Mathf.Lerp(0,.12f,Mathf.InverseLerp(-2.1f,-.65f,d))
                :d<.35f?Mathf.Lerp(.12f,.86f,Mathf.InverseLerp(-.65f,.35f,d))
                :Mathf.Lerp(.86f,1,Mathf.InverseLerp(.35f,1.35f,d));
            float cx=index==0?-32:34;
            float south=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,10,z)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,5.5f,Mathf.Abs(x-cx))));
            float ramp=Mathf.SmoothStep(0,1,Mathf.InverseLerp(index==0?-12:-15,index==0?4:6,z));
            wall=Mathf.Lerp(wall,ramp,south);
            float east=Mathf.SmoothStep(0,1,Mathf.InverseLerp(index==0?-27:41,index==0?-23:45,x))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,5.5f,Mathf.Abs(z-(index==0?14:13)))));
            ramp=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(index==0?-21:49,index==0?-3:65,x));
            return Mathf.Lerp(wall,ramp,east)*height;
        }
        public static float Height(float x,float z)
        {
            float wx=x,wz=z;
            if(z>Coast(x))
            {
                float d=Mathf.Max(IslandDistance(x,z,0),IslandDistance(x,z,1));
                return Mathf.Lerp(-.24f,1.85f,Mathf.SmoothStep(0,1,Mathf.Clamp01(d/8)))+.22f*Mathf.Sin(x*.12f)*Mathf.Sin(z*.15f)*Mathf.SmoothStep(0,1,Mathf.Clamp01(d/5));
            }
            x/=Spacing;z/=Spacing;
            float west=Terrace(x,z,0,3.8f),east=Terrace(x,z,1,6.2f);
            float rocky=Mathf.SmoothStep(0,2.1f,Mathf.Clamp01((20-Vector2.Distance(new Vector2(x,z),new Vector2(57,-40)))/11));
            float pond=Mathf.Min(Ellipse(x,z,-11,-30,8,5),Ellipse(x,z,16,-4,3,2));
            float depression=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.15f,pond)))*.6f;
            float coastFade=Mathf.SmoothStep(0,1,Mathf.Clamp01((Coast(wx)-wz)/7));
            float padFade=1;
            foreach(var pad in Pads)padFade=Mathf.Min(padFade,Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.2f,11,Vector2.Distance(new Vector2(x,z),pad))));
            float rolling=(.9f+.63f*Mathf.Sin(x*.12f+Mathf.Sin(z*.085f))+.5f*Mathf.Cos(z*.17f-x*.05f))*(.55f+.45f*Mathf.PerlinNoise(x*.047f+16,z*.047f+4));
            float mountain=13.5f*Mathf.Pow(Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,z),new Vector2(55,29))/16),1.6f);
            float h=Mathf.Max(west,east,rocky)+rolling*padFade*coastFade+mountain-depression;
            return TerrainHydrology.Carve(wx,wz,h);
        }
        public static Vector3 Point(float x,float z)=>new Vector3(x,Height(x,z),z);
    }
}
