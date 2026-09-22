using System;
using NUnit.Framework;
using UnityEngine;
namespace RiskAI.Tests
{
    public sealed class ImportedGroundDecodingTests
    {
        [Test]
        public void TileSelectionIgnoresVariationWaterAndCliffFlags()
        {
            // A palette index of 6 with every independent texture-variation byte.
            for(int variation=0;variation<256;variation++)
                for(int flags=0;flags<16;flags++)
                {
                    int packed=variation|(0x93<<8)|((6|(flags<<4))<<16)|(1<<24);
                    Assert.That(ImportedMapData.GroundTileIndex(packed),Is.EqualTo(6));
                }
        }

        [Test]
        public void SourcePathingSeparatesDrySharedAndDeepSurfaces()
        {
            var pathing=new byte[64];for(int i=0;i<pathing.Length;i++)pathing[i]=0x42;
            pathing[1*8+1]=0x08; // Wet, walkable and navigable.
            pathing[1*8+2]=0x0A; // Wet and navigable, but blocks walking.
            pathing[1*8+5]=0x40; // Dry walkable ground blocks ships.
            var data=Fixture(pathing);data.Validate();

            Vector2 shared=data.PathingCellCenter(1,1),deep=data.PathingCellCenter(2,1),dry=data.PathingCellCenter(5,1);
            Assert.That(data.IsLand(shared.x,shared.y),Is.False);
            Assert.That(data.IsWalkable(shared.x,shared.y),Is.True);
            Assert.That(data.IsShipNavigable(shared.x,shared.y),Is.True);
            Assert.That(data.IsSharedSurface(shared.x,shared.y),Is.True);
            Assert.That(data.WalkHeightAt(shared.x,shared.y),Is.EqualTo(data.HeightAt(shared.x,shared.y)));
            Assert.That(data.IsWalkable(deep.x,deep.y),Is.False);
            Assert.That(data.IsShipNavigable(deep.x,deep.y),Is.True);
            Assert.That(data.IsLand(dry.x,dry.y),Is.True);
            Assert.That(data.IsWalkable(dry.x,dry.y),Is.True);
            Assert.That(data.IsShipNavigable(dry.x,dry.y),Is.False);
            Assert.That(data.PathingCellNeedsFineGround(1,1),Is.True);
            Assert.That(data.PathingCellNeedsFineGround(2,1),Is.False,"Deep blocked water must never receive a ground collider.");
            Assert.That(data.PathingCellNeedsFineGround(5,1),Is.True,"A walkable fragment in a mixed dry cell needs precise collision.");
        }

        [Test]
        public void FullyWalkableDryCellKeepsTheCoarseNavigationQuad()
        {
            var pathing=new byte[64];for(int i=0;i<pathing.Length;i++)pathing[i]=0x40;
            var data=Fixture(pathing);for(int i=0;i<data.landSamples.Length;i++)data.landSamples[i]=1;data.Validate();
            Assert.That(data.TerrainCellUsesCoarseNavigation(0,0),Is.True);
            Assert.That(data.PathingCellNeedsFineGround(1,1),Is.False);
        }

        [Test]
        public void SourceAnchorOnPathingBoundaryUsesTheAuthoredCellDespiteFloatDrift()
        {
            var pathing=new byte[64];for(int i=0;i<pathing.Length;i++)pathing[i]=0x42;
            pathing[1*8+2]=0x08;
            var data=Fixture(pathing);data.Validate();

            Assert.That(data.IsSharedSurface(2f,1.5f),Is.True,"Exact source boundary");
            Assert.That(data.IsSharedSurface(2f-.00001f,1.5f),Is.True,"JSON-sized negative drift");
            Assert.That(data.IsSharedSurface(2f-.001f,1.5f),Is.False,"Meaningful offset still selects its geometric neighbour");
        }

        [Test]
        public void SourcePathingRejectsAnOriginThatDoesNotMatchTerrain()
        {
            var data=Fixture(new byte[64]);data.pathingOriginX=.25f;
            Assert.Throws<InvalidOperationException>(()=>data.Validate());
        }

        [Test]
        public void EveryImportedPortClaimIsAnAuthoredSharedSurface()
        {
            int ports=0;
            foreach(var scenario in new[]{ScenarioMap.Europe,ScenarioMap.NewWorld})
            {
                var data=ImportedMapData.Load(scenario);Assert.That(data.HasSourcePathing,Is.True,scenario.ToString());
                foreach(var city in data.cities)
                {
                    if(!city.port)
                    {
                        Assert.That(data.IsWalkable(city.x,city.z),Is.True,city.id+" inland building is walkable");
                        Assert.That(data.IsLand(city.x,city.z),Is.True,city.id+" inland building is dry");
                        Assert.That(data.IsShipNavigable(city.x,city.z),Is.False,city.id+" inland building blocks ships");
                        continue;
                    }
                    ports++;
                    Assert.That(data.IsSharedSurface(city.claimX,city.claimZ),Is.True,city.id+" source claim is shared");
                    float depth=data.WaterAt(city.claimX,city.claimZ)-data.WalkHeightAt(city.claimX,city.claimZ);
                    Assert.That(depth,Is.InRange(.2f,.8f),city.id+" source wading depth");
                }
            }
            Assert.That(ports,Is.EqualTo(103));
        }

        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void SharedPortClaimsHaveAShallowWaterVisualCue(ScenarioMap scenario)
        {
            var previous=MapLayout.Scenario;
            try
            {
                MapLayout.Configure(scenario);
                foreach(var city in MapLayout.Imported.cities)
                    if(city.port)
                        Assert.That(ShoreAccess.ShoreBandWeight(city.claimX,city.claimZ),Is.GreaterThan(.1f),
                            city.id+" shared water must not look like open deep sea");
            }
            finally { MapLayout.Configure(previous); }
        }

        static ImportedMapData Fixture(byte[] pathing)
        {
            var data=new ImportedMapData
            {
                width=3,height=3,originX=0,originZ=0,cellSize=4,
                heightSamples=new float[9],waterSamples=new float[9],landSamples=new int[9],tileSamples=new int[9],
                pathingWidth=8,pathingHeight=8,pathingCellSize=1,pathingOriginX=0,pathingOriginZ=0,
                pathingSamples=Convert.ToBase64String(pathing),cities=Array.Empty<ImportedMapData.City>(),
                countries=new[]{new ImportedMapData.Country{name="Fixture"}}
            };
            for(int i=0;i<data.heightSamples.Length;i++)data.heightSamples[i]=-1;
            for(int z=0;z<3;z++)for(int x=1;x<3;x++)data.landSamples[z*3+x]=1;
            return data;
        }
    }
}
