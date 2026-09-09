using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class ImportedPortNavalIntegrationTests
    {
        [UnityTest]
        public IEnumerator EuropeLinkedPortRecruitsMarinesAndUsesDockedFrigateAsGuardian()
        {
            var previousMap=BattleSession.MapForNewMatch;var previousLayout=BattleSession.LayoutForNewMatch;
            var previousPlayers=BattleSession.PlayerCountForNewMatch;var previousSeed=BattleSession.SeedForNewMatch;
            var previousScenario=MapLayout.Scenario;var previousScene=SceneManager.GetActiveScene();Scene scene=default;GameObject root=null;
            try
            {
                BattleSession.MapForNewMatch=ScenarioMap.Europe;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
                BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=16016;
                scene=SceneManager.CreateScene("Imported port naval integration");SceneManager.SetActiveScene(scene);
                root=new GameObject("Imported port naval bootstrap");root.AddComponent<RiskBootstrap>();
                var battle=BattleSession.Current;battle.AiEnabled=false;var controller=Object.FindFirstObjectByType<RtsController>();if(controller)controller.enabled=false;
                yield return null;

                var home=battle.Towns.First(town=>town.IsPort&&town.State.Owner==0);var homePort=home.Port;
                Assert.That(home.GetComponentsInChildren<Transform>().Count(t=>t.name=="Common harbor building"),Is.EqualTo(1),"Imported ports use the same harbor catalog as authored ports, without a duplicate town hall.");
                Assert.That(home.GetComponentsInChildren<Transform>().Any(t=>t.name=="Masonry hall"),Is.False);
                Assert.That(homePort.GetComponentsInChildren<Transform>().Count(t=>t.name=="Harbor berth pier"),Is.EqualTo(1),
                    "Every imported claim quay must remain visibly connected to its runtime-derived safe berth.");
                battle.Economy.Gold[0]=BattleRules.Cost(UnitKind.MarinePrivate);
                Assert.That(homePort.RecruitLand(UnitKind.MarinePrivate,0),Is.Null);
                Assert.That(home.QueueCount,Is.EqualTo(1));
                Assert.That(home.QueuedKind(0),Is.EqualTo(UnitKind.MarinePrivate));

                var target=battle.Towns.First(town=>town.IsPort&&town.State.Owner!=0);var guard=target.Defender;
                guard.TakeDamage(guard.MaxHealth+1,0);
                Assert.That(target.Defender,Is.Not.Null);
                Assert.That(target.Defender.IsAlive,Is.False,"The dead source guard remains referenced until claim resolution.");
                var frigate=BattleTestScenario.Ship(battle.Naval,0,ShipKind.Galley,target.Port.Berth);
                target.SimTick(.1f);
                Assert.That(target.State.Owner,Is.EqualTo(0));
                Assert.That(target.Defense.Guardian,Is.SameAs(frigate));
                target.SimTick(.1f);target.SimTick(.1f);
                Assert.That(target.State.Owner,Is.EqualTo(0),"The docked guardian holds the linked town over later ticks.");
                frigate.transform.position+=Vector3.forward*(Harbor.BerthRadius+2);
                target.SimTick(.1f);
                Assert.That(target.State.Owner,Is.EqualTo(PlayerRules.NeutralOwner));
            }
            finally
            {
                if(root)Object.Destroy(root);if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
                SceneManager.SetActiveScene(previousScene);BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
                BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;MapLayout.Configure(previousScenario);
            }
            yield return null;
        }
    }
}
