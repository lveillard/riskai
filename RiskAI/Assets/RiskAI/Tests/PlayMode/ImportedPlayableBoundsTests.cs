using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    /// <summary>
    /// WC3 units never leave the W3I playable rectangle (plus a small disc around the
    /// few source posts on its edge). The source-walkable strip beyond it used to form
    /// a land corridor around the imported maps' edge.
    /// </summary>
    public sealed class ImportedPlayableBoundsTests
    {
        Scene previous, scene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;
        // NavMesh voxelisation can push a border triangle a fraction of a voxel outward.
        const float Tolerance=.35f;

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

        [Test]
        public void SourceWalkingAndSailingStopAtThePlayableRectangle()
        {
            foreach(var map in new[]{ScenarioMap.Europe,ScenarioMap.NewWorld})
            {
                var data=ImportedMapData.Load(map);
                int walkableOutside=0,navigableOutside=0,fineOutside=0,coarseOutside=0;
                for(int z=0;z<data.pathingHeight;z++)for(int x=0;x<data.pathingWidth;x++)
                {
                    var c=data.PathingCellCenter(x,z);
                    if(data.InPlayable(c.x,c.y))continue;
                    if(data.IsWalkable(c.x,c.y))walkableOutside++;
                    if(data.IsShipNavigable(c.x,c.y))navigableOutside++;
                    if(data.PathingCellNeedsFineGround(x,z))fineOutside++;
                }
                for(int z=0;z<data.height-1;z++)for(int x=0;x<data.width-1;x++)
                {
                    float cx=data.originX+(x+.5f)*data.cellSize,cz=data.originZ+(z+.5f)*data.cellSize;
                    if(!data.InPlayable(cx,cz)&&data.TerrainCellUsesCoarseNavigation(x,z))coarseOutside++;
                }
                Assert.That(walkableOutside,Is.Zero,map+" walkable source cells outside the playable rectangle");
                Assert.That(navigableOutside,Is.Zero,map+" navigable source cells outside the playable rectangle");
                Assert.That(fineOutside+coarseOutside,Is.Zero,map+" ground colliders outside the playable rectangle");
            }
        }

        [UnityTest]
        public IEnumerator EuropeAndNewWorldKeepEveryAnchorAndTheNavMeshInsideThePlayableRectangle()
        {
            foreach(var map in new[]{ScenarioMap.Europe,ScenarioMap.NewWorld}) yield return RunMap(map);
        }

        IEnumerator RunMap(ScenarioMap map)
        {
            BattleSession.MapForNewMatch=map; BattleSession.SeedForNewMatch=25000+(int)map;
            scene=SceneManager.CreateScene("Playable bounds "+map); SceneManager.SetActiveScene(scene);
            new GameObject("Playable bounds bootstrap").AddComponent<RiskBootstrap>();
            var battle=BattleSession.Current; battle.AiEnabled=false;
            var controller=Object.FindFirstObjectByType<RtsController>(); if(controller)controller.enabled=false;
            yield return null;
            var data=MapLayout.Imported;
            void Inside(Vector3 point,string label)=>Assert.That(data.InPlayable(point.x,point.z),Is.True,map+" "+label+" at "+point+" must lie inside the playable area.");
            foreach(var city in data.cities){Inside(new Vector3(city.x,0,city.z),city.id+" city");Inside(new Vector3(city.claimX,0,city.claimZ),city.id+" claim");}
            foreach(var town in battle.Towns){Inside(town.transform.position,town.name+" town");Inside(town.ClaimPoint,town.name+" claim");}
            foreach(var camp in battle.Camps)Inside(camp.SpawnPoint,camp.name+" camp spawn");
            foreach(var harbor in NavalWorld.Current.Harbors)
            {
                if(!harbor)continue;
                Inside(harbor.Landing,harbor.name+" landing");Inside(harbor.Berth,harbor.name+" berth");
                if(harbor.CanLaunch&&harbor.TryTransportLanding(out var landing,out _))Inside(landing,harbor.name+" transport landing");
            }

            var triangulation=NavMesh.CalculateTriangulation();
            Assert.That(triangulation.indices.Length,Is.GreaterThan(0));
            int outside=0;Vector3 example=default;
            for(int t=0;t<triangulation.indices.Length;t+=3)
            {
                var centroid=(triangulation.vertices[triangulation.indices[t]]+triangulation.vertices[triangulation.indices[t+1]]+triangulation.vertices[triangulation.indices[t+2]])/3f;
                if(!data.InPlayable(centroid.x,centroid.z,Tolerance)){outside++;example=centroid;}
            }
            Assert.That(outside,Is.Zero,map+" NavMesh triangles outside the playable rectangle, e.g. "+example);

            if(map==ScenarioMap.Europe)
            {
                // Morocco and Greenland were only joined by the source border corridor.
                Assert.That(LandPath(data,"Morocco","Greenland"),Is.Not.EqualTo(NavMeshPathStatus.PathComplete),"No land route may run around the map edge.");
                Assert.That(LandPath(data,"Spain","France"),Is.EqualTo(NavMeshPathStatus.PathComplete),"Control: neighbouring mainland countries stay connected.");
            }
            SceneManager.SetActiveScene(previous); yield return SceneManager.UnloadSceneAsync(scene); scene=default;
        }

        static NavMeshPathStatus LandPath(ImportedMapData data,string from,string to)
        {
            Vector3 City(string country)
            {
                int index=System.Array.FindIndex(data.countries,c=>c.name==country);
                Assert.That(index,Is.GreaterThanOrEqualTo(0),country);
                var city=data.cities.First(c=>c.country==index&&!c.port);
                Assert.That(NavMesh.SamplePosition(new Vector3(city.x,data.HeightAt(city.x,city.z),city.z),out var hit,3,NavMesh.AllAreas),Is.True,city.id);
                return hit.position;
            }
            var path=new NavMeshPath();
            NavMesh.CalculatePath(City(from),City(to),NavMesh.AllAreas,path);
            return path.status;
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
