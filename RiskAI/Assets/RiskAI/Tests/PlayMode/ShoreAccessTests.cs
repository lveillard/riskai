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
    public sealed class ShoreAccessTests
    {
        Scene previous, scene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene(); previousMap=BattleSession.MapForNewMatch;
            previousLayout=BattleSession.LayoutForNewMatch; previousMode=BattleSession.ModeForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch; previousSeed=BattleSession.SeedForNewMatch;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed; BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            BattleSession.PlayerCountForNewMatch=2; Time.timeScale=1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImportedEuropeAndNewWorldAcceptPortsRejectNonBeachShore()
        {
            foreach(var map in new[]{ScenarioMap.Europe,ScenarioMap.NewWorld}) yield return RunMap(map);
        }

        IEnumerator RunMap(ScenarioMap map)
        {
            BattleSession.MapForNewMatch=map; BattleSession.SeedForNewMatch=24000+(int)map;
            scene=SceneManager.CreateScene("Shore access "+map); SceneManager.SetActiveScene(scene);
            new GameObject("Shore access bootstrap").AddComponent<RiskBootstrap>();
            var battle=BattleSession.Current; battle.AiEnabled=false;
            var controller=Object.FindFirstObjectByType<RtsController>(); if(controller)controller.enabled=false;
            yield return null;
            var naval=NavalWorld.Current;
            var port=naval.Harbors.FirstOrDefault(h=>h.IsImportedPort&&h.CanLaunch);
            Assert.That(port,Is.Not.Null,map+" must have an imported launchable port.");
            var transport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,port.Berth);
            var soldier=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,port.Landing);
            Assert.That(transport.TryEmbark(soldier),Is.True,"The imported port landing must pass the real embark path.");
            Assert.That(transport.UnloadAt(port.Landing),Is.True,"The imported port landing must pass the real unload path.");
            Assert.That(transport.CargoCount,Is.Zero);

            Assert.That(FindNonBeachShore(out var shore,out var water),Is.True,map+" must expose a reachable green/rock coastal NavMesh point.");
            Assert.That(ShoreAccess.SurfaceWeights(shore.x,shore.z).x,Is.LessThan(.55f));
            Assert.That(ShoreAccess.TryLanding(shore,out _,out var error),Is.False);
            Assert.That(error,Does.Contain("orillas"));
            var rejectedTransport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,water);
            var rejectedSoldier=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,shore);
            Assert.That(rejectedTransport.TryEmbark(rejectedSoldier),Is.False,"A flat reachable non-sand shore must fail real embark validation.");
            Assert.That(rejectedTransport.LastActionError,Does.Contain("orillas"));

            var unloadTransport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,port.Berth);
            var unloadSoldier=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,port.Landing);
            Assert.That(unloadTransport.TryEmbark(unloadSoldier),Is.True);
            unloadTransport.transform.position=new Vector3(water.x,-.24f,water.z);
            Assert.That(unloadTransport.UnloadAt(shore),Is.False,"UnloadAt must apply the same Vcbp shore rule as embark.");
            Assert.That(unloadTransport.CargoCount,Is.EqualTo(1));
            SceneManager.SetActiveScene(previous); yield return SceneManager.UnloadSceneAsync(scene); scene=default;
        }

        bool FindNonBeachShore(out Vector3 shore,out Vector3 water)
        {
            var data=MapLayout.Imported;
            for(int z=2;z<data.height-2;z+=2) for(int x=2;x<data.width-2;x+=2)
            {
                float wx=data.originX+x*data.cellSize,wz=data.originZ+z*data.cellSize;
                if(!data.IsLand(wx,wz)||ShoreAccess.SurfaceWeights(wx,wz).x>=.55f)continue;
                if(!NavMesh.SamplePosition(new Vector3(wx,data.HeightAt(wx,wz),wz),out var hit,1.25f,NavMesh.AllAreas))continue;
                if(!MapLayout.IsLand(hit.position.x,hit.position.z)||ShoreAccess.SurfaceWeights(hit.position.x,hit.position.z).x>=.55f)continue;
                if(!SeaNavigation.TryNearestOcean(hit.position,Ship.LoadRadius,out water))continue;
                if(Vector3.Distance(new Vector3(water.x,0,water.z),new Vector3(hit.position.x,0,hit.position.z))>Ship.LoadRadius)continue;
                if(ShoreAccess.TryLanding(hit.position,out _,out _))continue;
                shore=hit.position; return true;
            }
            shore=water=default; return false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap; BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.ModeForNewMatch=previousMode; BattleSession.PlayerCountForNewMatch=previousPlayers;
            BattleSession.SeedForNewMatch=previousSeed; SceneManager.SetActiveScene(previous);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            MapLayout.Configure(previousMap);
        }
    }
}
