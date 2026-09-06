using System.Collections;
using System.Linq;
using System.Reflection;
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
        RtsController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMap=BattleSession.MapForNewMatch;
            previousLayout=BattleSession.LayoutForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Europe;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=2;
            previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Building selection");SceneManager.SetActiveScene(scene);
            new GameObject("Building selection bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            controller=Object.FindFirstObjectByType<RtsController>();if(controller)controller.enabled=false;
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

        [UnityTest]
        public IEnumerator ImportedPortHasOneBuildingSelectionIdentity()
        {
            var port=NavalWorld.Current.Harbors.First(h=>h.IsImportedPort);
            controller.SelectHarbor(port);

            Assert.That(controller.SelectedHarbors,Is.EquivalentTo(new[]{port}));
            Assert.That(controller.SelectedTowns,Is.Empty,"The imported town is represented by its selected harbor, not duplicated.");
            Assert.That(controller.SelectedHarbor,Is.SameAs(port));
            Assert.That(controller.SelectedTown,Is.SameAs(port.LinkedTown),"Existing single-building HUD callers retain the linked town primary.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImportedPortBoxSelectionAcceptsBothHouseAndBerth()
        {
            var port=NavalWorld.Current.Harbors.Where(h=>h.IsImportedPort)
                .OrderByDescending(h=>(h.transform.position-h.LinkedTown.transform.position).sqrMagnitude).First();
            var focus=(port.transform.position+port.LinkedTown.transform.position)*.5f;
            var cameraObject=new GameObject("Imported port box camera");var boxCamera=cameraObject.AddComponent<Camera>();
            boxCamera.transform.position=focus+new Vector3(0,52,-72);boxCamera.transform.LookAt(focus+Vector3.up*2);boxCamera.fieldOfView=58;boxCamera.nearClipPlane=.1f;boxCamera.farClipPlane=1000;
            controller.Initialize(battle,boxCamera);
            var house=ScreenPoint(boxCamera,BuildingSelection.Bounds(port.LinkedTown).center);
            var berth=ScreenPoint(boxCamera,BuildingSelection.Bounds(port).center);
            Assert.That(Vector2.Distance(house,berth),Is.GreaterThan(10),"The fixture needs distinct city-house and berth footprints.");

            var houseRect=RectAt(house);Assert.That(houseRect.Contains(berth),Is.False);
            BoxSelect(houseRect);Assert.That(controller.SelectedHarbors,Does.Contain(port),"A house-only box must select its imported Harbor identity.");
            controller.Clear();

            var berthRect=RectAt(berth);Assert.That(berthRect.Contains(house),Is.False);
            BoxSelect(berthRect);Assert.That(controller.SelectedHarbors,Does.Contain(port),"A berth-only box must select the same imported Harbor identity.");
            Object.Destroy(cameraObject);yield return null;
        }

        static Vector2 ScreenPoint(Camera camera,Vector3 point)
        {
            var screen=camera.WorldToScreenPoint(point);Assert.That(screen.z,Is.GreaterThan(0));
            return new Vector2(screen.x,Screen.height-screen.y);
        }
        static Rect RectAt(Vector2 point)=>new Rect(point.x-3,point.y-3,6,6);
        void BoxSelect(Rect rect)
        {
            typeof(RtsController).GetMethod("SelectBuildingsIn",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(controller,new object[]{rect,false});
        }

        [UnityTest]
        public IEnumerator GroupRecruitQueuesOneUnitAtTheShortestOwnTownQueue()
        {
            var towns=battle.Towns.Where(t=>!t.IsPort).Take(2).ToArray();
            Assert.That(towns.Length,Is.EqualTo(2));
            foreach(var town in towns)town.State.Owner=0;
            battle.Economy.Gold[0]=BattleRules.Cost(UnitKind.Footman)*3;
            Assert.That(towns[0].Recruit(UnitKind.Footman),Is.Null,"The first town establishes the longer queue.");
            controller.SelectBuildings(towns,null);

            Assert.That(controller.TryRecruitSelected(UnitKind.Footman),Is.Null);
            Assert.That(towns[0].QueueCount,Is.EqualTo(1));
            Assert.That(towns[1].QueueCount,Is.EqualTo(1),"One click picks the selected allied town with the shorter queue; it does not enqueue at every town.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NearbyTownGroupKeepsOwnVisibleCitiesWithinTheSelectionRadius()
        {
            var towns=battle.Towns.Where(t=>!t.IsPort).ToArray();
            var pair=(from first in towns from second in towns
                      where first!=second
                      let distance=Vector3.Distance(first.transform.position,second.transform.position)
                      orderby distance select new{first,second,distance}).First();
            Assert.That(pair.distance,Is.LessThan(90f),"The imported source map must supply a local city pair for this selection regression.");
            var outside=towns.First(t=>Vector3.Distance(t.transform.position,pair.first.transform.position)>90f);
            foreach(var town in battle.Towns)town.State.Owner=1;
            pair.first.State.Owner=pair.second.State.Owner=outside.State.Owner=0;
            var midpoint=(pair.first.transform.position+pair.second.transform.position)*.5f;
            var cameraObject=new GameObject("Nearby town group camera");var groupCamera=cameraObject.AddComponent<Camera>();
            groupCamera.transform.position=midpoint+new Vector3(0,42,-58);groupCamera.transform.LookAt(midpoint);groupCamera.fieldOfView=70;
            controller.Initialize(battle,groupCamera);
            controller.CameraRig.SetHome(midpoint);

            controller.SelectNearbyTowns(pair.first);

            Assert.That(controller.SelectedTowns,Does.Contain(pair.first));
            Assert.That(controller.SelectedTowns,Does.Contain(pair.second));
            Assert.That(controller.SelectedTowns,Has.No.Member(outside),"Nearby selection keeps the 90-unit local radius.");
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
