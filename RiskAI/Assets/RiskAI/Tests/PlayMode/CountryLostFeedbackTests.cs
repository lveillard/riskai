using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace RiskAI.Tests
{
    /// <summary>
    /// Losing a city of a country the player fully owned must always raise the loud
    /// "¡Has perdido X!" / "País roto…" toast with the country_lost cue, whether an enemy
    /// takes it, it falls neutral when its guardian dies, or it is a port city.
    /// </summary>
    public sealed class CountryLostFeedbackTests
    {
        Scene scene, previous;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers, previousSeed;
        BattleSession battle;
        readonly List<CaptureEvent> captures = new List<CaptureEvent>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene(); previousMap = BattleSession.MapForNewMatch; previousLayout = BattleSession.LayoutForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch; previousSeed = BattleSession.SeedForNewMatch;
            // Classic has no port cities; the imported Europe map does (a city sharing its Harbor).
            bool port = TestContext.CurrentContext.Test.Name.Contains("Port");
            BattleSession.MapForNewMatch = port ? ScenarioMap.Europe : ScenarioMap.Classic; BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch = 2; BattleSession.SeedForNewMatch = 19031;
            ProbeHooks.SetLanguage(GameLanguage.Spanish);
            scene = SceneManager.CreateScene("Country lost feedback"); SceneManager.SetActiveScene(scene);
            new GameObject("Country lost bootstrap").AddComponent<RiskBootstrap>(); battle = BattleSession.Current; battle.AiEnabled = false;
            Object.FindFirstObjectByType<RtsController>().enabled = false;
            captures.Clear(); battle.Feedback.Captured += captures.Add;
            yield return null;
        }

        Label Toast() => GameObject.Find("Battle HUD feedback").GetComponent<UIDocument>().rootVisualElement.Q<Label>("HUD toast text");

        // A port city is claimed through its Harbor's zone.
        static CityClaimZone Zone(Settlement town) => town.IsPort && town.Port ? town.Port.ClaimZone : town.ClaimZone;

        /// <summary>Gives player 0 every city of the country, each held by its own living guardian.</summary>
        List<Settlement> OwnCountry(System.Func<Settlement, bool> containing)
        {
            var towns = battle.Towns.Where(t => t && t.State.Country >= 0).ToList();
            var target = towns.FirstOrDefault(containing);
            Assume.That(target, Is.Not.Null, "The fixture map needs a city of the requested kind.");
            var country = towns.Where(t => t.State.Country == target.State.Country).ToList();
            foreach (var tower in battle.Towers.ToArray()) { tower.enabled = false; battle.Targets.Remove(tower); }
            // Only this country's posts are touched: every other post keeps its garrison, so no
            // unrelated capture (or another broken country) competes with the announcement.
            float clear = ClaimRules.TakeoverRadius + 2;
            foreach (var unit in battle.Units.ToArray())
            {
                if (!unit || !country.Any(town => Flat(unit.transform.position, Zone(town).Center) < clear * clear)) continue;
                if (unit.IsGarrison) unit.Garrison.SetDefender(null);
                unit.gameObject.SetActive(false);
            }
            if (NavalWorld.Current)
                foreach (var ship in NavalWorld.Current.Ships.ToArray())
                    if (ship && country.Any(town => town.Port && Flat(ship.transform.position, town.Port.Berth) < 30 * 30)) ship.gameObject.SetActive(false);
            foreach (var town in country)
            {
                town.State.Owner = 0;
                if (town.Port) town.Port.State.Owner = 0;
                var guard = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, town.ClaimPoint);
                Warp(guard, Zone(town).Center);
                Zone(town).SetDefender(guard);
                Assert.That(Zone(town).Defender, Is.SameAs(guard));
            }
            Assert.That(battle.Economy.CountryOwner(target.State.Country), Is.EqualTo(0), "Fixture: player 0 owns the whole country.");
            // Move the target first in the list so callers can take it.
            country.Remove(target); country.Insert(0, target);
            return country;
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0; return (a - b).sqrMagnitude; }

        static void Warp(Soldier unit, Vector3 point)
        {
            Assert.That(NavMesh.SamplePosition(point, out var hit, 2.5f, NavMesh.AllAreas), Is.True);
            unit.Agent.Warp(hit.position); unit.Stop();
        }

        IEnumerator UntilOwnerChanges(Settlement town, int from)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (town.State.Owner == from && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; yield return null;
        }

        void AssertBrokenCountryAnnounced(Settlement town)
        {
            var capture = captures.LastOrDefault(c => c.Name == town.DisplayName);
            Assert.That(capture.Name, Is.EqualTo(town.DisplayName), "The capture must reach BattleFeedback.");
            Assert.That(capture.Previous, Is.EqualTo(0));
            Assert.That(capture.CountryLost, Is.True, "Losing a city of a fully owned country breaks it.");
            string country = MapLayout.Countries[town.State.Country].Name;
            Assert.That(CaptureCue.For(capture, country, out var cue), Is.True);
            Assert.That(cue.Sound, Is.EqualTo(SfxId.CountryLost));
            var toast = Toast();
            Assert.That(toast.text, Does.StartWith("¡Has perdido " + country + "!"), "The broken-country toast is the one on screen.");
            Assert.That(toast.text, Does.Contain("País roto"));
            Assert.That(toast.parent.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(battle.Feedback.Log.VisibleCount(Time.unscaledTime, 8) > 0 &&
                Enumerable.Range(0, battle.Feedback.Log.Count).Any(i => battle.Feedback.Log[i].Text.StartsWith("¡Has perdido " + country)), Is.True);
        }

        [UnityTest]
        public IEnumerator EnemyCaptureOfACompleteCountryAnnouncesTheBrokenCountry()
        {
            var country = OwnCountry(t => !t.IsPort);
            var target = country[0];
            var guard = Zone(target).Defender;
            var attacker = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, target.ClaimPoint);
            Warp(attacker, Zone(target).Center);
            guard.TakeDamage(10000, 1);
            yield return UntilOwnerChanges(target, 0);
            Assert.That(target.State.Owner, Is.EqualTo(1));
            AssertBrokenCountryAnnounced(target);
        }

        [UnityTest]
        public IEnumerator BrokenCountryReplacesARoutineCityToastAlreadyOnScreen()
        {
            // The user-reported miss: a routine "Has perdido <ciudad>" toast (another post lost a
            // moment earlier) held the screen and the broken-country warning waited behind it.
            var country = OwnCountry(t => !t.IsPort);
            var target = country[0];
            var other = battle.Towns.First(t => t && t.State.Country != target.State.Country && !t.IsPort);
            float clear = ClaimRules.TakeoverRadius + 2;
            foreach (var unit in battle.Units.ToArray())
                if (unit && Flat(unit.transform.position, other.ClaimZone.Center) < clear * clear) { if (unit.IsGarrison) unit.Garrison.SetDefender(null); unit.gameObject.SetActive(false); }
            other.State.Owner = 0;
            var otherGuard = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, other.ClaimPoint);
            Warp(otherGuard, other.ClaimZone.Center); other.ClaimZone.SetDefender(otherGuard);
            otherGuard.TakeDamage(10000, 1);
            yield return UntilOwnerChanges(other, 0);
            Assert.That(Toast().text, Does.StartWith("Has perdido " + other.DisplayName), "Fixture: a routine loss toast is on screen.");
            Zone(target).Defender.TakeDamage(10000, 1);
            yield return UntilOwnerChanges(target, 0);
            AssertBrokenCountryAnnounced(target);
        }

        [UnityTest]
        public IEnumerator GuardianDeathLeavingTheCityNeutralAnnouncesTheBrokenCountry()
        {
            var country = OwnCountry(t => !t.IsPort);
            var target = country[0];
            Zone(target).Defender.TakeDamage(10000, 1);
            yield return UntilOwnerChanges(target, 0);
            Assert.That(target.State.Owner, Is.EqualTo(PlayerRules.NeutralOwner));
            AssertBrokenCountryAnnounced(target);
        }

        [UnityTest]
        public IEnumerator LosingAPortCityOfACompleteCountryAnnouncesTheBrokenCountry()
        {
            var country = OwnCountry(t => t.IsPort && t.Port);
            var target = country[0];
            var attacker = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman, target.ClaimPoint);
            Warp(attacker, Zone(target).Center);
            Zone(target).Defender.TakeDamage(10000, 1);
            yield return UntilOwnerChanges(target, 0);
            Assert.That(target.State.Owner, Is.Not.EqualTo(0));
            AssertBrokenCountryAnnounced(target);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (battle) battle.Feedback.Captured -= captures.Add;
            ProbeHooks.SetLanguage(GameLanguage.English);
            BattleSession.MapForNewMatch = previousMap; BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.PlayerCountForNewMatch = previousPlayers; BattleSession.SeedForNewMatch = previousSeed;
            SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
