using UnityEngine;
using UnityEngine.AI;
using RiskAI.Core;

namespace RiskAI
{
    /// <summary>Shared shore policy for transport commands and the painted ground.</summary>
    public static class ShoreAccess
    {
        public const float SandThreshold=.55f;
        public const float VisualDepthRange=2f;
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
            Shader.SetGlobalFloat("_RiskCoastDepthRange",VisualDepthRange);
            // Beyond the playable rectangle, art extrudes the edge coast (zero disables).
            Shader.SetGlobalVector("_RiskPlayableBounds",MapLayout.IsImported?ImportedMapSkirt.Bounds(MapLayout.Imported):Vector4.zero);
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
                    // Source-authored wading shelves can extend beyond the dry
                    // bank. Give that shared water the same shallow optical cue.
                    band=land&&water||data.IsSharedSurface(wx,wz)?1:0;
                }
                // The spare alpha channel describes appearance, not walkability.
                // Dry source tiles stay dry even if their encoded water level is higher.
                float submerged=data!=null&&!data.IsLand(wx,wz)?
                    Mathf.Clamp01((data.WaterAt(wx,wz)-data.HeightAt(wx,wz))/VisualDepthRange):0;
                surface[z*surfaceWidth+x]=(Color32)new Color(weights.x,weights.y,band,submerged);
            }
            if(data!=null)RefineImportedSurface(data);
        }
        /// <summary>
        /// Imported sand comes from per-vertex source tiles (Vcbp or not), so its 0.55 contour
        /// followed the 2.56 m W3E cells as straight segments and chunky corners. The field is
        /// resampled at twice the resolution: band, rock and depth bilinearly (the values the
        /// GPU interpolated anyway), sand from a blurred tile indicator with warped noise at the
        /// boundary. The shader and the landing rule keep reading the same texels.
        /// </summary>
        const int ImportedRefine=2;
        static void RefineImportedSurface(ImportedMapData data)
        {
            int w=surfaceWidth,h=surfaceHeight;var coarse=surface;
            var sand=new float[w*h];
            for(int i=0;i<sand.Length;i++)sand[i]=coarse[i].r/255f;
            sand=Blur(sand,w,h,.85f);
            int fw=(w-1)*ImportedRefine+1,fh=(h-1)*ImportedRefine+1;float step=surfaceStep/ImportedRefine;
            var fine=new Color32[fw*fh];
            for(int z=0;z<fh;z++)for(int x=0;x<fw;x++)
            {
                float gx=x/(float)ImportedRefine,gz=z/(float)ImportedRefine;
                int ix=Mathf.Min((int)gx,w-2),iz=Mathf.Min((int)gz,h-2),k=iz*w+ix;float u=gx-ix,v=gz-iz;
                Color c=Color.Lerp(Color.Lerp(coarse[k],coarse[k+1],u),Color.Lerp(coarse[k+w],coarse[k+w+1],u),v);
                float wx=surfaceX+x*step,wz=surfaceZ+z*step;
                // Sand is read at a domain-warped point (about +-1.3 cells), so long straight
                // source tile edges meander as well as the cell corners.
                float sx=Mathf.Clamp(gx+(FictionalGround.Fbm(wx*.07f+1.7f,wz*.07f+9.2f)-.5f)*2.6f,0,w-1.001f);
                float sz=Mathf.Clamp(gz+(FictionalGround.Fbm(wx*.07f+6.3f,wz*.07f+4.4f)-.5f)*2.6f,0,h-1.001f);
                int jx=(int)sx,jz=(int)sz,j=jz*w+jx;float su=sx-jx,sv=sz-jz;
                float blurred=Mathf.Lerp(Mathf.Lerp(sand[j],sand[j+1],su),Mathf.Lerp(sand[j+w],sand[j+w+1],su),sv);
                // Boost so a one-cell source beach keeps its sand after the blur.
                blurred=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.5f,blurred));
                float edge=4*blurred*(1-blurred);
                float value=Mathf.Clamp01(blurred+(FictionalGround.Fbm(wx*.19f+3.1f,wz*.19f+7.3f)-.5f)*.55f*edge);

                c.r=value;c.g*=1-value;
                fine[z*fw+x]=(Color32)c;
            }
            surface=fine;surfaceWidth=fw;surfaceHeight=fh;surfaceStep=step;
        }
        static float[] Blur(float[] source,int w,int h,float sigma)
        {
            int radius=Mathf.CeilToInt(sigma*2.5f);var kernel=new float[radius*2+1];float sum=0;
            for(int i=-radius;i<=radius;i++){kernel[i+radius]=Mathf.Exp(-.5f*i*i/(sigma*sigma));sum+=kernel[i+radius];}
            for(int i=0;i<kernel.Length;i++)kernel[i]/=sum;
            var temp=new float[source.Length];var result=new float[source.Length];
            for(int z=0;z<h;z++)for(int x=0;x<w;x++){float acc=0;for(int k=-radius;k<=radius;k++)acc+=kernel[k+radius]*source[z*w+Mathf.Clamp(x+k,0,w-1)];temp[z*w+x]=acc;}
            for(int z=0;z<h;z++)for(int x=0;x<w;x++){float acc=0;for(int k=-radius;k<=radius;k++)acc+=kernel[k+radius]*temp[Mathf.Clamp(z+k,0,h-1)*w+x];result[z*w+x]=acc;}
            return result;
        }
        static Color SampleSurface(float x,float z)
        {
            EnsureSurface();
            // Same clamp as RiskClampPlayable: identity inside the playable rectangle.
            if(surfaceMap!=null){var edge=ImportedMapSkirt.Clamp(surfaceMap,x,z);x=edge.x;z=edge.y;}
            float gx=Mathf.Clamp((x-surfaceX)/surfaceStep,0,surfaceWidth-1),gz=Mathf.Clamp((z-surfaceZ)/surfaceStep,0,surfaceHeight-1);
            int ix=Mathf.Min(Mathf.FloorToInt(gx),surfaceWidth-2),iz=Mathf.Min(Mathf.FloorToInt(gz),surfaceHeight-2),k=iz*surfaceWidth+ix;
            return Color.Lerp(Color.Lerp(surface[k],surface[k+1],gx-ix),Color.Lerp(surface[k+surfaceWidth],surface[k+surfaceWidth+1],gx-ix),gz-iz);
        }
        public static float ShoreBandWeight(float x,float z)=>SampleSurface(x,z).b;
        /// <summary>Band, sand and rock of one imported W3E sample (the field shares the W3E lattice).</summary>
        public static Vector3 ImportedSampleWeights(int index)
        {
            EnsureSurface();
            var data=MapLayout.Imported;int x=index%data.width,z=index/data.width;
            Color value=surface[z*ImportedRefine*surfaceWidth+x*ImportedRefine];
            return new Vector3(value.b,value.r,value.g);
        }
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
        // Authored ports expose a walkable pier; imported WPM ports use shared
        // submerged ground. Visual dry land alone cannot decide either case.
        public static bool TryLanding(Vector3 requested,out Vector3 landing,out string error)
        {
            return TryNearestLanding(requested,3f,out landing,out error);
        }

        public static bool TryNearestLanding(Vector3 requested,float searchRadius,out Vector3 landing,out string error)
        {
            if(TryLandingCandidate(requested,out landing,out error))return true;
            string exactError=error;
            searchRadius=Mathf.Clamp(searchRadius,0,UnitCatalog.TransportLoadRadius);
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
            if(IsDock(hit.position)||(MapLayout.IsImported&&MapLayout.Imported.IsSharedSurface(hit.position.x,hit.position.z)))
            {landing=hit.position;return true;}
            if(!IsGroundWalkable(hit.position.x,hit.position.z)||
                !IsSandySurface(hit.position.x,hit.position.z))
            {error="Sólo se puede embarcar en playas de arena y muelles; las orillas verdes o rocosas no sirven.";return false;}
            if(!Gentle(hit.position))
            {error="Ese borde es demasiado escarpado para desembarcar.";return false;}
            if(!SeaNavigation.TryNearestOcean(hit.position,UnitCatalog.TransportLoadRadius,out _))
            {error="La playa queda demasiado lejos del agua navegable.";return false;}
            landing=hit.position;return true;
        }

        public static bool IsWalkableLanding(Vector3 point) => IsGroundWalkable(point.x,point.z)||IsDock(point);

        static bool IsGroundWalkable(float x,float z)=>MapLayout.IsImported
            ? MapLayout.Imported.IsWalkable(x,z)
            : MapLayout.IsLand(x,z);

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
            // Domain-warped noise: a single Perlin lattice cell can vary along one axis only,
            // which thresholded into a straight sand edge; the warp bends that lattice.
            float warpX=(Mathf.PerlinNoise(x*.011f+5.1f,z*.011f+2.7f)-.5f)*1.6f,warpZ=(Mathf.PerlinNoise(x*.011f+9.4f,z*.011f+6.2f)-.5f)*1.6f;
            float sandNoise=.5f+(FictionalGround.Fbm(x*.024f+19+warpX,z*.024f+57+warpZ)-.5f)*1.35f;
            float sandWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.44f,.60f,sandNoise));
            var ports=AuthoredPorts();
            for(int i=0;i<ports.Length;i++)sandWeight=Mathf.Max(sandWeight,NearPort(x,z,ports[i]));
            float rockWeight=(1-sandWeight)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46f,.61f,.5f+(FictionalGround.Fbm(x*.031f+71+warpZ,z*.031f+9+warpX)-.5f)*1.35f));
            return new Vector2(sandWeight,rockWeight);
        }
        static float NearPort(float x,float z,Vector3 port)
        {
            // An irregular beach that hugs the shoreline, never a perfect disc: a noise-
            // warped radius, limited to the coastal strip except in the landing core.
            float dx=x-port.x,dz=z-port.z,distance=Mathf.Sqrt(dx*dx+dz*dz);
            if(distance>20)return 0;
            float angle=Mathf.Atan2(dz,dx);
            float wobble=(Mathf.PerlinNoise(x*.11f+port.x*.37f+7.1f,z*.11f+port.z*.29f+3.3f)-.5f)*6.5f+1.3f*Mathf.Sin(angle*3+port.x*.7f);
            float radial=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,14,distance+wobble));
            float core=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,6.5f,distance));
            // The inland reach of the beach varies along the coast so it never runs parallel to it.
            float reach=8.5f+(Mathf.PerlinNoise(x*.07f+3.9f,z*.07f+8.3f)-.5f)*9;
            float shore=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,Mathf.Max(4,reach),ShoreDistance(x,z)+(Mathf.PerlinNoise(x*.21f+11,z*.21f+5)-.5f)*3));
            return radial*Mathf.Max(core,shore);
        }
        // Authored maps: metres to the mainland coast or the nearest island shore.
        static float ShoreDistance(float x,float z)
        {
            // Cross-shore distance to the curved coast (not the vertical offset, which
            // stretches the beach into a straight band where the coast is nearly level).
            float probe=2f,slope=(MapLayout.Coast(x+probe)-MapLayout.Coast(x-probe))/(2*probe);
            float d=Mathf.Abs(z-MapLayout.Coast(x))/Mathf.Sqrt(1+slope*slope);
            for(int i=0;i<MapLayout.Islands.Length;i++)d=Mathf.Min(d,Mathf.Abs(MapLayout.IslandDistance(x,z,i)));
            if(MapLayout.IsExpanded)d=Mathf.Min(d,Mathf.Max(0,TerrainHydrology.DistanceToRiver(x,z)));
            return d;
        }
        static bool Gentle(Vector3 point)
        {
            const float probe=.8f;int landNeighbors=0;
            for(int i=0;i<4;i++)
            {
                var offset=i==0?Vector3.right*probe:i==1?Vector3.left*probe:i==2?Vector3.forward*probe:Vector3.back*probe;
                if(!NavMesh.SamplePosition(point+offset,out var neighbor,.9f,NavMesh.AllAreas)||
                    !IsGroundWalkable(neighbor.position.x,neighbor.position.z))continue;
                landNeighbors++;
                if(Mathf.Abs(neighbor.position.y-point.y)>.7f)return false;
            }
            // A real coast necessarily has a missing seaward neighbour. Requiring
            // all four made broad, visibly sandy beaches fail their own rule.
            return landNeighbors>=2;
        }
    }
}
