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
        public static ScenarioMap Scenario { get; private set; }
        public static ImportedMapData Imported { get; private set; }
        public static bool IsImported => Imported != null;
        public static Vector2 PlayableMin => IsImported ? new Vector2(Imported.PlayableMinX, Imported.PlayableMinZ) : new Vector2(-HalfWidth, -HalfDepth);
        public static Vector2 PlayableMax => IsImported ? new Vector2(Imported.PlayableMaxX, Imported.PlayableMaxZ) : new Vector2(HalfWidth, HalfDepth);
        public static Vector3 PlayableCenter => new Vector3((PlayableMin.x + PlayableMax.x) * .5f, 0, (PlayableMin.y + PlayableMax.y) * .5f);
        public static string MapName => IsImported ? (Scenario == ScenarioMap.Europe ? "Europe" : "New World") : IsExpanded ? "Cuatro Riberas" : "Las Marcas";
        public static string ScenarioDetail(ScenarioMap scenario)
        {
            if (scenario == ScenarioMap.Classic) return ClassicPads.Length + " ciudades · " + ClassicCountries.Length + " grupos";
            if (scenario == ScenarioMap.Riverlands) return ExpandedPads.Length + " ciudades · " + ExpandedCountries.Length + " grupos";
            return ImportedMapData.ScenarioDetail(scenario);
        }
        public static int MaximumPlayersForScenario(ScenarioMap scenario)
        {
            int cities = scenario == ScenarioMap.Classic ? ClassicPads.Length
                : scenario == ScenarioMap.Riverlands ? ExpandedPads.Length : ImportedMapData.ScenarioCityCount(scenario);
            return PlayerRules.MaximumPlayersForCityCount(cities);
        }
        static readonly int[] ClassicMainlandHarborX = { -58, -37, -3, 22, 43 };
        static readonly int[] ExpandedMainlandHarborX = { -54, -41, -19, 20, 43 };
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
            public readonly bool IsPort;
            public readonly Vector3 ClaimPoint;
            public City(string id, string name, float x, float z, int owner, int region, int country, bool capital = false)
            { Id = id; Name = name; Position = Point(x * Spacing, z * Spacing); Owner = owner; Region = region; Country = country; Capital = capital; IsPort=false;ClaimPoint=Point(Position.x,Position.z-4.2f); }
            public City(ImportedMapData.City city)
            { Id=city.id;Name=city.name;Position=city.port?new Vector3(city.x,.55f,city.z):Point(city.x,city.z);Owner=city.country%2;Region=Country=city.country;Capital=false;IsPort=city.port;ClaimPoint=city.port?new Vector3(city.claimX,.55f,city.claimZ):Point(city.claimX,city.claimZ); }
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

        // One authored record supplies both the town and its terrain clearing.
        // Coordinates cannot drift between a rendered city and a separate pad list.
        readonly struct AuthoredCity
        {
            readonly string id,name;readonly float x,z;readonly int owner,region,country;readonly bool capital;
            public AuthoredCity(string id,string name,float x,float z,int owner,int region,int country,bool capital=false)
            {this.id=id;this.name=name;this.x=x;this.z=z;this.owner=owner;this.region=region;this.country=country;this.capital=capital;}
            public Vector2 Pad=>new Vector2(x,z);
            public City Build()=>new City(id,name,x,z,owner,region,country,capital);
        }
        static readonly AuthoredCity[] ClassicCityCatalog=BuildClassicCatalog();
        static readonly AuthoredCity[] ExpandedCityCatalog=BuildExpandedCatalog();
        static readonly Country[] ClassicCountries = {
            new Country("Marca del Alba",0,UnitKind.Archer,1), new Country("Valle de los Pinos",1,UnitKind.Archer,1),
            new Country("Escarpa de Poniente",2,UnitKind.Archer,1), new Country("Cuenca del Fresno",3,UnitKind.Archer,1),
            new Country("Puertas de Oriente",4,UnitKind.Archer,1), new Country("Sierra Carmesí",5,UnitKind.Archer,1),
            new Country("Dehesa de Poniente",6,UnitKind.Archer,1), new Country("Campos del Secano",7,UnitKind.Archer,1),
            new Country("Lomas de Azafrán",8,UnitKind.Archer,1), new Country("Costa de Sal",9,UnitKind.Archer,1),
            new Country("Islas del Norte",10,UnitKind.Archer,1)
        };
        // Each authored pad is a deliberately clear, level site: away from ponds,
        // the river and cliff edges, with larger country groups where the land opens up.
        static readonly Vector2[] ClassicPads = System.Array.ConvertAll(ClassicCityCatalog,city=>city.Pad);
        static readonly Vector4[] ClassicIslands = { new(-47,53,12,12),new(-8,69,13,11) };
        static readonly Vector2[][] ClassicCliffs = {
            new[]{new Vector2(-58,3),new Vector2(-53,-4),new Vector2(-40,-6),new Vector2(-32,-3),new Vector2(-24,-7),new Vector2(-17,-1),new Vector2(-17,9),new Vector2(-10,14),new Vector2(-13,21),new Vector2(-18,24),new Vector2(-18,33),new Vector2(-29,34),new Vector2(-34,27),new Vector2(-45,26),new Vector2(-52,22),new Vector2(-59,16)},
            new[]{new Vector2(16,8),new Vector2(19,0),new Vector2(27,-4),new Vector2(35,-5),new Vector2(43,-1),new Vector2(45,6),new Vector2(56,8),new Vector2(59,15),new Vector2(54,19),new Vector2(56,30),new Vector2(46,36),new Vector2(34,36),new Vector2(30,40),new Vector2(23,34),new Vector2(18,26),new Vector2(21,18)}
        };
        static readonly Vector2[] ExpandedPads = System.Array.ConvertAll(ExpandedCityCatalog,city=>city.Pad);
        // Independent island settlements are kept on their original navigable land,
        // while mainland groups follow river banks, crossings, hills and coasts.
        static readonly Vector4[] ExpandedIslands = { new(-50,87,12,14),new(-12,96,18,16),new(30,94,11,13) };
        static readonly Vector2[][] ExpandedCliffs = {
            new[]{new Vector2(-61,8),new Vector2(-55,-4),new Vector2(-42,-8),new Vector2(-32,-2),new Vector2(-28,10),new Vector2(-32,25),new Vector2(-43,32),new Vector2(-56,28)},
            new[]{new Vector2(29,7),new Vector2(34,-4),new Vector2(48,-7),new Vector2(61,2),new Vector2(59,20),new Vector2(51,32),new Vector2(36,28),new Vector2(27,17)}
        };
        static readonly Country[] ExpandedCountries = {
            new Country("Marca Occidental",0,UnitKind.Archer,2), new Country("Bosques de Poniente",1,UnitKind.Archer,2),
            new Country("Cuenca del Río",2,UnitKind.Archer,2), new Country("Costa Occidental",3,UnitKind.Archer,2),
            new Country("Colinas Occidentales",4,UnitKind.Archer,2), new Country("Ribera Alta",5,UnitKind.Archer,2),
            new Country("Altos Centrales",6,UnitKind.Archer,2), new Country("Frontera Oriental",7,UnitKind.Archer,2),
            new Country("Llanuras de Levante",8,UnitKind.Archer,2), new Country("Puertas del Estuario",9,UnitKind.Archer,2),
            new Country("Archipiélago Norte",10,UnitKind.Archer,2)
        };

        static MapLayout() { Configure(false); }
        public static void Configure(bool expanded) => Configure(expanded ? ScenarioMap.Riverlands : ScenarioMap.Classic);
        public static void Configure(ScenarioMap scenario)
        {
            Scenario=scenario;IsExpanded=scenario==ScenarioMap.Riverlands;Imported=null;
            bool expanded=IsExpanded;
            if (scenario == ScenarioMap.Europe || scenario == ScenarioMap.NewWorld)
            {
                Imported=ImportedMapData.Load(scenario);HalfWidth=Imported.HalfWidth;HalfDepth=Imported.HalfDepth;
                Islands=System.Array.Empty<Vector4>();Cliffs=System.Array.Empty<Vector2[]>();Pads=new Vector2[Imported.cities.Length];
                Countries=new Country[Imported.countries.Length];Towns=new City[Imported.cities.Length];
                for(int i=0;i<Countries.Length;i++){var c=Imported.countries[i];Countries[i]=new Country(c.name,i,UnitKind.Archer,0,Point(c.x,c.z));}
                for(int i=0;i<Towns.Length;i++){Towns[i]=new City(Imported.cities[i]);Pads[i]=new Vector2(Towns[i].Position.x/Spacing,Towns[i].Position.z/Spacing);}
                Countries=ApplySharedReinforcementFormula(Countries,Towns);
                TerrainHydrology.Configure(false);UploadShaderGlobals();return;
            }
            HalfWidth = 72 * Spacing; HalfDepth = (expanded ? 112 : 90) * Spacing;
            Pads = expanded ? ExpandedPads : ClassicPads; Islands = expanded ? ExpandedIslands : ClassicIslands; Cliffs = expanded ? ExpandedCliffs : ClassicCliffs;
            TerrainHydrology.Configure(expanded); Towns = System.Array.ConvertAll(expanded?ExpandedCityCatalog:ClassicCityCatalog,city=>city.Build());
            Countries = ApplySharedReinforcementFormula(expanded ? ExpandedCountries : ClassicCountries,Towns);
            UploadShaderGlobals();
        }
        // Saran's FFA trigger awards ceil(cityCount / 2) reinforcement points per
        // completed region.  Derive every authored and imported country from its
        // actual roster so map density cannot quietly create a second economy rule.
        static Country[] ApplySharedReinforcementFormula(Country[] source,City[] towns)
        {
            var counts=new int[source.Length];
            foreach(var town in towns) if(town.Country>=0&&town.Country<counts.Length) counts[town.Country]++;
            var result=new Country[source.Length];
            for(int i=0;i<source.Length;i++)
            {
                var country=source[i];
                result[i]=new Country(country.Name,country.Region,country.Reinforcement,
                    BattleRules.CountryReinforcementPointsPerRound(counts[i]),country.CampPoint);
            }
            return result;
        }

        static AuthoredCity[] BuildClassicCatalog()
        {
            return new[] {
                new AuthoredCity("dawn","Bastión del Alba",-38,-12,0,0,0,true),new AuthoredCity("pine","Pinar Alto",-47,12,0,0,0),new AuthoredCity("cordillera-norte","Cordillera del Alba",-60,-8,-1,0,0),
                new AuthoredCity("mill","Molino Viejo",-21,5,-1,1,1),new AuthoredCity("meadow","Valdeluz",-23,30,-1,1,1),new AuthoredCity("encinar-centro","Encinar Central",-37,-47,-1,2,2),
                new AuthoredCity("crest-west","Cresta de Poniente",-60,-30,-1,2,2),new AuthoredCity("dehesa-norte","Dehesa Norte",-54,-47,-1,2,2),
                new AuthoredCity("gate","Puerta de Piedra",-2,24,-1,3,3),new AuthoredCity("ford","Valle del Fresno",8,3,-1,3,3),new AuthoredCity("stone","Piedra Vieja",1,-18,-1,3,3),new AuthoredCity("west","Marca del Sur",-24,-34,-1,3,3),
                new AuthoredCity("ash","Torre del Roble",28,30,-1,4,4),new AuthoredCity("watch","Vigía del Este",43,9,-1,4,4),new AuthoredCity("senda-orient","Senda Oriental",65,15,-1,4,4),new AuthoredCity("torre-norte","Torre del Norte",15,45,-1,4,4),
                new AuthoredCity("red","Fortaleza Carmesí",38,-25,1,5,5,true),new AuthoredCity("highland","Altos de Ceniza",20,-39,1,5,5),new AuthoredCity("guardia-oriental","Guardia Oriental",57,-25,-1,5,5),
                new AuthoredCity("dehesa","Dehesa de Poniente",-58,-62,-1,6,6),new AuthoredCity("encina","Encinar Bajo",-49,-81,-1,6,6),
                new AuthoredCity("secano","Campos del Secano",-33,-63,-1,7,7),new AuthoredCity("trigal","Trigal Dorado",-32,-85,-1,7,7),new AuthoredCity("azafran-norte","Azafrán del Norte",-10,-48,-1,7,7),
                new AuthoredCity("azafran","Lomas de Azafrán",-8,-69,-1,8,8),new AuthoredCity("olivar","Olivar de la Marca",1,-85,-1,8,8),new AuthoredCity("loma-sur","Loma del Sur",18,-62,-1,8,8),
                new AuthoredCity("costa-sur","Costa del Sur",36,-70,-1,9,9),new AuthoredCity("vigia-sal","Vigía de la Sal",59,-56,-1,9,9),
                new AuthoredCity("isla-bruma","Isla de la Bruma",-47,57,-1,10,10),new AuthoredCity("isla-roble","Dehesa de la Frontera",-69,-78,-1,6,6),new AuthoredCity("isla-viento","Isla del Viento",-8,74,-1,10,10),new AuthoredCity("isla-faro","Torre de la Sal",50,-83,-1,9,9)
            };
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
        static AuthoredCity[] BuildExpandedCatalog()
        {
            return new[] {
                new AuthoredCity("west-01","Bastión Occidental",-55,-59,0,0,0,true),new AuthoredCity("west-05","Marca del Sur",-67,-82,-1,0,0),new AuthoredCity("west-06","Bosque Bajo",-43,-83,-1,0,0),new AuthoredCity("west-07","Paso de Poniente",-66,-44,-1,0,0),
                new AuthoredCity("west-02","Pinar Occidental",-50,-27,0,1,1),new AuthoredCity("west-08","Loma del Roble",-39,-48,-1,1,1),new AuthoredCity("west-09","Marjal Occidental",-65,-10,-1,1,1),new AuthoredCity("west-10","Cresta del Bosque",-42,-5,-1,1,1),
                new AuthoredCity("river-01","Puerta del Río",-28,-59,0,2,2),new AuthoredCity("river-05","Vega del Sur",-20,-86,-1,2,2),new AuthoredCity("river-02","Molino del Río",-23,-22,0,2,2),new AuthoredCity("river-06","Vado Bajo",-14,-5,-1,2,2),new AuthoredCity("river-03","Ribera del Río",-27,12,1,2,2),
                new AuthoredCity("west-03","Linde Occidental",-53,8,1,3,3),new AuthoredCity("west-11","Peña del Mar",-64,24,-1,3,3),new AuthoredCity("west-04","Cresta Occidental",-44,44,1,4,4),new AuthoredCity("west-13","Puerto Alto",-63,55,-1,3,3),
                new AuthoredCity("west-12","Colina del Vado",-40,25,-1,4,4),new AuthoredCity("west-14","Senda del Norte",-31,58,-1,4,4),
                new AuthoredCity("river-04","Ribera Alta",-19,43,1,5,5),new AuthoredCity("river-07","Paso de los Sauces",-12,22,-1,5,5),new AuthoredCity("river-08","Estuario Verde",-7,59,-1,5,5),
                new AuthoredCity("high-01","Bastión Central",17,-58,0,6,6),new AuthoredCity("high-05","Altos del Sur",42,-85,-1,6,6),new AuthoredCity("high-02","Loma Central",22,-28,0,6,6),new AuthoredCity("high-06","Cerro de Piedra",35,-43,-1,6,6),
                new AuthoredCity("east-01","Puerta Oriental",49,-57,0,7,7),new AuthoredCity("east-05","Frontera del Sur",67,-75,-1,7,7),new AuthoredCity("east-02","Cantera Oriental",44,-18,0,7,7),new AuthoredCity("east-06","Paso de Levante",68,-40,-1,7,7),new AuthoredCity("high-07","Mirador del Río",32,-3,-1,7,7),new AuthoredCity("east-07","Fuerte del Este",66,-4,-1,7,7),
                new AuthoredCity("high-03","Paso Central",18,9,1,8,8),new AuthoredCity("east-03","Vigía Oriental",48,17,1,8,8),new AuthoredCity("high-08","Atalaya del Llano",31,28,-1,8,8),new AuthoredCity("east-08","Costa de Levante",65,31,-1,8,8),new AuthoredCity("east-04","Cresta Oriental",51,47,1,8,8),
                new AuthoredCity("high-04","Altos del Estuario",26,46,1,9,9),new AuthoredCity("high-09","Puerta del Estuario",18,63,-1,9,9),
                new AuthoredCity("isle-01","Isla del Roble",-54,94,0,10,10),new AuthoredCity("isle-02","Isla del Viento",-12,104,0,10,10),new AuthoredCity("isle-03","Vega del Confín",-60,-95,1,0,0),new AuthoredCity("isle-04","Isla del Alba",34,103,1,10,10),new AuthoredCity("isle-05","Vigía de Levante",68,52,-1,8,8)
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
            if(IsImported)return Imported.IsLand(x,z);
            if (Mathf.Abs(x) > HalfWidth || Mathf.Abs(z) > HalfDepth) return false;
            bool land = (z <= Coast(x) && !IsPond(x, z)) || AnyIslandAt(x, z);
            return land && (!IsExpanded || !TerrainHydrology.IsChannel(x, z));
        }
        public static bool IsOcean(float x, float z) => IsImported ? Imported.Contains(x,z) && !Imported.IsLand(x,z) && Mathf.Abs(Imported.WaterAt(x,z)+.24f)<.4f : Mathf.Abs(x) < HalfWidth && Mathf.Abs(z) < HalfDepth && z > Coast(x) && !AnyIslandAt(x, z);
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
            if(IsImported)return Imported.HeightAt(x,z);
            float height=IsExpanded?ExpandedHeight(x,z):ClassicHeight(x,z);
            float pond=PondDistance(x,z);
            return Mathf.Lerp(-1.05f+.25f*Mathf.Clamp01(pond),height,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.18f,pond)));
        }
        static float ExpandedHeight(float x, float z)
        {
            if (z > Coast(x)) { float d = float.NegativeInfinity; for (int i = 0; i < Islands.Length; i++) d = Mathf.Max(d, IslandDistance(x, z, i)); if (d < 0) return SeaFloor(x, z); return Mathf.Lerp(-.24f, 1.85f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / 8))) + .16f * Mathf.Sin(x * .12f) * Mathf.Sin(z * .15f) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / 5)); }
            float nx = x / Spacing, nz = z / Spacing;var point=new Vector2(nx,nz);float rolling = .8f + .5f * Mathf.Sin(nx * .12f + Mathf.Sin(nz * .085f)) + .35f * Mathf.Cos(nz * .17f - nx * .05f);
            float westPeak=9.2f*Mathf.Pow(Mathf.Clamp01(1-Vector2.Distance(point,new Vector2(-18,-87))/19),1.55f);
            float eastPeak=9.2f*Mathf.Pow(Mathf.Clamp01(1-Vector2.Distance(point,new Vector2(18,-87))/19),1.55f);
            float peaks=Mathf.Max(westPeak,eastPeak),foothills=1.35f*Mathf.Clamp01(1-Mathf.Min(Vector2.Distance(point,new Vector2(-18,-87)),Vector2.Distance(point,new Vector2(18,-87)))/32);
            float westRidge=Ridge(point,new Vector2(-67,-74),new Vector2(-45,42),9,6.1f,31);
            float eastRidge=Ridge(point,new Vector2(67,-74),new Vector2(45,42),9,6.1f,47);
            float h = Mathf.Max(1.2f, rolling + Mathf.Max(peaks + foothills,Mathf.Max(westRidge,eastRidge)));
            for (int i = 0; i < Pads.Length; i++) { float distance = Vector2.Distance(new Vector2(nx, nz), Pads[i]); if (distance < 7.5f) h = Mathf.Lerp(h, 1.7f + .15f * Mathf.Sin(Pads[i].x), Mathf.SmoothStep(1, 0, Mathf.InverseLerp(1.8f, 7.5f, distance))); }
            h=Mathf.Lerp(-.24f,h,Mathf.SmoothStep(0,1,Mathf.Clamp01((Coast(x)-z)/CoastBlendWidth(x))));
            return TerrainHydrology.Carve(x, z, h);
        }
        static float ClassicHeight(float x, float z)
        {
            float wx = x, wz = z; if (z > Coast(x)) { float d = Mathf.Max(IslandDistance(x, z, 0), IslandDistance(x, z, 1)); if (d < 0) return SeaFloor(x, z); return Mathf.Lerp(-.24f, 1.85f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / 8))) + .22f * Mathf.Sin(x * .12f) * Mathf.Sin(z * .15f) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / 5)); }
            x /= Spacing; z /= Spacing; float west = Terrace(x, z, 0, 3.8f), east = Terrace(x, z, 1, 6.2f); float rocky = Mathf.SmoothStep(0, 2.1f, Mathf.Clamp01((20 - Vector2.Distance(new Vector2(x, z), new Vector2(57, -40))) / 11));
            float pond = Mathf.Min(Ellipse(x, z, -11, -30, 8, 5), Ellipse(x, z, 16, -4, 3, 2)); float depression = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1.15f, pond))) * .6f; float coastFade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Coast(wx) - wz) / CoastBlendWidth(wx))); float padFade = 1;
            foreach (var pad in ClassicPads) padFade = Mathf.Min(padFade, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5.2f, 11, Vector2.Distance(new Vector2(x, z), pad))));
            float rolling = (.9f + .63f * Mathf.Sin(x * .12f + Mathf.Sin(z * .085f)) + .5f * Mathf.Cos(z * .17f - x * .05f)) * (.55f + .45f * Mathf.PerlinNoise(x * .047f + 16, z * .047f + 4)); float mountain = 13.5f * Mathf.Pow(Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x, z), new Vector2(55, 29)) / 16), 1.6f);
            float south=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-38,-78,z));
            float dryHills=south*(.2f+1.05f*Mathf.PerlinNoise(x*.061f+73,z*.038f+29));
            float washCenter=-18+7*Mathf.Sin(z*.055f),wash=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,13,Mathf.Abs(x-washCenter)));
            return TerrainHydrology.Carve(wx, wz, Mathf.Max(west, east, rocky) + (rolling+dryHills-wash*south*.48f) * padFade * coastFade + mountain - depression);
        }
        static float Ridge(Vector2 point,Vector2 start,Vector2 end,float width,float height,float seed)
        {
            var axis=end-start;float along=Mathf.Clamp01(Vector2.Dot(point-start,axis)/axis.sqrMagnitude);
            float endFade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.12f,along))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,.88f,along));
            float distance=Vector2.Distance(point,start+axis*along),crest=Mathf.Pow(Mathf.Clamp01(1-distance/width),1.7f);
            float fracture=.78f+.22f*Mathf.PerlinNoise(point.x*.11f+seed,point.y*.085f+seed*.37f);
            return height*crest*endFade*fracture;
        }
        static float CoastExposure(float x)
        {
            float probe=6*Spacing,center=Coast(x),before=Coast(x-probe),after=Coast(x+probe);
            float slope=Mathf.Abs(after-before)/(2*probe);
            float headland=Mathf.Max(0,center-(before+after)*.5f)/(Spacing*.45f);
            return Mathf.Clamp01(.12f+slope*.68f+headland*.5f);
        }
        static float CoastBlendWidth(float x) => Mathf.Lerp(10.5f,5.5f,CoastExposure(x));
        public static Vector3 Point(float x, float z) => new Vector3(x, Height(x, z), z);
        public static float SeaFloor(float x, float z)
        {
            float nearestIsland = float.MaxValue; for (int i = 0; i < Islands.Length; i++) nearestIsland = Mathf.Min(nearestIsland, -IslandDistance(x, z, i));
            float shore = Mathf.Max(0, Mathf.Min(z - Coast(x), nearestIsland));
            float falloff=Mathf.Lerp(.055f,.145f,CoastExposure(x));
            return TerrainHydrology.Carve(x, z, -.24f - Mathf.Min(24, falloff * shore * shore));
        }
    }
}
