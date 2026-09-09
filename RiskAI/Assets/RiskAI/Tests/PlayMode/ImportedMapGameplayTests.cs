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

                        var anchored = new Dictionary<Settlement, Soldier>(test.Cities);
                        foreach (var town in battle.Towns)
                        {
                            var city = sourceById[town.State.Id];
                            Assert.That(town.Defender, Is.Not.Null, town.State.Id + " has a source-circle defender.");
                            Assert.That(town.Defender.Kind, Is.EqualTo(UnitKind.Archer));
                            Assert.That(town.Defender.IsGarrison, Is.True);
                            if(town.IsPort)
                            {
                                var anchors=ImportedPortLayout.Resolve(town.transform.position,new Vector3(city.claimX,town.transform.position.y,city.claimZ));
                                Assert.That(Vector3.Distance(town.ClaimZone.Center,anchors.Claim),Is.LessThan(.001f));
                            }
                            else
                            {
                                Assert.That(town.ClaimZone.Center.x, Is.EqualTo(city.claimX).Within(.001f));
                                Assert.That(town.ClaimZone.Center.z, Is.EqualTo(city.claimZ).Within(.001f));
                            }
                            Assert.That(FlatDistance(town.Defender.transform.position, town.ClaimPoint), Is.LessThanOrEqualTo(.2f),
                                town.State.Id + " guard must remain centered on its claim circle.");
                            Assert.That(Mathf.Abs(town.Defender.transform.position.y - town.ClaimPoint.y), Is.LessThanOrEqualTo(.21f),
                                town.State.Id + " guard NavMesh height must stay close to its source claim point.");
                            Assert.That(NavMesh.SamplePosition(town.ClaimPoint, out var navHit, .9f, NavMesh.AllAreas), Is.True, town.State.Id);
                            Assert.That(Mathf.Abs(navHit.position.y - town.ClaimPoint.y), Is.LessThanOrEqualTo(.21f), town.State.Id);
                            if (!town.IsPort)
                            {
                                Assert.That(town.transform.position.x, Is.EqualTo(city.x).Within(.001f));
                                Assert.That(town.transform.position.z, Is.EqualTo(city.z).Within(.001f));
                            }
                            anchored.Add(town, town.Defender);
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
                            Assert.That(port.State, Is.SameAs(town.State));
                            Assert.That(port.Defense, Is.SameAs(town.Defense));
                            Assert.That(port.ClaimZone, Is.SameAs(town.ClaimZone));
                            Assert.That(port.Defender, Is.SameAs(town.Defender));
                            Assert.That(port.CanLaunch, Is.True, town.State.Id + " has a usable naval berth.");
                        }

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
                        Assert.That(anchored.All(pair => FlatDistance(pair.Value.transform.position, pair.Key.ClaimPoint) <= .2f), Is.True,
                            "Bound source guards must stay centered after simulation advances.");
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
    }
}
