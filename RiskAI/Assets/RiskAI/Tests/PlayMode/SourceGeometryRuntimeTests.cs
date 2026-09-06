using System.Collections;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class SourceGeometryRuntimeTests
    {
        Scene previous, scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMap = BattleSession.MapForNewMatch;
            previousLayout = BattleSession.LayoutForNewMatch;
            previousPlayers = BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch = ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch = 2;
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Source geometry runtime");
            SceneManager.SetActiveScene(scene);
            new GameObject("Source geometry bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoldierInitializeAssignsVerifiedAgentCollisionSizes()
        {
            var archer = BattleTestScenario.Mobile(battle, 0, UnitKind.Archer, new Vector3(-30, 0, -16));
            var medic = BattleTestScenario.Mobile(battle, 0, UnitKind.Medic, new Vector3(-28, 0, -16));
            var guard = BattleTestScenario.Mobile(battle, 0, UnitKind.Guard, new Vector3(-26, 0, -16));
            var mortar = BattleTestScenario.Mobile(battle, 0, UnitKind.Mortar, new Vector3(-24, 0, -16));

            Assert.That(archer.Agent.radius, Is.EqualTo(.32f));
            Assert.That(medic.Agent.radius, Is.EqualTo(.32f));
            Assert.That(guard.Agent.radius, Is.EqualTo(.64f));
            Assert.That(mortar.Agent.radius, Is.EqualTo(.64f));
            Assert.That(archer.Agent.height, Is.EqualTo(1.3f), "Navigation clearance is separate from presentation.");
            foreach(var unit in new[]{archer,medic,guard,mortar})
            {
                Transform model=unit.transform.Find(BattleRules.Model(unit.Kind)+"(Clone)");
                if(!model)model=unit.transform.Find("Mortar model");
                Assert.That(model,Is.Not.Null);
                var animation=model.GetComponentInChildren<Animation>();
                if(animation&&animation["Idle"]){animation.Stop();animation["Idle"].clip.SampleAnimation(animation.gameObject,0); }
                float renderedHeight=ModelMetrics.Measure(model).size.y*model.lossyScale.y;
                Assert.That(renderedHeight,Is.EqualTo(SourceGeometry.StandingHeight(unit.Kind)).Within(.01f),unit.Kind+" must use source standing height, independent of physical radius.");
                var hitbox=RtsPicking.Bounds(Camera.main,unit);
                var head=Camera.main.WorldToScreenPoint(unit.transform.position+Vector3.up*renderedHeight);
                Assert.That(hitbox.Contains(head),Is.True,unit.Kind+" head must remain clickable after visual calibration.");
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch = previousMap;
            BattleSession.LayoutForNewMatch = previousLayout;
            BattleSession.PlayerCountForNewMatch = previousPlayers;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
