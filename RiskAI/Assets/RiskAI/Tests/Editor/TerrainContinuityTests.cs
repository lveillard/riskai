using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class TerrainContinuityTests
    {
        [Test]
        public void AdjacentImportedChunksShareLightingNormalsAcrossACurvedSlope()
        {
            var previous=MapLayout.Scenario;var root=new GameObject("Terrain continuity fixture");
            var resources=root.AddComponent<ImportedTerrainResources>();
            try
            {
                MapLayout.Configure(ScenarioMap.Classic);
                const int width=65,height=4;
                var data=new ImportedMapData { width=width,height=height,cellSize=1,
                    heightSamples=new float[width*height],waterSamples=new float[width*height],
                    landSamples=new int[width*height],tileSamples=new int[width*height],
                    cities=new ImportedMapData.City[0],countries=new ImportedMapData.Country[0] };
                for(int z=0;z<height;z++)for(int x=0;x<width;x++)
                {
                    int index=z*width+x;data.landSamples[index]=1;
                    data.heightSamples[index]=2+Mathf.Sin((x-31)*.6f)+z*.2f;
                }
                var create=typeof(ImportedTerrain).GetMethod("CreateChunk",BindingFlags.Static|BindingFlags.NonPublic);
                create.Invoke(null,new object[]{root.transform,resources,data,0,0,32,3,null,null});
                create.Invoke(null,new object[]{root.transform,resources,data,32,0,32,3,null,null});
                var left=root.transform.Find("Terrain 0,0").GetComponent<MeshFilter>().sharedMesh;
                var right=root.transform.Find("Terrain 32,0").GetComponent<MeshFilter>().sharedMesh;
                for(int z=0;z<height;z++)
                {
                    Assert.That(left.vertices[z*33+32],Is.EqualTo(right.vertices[z*33]));
                    Assert.That(Vector3.Distance(left.normals[z*33+32],right.normals[z*33]),Is.LessThan(.00001f));
                    Assert.That(Vector3.Distance(left.normals[z*33+32],Vector3.up),Is.GreaterThan(.1f),"Fixture must exercise sloping lighting, not a flat plane.");
                }
            }
            finally
            {
                foreach(var mesh in resources.Meshes)if(mesh)Object.DestroyImmediate(mesh);
                resources.Meshes.Clear();Object.DestroyImmediate(root);MapLayout.Configure(previous);
            }
        }
    }
}
