using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class RandomStartTests
    {
        [UnityTest]
        public IEnumerator ClassicStartingGuardsCannotShootOtherStartingPosts()
        {
            yield return LoadLayout(BattleSession.StartLayout.RandomCities, 701);
            Assert.That(MapLayout.IsExpanded, Is.False);
            var guards = battle.Units.ToArray();
            Assert.That(guards.Length, Is.EqualTo(19));
            foreach (var tower in battle.Towers)
                foreach (var guard in guards)
                {
                    bool ownGuard = battle.Towns.Any(t => t.Defense == tower && t.Defender == guard) ||
                        battle.Naval.Harbors.Any(h => h.Defense == tower && h.Defender == guard);
                    if (ownGuard) continue;
                    Assert.That(Vector3.Distance(tower.transform.position, guard.transform.position),
                        Is.GreaterThan(ReforgedProfiles.CapturableTower.Range), tower.HostName);
                }
            foreach (var guard in guards)
                foreach (var other in guards)
                    if (guard != other)
                        Assert.That(Vector3.Distance(guard.transform.position, other.transform.position),
                            Is.GreaterThan(BattleRules.Range(UnitKind.Archer)));
            yield return new WaitForSecondsRealtime(3);
            Assert.That(guards.All(g => g && g.IsAlive && g.Health == g.MaxHealth), Is.True);
            Assert.That(battle.Towers.All(t => t.ShotsFired == 0), Is.True);
        }
        Scene previous, active;
        BattleSession battle;
        RtsController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothRandomLayoutsCreateBalancedHomesAndNavMeshUnits()
        {
            var layouts = new[] { BattleSession.StartLayout.RandomCities, BattleSession.StartLayout.RandomCountries };
            foreach (var layout in layouts)
            {
                yield return LoadLayout(layout, 2468);

                var expectedOwned = layout == BattleSession.StartLayout.RandomCities ? 6 : 2;
                var expectedNeutral = layout == BattleSession.StartLayout.RandomCities ? 0 : 8;
                Assert.That(battle.Towns.Count(t => t.State.Owner == 0), Is.EqualTo(expectedOwned));
                Assert.That(battle.Towns.Count(t => t.State.Owner == 1), Is.EqualTo(expectedOwned));
                Assert.That(battle.Towns.Count(t => t.State.Owner < 0), Is.EqualTo(expectedNeutral));
                Assert.That(battle.StartingOwners(), Is.EqualTo(battle.Towns.Select(t => t.State.Owner).ToArray()));

                var posts = battle.Towns.Select(town => new { Owner = town.State.Owner, Defender = town.Defender })
                    .Concat(NavalWorld.Current.Harbors.Select(harbor => new { Owner = harbor.Owner, Defender = harbor.Defender }))
                    .ToArray();
                Assert.That(posts.Length, Is.EqualTo(19));
                Assert.That(battle.Units.Count, Is.EqualTo(posts.Length));
                Assert.That(posts.All(post => post.Defender && post.Defender.Kind == UnitKind.Archer && post.Defender.IsGarrison), Is.True);
                Assert.That(posts.All(post => post.Defender.Team == (post.Owner >= 0 ? post.Owner : 2)), Is.True);
                Assert.That(posts.Select(post => post.Defender).Distinct().Count(), Is.EqualTo(posts.Length));
                Assert.That(battle.Units.All(unit => unit.IsGarrison), Is.True);
                Assert.That(NavalWorld.Current.Ships, Is.Empty);
                Assert.That(battle.Units.All(u => u != null && u.Agent != null && u.Agent.isOnNavMesh), Is.True,
                    "Every bootstrap unit must be placed on the gameplay NavMesh.");

                for (var team = 0; team < 2; team++)
                {
                    var capitals = battle.Towns.Where(t => t.IsCapital && t.State.Owner == team).ToArray();
                    Assert.That(capitals.Length, Is.EqualTo(1), "Each team must have exactly one capital.");
                    Assert.That(capitals[0].FoundingTeam, Is.EqualTo(team));
                    Assert.That(capitals[0].State.Owner, Is.EqualTo(team));
                }

                var home = battle.Towns.First(t => t.IsCapital && t.State.Owner == 0);
                Assert.That(controller.SelectedTown, Is.SameAs(home));
                Assert.That(controller.SelectedTown.State.Owner, Is.EqualTo(0));
                Assert.That(controller.SelectedTown.IsCapital, Is.True);

                yield return UnloadLayout();
            }
        }

        [UnityTest]
        public IEnumerator SeedsRepeatOwnershipAndMortarUiPurchaseRefundsOnce()
        {
            var layouts = new[] { BattleSession.StartLayout.RandomCities, BattleSession.StartLayout.RandomCountries };
            var seeds = new[] { 101, 202, 303 };
            foreach (var layout in layouts)
            {
                foreach (var seed in seeds)
                {
                    var first = OwnersWithoutBootstrap(layout, seed);
                    var repeat = OwnersWithoutBootstrap(layout, seed);
                    var other = OwnersWithoutBootstrap(layout, seed + 1);
                    Assert.That(first, Is.EqualTo(repeat), layout + " must repeat ownership for seed " + seed + ".");
                    Assert.That(first.Where((owner, index) => owner != other[index]).Count(), Is.GreaterThan(0),
                        layout + " must vary ownership for a different seed.");
                }
            }

            yield return LoadLayout(BattleSession.StartLayout.RandomCities, 4040);
            var town = battle.Towns.First(t => t.State.Owner == 0);
            int mortarCost = BattleRules.Cost(UnitKind.Mortar);
            battle.Economy.Gold[0] = mortarCost;
            controller.SelectTown(town);
            controller.Recruit(UnitKind.Mortar);
            Assert.That(town.QueueCount, Is.EqualTo(1), "Mortars are available at the declared profile level.");
            Assert.That(town.QueuedKind(0), Is.EqualTo(UnitKind.Mortar));
            Assert.That(battle.Economy.Gold[0], Is.EqualTo(0));
            Assert.That(town.CancelTraining(0), Is.Null);
            Assert.That(town.QueueCount, Is.Zero);
            Assert.That(battle.Economy.Gold[0], Is.EqualTo(mortarCost), "Cancelling must refund the mortar exactly once.");
            Assert.That(town.CancelTraining(0), Is.Not.Null);
            Assert.That(battle.Economy.Gold[0], Is.EqualTo(mortarCost));
            yield return UnloadLayout();
        }

        IEnumerator LoadLayout(BattleSession.StartLayout layout, int seed)
        {
            BattleSession.ExpandedMapForNewMatch = false;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = layout;
            BattleSession.SeedForNewMatch = seed;
            active = SceneManager.CreateScene("Random start test");
            SceneManager.SetActiveScene(active);
            new GameObject("Random start bootstrap").AddComponent<RiskBootstrap>();
            yield return null;
            battle = BattleSession.Current;
            Assert.That(battle.Seed,Is.EqualTo(seed),"A new match must use the seed selected by the caller, even when a launch seed was supplied.");
            battle.AiEnabled = false;
            controller = Object.FindFirstObjectByType<RtsController>();
            controller.enabled = false;
        }

        static int[] OwnersWithoutBootstrap(BattleSession.StartLayout layout, int seed)
        {
            BattleSession.LayoutForNewMatch = layout;
            BattleSession.SeedForNewMatch = seed;
            var host = new GameObject("Starting owner probe");
            var session = host.AddComponent<BattleSession>();
            var owners = session.StartingOwners();
            Object.DestroyImmediate(host);
            return owners;
        }

        IEnumerator UnloadLayout()
        {
            Time.timeScale = 1;
            SceneManager.SetActiveScene(previous);
            if (active.IsValid() && active.isLoaded) yield return SceneManager.UnloadSceneAsync(active);
            active = default(Scene);
            battle = null;
            controller = null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (active.IsValid() && active.isLoaded) yield return UnloadLayout();
            Time.timeScale = 1;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
        }
    }
}
