using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public static class MapLayout
    {
        public const float Spacing = 1.4f;
        public const int TerrainLayer = 8;
        public static float HalfWidth { get; private set; }
        public static float HalfDepth { get; private set; }
        public static bool IsExpanded { get; private set; }
        public static string MapName => IsExpanded ? "Cuatro Riberas" : "Las Marcas";
        static readonly int[] ClassicMainlandHarborX = { -58, -37, -3, 20, 43 };
        static readonly int[] ExpandedMainlandHarborX = { -58, -32, -7, 20, 43 };
        public static int[] MainlandHarborX => IsExpanded ? ExpandedMainlandHarborX : ClassicMainlandHarborX;

        // Shared by port placement and vegetation, before the ports exist in the scene.
        public static Vector3 MainlandHarborLanding(int index)
        {
            float x = MainlandHarborX[index] * Spacing;
            return Point(x, Coast(x) - 4);
        }
        public static Vector3 IslandHarborLanding(int index)
        {
            var island = Islands[index];
            return Point(island.x * Spacing, (island.y - island.w) * Spacing + 4);
        }

        public readonly struct City
        {
            public readonly string Id, Name; public readonly Vector3 Position; public readonly int Owner, Region, Country; public readonly bool Capital;
            public City(string id, string name, float x, float z, int owner, int region, int country, bool capital = false)
            { Id = id; Name = name; Position = Point(x * Spacing, z * Spacing); Owner = owner; Region = region; Country = country; Capital = capital; }
        }
        public readonly struct Country
        {
            public readonly string Name; public readonly int Region, PerTurn; public readonly UnitKind Reinforcement; public readonly Vector3 CampPoint;
            public Country(string name, int region, UnitKind unit, int perTurn, Vector3 campPoint = default(Vector3))
            { Name = name; Region = region; Reinforcement = unit; PerTurn = perTurn; CampPoint = campPoint; }
        }
        public static Country[] Countries { get; private set; }
        public static Vector2[][] Cliffs { get; private set; }
        public static Vector2[] Pads { get; private set; }
        public static Vector4[] Islands { get; private set; }
        public static City[] Towns { get; private set; }

        static readonly Country[] ClassicCountries = {
            new Country("Marca del Alba",0,UnitKind.Archer,1), new Country("Valdeluz",0,UnitKind.Archer,1),
            new Country("Paso del Rey",1,UnitKind.Archer,1), new Country("Ribera Gris",1,UnitKind.Archer,1),
            new Country("Las Atalayas",2,UnitKind.Archer,1), new Country("Ceniza",2,UnitKind.Archer,1)
        };
        static readonly Vector2[] ClassicPads = { new(-38,-12),new(-47,12),new(-21,5),new(-23,30),new(-2,24),new(8,3),new(1,-18),new(28,30),new(43,9),new(38,-25),new(20,-39),new(-24,-34) };
        static readonly Vector4[] ClassicIslands = { new(-47,53,12,8),new(-8,69,13,9) };
        static readonly Vector2[][] ClassicCliffs = {
            new[]{new Vector2(-58,3),new Vector2(-53,-4),new Vector2(-40,-6),new Vector2(-32,-3),new Vector2(-24,-7),new Vector2(-17,-1),new Vector2(-17,9),new Vector2(-10,14),new Vector2(-13,21),new Vector2(-18,24),new Vector2(-18,33),new Vector2(-29,34),new Vector2(-34,27),new Vector2(-45,26),new Vector2(-52,22),new Vector2(-59,16)},
            new[]{new Vector2(16,8),new Vector2(19,0),new Vector2(27,-4),new Vector2(35,-5),new Vector2(43,-1),new Vector2(45,6),new Vector2(56,8),new Vector2(59,15),new Vector2(54,19),new Vector2(56,30),new Vector2(46,36),new Vector2(34,36),new Vector2(30,40),new Vector2(23,34),new Vector2(18,26),new Vector2(21,18)}
        };
        static City[] ClassicTowns;
        static readonly Vector2[] ExpandedPads = {
            new(-54,-58),new(-52,-25),new(-50,10),new(-46,42), new(-26,-58),new(-25,-24),new(-24,10),new(-20,45),
            new(18,-60),new(20,-26),new(19,11),new(23,45), new(48,-55),new(46,-20),new(45,15),new(42,45),
            new(-50,92),new(-21,98),new(-3,98),new(30,99)
        };
        // Extend islands northward, retaining the south coast/berths. Independent
        // city and port garrisons must not start inside each other's tower range.
        static readonly Vector4[] ExpandedIslands = { new(-50,87,10,11),new(-12,96,18,16),new(30,94,10,11) };
        static readonly Vector2[][] ExpandedCliffs = {
            new[]{new Vector2(-61,8),new Vector2(-55,-4),new Vector2(-42,-8),new Vector2(-32,-2),new Vector2(-28,10),new Vector2(-32,25),new Vector2(-43,32),new Vector2(-56,28)},
            new[]{new Vector2(29,7),new Vector2(34,-4),new Vector2(48,-7),new Vector2(61,2),new Vector2(59,20),new Vector2(51,32),new Vector2(36,28),new Vector2(27,17)}
        };
        static readonly Country[] ExpandedCountries = {
            new Country("Marca Occidental",0,UnitKind.Archer,2,new Vector3(-54 * Spacing,0,-58 * Spacing)), new Country("Cuenca del Río",1,UnitKind.Archer,2,new Vector3(-25 * Spacing,0,-24 * Spacing)),
            new Country("Altos Centrales",2,UnitKind.Archer,2,new Vector3(20 * Spacing,0,-26 * Spacing)), new Country("Frontera Oriental",3,UnitKind.Archer,2,new Vector3(46 * Spacing,0,-20 * Spacing)),
            new Country("Archipiélago Norte",4,UnitKind.Archer,2,new Vector3(-16 * Spacing,0,90 * Spacing))
        };

        static MapLayout() { Configure(false); }
        public static void Configure(bool expanded)
        {
            IsExpanded = expanded; HalfWidth = 72 * Spacing; HalfDepth = (expanded ? 112 : 84) * Spacing;
            Pads = expanded ? ExpandedPads : ClassicPads; Islands = expanded ? ExpandedIslands : ClassicIslands; Cliffs = expanded ? ExpandedCliffs : ClassicCliffs;
            Countries = expanded ? ExpandedCountries : ClassicCountries; TerrainHydrology.Configure(expanded); Towns = expanded ? BuildExpandedTowns() : BuildClassicTowns();
            UploadShaderGlobals();
        }
        static City[] BuildClassicTowns()
        {
            if (ClassicTowns != null) return ClassicTowns;
            ClassicTowns = new[] {
                new City("dawn","Bastión del Alba",-38,-12,0,0,0,true),new City("pine","Pinar Alto",-47,12,0,0,0),
                new City("mill","Molino Viejo",-21,5,-1,0,1),new City("meadow","Valdeluz",-23,30,-1,0,1),
                new City("gate","Puerta de Piedra",-2,24,-1,1,2),new City("ford","Valle del Fresno",8,3,-1,1,2),
                new City("stone","Piedra Vieja",1,-18,-1,1,3),new City("ash","Torre del Roble",28,30,-1,2,4),
                new City("watch","Vigía del Este",43,9,-1,2,4),new City("red","Fortaleza Carmesí",38,-25,1,2,5,true),
                new City("highland","Altos de Ceniza",20,-39,1,2,5),new City("west","Marca del Sur",-24,-34,-1,1,3)
            };
            return ClassicTowns;
        }
        static void UploadShaderGlobals()
        {
            Shader.SetGlobalFloat("_RiskExpandedMap", IsExpanded ? 1f : 0f);
            Shader.SetGlobalInt("_RiskIslandCount", Islands == null ? 0 : Islands.Length);
            var islands = new Vector4[8];
            if (Islands != null) for (int i = 0; i < Islands.Length && i < islands.Length; i++) islands[i] = Islands[i];
            Shader.SetGlobalVectorArray("_RiskIslands", islands);
            Shader.SetGlobalVector("_RiskCoastParams0", IsExpanded ? new Vector4(70f, .13f, 3.5f, 2f) : new Vector4(40f, .26f, 4f, 3f));
            Shader.SetGlobalVector("_RiskCoastParams1", IsExpanded ? new Vector4(.08f, .16f, 0f, 0f) : new Vector4(.09f, .2f, 0f, 0f));
        }
        static City[] BuildExpandedTowns()
        {
            return new[] {
                new City("west-01","Bastión Occidental",-54,-58,0,0,0,true),new City("west-02","Pinar Occidental",-52,-25,0,0,0),new City("west-03","Marjal Occidental",-50,10,1,0,0),new City("west-04","Cresta Occidental",-46,42,1,0,0),
                new City("river-01","Puerta del Río",-26,-58,0,1,1),new City("river-02","Molino del Río",-25,-24,0,1,1),new City("river-03","Vado del Río",-24,10,1,1,1),new City("river-04","Ribera Alta",-20,45,1,1,1),
                new City("high-01","Bastión Central",18,-60,0,2,2),new City("high-02","Loma Central",20,-26,0,2,2),new City("high-03","Paso Central",19,11,1,2,2),new City("high-04","Atalaya Central",23,45,1,2,2),
                new City("east-01","Puerta Oriental",48,-55,0,3,3),new City("east-02","Cantera Oriental",46,-20,0,3,3),new City("east-03","Vigía Oriental",45,15,1,3,3),new City("east-04","Cresta Oriental",42,45,1,3,3),
                new City("isle-01","Isla del Roble",-50,92,0,4,4),new City("isle-02","Isla del Viento",-21,98,0,4,4),new City("isle-03","Isla del Faro",-3,98,1,4,4),new City("isle-04","Isla del Alba",30,99,1,4,4)
            };
        }
        public static float Coast(float x)
        {
            float normalized = x / Spacing;
            return (IsExpanded ? 70 + .13f * normalized + 3.5f * Mathf.Sin(normalized * .08f) + 2f * Mathf.Sin(normalized * .16f) : 40 + .26f * normalized + 4 * Mathf.Sin(normalized * .09f) + 3 * Mathf.Sin(normalized * .2f)) * Spacing;
        }
        public static float IslandDistance(float x, float z, int index)
        {
            if (index < 0 || index >= Islands.Length) return -1;
            var island = Islands[index]; float px = x / Spacing - island.x, pz = z / Spacing - island.y;
            float angle = Mathf.Atan2(pz / island.w, px / island.z); float shape = 1 + .075f * Mathf.Sin(angle * 3 + index) + .045f * Mathf.Cos(angle * 5);
            return (1 - new Vector2(px / island.z, pz / island.w).magnitude / shape) * Mathf.Min(island.z, island.w) * Spacing;
        }
        public static bool IsLand(float x, float z)
        {
            if (Mathf.Abs(x) > HalfWidth || Mathf.Abs(z) > HalfDepth) return false;
            bool land = (z <= Coast(x) && !IsPond(x, z)) || AnyIslandAt(x, z);
            return land && (!IsExpanded || !TerrainHydrology.IsChannel(x, z));
        }
        public static bool IsOcean(float x, float z) => Mathf.Abs(x) < HalfWidth && Mathf.Abs(z) < HalfDepth && z > Coast(x) && !AnyIslandAt(x, z);
        static bool AnyIslandAt(float x, float z) { for (int i = 0; i < Islands.Length; i++) if (IslandDistance(x, z, i) >= 0) return true; return false; }
        public static bool IsPond(float x, float z)
        {
            return PondDistance(x,z)<.87f;
        }
        static float PondDistance(float x,float z)
        {
            x/=Spacing;z/=Spacing;
            return IsExpanded ? Mathf.Min(Ellipse(x,z,-39,-38,9,5),Ellipse(x,z,35,-32,6,4)) : Mathf.Min(Ellipse(x,z,-11,-30,8,5),Ellipse(x,z,16,-4,3,2));
        }
        static float Ellipse(float x, float z, float cx, float cz, float rx, float rz) => new Vector2((x - cx) / rx, (z - cz) / rz).magnitude;
        public static float CliffDistance(Vector2 point, int index)
        {
            var polygon = Cliffs[index]; float distance = float.MaxValue; bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[j]; var b = polygon[i]; var edge = b - a; distance = Mathf.Min(distance, (point - a - edge * Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude)).magnitude);
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? distance : -distance;
        }
        static float Terrace(float x, float z, int index, float height)
        {
            float distance = CliffDistance(new Vector2(x, z), index); float fracture = .16f * Mathf.Sin(x * 1.4f + z * .8f) + .08f * Mathf.Sin(z * 2.7f - x * .5f); float d = distance + fracture;
            float wall = d < -.65f ? Mathf.Lerp(0, .12f, Mathf.InverseLerp(-2.1f, -.65f, d)) : d < .35f ? Mathf.Lerp(.12f, .86f, Mathf.InverseLerp(-.65f, .35f, d)) : Mathf.Lerp(.86f, 1, Mathf.InverseLerp(.35f, 1.35f, d));
            float cx = index == 0 ? -32 : 34; float south = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6, 10, z))) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.5f, 5.5f, Mathf.Abs(x - cx))));
            float ramp = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(index == 0 ? -12 : -15, index == 0 ? 4 : 6, z)); wall = Mathf.Lerp(wall, ramp, south);
            float east = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(index == 0 ? -27 : 41, index == 0 ? -23 : 45, x)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.5f, 5.5f, Mathf.Abs(z - (index == 0 ? 14 : 13)))));
            ramp = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(index == 0 ? -21 : 49, index == 0 ? -3 : 65, x)); return Mathf.Lerp(wall, ramp, east) * height;
        }
        public static float Height(float x, float z)
        {
            float height=IsExpanded?ExpandedHeight(x,z):ClassicHeight(x,z);
            float pond=PondDistance(x,z);
            return Mathf.Lerp(-1.05f+.25f*Mathf.Clamp01(pond),height,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.18f,pond)));
        }
        static float ExpandedHeight(float x, float z)
        {
            if (z > Coast(x)) { float d = float.NegativeInfinity; for (int i = 0; i < Islands.Length; i++) d = Mathf.Max(d, IslandDistance(x, z, i)); if (d < 0) return SeaFloor(x, z); return Mathf.Lerp(-.24f, 1.85f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / 8))) + .16f * Mathf.Sin(x * .12f) * Mathf.Sin(z * .15f) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / 5)); }
            float nx = x / Spacing, nz = z / Spacing; var center = new Vector2(8, -85); float rolling = .8f + .5f * Mathf.Sin(nx * .12f + Mathf.Sin(nz * .085f)) + .35f * Mathf.Cos(nz * .17f - nx * .05f);
            float mountain = 10.5f * Mathf.Pow(Mathf.Clamp01(1 - Vector2.Distance(new Vector2(nx, nz), center) / 18), 1.55f); float foothills = 1.4f * Mathf.Clamp01(1 - Vector2.Distance(new Vector2(nx, nz), center) / 31); float h = Mathf.Max(1.2f, rolling + foothills + mountain);
            for (int i = 0; i < Pads.Length; i++) { float distance = Vector2.Distance(new Vector2(nx, nz), Pads[i]); if (distance < 7.5f) h = Mathf.Lerp(h, 1.7f + .15f * Mathf.Sin(Pads[i].x), Mathf.SmoothStep(1, 0, Mathf.InverseLerp(1.8f, 7.5f, distance))); }
            h=Mathf.Lerp(-.24f,h,Mathf.SmoothStep(0,1,Mathf.Clamp01((Coast(x)-z)/7)));
            return TerrainHydrology.Carve(x, z, h);
        }
        static float ClassicHeight(float x, float z)
        {
            float wx = x, wz = z; if (z > Coast(x)) { float d = Mathf.Max(IslandDistance(x, z, 0), IslandDistance(x, z, 1)); if (d < 0) return SeaFloor(x, z); return Mathf.Lerp(-.24f, 1.85f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / 8))) + .22f * Mathf.Sin(x * .12f) * Mathf.Sin(z * .15f) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / 5)); }
            x /= Spacing; z /= Spacing; float west = Terrace(x, z, 0, 3.8f), east = Terrace(x, z, 1, 6.2f); float rocky = Mathf.SmoothStep(0, 2.1f, Mathf.Clamp01((20 - Vector2.Distance(new Vector2(x, z), new Vector2(57, -40))) / 11));
            float pond = Mathf.Min(Ellipse(x, z, -11, -30, 8, 5), Ellipse(x, z, 16, -4, 3, 2)); float depression = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1.15f, pond))) * .6f; float coastFade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Coast(wx) - wz) / 7)); float padFade = 1;
            foreach (var pad in ClassicPads) padFade = Mathf.Min(padFade, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5.2f, 11, Vector2.Distance(new Vector2(x, z), pad))));
            float rolling = (.9f + .63f * Mathf.Sin(x * .12f + Mathf.Sin(z * .085f)) + .5f * Mathf.Cos(z * .17f - x * .05f)) * (.55f + .45f * Mathf.PerlinNoise(x * .047f + 16, z * .047f + 4)); float mountain = 13.5f * Mathf.Pow(Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x, z), new Vector2(55, 29)) / 16), 1.6f);
            return TerrainHydrology.Carve(wx, wz, Mathf.Max(west, east, rocky) + rolling * padFade * coastFade + mountain - depression);
        }
        public static Vector3 Point(float x, float z) => new Vector3(x, Height(x, z), z);
        public static float SeaFloor(float x, float z)
        {
            float nearestIsland = float.MaxValue; for (int i = 0; i < Islands.Length; i++) nearestIsland = Mathf.Min(nearestIsland, -IslandDistance(x, z, i));
            float shore = Mathf.Max(0, Mathf.Min(z - Coast(x), nearestIsland)); return TerrainHydrology.Carve(x, z, -.24f - Mathf.Min(24, .10f * shore * shore));
        }
    }
}
