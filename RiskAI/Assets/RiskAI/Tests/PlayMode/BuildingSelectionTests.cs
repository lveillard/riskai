using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class BuildingSelectionTests
    {
        Scene previous, scene;
        BattleSession battle;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers;
        Camera camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMap=BattleSession.MapForNewMatch;
            previousLayout=BattleSession.LayoutForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=2;
            previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Building selection");SceneManager.SetActiveScene(scene);
            new GameObject("Building selection bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            var controller=Object.FindFirstObjectByType<RtsController>();if(controller)controller.enabled=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator RoofHouseAndTowerPickTheSameSettlementAndRingCoversBoth()
        {
            var town=battle.Towns.First(item=>item.State.Owner==0&&!item.IsPort);
            var cameraObject=new GameObject("Building selection camera");camera=cameraObject.AddComponent<Camera>();
            camera.transform.position=town.transform.position+new Vector3(10,14,-16);
            camera.transform.LookAt(town.transform.position+Vector3.up*2.5f);
            camera.fieldOfView=44;camera.nearClipPlane=.1f;camera.farClipPlane=100;

            var roof=town.GetComponentsInChildren<MeshRenderer>()
                .First(renderer=>renderer.name=="Faction roof"&&!renderer.GetComponentInParent<DefenseTower>());
            var house=town.GetComponentsInChildren<MeshRenderer>().First(renderer=>renderer.name=="Masonry hall");
            var tower=town.GetComponentsInChildren<MeshRenderer>()
                .First(renderer=>renderer.GetComponentInParent<DefenseTower>()==town.Defense);
            foreach(var renderer in new[]{roof,house,tower})
            {
                var screen=camera.WorldToScreenPoint(renderer.bounds.center);
                Assert.That(screen.z,Is.GreaterThan(0),renderer.name+" must be in front of the test camera.");
                Assert.That(RtsPicking.Town(battle,camera,new Vector2(screen.x,screen.y)),Is.SameAs(town),
                    renderer.name+" must resolve to its settlement.");
            }

            var whole=BuildingSelection.Bounds(town);var ring=town.SelectionRing;
            Assert.That(ring,Is.Not.Null);Assert.That(ring.useWorldSpace,Is.True);
            Vector3 center=Vector3.zero;for(int i=0;i<ring.positionCount;i++)center+=ring.GetPosition(i);center/=ring.positionCount;
            var first=ring.GetPosition(0);first.y=0;var flatCenter=center;flatCenter.y=0;float radius=Vector3.Distance(first,flatCenter);
            // The helper ring is circular, so every horizontal corner of the whole
            // town-plus-tower footprint must remain inside it.
            foreach(float x in new[]{whole.min.x,whole.max.x})foreach(float z in new[]{whole.min.z,whole.max.z})
                Assert.That(Vector2.Distance(new Vector2(x,z),new Vector2(center.x,center.z)),Is.LessThanOrEqualTo(radius+.02f));
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap;
            BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.PlayerCountForNewMatch=previousPlayers;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
