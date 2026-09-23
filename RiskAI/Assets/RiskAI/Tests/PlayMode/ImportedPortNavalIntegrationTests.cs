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
        public IEnumerator EuropeLinkedPortRecruitsMarinesAndUsesCatalogCaptureCapabilities()
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
                Assert.That(home.VisualVariant,Is.EqualTo(BuildingVariant.IntegratedHarbor));
                Assert.That(homePort.VisualVariant,Is.EqualTo(BuildingVariant.IntegratedHarbor));
                Assert.That(home.GetComponentsInChildren<Transform>().Count(t=>t.name=="Integrated harbor building"),Is.EqualTo(1),"Imported ports use one compact source-position harbor, without a duplicate town hall.");
                Assert.That(home.GetComponentsInChildren<Transform>().Any(t=>t.name=="Common harbor building"),Is.False);
                Assert.That(home.GetComponentsInChildren<Transform>().Any(t=>t.name=="Masonry hall"),Is.False);
                Assert.That(homePort.GetComponentsInChildren<Transform>().Any(t=>t.name=="Harbor berth pier"),Is.False,
                    "An imported port uses the source Circle of Power instead of drawing a second offshore pier.");
                Assert.That(Vector2.Distance(new Vector2(home.Defense.transform.position.x,home.Defense.transform.position.z),new Vector2(home.transform.position.x,home.transform.position.z)),Is.LessThan(.001f));
                Assert.That(Vector2.Distance(new Vector2(home.Defense.AttackOrigin.x,home.Defense.AttackOrigin.z),new Vector2(home.transform.position.x,home.transform.position.z)),Is.LessThan(.001f));
                Assert.That(home.Defense.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(),Is.Empty,"The integrated tower must not carve a second obstacle over the source post.");
                Assert.That(home.Defense.GetComponentsInChildren<Collider>().Any(collider=>collider.enabled&&!collider.isTrigger),Is.False,"Integrated tower art must not add a solid collider over shared terrain.");
                Assert.That(home.GetComponentsInChildren<Collider>().Count(collider=>collider.enabled&&!collider.isTrigger),Is.EqualTo(1),"Only the compact harbormaster body blocks walking; platform and turret remain terrain-owned.");
                Assert.That(Vector2.Distance(new Vector2(home.Defender.transform.position.x,home.Defender.transform.position.z),new Vector2(home.transform.position.x,home.transform.position.z)),Is.GreaterThan(3f));
                Assert.That(Vector3.Distance(homePort.Berth,home.ClaimPoint),Is.LessThanOrEqualTo(ClaimRules.TakeoverRadius),
                    "The hull-safe berth must remain inside the shared source-circle capture radius.");
                battle.Economy.Gold[0]=UnitCatalog.Get(UnitKind.MarinePrivate).Cost;
                Assert.That(homePort.RecruitLand(UnitKind.MarinePrivate,0),Is.Null);
                Assert.That(home.QueueCount,Is.EqualTo(1));
                Assert.That(home.QueuedKind(0),Is.EqualTo(UnitKind.MarinePrivate));

                var target=battle.Towns.First(town=>town.IsPort&&town.State.Owner!=0);var guard=target.Defender;
                guard.TakeDamage(guard.MaxHealth+1,0);
                Assert.That(target.Defender,Is.Not.Null);
                Assert.That(target.Defender.IsAlive,Is.False,"The dead source guard remains referenced until claim resolution.");
                var frigate=BattleTestScenario.Ship(battle.Naval,0,NavalUnitKind.Frigate,target.Port.Berth);
                target.SimTick(.1f);
                Assert.That(target.State.Owner,Is.EqualTo(0),"A capture-capable warship occupies an empty amphibious port.");
                Assert.That(target.Port.ClaimZone.NavalDefender,Is.SameAs(frigate));
                frigate.TakeDamage(frigate.MaxHealth+1,1);target.State.Owner=PlayerRules.NeutralOwner;target.SimTick(.1f);
                var transport=BattleTestScenario.Ship(battle.Naval,0,NavalUnitKind.Transport,target.Port.Berth);
                target.SimTick(.1f);
                Assert.That(target.State.Owner,Is.EqualTo(PlayerRules.NeutralOwner),"Transport capability never implies capture capability.");
                Assert.That(target.Port.ClaimZone.Guardian,Is.Null);
                var marine=BattleTestScenario.Mobile(battle,0,UnitKind.MarinePrivate,target.ClaimPoint);
                target.SimTick(.1f);
                Assert.That(target.State.Owner,Is.EqualTo(0),"A landed soldier remains a valid capture path.");
                Assert.That(target.Defense.Guardian,Is.SameAs(marine));
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
