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
        public IEnumerator SharedCoastTextureMatchesCpuAtSandBoundariesAndReleasesOnRestart()
        {
            foreach(var map in new[]{ScenarioMap.Classic,ScenarioMap.Riverlands,ScenarioMap.Europe,ScenarioMap.NewWorld})
            {
                MapLayout.Configure(map);
                var root=new GameObject("Coast field test");
                ShoreAccess.BakeSurface(root.transform);
                var texture=Shader.GetGlobalTexture("_RiskCoastField") as Texture2D;
                Assert.That(texture,Is.Not.Null);
                Assert.That(texture.mipmapCount,Is.EqualTo(1));
                Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Bilinear));
                var grid=Shader.GetGlobalVector("_RiskCoastGrid");
                var previousTarget=RenderTexture.active;
                bool previousSrgbWrite=GL.sRGBWrite;
                var target=RenderTexture.GetTemporary(1,1,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                var readback=new Texture2D(1,1,TextureFormat.RGBA32,false,true);
                var probeShader=Shader.Find("Hidden/RiskAI/Tests/CoastSurfaceProbe");
                Assert.That(probeShader,Is.Not.Null);
                var probe=new Material(probeShader);
                try
                {
                    bool boundary=false;
                    for(int z=1;z<texture.height-1&&!boundary;z++)for(int x=1;x<texture.width-1&&!boundary;x++)
                    {
                        // The CPU field only locates a useful boundary; the assertion below
                        // samples production HLSL on the GPU, not Texture2D.GetPixelBilinear.
                        float a=ShoreAccess.SurfaceWeights(grid.x+x/grid.z,grid.y+z/grid.z).x;
                        float b=ShoreAccess.SurfaceWeights(grid.x+(x+1)/grid.z,grid.y+z/grid.z).x;
                        if(Mathf.Min(a,b)>.50f||Mathf.Max(a,b)<.60f)continue;
                        // Test both sides of the material/rule cutoff inside a source cell.
                        foreach(float sand in new[]{.50f,.60f})
                        {
                            float f=(sand-a)/(b-a),wx=grid.x+(x+f)/grid.z,wz=grid.y+z/grid.z;
                            probe.SetVector("_WorldPoint",new Vector4(wx,wz,0,0));
                            GL.sRGBWrite=false;
                            Graphics.Blit(Texture2D.whiteTexture,target,probe);
                            RenderTexture.active=target;
                            readback.ReadPixels(new Rect(0,0,1,1),0,0);readback.Apply();
                            var painted=readback.GetPixel(0,0);
                            var policy=ShoreAccess.SurfaceWeights(wx,wz);
                            Assert.That(policy.x,Is.EqualTo(painted.r).Within(.006f),map+" shared sand field");
                            Assert.That(policy.y,Is.EqualTo(painted.g).Within(.006f),map+" shared rock field");
                            Assert.That(ShoreAccess.IsSandySurface(wx,wz),Is.EqualTo(sand>ShoreAccess.SandThreshold));
                            Assert.That(painted.b,Is.EqualTo(sand>ShoreAccess.SandThreshold?1f:0f).Within(.02f),map+" shader sand blend follows the gameplay cutoff");
                        }
                        boundary=true;
                    }
                    Assert.That(boundary,Is.True,map+" needs a mixed beach edge to exercise interpolation.");
                }
                finally
                {
                    GL.sRGBWrite=previousSrgbWrite;
                    RenderTexture.active=previousTarget;RenderTexture.ReleaseTemporary(target);
                    Object.Destroy(probe);Object.Destroy(readback);Object.Destroy(root);
                }
                yield return null;
                Assert.That(texture==null,Is.True,"Scene teardown must destroy the GPU coast field.");
            }
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

            Assert.That(FindBeachShore(out var beach,out var beachWater),Is.True,map+" must retain a safe visible sandy landing.");
            var beachTransport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,beachWater);
            var beachSoldier=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,beach);
            Assert.That(beachTransport.TryEmbark(beachSoldier),Is.True,"Visible safe beach must accept real boarding.");
            Assert.That(beachTransport.UnloadAt(beach),Is.True,"The same sandy beach must accept real unloading.");
            Assert.That(beachTransport.CargoCount,Is.Zero);

            Assert.That(FindNonBeachShore(out var shore,out var water),Is.True,map+" must expose a reachable green/rock coastal NavMesh point.");
            Assert.That(IsSourceNonBeach(MapLayout.Imported,shore.x,shore.z),Is.True,"The selected point must be non-beach according to the imported terrain.");
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
            Assert.That(unloadTransport.UnloadAt(shore),Is.False,"UnloadAt must apply the same shared shore rule as embark.");
            Assert.That(unloadTransport.CargoCount,Is.EqualTo(1));
            SceneManager.SetActiveScene(previous); yield return SceneManager.UnloadSceneAsync(scene); scene=default;
        }

        bool FindBeachShore(out Vector3 shore,out Vector3 water)
        {
            var data=MapLayout.Imported;
            for(int z=2;z<data.height-2;z++)for(int x=2;x<data.width-2;x++)
            {
                float wx=data.originX+x*data.cellSize,wz=data.originZ+z*data.cellSize;
                if(!data.IsLand(wx,wz)||ShoreAccess.SurfaceWeights(wx,wz).x<.65f||ShoreAccess.ShoreBandWeight(wx,wz)<.5f)continue;
                if(!ShoreAccess.TryLanding(MapLayout.Point(wx,wz),out shore,out _)||IsLegitimateDock(shore))continue;
                if(ShoreAccess.SurfaceWeights(shore.x,shore.z).x<.65f||ShoreAccess.ShoreBandWeight(shore.x,shore.z)<.5f)continue;
                if(!SeaNavigation.TryNearestOcean(shore,Ship.LoadRadius,out water))continue;
                return true;
            }
            shore=water=default;return false;
        }

        bool FindNonBeachShore(out Vector3 shore,out Vector3 water)
        {
            var data=MapLayout.Imported;
            for(int z=2;z<data.height-2;z+=2) for(int x=2;x<data.width-2;x+=2)
            {
                float wx=data.originX+x*data.cellSize,wz=data.originZ+z*data.cellSize;
                if(!data.IsLand(wx,wz)||!IsSourceNonBeach(data,wx,wz))continue;
                if(!NavMesh.SamplePosition(new Vector3(wx,data.HeightAt(wx,wz),wz),out var hit,1.25f,NavMesh.AllAreas))continue;
                if(!MapLayout.IsLand(hit.position.x,hit.position.z)||!IsSourceNonBeach(data,hit.position.x,hit.position.z)||IsLegitimateDock(hit.position)||ShoreAccess.IsSandySurface(hit.position.x,hit.position.z))continue;
                if(!SeaNavigation.TryNearestOcean(hit.position,Ship.LoadRadius,out water))continue;
                if(Vector3.Distance(new Vector3(water.x,0,water.z),new Vector3(hit.position.x,0,hit.position.z))>Ship.LoadRadius)continue;
                shore=hit.position; return true;
            }
            shore=water=default; return false;
        }

        static bool IsSourceNonBeach(ImportedMapData data,float x,float z)
        {
            if(data.tileNames==null)return false;
            int tile=ImportedMapData.GroundTileIndex(data.TileAt(x,z));
            return tile>=0&&tile<data.tileNames.Length&&data.tileNames[tile]!="Vcbp";
        }

        static bool IsLegitimateDock(Vector3 point)
        {
            var naval=NavalWorld.Current;if(!naval)return false;
            foreach(var harbor in naval.Harbors)
            {
                if(!harbor||!harbor.CanLaunch)continue;
                var delta=point-harbor.Landing;
                if(delta.x*delta.x+delta.z*delta.z<=3.4f*3.4f&&Mathf.Abs(delta.y)<=1.5f)return true;
            }
            return false;
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
