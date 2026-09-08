using System;
using UnityEngine;

namespace RiskAI
{
    public enum ScenarioMap { Classic, Riverlands, Europe, NewWorld }

    /// <summary>Numeric source geography. Art and gameplay remain RiskAI's own adapters.</summary>
    [Serializable]
    public sealed class ImportedMapData
    {
        [Serializable] public sealed class City
        {
            public string id, name;
            public float x, z, claimX, claimZ;
            public int country;
            public bool port;
        }
        [Serializable] public sealed class Country { public string name; public float x, z; public int count; }
        [Serializable] public sealed class SourceTree
        {
            public float x, z, rotationDegrees, scaleX, scaleY, scaleZ;
            public int species, variation, pathingFlags, lifePercent, sourceRecord;
        }
        public string mapId, name;
        public int width, height;
        public float originX, originZ, cellSize;
        // W3I's camera/playable rectangle. Old resources omit these fields;
        // properties below then fall back to the complete W3E grid.
        public float playableMinX, playableMaxX, playableMinZ, playableMaxZ;
        public float[] heightSamples, waterSamples;
        public int[] landSamples, tileSamples;
        public string[] tileNames;
        public City[] cities;
        public Country[] countries;
        public SourceTree[] sourceTrees;
        public string[] treeSpecies;
        public ImportedCoastGeometry CoastGeometry { get; private set; }
        public void PrepareCoastGeometry()=>CoastGeometry=new ImportedCoastGeometry(this);
        public Vector2 TerrainVertex(int x,int z)=>CoastGeometry!=null?CoastGeometry.Vertex(x,z):new Vector2(originX+x*cellSize,originZ+z*cellSize);
        public float HalfWidth => (width - 1) * cellSize * .5f;
        public float HalfDepth => (height - 1) * cellSize * .5f;
        bool HasPlayableBounds => Finite(playableMinX) && Finite(playableMaxX) && Finite(playableMinZ) && Finite(playableMaxZ) && playableMinX < playableMaxX && playableMinZ < playableMaxZ;
        public float PlayableMinX => HasPlayableBounds ? playableMinX : originX;
        public float PlayableMaxX => HasPlayableBounds ? playableMaxX : originX + (width - 1) * cellSize;
        public float PlayableMinZ => HasPlayableBounds ? playableMinZ : originZ;
        public float PlayableMaxZ => HasPlayableBounds ? playableMaxZ : originZ + (height - 1) * cellSize;
        public Vector4 PlayableBounds => new Vector4(PlayableMinX, PlayableMinZ, PlayableMaxX, PlayableMaxZ);

        [Serializable] sealed class CensusCity { public bool port; }
        [Serializable] sealed class Census { public CensusCity[] cities; public Country[] countries; }
        static readonly string[] censusDescriptions=new string[4];
        static readonly Census[] censuses=new Census[4];
        static Census ReadCensus(ScenarioMap scenario)
        {
            int index=(int)scenario;
            return censuses[index] ?? (censuses[index]=JsonUtility.FromJson<Census>(LoadSource(scenario).text));
        }
        public static int ScenarioCityCount(ScenarioMap scenario) => ReadCensus(scenario).cities.Length;
        // Read only the source metadata once. Setup does not instantiate or sculpt terrain.
        public static string ScenarioDetail(ScenarioMap scenario)
        {
            int index=(int)scenario;
            if(censusDescriptions[index]!=null)return censusDescriptions[index];
            var census=ReadCensus(scenario);
            int ports=0;foreach(var city in census.cities)if(city.port)ports++;
            return censusDescriptions[index]=census.cities.Length+" ciudades · "+census.countries.Length+" grupos · "+ports+" puertos";
        }

        static TextAsset LoadSource(ScenarioMap scenario)
        {
            string resource=scenario==ScenarioMap.Europe?"Europe":"NewWorld";
            var source=Resources.Load<TextAsset>("Maps/"+resource);
            if(!source)throw new InvalidOperationException("Missing imported map: "+resource);
            return source;
        }

        public static ImportedMapData Load(ScenarioMap scenario)
        {
            var source = LoadSource(scenario);
            var data = JsonUtility.FromJson<ImportedMapData>(source.text);
            data.Validate();
            ImportedLandscapeAugment.Apply(data);
            data.PrepareBuildingPads();
            data.PrepareCoastGeometry();
            return data;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void Validate()
        {
            int size = checked(width * height);
            if (width < 2 || height < 2 || cellSize <= 0 || !Finite(cellSize) ||
                heightSamples == null || heightSamples.Length != size || landSamples == null || landSamples.Length != size ||
                waterSamples == null || waterSamples.Length != size || tileSamples == null || tileSamples.Length != size ||
                cities == null || countries == null || countries.Length == 0)
                throw new InvalidOperationException("Invalid imported terrain arrays.");
            for (int i = 0; i < size; i++)
                if (!Finite(heightSamples[i]) || !Finite(waterSamples[i]))
                    throw new InvalidOperationException("Non-finite imported terrain.");
            foreach (var city in cities)
                if (city.country < 0 || city.country >= countries.Length || !Finite(city.x) || !Finite(city.z) ||
                    !Finite(city.claimX) || !Finite(city.claimZ))
                    throw new InvalidOperationException("Invalid imported city: " + city.id);
            if (sourceTrees != null)
                foreach (var tree in sourceTrees)
                    if (!Finite(tree.x) || !Finite(tree.z) || !Finite(tree.rotationDegrees) ||
                        !Finite(tree.scaleX) || !Finite(tree.scaleY) || !Finite(tree.scaleZ) ||
                        tree.scaleX <= 0 || tree.scaleY <= 0 || tree.scaleZ <= 0 ||
                        treeSpecies == null || tree.species < 0 || tree.species >= treeSpecies.Length)
                        throw new InvalidOperationException("Invalid imported source tree.");
        }
        public bool Contains(float x, float z) => x >= originX && z >= originZ && x <= originX + (width - 1) * cellSize && z <= originZ + (height - 1) * cellSize;
        public float HeightAt(float x, float z) => Sample(heightSamples, x, z);
        public float WaterAt(float x, float z) => Sample(waterSamples, x, z);
        public bool IsLand(float x, float z)
        {
            if(!Contains(x,z))return false;
            ResolveCell(x,z,out int index,out float u,out float v);
            // W3E may disable water even below the encoded water elevation.
            if(landSamples[index]+landSamples[index+1]+landSamples[index+width]+landSamples[index+width+1]==4)return true;
            return SampleCell(heightSamples,index,u,v)>=SampleCell(waterSamples,index,u,v)+.02f;
        }
        // W3E's terrain index is the low nibble of the flags/texture byte.
        // The low byte of our packed sample holds variation, not texture ID.
        public static int GroundTileIndex(int packed) => (packed >> 16) & 15;
        public Vector2 SourcePositionAt(float x,float z)
        {
            ResolveCell(x,z,out int index,out float u,out float v);
            return new Vector2(originX+(index%width+u)*cellSize,originZ+(index/width+v)*cellSize);
        }
        // Material policy stays anchored in canonical world coordinates.
        public int TileAt(float x,float z)
        {
            int ix=Mathf.Clamp(Mathf.RoundToInt((x-originX)/cellSize),0,width-1);
            int iz=Mathf.Clamp(Mathf.RoundToInt((z-originZ)/cellSize),0,height-1);
            return tileSamples[iz*width+ix];
        }
        void ResolveCell(float x,float z,out int index,out float u,out float v)
        {
            float fx=Mathf.Clamp((x-originX)/cellSize,0,width-1),fz=Mathf.Clamp((z-originZ)/cellSize,0,height-1);
            int ix=Mathf.Min(Mathf.FloorToInt(fx),width-2),iz=Mathf.Min(Mathf.FloorToInt(fz),height-2);
            u=fx-ix;v=fz-iz;
            if(CoastGeometry!=null&&CoastGeometry.Resolve(originX+fx*cellSize,originZ+fz*cellSize,ref ix,ref iz,out float mappedU,out float mappedV))
            {u=mappedU;v=mappedV;}
            index=iz*width+ix;
        }
        float Sample(float[] samples,float x,float z)
        {
            ResolveCell(x,z,out int index,out float u,out float v);
            return SampleCell(samples,index,u,v);
        }
        float SampleCell(float[] samples,int index,float u,float v)
        {
            float lowerLeft = samples[index], lowerRight = samples[index + 1];
            float upperLeft = samples[index + width], upperRight = samples[index + width + 1];
            // ImportedTerrain splits every source quad from upper-left to
            // lower-right.  Sampling that same pair of triangles keeps gameplay
            // points, claim rings, colliders and NavMesh agents on one surface;
            // bilinear interpolation produces a different height inside a cell.
            return u + v <= 1
                ? lowerLeft + u * (lowerRight - lowerLeft) + v * (upperLeft - lowerLeft)
                : upperRight + (1 - v) * (lowerRight - upperRight) + (1 - u) * (upperLeft - upperRight);
        }
        void PrepareBuildingPads()
        {
            // Keep source XY. Small own foundations smooth a model footprint;
            // the original sampled coast and relief continue outside that footprint.
            foreach (var city in cities)
            {
                Flatten(city.x, city.z, 2.7f);
                Flatten(city.claimX, city.claimZ, .9f);
            }
        }
        void Flatten(float x, float z, float radius)
        {
            float heightAtCenter = HeightAt(x, z);
            if (!IsLand(x, z)) return;
            int cx = Mathf.RoundToInt((x - originX) / cellSize), cz = Mathf.RoundToInt((z - originZ) / cellSize);
            int cells = Mathf.CeilToInt((radius + cellSize) / cellSize);
            for (int dz = -cells; dz <= cells; dz++) for (int dx = -cells; dx <= cells; dx++)
            {
                int ix = cx + dx, iz = cz + dz;
                if (ix < 0 || iz < 0 || ix >= width || iz >= height) continue;
                float distance = Vector2.Distance(new Vector2(x,z), new Vector2(originX + ix*cellSize,originZ + iz*cellSize));
                if (distance > radius + cellSize) continue;
                int index = iz * width + ix;
                if (landSamples[index] == 0) continue;
                heightSamples[index] = Mathf.Lerp(heightSamples[index], heightAtCenter,
                    1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(radius, radius + cellSize, distance)));
            }
        }
    }
}
