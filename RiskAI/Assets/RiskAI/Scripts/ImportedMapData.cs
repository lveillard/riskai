using System;
using UnityEngine;

namespace RiskAI
{
    public enum ScenarioMap { Classic, Riverlands, Europe, NewWorld }

    /// <summary>Numeric source geography. Art and gameplay remain RiskAI's own adapters.</summary>
    [Serializable]
    public sealed class ImportedMapData
    {
        const float PathingIndexEpsilon=.0001f;
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
        // Authored WPM grid, encoded as base64 to keep the JSON compact. WPM cells
        // are normally one quarter of a W3E cell along each axis.
        public int pathingWidth, pathingHeight;
        public float pathingCellSize, pathingOriginX, pathingOriginZ;
        public string pathingSamples;
        public string[] tileNames;
        public City[] cities;
        public Country[] countries;
        public SourceTree[] sourceTrees;
        public string[] treeSpecies;
        [NonSerialized] byte[] decodedPathing;
        [NonSerialized] bool[] coarseNavigationCells;
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
            outsideAnchors = null;
            int size = checked(width * height);
            if (width < 2 || height < 2 || cellSize <= 0 || !Finite(cellSize) ||
                heightSamples == null || heightSamples.Length != size || landSamples == null || landSamples.Length != size ||
                waterSamples == null || waterSamples.Length != size || tileSamples == null || tileSamples.Length != size ||
                cities == null || countries == null || countries.Length == 0)
                throw new InvalidOperationException("Invalid imported terrain arrays.");
            for (int i = 0; i < size; i++)
                if (!Finite(heightSamples[i]) || !Finite(waterSamples[i]))
                    throw new InvalidOperationException("Non-finite imported terrain.");
            bool hasAnyPathing=pathingWidth!=0||pathingHeight!=0||pathingCellSize!=0||!string.IsNullOrEmpty(pathingSamples);
            if(hasAnyPathing)
            {
                if(pathingWidth<1||pathingHeight<1||pathingCellSize<=0||!Finite(pathingCellSize)||
                    !Finite(pathingOriginX)||!Finite(pathingOriginZ)||string.IsNullOrEmpty(pathingSamples))
                    throw new InvalidOperationException("Invalid imported pathing fields.");
                try { decodedPathing=Convert.FromBase64String(pathingSamples); }
                catch(FormatException exception) { throw new InvalidOperationException("Invalid imported pathing encoding.",exception); }
                if(decodedPathing.Length!=checked(pathingWidth*pathingHeight))
                    throw new InvalidOperationException("Invalid imported pathing sample count.");
                float terrainWidth=(width-1)*cellSize,terrainHeight=(height-1)*cellSize;
                if(Mathf.Abs(pathingWidth*pathingCellSize-terrainWidth)>.01f||Mathf.Abs(pathingHeight*pathingCellSize-terrainHeight)>.01f)
                    throw new InvalidOperationException("Imported pathing and terrain extents do not match.");
                if(Mathf.Abs(pathingOriginX-originX)>.01f||Mathf.Abs(pathingOriginZ-originZ)>.01f)
                    throw new InvalidOperationException("Imported pathing and terrain origins do not match.");
                BuildNavigationCache();
            }
            else { decodedPathing=null;coarseNavigationCells=null; }
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
        // WC3 units never leave the playable rectangle: the source-walkable strip beyond
        // the W3I camera bounds would otherwise form a land corridor around the map edge.
        // A few source posts sit up to one W3E cell past the rectangle; a small disc
        // around each keeps them usable without reopening the corridor.
        public const float OutsideAnchorRadius = 12f;
        [NonSerialized] Vector2[] outsideAnchors;
        public bool InPlayable(float x, float z, float margin = 0)
        {
            if (x >= PlayableMinX - .001f - margin && x <= PlayableMaxX + .001f + margin && z >= PlayableMinZ - .001f - margin && z <= PlayableMaxZ + .001f + margin) return true;
            return NearOutsideAnchor(x, z, margin);
        }
        /// <summary>Within the playable disc of a source post that lies just past the rectangle.</summary>
        public bool NearOutsideAnchor(float x, float z, float margin = 0)
        {
            if (outsideAnchors == null) outsideAnchors = OutsideAnchors();
            float radius = (OutsideAnchorRadius + margin) * (OutsideAnchorRadius + margin);
            foreach (var anchor in outsideAnchors)
                if ((anchor.x - x) * (anchor.x - x) + (anchor.y - z) * (anchor.y - z) <= radius) return true;
            return false;
        }
        Vector2[] OutsideAnchors()
        {
            var list = new System.Collections.Generic.List<Vector2>();
            bool Outside(float x, float z) => x < PlayableMinX || x > PlayableMaxX || z < PlayableMinZ || z > PlayableMaxZ;
            if (cities != null) foreach (var city in cities)
            {
                if (Outside(city.x, city.z)) list.Add(new Vector2(city.x, city.z));
                if (Outside(city.claimX, city.claimZ)) list.Add(new Vector2(city.claimX, city.claimZ));
            }
            if (countries != null) foreach (var country in countries) if (Outside(country.x, country.z)) list.Add(new Vector2(country.x, country.z));
            return list.ToArray();
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
        public bool HasSourcePathing => decodedPathing!=null;
        public float WalkHeightAt(float x,float z)=>HeightAt(x,z);
        public bool IsWalkable(float x,float z)
        {
            if(!Contains(x,z)||!InPlayable(x,z))return false;
            if(!HasSourcePathing)return IsLand(x,z);
            return TryPathingIndex(x,z,out int index)&&(decodedPathing[index]&0x02)==0;
        }
        public bool IsShipNavigable(float x,float z)
        {
            if(!Contains(x,z)||!InPlayable(x,z)||IsLand(x,z)||Mathf.Abs(WaterAt(x,z)+.24f)>=.4f)return false;
            if(!HasSourcePathing)return Mathf.Abs(WaterAt(x,z)+.24f)<.4f;
            return TryPathingIndex(x,z,out int index)&&(decodedPathing[index]&0x40)==0;
        }
        public bool IsSharedSurface(float x,float z)=>IsWalkable(x,z)&&IsShipNavigable(x,z);
        public bool IsSharedPathingCell(int x,int z)
        {
            if((PathingAt(x,z)&0x42)!=0)return false;
            var center=PathingCellCenter(x,z);return !IsLand(center.x,center.y);
        }
        public bool PathingCellNeedsFineGround(int x,int z)
        {
            if((PathingAt(x,z)&0x02)!=0)return false;
            float sourceX=pathingOriginX+(x+.5f)*pathingCellSize,sourceZ=pathingOriginZ+(z+.5f)*pathingCellSize;
            if(!InPlayable(sourceX,sourceZ))return false;
            int terrainX=Mathf.Clamp(Mathf.FloorToInt((sourceX-originX)/cellSize),0,width-2);
            int terrainZ=Mathf.Clamp(Mathf.FloorToInt((sourceZ-originZ)/cellSize),0,height-2);
            return !TerrainCellUsesCoarseNavigation(terrainX,terrainZ);
        }
        public bool TerrainCellUsesCoarseNavigation(int x,int z)
        {
            if(!HasSourcePathing)return false;
            if(x<0||z<0||x>=width-1||z>=height-1)return false;
            return coarseNavigationCells[z*(width-1)+x];
        }
        public bool HasDryGroundGeometry(float x,float z)
        {
            Vector2 source=SourcePositionAt(x,z);
            int ix=Mathf.Clamp(Mathf.FloorToInt((source.x-originX)/cellSize),0,width-2);
            int iz=Mathf.Clamp(Mathf.FloorToInt((source.y-originZ)/cellSize),0,height-2);
            int index=iz*width+ix;
            return landSamples[index]+landSamples[index+1]+landSamples[index+width]+landSamples[index+width+1]>0;
        }
        public byte PathingAt(int x,int z)
        {
            if(decodedPathing==null||x<0||z<0||x>=pathingWidth||z>=pathingHeight)return 0x42;
            return decodedPathing[z*pathingWidth+x];
        }
        public Vector2 PathingCellCenter(int x,int z)=>TerrainPointFromSource(
            pathingOriginX+(x+.5f)*pathingCellSize,pathingOriginZ+(z+.5f)*pathingCellSize);
        public Vector2 PathingVertex(int x,int z)=>TerrainPointFromSource(
            pathingOriginX+x*pathingCellSize,pathingOriginZ+z*pathingCellSize);

        bool TryPathingIndex(float x,float z,out int index)
        {
            index=-1;if(decodedPathing==null)return false;
            Vector2 source=SourcePositionAt(x,z);
            // Source objects are commonly authored exactly on WPM cell edges. JSON
            // float round-tripping can leave them microscopically below that edge.
            int ix=Mathf.FloorToInt((source.x-pathingOriginX)/pathingCellSize+PathingIndexEpsilon);
            int iz=Mathf.FloorToInt((source.y-pathingOriginZ)/pathingCellSize+PathingIndexEpsilon);
            if(ix<0||iz<0||ix>=pathingWidth||iz>=pathingHeight)return false;
            index=iz*pathingWidth+ix;return true;
        }
        void BuildNavigationCache()
        {
            coarseNavigationCells=new bool[(width-1)*(height-1)];
            for(int z=0;z<height-1;z++)for(int x=0;x<width-1;x++)
            {
                float sourceX=originX+(x+.5f)*cellSize,sourceZ=originZ+(z+.5f)*cellSize;
                Vector2 center=TerrainPointFromSource(sourceX,sourceZ);
                if(!InPlayable(sourceX,sourceZ)||!IsLand(center.x,center.y))continue;
                int minX=Mathf.Max(0,Mathf.FloorToInt((originX+x*cellSize-pathingOriginX)/pathingCellSize));
                int minZ=Mathf.Max(0,Mathf.FloorToInt((originZ+z*cellSize-pathingOriginZ)/pathingCellSize));
                int maxX=Mathf.Min(pathingWidth,Mathf.CeilToInt((originX+(x+1)*cellSize-pathingOriginX)/pathingCellSize));
                int maxZ=Mathf.Min(pathingHeight,Mathf.CeilToInt((originZ+(z+1)*cellSize-pathingOriginZ)/pathingCellSize));
                bool walkable=minX<maxX&&minZ<maxZ;
                for(int pz=minZ;pz<maxZ&&walkable;pz++)for(int px=minX;px<maxX;px++)
                    if((PathingAt(px,pz)&0x02)!=0){walkable=false;break;}
                coarseNavigationCells[z*(width-1)+x]=walkable;
            }
        }
        // W3E's terrain index is the low nibble of the flags/texture byte.
        // The low byte of our packed sample holds variation, not texture ID.
        public static int GroundTileIndex(int packed) => (packed >> 16) & 15;
        public Vector2 SourcePositionAt(float x,float z)
        {
            ResolveCell(x,z,out int index,out float u,out float v);
            return new Vector2(originX+(index%width+u)*cellSize,originZ+(index/width+v)*cellSize);
        }
        public Vector2 TerrainPointFromSource(float x,float z)
        {
            float fx=Mathf.Clamp((x-originX)/cellSize,0,width-1),fz=Mathf.Clamp((z-originZ)/cellSize,0,height-1);
            int ix=Mathf.Min(Mathf.FloorToInt(fx),width-2),iz=Mathf.Min(Mathf.FloorToInt(fz),height-2);
            float u=fx-ix,v=fz-iz;
            Vector2 a=TerrainVertex(ix,iz),b=TerrainVertex(ix+1,iz),c=TerrainVertex(ix,iz+1),d=TerrainVertex(ix+1,iz+1);
            return u+v<=1?a+u*(b-a)+v*(c-a):d+(1-v)*(b-d)+(1-u)*(c-d);
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
