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
    public sealed class NavalExpeditionCommanderTests
    {
        Scene previous, scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers, previousSeed;
        float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=51829;
            previousTimeScale=Time.timeScale;Time.timeScale=10f;
            scene=SceneManager.CreateScene("Naval expedition commander");SceneManager.SetActiveScene(scene);
            new GameObject("Naval expedition commander bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;Object.FindFirstObjectByType<RtsController>().enabled=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AiBuysBoardsSailsUnloadsAndOrdersOneRealExpedition()
        {
            var naval=NavalWorld.Current;
            var home=naval.Harbors.First(harbor=>harbor.Owner==1&&harbor.CanLaunch);
            foreach(var unit in battle.Units.Where(unit=>unit&&unit.Team==1&&!unit.IsGarrison))unit.gameObject.SetActive(false);
            var troops=BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,2,Sample(home.Landing+Vector3.right*3f));
            var sourceGalley=BattleTestScenario.Ship(naval,1,ShipKind.Galley,home.Berth);
            Assert.That(sourceGalley,Is.Not.Null,"The occupied source berth is part of the transport integration fixture.");
            // Advance only the clock through the naval grace; no rules tick means
            // no income or recruitment is introduced into this economic fixture.
            while(battle.BattleTime<battle.AiFirstNavalOffensiveTime+.1f)battle.Clock.Advance(.4f,false,_=>{});
            int cost=Harbor.Cost(ShipKind.Transport);battle.Economy.Gold[1]=cost;
            battle.AiEnabled=true;
            // Reserve and queue before the land commander receives its first post-grace turn.
            naval.ExpeditionFor(1).Tick(0);

            bool embarked=false,unloaded=false,attackOrders=false;int goldWhenQueued=naval.PendingShips(1)>0?battle.Economy.Gold[1]:-1;Ship transport=null;Soldier[] expedition=null;
            float deadline=Time.realtimeSinceStartup+30f;
            while(Time.realtimeSinceStartup<deadline&&!attackOrders)
            {
                yield return null; // NavMeshAgent path solving and movement need live engine frames.
                transport=naval.Ships.FirstOrDefault(ship=>ship&&ship.Team==1&&ship.Kind==ShipKind.Transport);
                if(naval.PendingShips(1)>0&&goldWhenQueued<0)goldWhenQueued=battle.Economy.Gold[1];
                if(transport&&transport.CargoCount>=2){embarked=true;if(expedition==null)expedition=transport.Cargo.Take(2).ToArray();}
                if(embarked&&expedition!=null&&expedition.All(unit=>unit&&unit.IsAlive&&unit.gameObject.activeInHierarchy))
                {
                    unloaded=true;
                    attackOrders=expedition.All(unit=>
                        unit.Agent.enabled&&unit.Agent.isOnNavMesh&&!unit.Agent.pathPending&&unit.Agent.hasPath&&
                        unit.Agent.pathStatus==NavMeshPathStatus.PathComplete&&DistanceXZ(unit.Agent.destination,home.Landing)>10f);
                }
            }

            Assert.That(goldWhenQueued,Is.EqualTo(0),"The expedition must pay the catalog transport cost through the harbor queue.");
            Assert.That(transport,Is.Not.Null,"The harbor must finish a paid transport instead of spawning one.");
            Assert.That(embarked,Is.True,"The expedition must board the selected mobile troops through the transport load radius.");
            Assert.That(unloaded,Is.True,"The same transport must reach a marked destination harbor and unload its cargo.");
            Assert.That(attackOrders,Is.True,"Landed troops must receive the normal attack-move capture order.");
            Assert.That(expedition,Is.Not.Null,"The paid transport must board an existing AI squad.");
            Assert.That(expedition,Is.EquivalentTo(troops),"Only the two mobile fixture troops may form the opening expedition.");
            foreach(var unit in expedition)
            {
                Assert.That(unit.Agent.enabled,Is.True);
                Assert.That(unit.Agent.isOnNavMesh,Is.True);
                Assert.That(unit.Agent.pathStatus,Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(unit.Agent.hasPath,Is.True,"Landed troops must receive the normal attack-move capture order.");
            }
        }

        static float DistanceXZ(Vector3 a,Vector3 b)
        {
            a.y=b.y=0;return Vector3.Distance(a,b);
        }

        static Vector3 Sample(Vector3 point)
        {
            Assert.That(NavMesh.SamplePosition(point,out var hit,2f,NavMesh.AllAreas),Is.True,"The fixture must stand on the source port NavMesh.");
            return hit.position;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;Time.timeScale=previousTimeScale;
            SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
