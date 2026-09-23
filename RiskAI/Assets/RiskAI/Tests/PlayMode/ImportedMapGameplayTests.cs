using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    /// <summary>End-to-end assertions for the two numerical WC3 map imports.</summary>
    public sealed class ImportedMapGameplayTests
    {
        readonly struct Expectation
        {
            public readonly ScenarioMap Map;
            public readonly int Cities, Countries, Ports;
            public Expectation(ScenarioMap map, int cities, int countries, int ports)
            { Map = map; Cities = cities; Countries = countries; Ports = ports; }
        }

        [UnityTest]
        public IEnumerator ImportedMapsBootstrapSourcePostsAndKeepTheirSharedPortState()
        {
            var previousMap = BattleSession.MapForNewMatch;
            var previousConfiguredMap = MapLayout.Scenario;
            var previousMode = BattleSession.ModeForNewMatch;
            var previousLayout = BattleSession.LayoutForNewMatch;
            var previousDifficulty = BattleSession.DifficultyForNewMatch;
            var previousSeed = BattleSession.SeedForNewMatch;
            var previousTimeScale = Time.timeScale;
            var previousRandom = UnityEngine.Random.state;
            var previousScene = SceneManager.GetActiveScene();
            var cases = new[] { new Expectation(ScenarioMap.Europe, 212, 69, 44), new Expectation(ScenarioMap.NewWorld, 293, 100, 59) };

            try
            {
                Time.timeScale = 1;
                foreach (var test in cases)
                {
                    Scene scene = default;
                    GameObject root = null;
                    try
                    {
                        BattleSession.MapForNewMatch = test.Map;
                        BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
                        BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
                        BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
                        BattleSession.SeedForNewMatch = 22031 + (int)test.Map;
                        scene = SceneManager.CreateScene("Imported gameplay " + test.Map);
                        SceneManager.SetActiveScene(scene);
                        root = new GameObject("Imported gameplay bootstrap " + test.Map);
                        root.AddComponent<RiskBootstrap>();
                        var battle = BattleSession.Current;
                        battle.AiEnabled = false;
                        var controller = UnityEngine.Object.FindFirstObjectByType<RtsController>();
                        if (controller) controller.enabled = false;
                        yield return null; // BuildNavMesh and bind every source circle before inspecting it.

                        Assert.That(MapLayout.Scenario, Is.EqualTo(test.Map));
                        Assert.That(MapLayout.IsImported, Is.True);
                        Assert.That(MapLayout.Towns.Length, Is.EqualTo(test.Cities));
                        Assert.That(MapLayout.Countries.Length, Is.EqualTo(test.Countries));
                        Assert.That(battle.Towns.Count, Is.EqualTo(test.Cities));
                        Assert.That(battle.Camps.Count, Is.EqualTo(test.Countries));
                        Assert.That(battle.Camps.All(camp => camp), Is.True, "Every source country must have a usable reinforcement camp.");
                        Assert.That(battle.Towns.All(town => town.State.Owner == 0 || town.State.Owner == 1), Is.True,
                            "Imported fixed starts use the source country's stable two-team assignment.");
                        Assert.That(battle.Towns.Count(town => town.State.Owner == 0), Is.GreaterThan(0));
                        Assert.That(battle.Towns.Count(town => town.State.Owner == 1), Is.GreaterThan(0));

                        var source = MapLayout.Imported;
                        var sourceById = source.cities.ToDictionary(city => city.id);
                        Assert.That(sourceById.Count, Is.EqualTo(test.Cities));
                        Assert.That(battle.Units.Count, Is.EqualTo(test.Cities), "Only one source-circle guard is spawned for each city.");
                        Assert.That(battle.Units.All(unit => unit && unit.Kind == UnitKind.Archer && unit.IsGarrison), Is.True,
                            "Imported opening guards are garrisoned archers, never mobile recruits.");
                        Assert.That(battle.Naval.Ships, Is.Empty);
                        Assert.That(battle.Towns.Select(town => town.Defender).Distinct().Count(), Is.EqualTo(test.Cities));

                        LogNavigationDiagnostics(test.Map,battle.Towns);

                        var anchored = new Dictionary<Settlement, Soldier>(test.Cities);
                        var anchoredPositions = new Dictionary<Settlement, Vector3>(test.Cities);
                        foreach (var town in battle.Towns)
                        {
                            var city = sourceById[town.State.Id];
                            var expectedVariant=town.IsPort?BuildingVariant.IntegratedHarbor:BuildingVariant.IntegratedTown;
                            Assert.That(town.VisualVariant,Is.EqualTo(expectedVariant),town.State.Id+" explicit building variant");
                            Assert.That(town.Defense.VisualVariant,Is.EqualTo(expectedVariant),town.State.Id+" tower variant");
                            Assert.That(FlatDistance(town.Defense.transform.position,town.transform.position),Is.LessThan(.001f),town.State.Id+" integrated tower center");
                            Assert.That(FlatDistance(town.Defense.AimPoint,town.transform.position),Is.LessThan(.001f),town.State.Id+" tower aim center");
                            Assert.That(FlatDistance(town.Defense.AttackOrigin,town.transform.position),Is.LessThan(.001f),town.State.Id+" tower launch center");
                            Assert.That(town.Defense.AimPoint.y-town.Defense.transform.position.y,Is.EqualTo(VisualMetrics.IntegratedTowerGalleryHeight).Within(.001f),town.State.Id+" tower aim height");
                            Assert.That(town.Defense.AttackOrigin.y-town.Defense.transform.position.y,Is.EqualTo(VisualMetrics.IntegratedTowerAttackHeight).Within(.001f),town.State.Id+" tower launch height");
                            var towerRenderers=town.Defense.GetComponentsInChildren<Renderer>();
                            var footing=towerRenderers.Single(renderer=>renderer.name=="Integrated tower base");
                            Assert.That(footing.bounds.min.y,Is.EqualTo(town.transform.position.y).Within(.01f),town.State.Id+" tower base must reach the building floor");
                            var turret=towerRenderers.Single(renderer=>renderer.name=="Integrated stone turret");
                            // Renderer.bounds is a world AABB of the local box, so a port keep turned
                            // seaward reads up to sqrt(2) wider. Check the shared mesh in its own frame,
                            // and only a yaw-independent envelope for the placed renderer.
                            float crownWidth=2*TowerArt.CrownRadius*VisualMetrics.TowerScale;
                            Assert.That(TowerArt.Stone.bounds.size.x*VisualMetrics.TowerScale,Is.EqualTo(crownWidth).Within(.03f),town.State.Id+" slender keep with a corbelled crown");
                            Assert.That(TowerArt.Stone.bounds.size.z*VisualMetrics.TowerScale,Is.EqualTo(crownWidth).Within(.03f),town.State.Id+" round crown depth");
                            Assert.That(turret.bounds.size.x,Is.InRange(crownWidth-.03f,crownWidth*1.415f+.03f),town.State.Id+" placed keep footprint");
                            Assert.That(turret.bounds.max.y-town.Defense.transform.position.y,Is.EqualTo(TowerArt.CrownTop*VisualMetrics.TowerScale).Within(.01f),town.State.Id+" crenellated crown height");
                            Assert.That(VisualMetrics.IntegratedTowerAttackHeight,Is.InRange(VisualMetrics.IntegratedTowerGalleryHeight,turret.bounds.max.y-town.Defense.transform.position.y),
                                town.State.Id+" bolts leave from the battlements");
                            Assert.That(towerRenderers.Count(renderer=>renderer.name=="Integrated arrow slits"),Is.EqualTo(1),town.State.Id+" loopholes");
                            Assert.That(towerRenderers.Count(renderer=>renderer.name=="Faction roof"||renderer.name=="Faction pennant"),Is.EqualTo(2),town.State.Id+" conical roof and pennant carry the owner colour");
                            Assert.That(towerRenderers.Count(renderer=>renderer.enabled),Is.LessThanOrEqualTo(10),town.State.Id+" bounded integrated tower renderer budget");
                            float towerTop=towerRenderers.Max(renderer=>renderer.bounds.max.y)-town.Defense.transform.position.y;
                            Assert.That(towerTop,Is.EqualTo(TowerArt.MastTop*VisualMetrics.TowerScale).Within(.08f),town.State.Id+" integrated keep rises clearly above the civic hall");
                            Assert.That(VisualMetrics.BuildingLabelHeight(town.VisualVariant),Is.GreaterThan(towerTop),town.State.Id+" label clears the mast");
                            Assert.That(town.Defense.GetComponentsInChildren<NavMeshObstacle>(),Is.Empty,town.State.Id+" integrated tower cannot carve the shared surface");
                            Assert.That(town.Defense.GetComponentsInChildren<Collider>().Any(collider=>collider.enabled&&!collider.isTrigger),Is.False,town.State.Id+" integrated tower cannot add a solid footprint");
                            Assert.That(town.GetComponentsInChildren<Collider>().Count(collider=>collider.enabled&&!collider.isTrigger),
                                Is.EqualTo(1),town.State.Id+" integrated art owns exactly one compact building collider");
                            Assert.That(town.Defender, Is.Not.Null, town.State.Id + " has a source-circle defender.");
                            Assert.That(town.Defender.Kind, Is.EqualTo(UnitKind.Archer));
                            Assert.That(town.Defender.IsGarrison, Is.True);
                            Assert.That(FlatDistance(town.Defender.transform.position,town.transform.position),Is.GreaterThan(3f),town.State.Id+" guard stays outside the building body");
                            Assert.That(town.ClaimZone.Center.x, Is.EqualTo(city.claimX).Within(.001f));
                            Assert.That(town.ClaimZone.Center.z, Is.EqualTo(city.claimZ).Within(.001f));
                            Assert.That(Vector3.Distance(town.Defender.transform.position, town.ClaimPoint), Is.LessThanOrEqualTo(CityClaimZone.AnchorSearchRadius+.001f),
                                town.State.Id + " guard must use the nearest valid floor inside its source circle.");
                            Vector3 guardPosition=town.Defender.transform.position;
                            Assert.That(Mathf.Abs(guardPosition.y-MapLayout.Height(guardPosition.x,guardPosition.z)), Is.LessThanOrEqualTo(.21f),
                                town.State.Id + " guard must stand on the actual source floor at its valid anchor.");
                            Assert.That(NavMesh.SamplePosition(town.ClaimPoint, out var navHit, CityClaimZone.AnchorSearchRadius, NavMesh.AllAreas), Is.True, town.State.Id);
                            Assert.That(Vector3.Distance(town.Defender.transform.position,navHit.position),Is.LessThanOrEqualTo(.02f),
                                town.State.Id+" guard must use the cached NavMesh anchor, not an arbitrary offset.");
                            Assert.That(Mathf.Abs(navHit.position.y-MapLayout.Height(navHit.position.x,navHit.position.z)), Is.LessThanOrEqualTo(.21f), town.State.Id);
                            if(town.IsPort)
                            {
                                Assert.That(town.PortBuildingPoint.x,Is.EqualTo(city.x).Within(.001f));
                                Assert.That(town.PortBuildingPoint.z,Is.EqualTo(city.z).Within(.001f));
                                Assert.That(town.GetComponentsInChildren<Transform>().Count(item=>item.name=="Integrated harbor building"),Is.EqualTo(1));
                                Assert.That(town.GetComponentsInChildren<Transform>().Count(item=>item.name=="Integrated tower plinth"),Is.EqualTo(1),town.State.Id+" embedded harbor tower base");
                                Assert.That(town.GetComponentsInChildren<Transform>().Count(item=>item.name=="Integrated tower tie"),Is.EqualTo(2),town.State.Id+" embedded harbor tower ties");
                                Assert.That(town.GetComponentsInChildren<Transform>().Any(item=>item.name=="Harbor pier"||item.name=="Harbor berth pier"),Is.False);
                                Assert.That(NavMesh.SamplePosition(town.PortLandEntry,out var landHit,1.2f,NavMesh.AllAreas),Is.True,town.State.Id+" land entry");
                                var path=new NavMeshPath();
                                Assert.That(NavMesh.CalculatePath(landHit.position,navHit.position,NavMesh.AllAreas,path),Is.True,town.State.Id+" route");
                                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),town.State.Id+" route must join land and source B00R");
                                Assert.That(Vector3.Distance(town.Port.Berth,town.ClaimPoint),Is.LessThanOrEqualTo(ClaimRules.TakeoverRadius),town.State.Id+" shared naval circle");
                            }
                            else
                            {
                                Assert.That(town.GetComponentsInChildren<Transform>().Count(item=>item.name.EndsWith(" · integrated architecture")),Is.EqualTo(1));
                                Assert.That(town.transform.position.x, Is.EqualTo(city.x).Within(.001f));
                                Assert.That(town.transform.position.z, Is.EqualTo(city.z).Within(.001f));
                            }
                            anchored.Add(town, town.Defender);
                            anchoredPositions.Add(town,town.Defender.transform.position);
                        }

                        var portTowns = battle.Towns.Where(town => town.IsPort).ToArray();
                        Assert.That(portTowns.Length, Is.EqualTo(test.Ports));
                        Assert.That(battle.Naval.Harbors.Count, Is.EqualTo(test.Ports));
                        foreach (var town in portTowns)
                        {
                            var port = town.Port;
                            Assert.That(port, Is.Not.Null, town.State.Id + " must expose its imported harbor adapter.");
                            Assert.That(port.IsImportedPort, Is.True);
                            Assert.That(port.LinkedTown, Is.SameAs(town));
                            Assert.That(port.VisualVariant,Is.EqualTo(BuildingVariant.IntegratedHarbor));
                            Assert.That(port.State, Is.SameAs(town.State));
                            Assert.That(port.Defense, Is.SameAs(town.Defense));
                            Assert.That(port.ClaimZone, Is.SameAs(town.ClaimZone));
                            Assert.That(port.Defender, Is.SameAs(town.Defender));
                            Assert.That(port.CanLaunch, Is.True, town.State.Id + " has a usable naval berth.");
                            Assert.That(source.IsSharedSurface(town.ClaimPoint.x,town.ClaimPoint.z),Is.True,
                                town.State.Id+" claim must remain authored for ground and naval capture.");
                            Assert.That(HasOpenWaterApproach(port.Berth,town.PortSeaward,10f),Is.True,
                                town.State.Id+" berth must route to clear navigable water 10 metres away.");
                        }

                        var deepWater=DeepWaterCenter(source);
                        Assert.That(NavMesh.SamplePosition(deepWater,out _,.1f,NavMesh.AllAreas),Is.False,
                            test.Map+" deep source water must not receive walkable NavMesh.");

                        // Guards do not consume the imported map's 100-mobile-unit budget.
                        Assert.That(battle.Population(0), Is.GreaterThan(BattleRules.PopulationLimit),
                            "The fixed source ownership gives team zero over 100 opening guards on both imported maps.");
                        Assert.That(battle.RecruitmentPopulation(0), Is.Zero);
                        var recruitTown = battle.Towns.First(town => town.State.Owner == 0 && !town.IsPort);
                        battle.Economy.Gold[0] = BattleRules.Cost(UnitKind.Footman);
                        Assert.That(recruitTown.Recruit(UnitKind.Footman, 0), Is.Null,
                            "A source map with more than 100 garrison guards must still accept mobile recruitment.");
                        Assert.That(recruitTown.QueueCount, Is.EqualTo(1));
                        Assert.That(recruitTown.CancelTraining(0, 0), Is.Null);

                        float minCityDistance = MinimumPairDistance(battle.Towns.Select(town => town.transform.position));
                        float minClaimDistance = MinimumPairDistance(battle.Towns.Select(town => town.ClaimPoint));
                        float minForeignTowerGuardDistance = MinimumForeignTowerGuardDistance(battle.Towns);
                        Assert.That(minForeignTowerGuardDistance, Is.GreaterThan(ReforgedProfiles.CapturableTower.Range),
                            "Own tower footprints must leave neighbouring source circles safe under any ownership seed.");
                        Debug.Log($"RISKAI_IMPORTED_MAP_OPENING: {test.Map} minCity={minCityDistance:F3} minClaim={minClaimDistance:F3} minForeignTowerGuard={minForeignTowerGuardDistance:F3}");

                        // Preserve source positions while ensuring the added towers cannot damage opening posts.
                        yield return new WaitForSecondsRealtime(3f);
                        Assert.That(anchored.All(pair => pair.Key.Defender == pair.Value && pair.Value && pair.Value.IsGarrison), Is.True,
                            "Source guards must retain identity and garrison binding over simulation frames.");
                        Assert.That(anchored.All(pair => Vector3.Distance(pair.Value.transform.position, anchoredPositions[pair.Key]) <= .02f), Is.True,
                            "Bound source guards must not drift from their initial valid anchor after simulation advances.");
                        float minimumGuardHealth = anchored.Min(pair => pair.Value.Health);
                        int totalTowerShots = battle.Towers.Sum(tower => tower ? tower.ShotsFired : 0);
                        Debug.Log($"RISKAI_IMPORTED_MAP_AFTER_3S: {test.Map} minGuardHealth={minimumGuardHealth:F2} towerShots={totalTowerShots}");
                        Assert.That(anchored.All(pair => pair.Value.Health == pair.Value.MaxHealth), Is.True,
                            "Opening defenders must retain full health before any mobile troops are recruited.");
                        Assert.That(totalTowerShots, Is.Zero, "Towers must not fire on independent opening posts.");
                    }
                    finally
                    {
                        if (root) UnityEngine.Object.Destroy(root);
                        if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                        SceneManager.SetActiveScene(previousScene);
                    }
                    yield return null;
                }
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                UnityEngine.Random.state = previousRandom;
                BattleSession.MapForNewMatch = previousMap;
                BattleSession.ModeForNewMatch = previousMode;
                BattleSession.LayoutForNewMatch = previousLayout;
                BattleSession.DifficultyForNewMatch = previousDifficulty;
                BattleSession.SeedForNewMatch = previousSeed;
                SceneManager.SetActiveScene(previousScene);
                MapLayout.Configure(previousConfiguredMap);
            }
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0;
            return Vector3.Distance(a, b);
        }

        static float MinimumPairDistance(IEnumerable<Vector3> points)
        {
            var array = points.ToArray();
            float minimum = float.MaxValue;
            for (int i = 0; i < array.Length; i++)
                for (int j = i + 1; j < array.Length; j++)
                    minimum = Mathf.Min(minimum, FlatDistance(array[i], array[j]));
            return minimum;
        }

        static float MinimumForeignTowerGuardDistance(IEnumerable<Settlement> towns)
        {
            var posts = towns.ToArray();
            float minimum = float.MaxValue;
            foreach (var towerTown in posts)
                foreach (var guardTown in posts)
                    if (towerTown != guardTown)
                        minimum = Mathf.Min(minimum, FlatDistance(towerTown.Defense.transform.position, guardTown.Defender.transform.position));
            return minimum;
        }

        static Vector3 DeepWaterCenter(ImportedMapData data)
        {
            for(int z=1;z<data.pathingHeight-1;z++)for(int x=1;x<data.pathingWidth-1;x++)
            {
                bool clear=true;
                for(int dz=-1;dz<=1&&clear;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    Vector2 neighbor=data.PathingCellCenter(x+dx,z+dz);
                    if(data.IsWalkable(neighbor.x,neighbor.y)||!data.IsShipNavigable(neighbor.x,neighbor.y))
                    {clear=false;break;}
                }
                if(!clear)continue;
                Vector2 center=data.PathingCellCenter(x,z);
                return new Vector3(center.x,data.WalkHeightAt(center.x,center.y),center.y);
            }
            Assert.Fail("Imported source has no interior deep-water WPM cell.");
            return default;
        }

        static bool HasOpenWaterApproach(Vector3 berth,Vector3 preferredDirection,float distance)
        {
            float baseAngle=Mathf.Atan2(preferredDirection.z,preferredDirection.x);
            for(int i=0;i<16;i++)
            {
                float angle=baseAngle+i*Mathf.PI/8f;
                var target=berth+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*distance;
                if(!SeaNavigation.HasClearance(target)||!SeaNavigation.TryBuildPath(berth,target,out var route))continue;
                Vector3 previous=berth;bool valid=true;
                foreach(var waypoint in route)
                {
                    if(!SeaNavigation.ClearSegment(previous,waypoint)){valid=false;break;}
                    previous=waypoint;
                }
                if(valid&&FlatDistance(previous,target)<.01f)return true;
            }
            return false;
        }

        static void LogNavigationDiagnostics(ScenarioMap scenario,IEnumerable<Settlement> towns)
        {
            var guardOffsets=new List<string>();var missingClaims=new List<string>();var disconnectedPorts=new List<string>();
            foreach(var town in towns)
            {
                float offset=town.Defender?FlatDistance(town.Defender.transform.position,town.ClaimPoint):float.PositiveInfinity;
                if(offset>.2f)guardOffsets.Add(town.State.Id+":"+offset.ToString("F3"));
                bool hasClaim=NavMesh.SamplePosition(town.ClaimPoint,out var claimHit,.9f,NavMesh.AllAreas);
                if(!hasClaim){missingClaims.Add(town.State.Id);continue;}
                if(!town.IsPort)continue;
                if(!NavMesh.SamplePosition(town.PortLandEntry,out var landHit,1.2f,NavMesh.AllAreas))
                {disconnectedPorts.Add(town.State.Id+":no-land-entry");continue;}
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(landHit.position,claimHit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                    disconnectedPorts.Add(town.State.Id+":"+path.status);
            }
            Debug.Log("RISKAI_IMPORTED_NAV_DIAGNOSTIC: "+scenario+
                " guardOffsets="+guardOffsets.Count+" ["+string.Join(",",guardOffsets)+"]"+
                " missingClaims="+missingClaims.Count+" ["+string.Join(",",missingClaims)+"]"+
                " disconnectedPorts="+disconnectedPorts.Count+" ["+string.Join(",",disconnectedPorts)+"]");
        }
    }
}
