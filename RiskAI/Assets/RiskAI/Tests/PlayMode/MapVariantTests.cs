using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class MapVariantTests
    {
        Scene scene, previous;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            MapLayout.Configure(true);
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Expanded map");
            SceneManager.SetActiveScene(scene);
            BattleSession.ExpandedMapForNewMatch = true;
            new GameObject("Expanded map bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExpandedMapHasBalancedCountriesFlatPadsAndArchipelago()
        {
            Assert.That(MapLayout.IsExpanded, Is.True);
            Assert.That(MapLayout.MapName, Is.EqualTo("Cuatro Riberas"));
            Assert.That(MapLayout.Towns.Length, Is.EqualTo(20));
            Assert.That(MapLayout.Countries.Length, Is.EqualTo(5));
            Assert.That(MapLayout.Islands.Length, Is.EqualTo(3));
            Assert.That(NavalWorld.Current.Harbors.Count, Is.EqualTo(8));
            Assert.That(battle.Units.Count, Is.EqualTo(28));
            Assert.That(battle.Towns.All(town => town.Defender && town.Defender.Kind == UnitKind.Archer && town.Defender.IsGarrison), Is.True);
            Assert.That(NavalWorld.Current.Harbors.All(harbor => harbor.Defender && harbor.Defender.Kind == UnitKind.Archer && harbor.Defender.IsGarrison), Is.True);
            Assert.That(battle.Units.All(unit => unit.IsGarrison), Is.True);
            Assert.That(NavalWorld.Current.Ships, Is.Empty);
            Assert.That(MapLayout.HalfDepth, Is.EqualTo(112 * MapLayout.Spacing).Within(.001f));
            Assert.That(MapLayout.Coast(0), Is.EqualTo(70 * MapLayout.Spacing).Within(8 * MapLayout.Spacing));
            Assert.That(MapLayout.Towns.Count(t => t.Owner == 0), Is.EqualTo(10));
            Assert.That(MapLayout.Towns.Count(t => t.Owner == 1), Is.EqualTo(10));
            for (int country = 0; country < MapLayout.Countries.Length; country++)
            {
                var cities = MapLayout.Towns.Where(t => t.Country == country).ToArray();
                Assert.That(cities.Length, Is.EqualTo(4), "Each expanded country group has four cities.");
                Assert.That(cities.All(t => t.Region == country), Is.True, "Expanded country and region indices must match.");
                for (int city = 1; city < cities.Length; city++)
                {
                    int previousIsland = IslandAt(cities[city - 1].Position);
                    int currentIsland = IslandAt(cities[city].Position);
                    if (previousIsland != currentIsland && (previousIsland >= 0 || currentIsland >= 0)) continue;
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(battle.Towns.First(t=>t.State.Id==cities[city-1].Id).Rally, battle.Towns.First(t=>t.State.Id==cities[city].Id).Rally, NavMesh.AllAreas, path), Is.True, cities[city].Name + " must have a route inside its country.");
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), cities[city].Name + " must have a complete route inside its country.");
                }
            }
            foreach (var city in MapLayout.Towns)
            {
                Assert.That(MapLayout.IsLand(city.Position.x, city.Position.z), Is.True, city.Name + " must be on an authored land pad.");
                Assert.That(Mathf.Abs(MapLayout.Height(city.Position.x + 1.4f, city.Position.z) - city.Position.y), Is.LessThan(.8f), city.Name + " has a steep east pad edge.");
                Assert.That(Mathf.Abs(MapLayout.Height(city.Position.x, city.Position.z + 1.4f) - city.Position.y), Is.LessThan(.8f), city.Name + " has a steep north pad edge.");
                Assert.That(NavMesh.SamplePosition(battle.Towns.First(t=>t.State.Id==city.Id).ClaimPoint, out _, 1.2f, NavMesh.AllAreas), Is.True, city.Name + " must be reachable on the baked NavMesh.");
            }
            yield return null;
        }

        static int IslandAt(Vector3 position)
        {
            for (int island = 0; island < MapLayout.Islands.Length; island++)
                if (MapLayout.IslandDistance(position.x, position.z, island) >= 0) return island;
            return -1;
        }

        [UnityTest]
        public IEnumerator IndependentStartingPostsStayOutsideEachOthersWeapons()
        {
            var guards = battle.Units.ToArray();
            Assert.That(guards.Length, Is.EqualTo(28));
            foreach (var tower in battle.Towers)
                foreach (var guard in guards)
                {
                    var ownGuard = battle.Towns.Any(t => t.Defense == tower && t.Defender == guard) ||
                        battle.Naval.Harbors.Any(h => h.Defense == tower && h.Defender == guard);
                    if (ownGuard) continue;
                    Assert.That(Vector3.Distance(tower.transform.position, guard.transform.position),
                        Is.GreaterThan(ReforgedProfiles.CapturableTower.Range),
                        tower.HostName + " must not shoot another starting post after random allocation.");
                }
            yield return new WaitForSecondsRealtime(3);
            Assert.That(guards.All(g => g && g.IsAlive && g.Health == g.MaxHealth), Is.True,
                "An idle opening must not lose guards before either player recruits.");
            Assert.That(battle.Towers.All(t => t.ShotsFired == 0), Is.True);
        }

        [UnityTest]
        public IEnumerator SelectingCampHighlightsItsCitiesAndReinforcementsSpawnThere()
        {
            Assert.That(battle.Camps.Count, Is.EqualTo(5));
            foreach (var camp in battle.Camps)
            {
                Assert.That(camp, Is.Not.Null);
                Assert.That(NavMesh.SamplePosition(camp.SpawnPoint, out _, .5f, NavMesh.AllAreas), Is.True);
            }
            var selected = battle.Camps[0];
            var controller = Object.FindFirstObjectByType<RtsController>();
            controller.SelectCamp(selected);
            var overlay = selected.transform.Find("Territorio seleccionado");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.gameObject.activeSelf, Is.True);
            Assert.That(overlay.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(overlay.GetComponentsInChildren<LineRenderer>().Length, Is.EqualTo(4));
            foreach (var town in battle.Towns.Where(t => t.State.Country == 0)) town.State.Owner = 0;
            battle.Reinforcements.CreditRound();
            Assert.That(battle.Reinforcements.Pending(0), Is.EqualTo(MapLayout.Countries[0].PerTurn));
            Assert.That(battle.Units.Count(u => u && u.OriginCountry == 0), Is.Zero, "Country credit must not spawn immediately.");
            battle.Reinforcements.Tick(.5f);
            var reinforcements = battle.Units.Where(u => u && u.OriginCountry == 0).ToArray();
            Assert.That(reinforcements.Length, Is.EqualTo(1));
            Assert.That(reinforcements.All(unit => unit.Kind == UnitKind.Archer), Is.True);
            foreach (var unit in reinforcements)
                Assert.That(Vector3.Distance(unit.transform.position, selected.SpawnPoint), Is.LessThan(4));
            battle.Reinforcements.Tick(.5f);
            Assert.That(battle.Units.Count(u => u && u.OriginCountry == 0), Is.EqualTo(MapLayout.Countries[0].PerTurn));
            controller.Clear();
            Assert.That(overlay.gameObject.activeSelf, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExpandedRiverLeavesUnderwaterChannelClosedExceptAtCrossings()
        {
            var samples = TerrainHydrology.Samples;
            Assert.That(MapLayout.IsLand(samples[1].x, samples[1].z), Is.False, "The expanded river channel must be non-land.");
            Assert.That(NavMesh.SamplePosition(samples[1], out _, .2f, NavMesh.AllAreas), Is.False, "The underwater channel must not bake as walkable terrain.");
            var crossRiver=new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(battle.Towns[5].Rally,battle.Towns[9].Rally,NavMesh.AllAreas,crossRiver),Is.True);
            Assert.That(crossRiver.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"The two mainland river banks must connect through a real crossing.");
            foreach (var crossing in TerrainHydrology.CrossingPoints)
                Assert.That(NavMesh.SamplePosition(crossing + Vector3.up * .45f, out _, 1.2f, NavMesh.AllAreas), Is.True, "Authored bridge/ford must reconnect the baked NavMesh.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
            BattleSession.ExpandedMapForNewMatch = false;
            MapLayout.Configure(false);
        }
    }
}
