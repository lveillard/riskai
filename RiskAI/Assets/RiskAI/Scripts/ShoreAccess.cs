using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    /// <summary>Shared shore policy for transport commands and the painted ground.</summary>
    public static class ShoreAccess
    {
        public const float SandThreshold=.55f;
        static Color32[] surface;
        static ImportedMapData surfaceMap;
        static ScenarioMap surfaceScenario=(ScenarioMap)(-1);
        static int surfaceWidth,surfaceHeight;
        static float surfaceX,surfaceZ,surfaceStep;

        /// <summary>One scene-owned field for fragment shading and transport policy.</summary>
        public static void BakeSurface(Transform root)
        {
            EnsureSurface();
            var texture=new Texture2D(surfaceWidth,surfaceHeight,TextureFormat.RGBA32,false,true)
            {name="Shared coast classification",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(surface);texture.Apply(false,true);
            GeneratedResourceOwner.For(root).Track(texture);
            Shader.SetGlobalTexture("_RiskCoastField",texture);
            Shader.SetGlobalVector("_RiskCoastGrid",new Vector4(surfaceX,surfaceZ,1/surfaceStep,0));
            Shader.SetGlobalVector("_RiskCoastSize",new Vector4(surfaceWidth,surfaceHeight,1f/surfaceWidth,1f/surfaceHeight));
            Shader.SetGlobalFloat("_RiskSandThreshold",SandThreshold);
        }
        static void EnsureSurface()
        {
            if(surface!=null&&surfaceScenario==MapLayout.Scenario&&surfaceMap==MapLayout.Imported)return;
            surfaceScenario=MapLayout.Scenario;surfaceMap=MapLayout.Imported;
            var data=MapLayout.IsImported?MapLayout.Imported:null;
            surfaceStep=data!=null?data.cellSize:2f;
            surfaceX=data!=null?data.originX:-MapLayout.HalfWidth;
            surfaceZ=data!=null?data.originZ:-MapLayout.HalfDepth;
            surfaceWidth=data!=null?data.width:Mathf.CeilToInt(2*MapLayout.HalfWidth/surfaceStep)+1;
            surfaceHeight=data!=null?data.height:Mathf.CeilToInt(2*MapLayout.HalfDepth/surfaceStep)+1;
            surface=new Color32[surfaceWidth*surfaceHeight];
            for(int z=0;z<surfaceHeight;z++)for(int x=0;x<surfaceWidth;x++)
            {
                float wx=surfaceX+x*surfaceStep,wz=surfaceZ+z*surfaceStep;
                var weights=CandidateWeights(wx,wz);
                float band=0;
                if(data!=null)
                {
                    // Extend through the first wet vertex so the bank reaches the waterline.
                    bool land=false,water=false;
                    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    {
                        int k=Mathf.Clamp(z+dz,0,data.height-1)*data.width+Mathf.Clamp(x+dx,0,data.width-1);
                        land|=data.landSamples[k]!=0;water|=data.landSamples[k]==0;
                    }
                    band=land&&water?1:0;
                }
                surface[z*surfaceWidth+x]=(Color32)new Color(weights.x,weights.y,band,1);
            }
        }
        static Color SampleSurface(float x,float z)
        {
            EnsureSurface();
            float gx=Mathf.Clamp((x-surfaceX)/surfaceStep,0,surfaceWidth-1),gz=Mathf.Clamp((z-surfaceZ)/surfaceStep,0,surfaceHeight-1);
            int ix=Mathf.Min(Mathf.FloorToInt(gx),surfaceWidth-2),iz=Mathf.Min(Mathf.FloorToInt(gz),surfaceHeight-2),k=iz*surfaceWidth+ix;
            return Color.Lerp(Color.Lerp(surface[k],surface[k+1],gx-ix),Color.Lerp(surface[k+surfaceWidth],surface[k+surfaceWidth+1],gx-ix),gz-iz);
        }
        public static float ShoreBandWeight(float x,float z)=>SampleSurface(x,z).b;
        public static bool IsSandySurface(float x,float z)=>SurfaceWeights(x,z).x>=SandThreshold;

        static ScenarioMap cachedScenario=(ScenarioMap)(-1);
        static Vector3[] authoredPorts;
        static Vector3[] AuthoredPorts()
        {
            if(authoredPorts!=null&&cachedScenario==MapLayout.Scenario)return authoredPorts;
            cachedScenario=MapLayout.Scenario;
            int count=MapLayout.MainlandHarborX.Length;
            authoredPorts=new Vector3[count+MapLayout.Islands.Length];
            for(int i=0;i<count;i++)authoredPorts[i]=MapLayout.MainlandHarborLanding(i);
            for(int i=0;i<MapLayout.Islands.Length;i++)authoredPorts[count+i]=MapLayout.IslandHarborLanding(i);
            return authoredPorts;
        }
        // Source A00V/A00X requires Vcbp; our field blends its beach edges.
        // Ports also expose an explicit walkable
        // pier: its platform may stand over W3E water, so IsLand alone is wrong.
        public static bool TryLanding(Vector3 requested,out Vector3 landing,out string error)
        {
            return TryNearestLanding(requested,3f,out landing,out error);
        }

        public static bool TryNearestLanding(Vector3 requested,float searchRadius,out Vector3 landing,out string error)
        {
            if(TryLandingCandidate(requested,out landing,out error))return true;
            string exactError=error;
            searchRadius=Mathf.Clamp(searchRadius,0,Ship.LoadRadius);
            const int directions=24;
            for(float radius=.5f;radius<=searchRadius+.001f;radius+=.5f)
                for(int i=0;i<directions;i++)
                {
                    float angle=i*Mathf.PI*2/directions;
                    var candidate=requested+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                    if(TryLandingCandidate(candidate,out landing,out _))return true;
                }
            landing=default;error=exactError??"Elige una playa de arena o un muelle transitable.";return false;
        }

        static bool TryLandingCandidate(Vector3 requested,out Vector3 landing,out string error)
        {
            landing=default;error=null;
            if(!NavMesh.SamplePosition(requested,out var hit,.9f,NavMesh.AllAreas))
            {error="Elige una playa de arena o un muelle transitable.";return false;}
            if(IsDock(hit.position)){landing=hit.position;return true;}
            if(!MapLayout.IsLand(hit.position.x,hit.position.z)||
                !IsSandySurface(hit.position.x,hit.position.z))
            {error="Sólo se puede embarcar en playas de arena y muelles; las orillas verdes o rocosas no sirven.";return false;}
            if(!Gentle(hit.position))
            {error="Ese borde es demasiado escarpado para desembarcar.";return false;}
            if(!SeaNavigation.TryNearestOcean(hit.position,Ship.LoadRadius,out _))
            {error="La playa queda demasiado lejos del agua navegable.";return false;}
            landing=hit.position;return true;
        }

        public static bool IsWalkableLanding(Vector3 point) => MapLayout.IsLand(point.x,point.z)||IsDock(point);

        static bool IsDock(Vector3 point)
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

        /// <returns>Sand and rock weights. Green is the remainder.</returns>
        public static Vector2 SurfaceWeights(float x,float z)
        {
            var value=SampleSurface(x,z);return new Vector2(value.r,value.g);
        }
        // Source tile IDs seed the field; interpolation is our local presentation/gameplay
        // policy, not a claim that the original map contains these blended beaches.
        static Vector2 CandidateWeights(float x,float z)
        {
            if(MapLayout.IsImported)
            {
                var data=MapLayout.Imported;
                int tile=ImportedMapData.GroundTileIndex(data.TileAt(x,z));
                bool sand=data.tileNames!=null&&tile<data.tileNames.Length&&data.tileNames[tile]=="Vcbp";
                return sand?Vector2.right:new Vector2(0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,.67f,Mathf.PerlinNoise(x*.027f+17,z*.027f+43))));
            }
            // These are authored landscape choices, not extra map-specific rules.
            // Bake the same weights into terrain vertices; commands read this field.
            float sandWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.44f,.60f,Mathf.PerlinNoise(x*.024f+19,z*.024f+57)));
            var ports=AuthoredPorts();
            for(int i=0;i<ports.Length;i++)sandWeight=Mathf.Max(sandWeight,NearPort(x,z,ports[i]));
            float rockWeight=(1-sandWeight)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46f,.61f,Mathf.PerlinNoise(x*.031f+71,z*.031f+9)));
            return new Vector2(sandWeight,rockWeight);
        }
        static float NearPort(float x,float z,Vector3 port)
        {
            float dx=x-port.x,dz=z-port.z;
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,12,Mathf.Sqrt(dx*dx+dz*dz)));
        }
        static bool Gentle(Vector3 point)
        {
            const float probe=.8f;int landNeighbors=0;
            for(int i=0;i<4;i++)
            {
                var offset=i==0?Vector3.right*probe:i==1?Vector3.left*probe:i==2?Vector3.forward*probe:Vector3.back*probe;
                if(!NavMesh.SamplePosition(point+offset,out var neighbor,.9f,NavMesh.AllAreas)||
                    !MapLayout.IsLand(neighbor.position.x,neighbor.position.z))continue;
                landNeighbors++;
                if(Mathf.Abs(neighbor.position.y-point.y)>.7f)return false;
            }
            // A real coast necessarily has a missing seaward neighbour. Requiring
            // all four made broad, visibly sandy beaches fail their own rule.
            return landNeighbors>=2;
        }
    }
}
