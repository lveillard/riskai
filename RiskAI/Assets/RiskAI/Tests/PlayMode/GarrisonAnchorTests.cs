using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class GarrisonAnchorTests
    {
        Scene previousScene, scene;
        BattleSession battle;
        float previousTimeScale;
        Random.State previousRandomState;
        bool previousExpandedMap;
        BattleSession.VictoryMode previousMode;
        BattleSession.StartLayout previousLayout;
        BattleSession.AiDifficulty previousDifficulty;
        int previousSeed;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            previousTimeScale = Time.timeScale;
            previousRandomState = Random.state;
            previousExpandedMap = BattleSession.ExpandedMapForNewMatch;
            previousMode = BattleSession.ModeForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch;
            previousDifficulty = BattleSession.DifficultyForNewMatch;
            previousSeed = BattleSession.SeedForNewMatch;

            Time.timeScale = 1;
            BattleSession.ExpandedMapForNewMatch = false;
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.DifficultyForNewMatch = BattleSession.AiDifficulty.Relaxed;
            BattleSession.SeedForNewMatch = 18473;
            scene = SceneManager.CreateScene("Garrison anchor");
            SceneManager.SetActiveScene(scene);
            new GameObject("Garrison anchor bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            foreach (var tower in battle.Towers) if (tower) tower.enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwappedGarrisonStaysAtItsNavMeshAnchorDuringCrowdedCombat()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0 && t.Defender);
            var formerDefender = town.Defender;
            var replacement = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, town.Rally);

            town.ClaimZone.SetDefender(replacement);
            Assert.That(town.Defender, Is.SameAs(replacement));
            Assert.That(formerDefender.IsGarrison, Is.False, "A swapped-out defender must no longer reserve the post.");
            Assert.That(formerDefender.Agent.updatePosition, Is.True, "Release must restore normal mobile-agent representation.");
            Assert.That(replacement.Agent.updatePosition, Is.False);
            Assert.That(replacement.Agent.updateRotation, Is.False);
            Assert.That(FlatDistance(replacement.transform.position, town.ClaimZone.Center), Is.LessThan(.08f),
                "The sampled anchor must remain at the visible claim-circle center on the fixed map.");

            Vector3 anchor = replacement.transform.position;
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                var attacker = BattleTestScenario.Mobile(battle, 1, UnitKind.Footman,
                    anchor + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 3.2f);
                attacker.Attack(replacement);
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = (i + .5f) * Mathf.PI * 2f / 4f;
                var crowd = BattleTestScenario.Mobile(battle, 0, UnitKind.Footman,
                    anchor + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2.4f);
                crowd.MoveTo(anchor, false, false);
            }

            float maxDrift = 0;
            for (int frame = 0; frame < 120; frame++)
            {
                maxDrift = Mathf.Max(maxDrift, Vector3.Distance(replacement.transform.position, anchor));
                yield return null;
            }

            Assert.That(replacement.IsGarrison, Is.True);
            Assert.That(town.Defender, Is.SameAs(replacement));
            Assert.That(replacement.Agent.isStopped, Is.True);
            Assert.That(maxDrift, Is.LessThan(.002f),
                "Crowd avoidance and attack rotations must not displace a bound guard from its sampled NavMesh anchor.");
            Assert.That(Vector3.Distance(replacement.transform.position, anchor), Is.LessThan(.002f));
        }

        [UnityTest]
        public IEnumerator DefenderMoveNeedsAnInCircleReliefAndHandoffsBeforeTheOrder()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0 && t.Defender && !t.IsPort);
            var defender = town.Defender;
            var target = town.Rally + Vector3.right * 3;
            Assert.That(battle.Commands.Submit(new UnitCommand(0, defender.EntityId, UnitCommandKind.Move, target.x, target.y, target.z)), Is.False);
            Assert.That(town.Defender, Is.SameAs(defender), "A defender cannot leave its circle without a nearby allied relief.");

            var relief = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, town.ClaimZone.Center);
            Assert.That(Vector3.Distance(relief.transform.position, town.ClaimZone.Center), Is.LessThan(ClaimRules.CircleRadius));
            Vector3 anchor = defender.transform.position;
            Assert.That(battle.Commands.Submit(new UnitCommand(0, defender.EntityId, UnitCommandKind.Move, target.x, target.y, target.z)), Is.True);
            battle.Commands.Tick();

            Assert.That(town.Defender, Is.SameAs(relief));
            Assert.That(relief.IsGarrison, Is.True);
            Assert.That(defender.IsGarrison, Is.False);
            Assert.That(Vector3.Distance(defender.transform.position, anchor), Is.LessThan(.01f), "The released defender begins its order from the guard anchor without a position jump.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator GuardReliefUsesTheExpandedRadiusButRejectsBeyondIt()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0 && t.Defender && !t.IsPort);
            var defender = town.Defender;
            var target = town.Rally + Vector3.right * 3;
            var relief=BattleTestScenario.Mobile(battle,0,UnitKind.Guard,town.ClaimZone.Center+Vector3.right*2.1f);
            Assert.That(FlatDistance(relief.transform.position,town.ClaimZone.Center),Is.GreaterThan(ClaimRules.ReliefRadius));
            Assert.That(battle.Commands.Submit(new UnitCommand(0,defender.EntityId,UnitCommandKind.Move,target.x,target.y,target.z)),Is.False,
                "The only candidate is beyond the relief margin.");
            Assert.That(town.Defender,Is.SameAs(defender));
            Assert.That(relief.Agent.Warp(town.ClaimZone.Center+Vector3.right*1.8f),Is.True);
            battle.Spatial.Rebuild(battle.Targets,battle.Units);
            float reliefDistance=FlatDistance(relief.transform.position,town.ClaimZone.Center);
            Assert.That(reliefDistance,Is.GreaterThan(ClaimRules.CircleRadius));
            Assert.That(reliefDistance,Is.LessThan(ClaimRules.ReliefRadius));
            Assert.That(battle.Commands.Submit(new UnitCommand(0,defender.EntityId,UnitCommandKind.Move,target.x,target.y,target.z)),Is.True);
            battle.Commands.Tick();
            Assert.That(town.Defender,Is.SameAs(relief));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PausedDefenderOrderReportsPauseBeforeReliefRequirement()
        {
            var town = battle.Towns.First(t => t.State.Owner == 0 && t.Defender);
            var defender = town.Defender;
            battle.TogglePause();
            var point = town.Rally;
            Assert.That(battle.Commands.Submit(new UnitCommand(0, defender.EntityId, UnitCommandKind.Move, point.x, point.y, point.z)), Is.False);
            Assert.That(battle.Commands.LastRejection, Is.EqualTo("La partida está detenida."));
            Assert.That(town.Defender, Is.SameAs(defender));
            battle.TogglePause();
            yield return null;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0;
            return Vector3.Distance(a, b);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            Random.state = previousRandomState;
            BattleSession.ExpandedMapForNewMatch = previousExpandedMap;
            BattleSession.ModeForNewMatch = previousMode;
            BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.DifficultyForNewMatch = previousDifficulty;
            BattleSession.SeedForNewMatch = previousSeed;
            SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            MapLayout.Configure(previousExpandedMap);
        }
    }
}
