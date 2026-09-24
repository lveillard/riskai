using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RiskAI.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RiskAI.Editor
{
    /// <summary>
    /// Quantitative audit and captures of the shared country territory atlas on every map.
    /// Batch usage:
    /// -executeMethod RiskAI.Editor.RiskTerritoryReview.Capture --riskai-territory-output DIR
    ///   [--riskai-territory-tag before] [--riskai-map all|classic|riverlands|europe|world]
    /// Writes DIR/{tag}-{map}.json (per-country metrics), DIR/{tag}-{map}-atlas.png (top-down
    /// country map with member cities), the strategic overview and inspections of the worst
    /// camps. The first tag that runs records DIR/focus-{map}.txt so later tags inspect the same camps.
    /// </summary>
    public static class RiskTerritoryReview
    {
        const int Width=1400,Height=1000;
        const float CoastBand=4f;

        [MenuItem("RiskAI/Review/Capture territory audit")]
        public static void Capture()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            var previousMap=BattleSession.MapForNewMatch;var previousLayout=BattleSession.LayoutForNewMatch;
            int previousPlayers=BattleSession.PlayerCountForNewMatch;bool previousCountdown=BattleSession.CountdownForNewMatch;
            var previousScenario=MapLayout.Scenario;
            try
            {
                string directory=Path.GetFullPath(Argument("--riskai-territory-output")??Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Screenshots","territories"));
                Directory.CreateDirectory(directory);
                string tag=Argument("--riskai-territory-tag")??"audit";
                string mapArg=(Argument("--riskai-map")??"all").ToLowerInvariant();
                var maps=mapArg=="all"?new[]{ScenarioMap.Classic,ScenarioMap.Riverlands,ScenarioMap.Europe,ScenarioMap.NewWorld}
                    :new[]{mapArg=="world"||mapArg=="newworld"?ScenarioMap.NewWorld:mapArg=="classic"?ScenarioMap.Classic:mapArg=="riverlands"?ScenarioMap.Riverlands:ScenarioMap.Europe};
                foreach(var map in maps)CaptureMap(map,directory,tag);
                Debug.Log("RISKAI_TERRITORY_REVIEW_OK: "+directory);
            }
            catch(Exception error){Debug.LogException(error);throw;}
            finally
            {
                BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
                BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.CountdownForNewMatch=previousCountdown;
                MapLayout.Configure(previousScenario);
                if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        static string Argument(string name)
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];
            return null;
        }

        sealed class Metrics
        {
            public string Name;public int Country,Cities,CitiesInside,CitiesOnWater,Pieces,OrphanPieces,SliverPieces,LandPixels,SeaPixels,CoastPixels,OrphanPixels;
            public float Compactness,SourceIoU=-1,HostLandRatio;
            public float Area(float pixelArea)=>LandPixels*pixelArea;
            public float CoastFraction=>LandPixels>0?CoastPixels/(float)LandPixels:0;
            public float SeaFraction=>LandPixels+SeaPixels>0?SeaPixels/(float)(LandPixels+SeaPixels):0;
            public bool CoastalStrip=>LandPixels>0&&CoastFraction>.9f&&HostLandRatio>4;
            public float Badness=>(Cities-CitiesInside)*3+OrphanPieces*1.5f+SliverPieces+(CoastalStrip?2:0)+(SourceIoU>=0?(1-SourceIoU)*4:0)+Mathf.Max(0,.35f-Compactness)*4;
        }

        static void CaptureMap(ScenarioMap map,string directory,string tag)
        {
            BattleSession.MapForNewMatch=map;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=Mathf.Min(PlayerRules.MaxPlayers,MapLayout.MaximumPlayersForScenario(map));BattleSession.CountdownForNewMatch=false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var bootstrapObject=new GameObject("Riesgus · Territory review · "+map);
            var bootstrap=bootstrapObject.AddComponent<RiskBootstrap>();
            var watch=System.Diagnostics.Stopwatch.StartNew();
            if(!bootstrapObject.GetComponent<BattleSession>())
            {
                try{typeof(RiskBootstrap).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(bootstrap,null);}
                catch(TargetInvocationException error)when(StrategicMapView.Current){Debug.LogWarning("RISKAI_TERRITORY_REVIEW_HUD_SKIPPED: "+error.InnerException?.Message);}
            }
            var session=bootstrapObject.GetComponent<BattleSession>();
            var view=StrategicMapView.Current;
            if(!session||!view)throw new InvalidOperationException("Territory review bootstrap did not create a session and strategic view.");
            float fieldMs=TerritoryField.LastBuildMilliseconds;
            var atlasWatch=System.Diagnostics.Stopwatch.StartNew();
            // The probe atlas is tracked on the review scene owner; no ad-hoc DestroyImmediate.
            var probe=new TerritoryAtlas(session,GeneratedResourceOwner.For(bootstrapObject.transform));float atlasMs=(float)atlasWatch.Elapsed.TotalMilliseconds;
            string prefix=tag+"-"+map.ToString().ToLowerInvariant();
            var atlas=view.Atlas;
            int size=atlas.Regions.width;
            var pixels=ReadTexture(atlas.Regions);
            var bounds=atlas.Bounds;float pixelArea=bounds.z*bounds.w/(size*(float)size);
            var metrics=Measure(pixels,size,bounds,out float mapAgreement,out int[] reference);
            Debug.Log("RISKAI_TERRITORY_REVIEW_MAP: map="+map+" countries="+metrics.Length+" atlasMs="+atlasMs.ToString("F1",CultureInfo.InvariantCulture)+" boot="+watch.ElapsedMilliseconds);
            WriteJson(Path.Combine(directory,prefix+".json"),map,metrics,pixelArea,atlasMs,fieldMs,mapAgreement);
            WriteAtlasImage(Path.Combine(directory,prefix+"-atlas.png"),pixels,size,bounds,session,reference,false);
            if(atlas.Borders)
            {
                var border=ReadTexture(atlas.Borders);var debug=new Texture2D(size,size,TextureFormat.RGB24,false);
                for(int i=0;i<border.Length;i++){byte r=(byte)Mathf.Min(255,border[i].r*8),g=(byte)Mathf.Min(255,border[i].g*8);border[i]=new Color32(r,g,(byte)(pixels[i].a>127?60:0),255);}
                debug.SetPixels32(border);debug.Apply();File.WriteAllBytes(Path.Combine(directory,prefix+"-borders.png"),debug.EncodeToPNG());UnityEngine.Object.DestroyImmediate(debug);
            }
            if(reference!=null)WriteAtlasImage(Path.Combine(directory,"source-"+map.ToString().ToLowerInvariant()+"-atlas.png"),pixels,size,bounds,session,reference,true);

            if(!MapLayout.IsImported)WriteLayout(Path.Combine(directory,prefix+"-layout.json"),session);
            // The same camps are inspected for every tag, chosen by the first run.
            string focusPath=Path.Combine(directory,"focus-"+map.ToString().ToLowerInvariant()+".txt");
            List<string> focus;
            var focusOverride=Argument("--riskai-territory-focus");
            if(focusOverride!=null)focus=focusOverride.Split(';').Where(n=>Array.FindIndex(MapLayout.Countries,c=>c.Name==n)>=0).ToList();
            else if(File.Exists(focusPath))focus=File.ReadAllLines(focusPath).Where(l=>l.Length>0).ToList();
            else
            {
                focus=metrics.OrderByDescending(m=>m.Badness).Take(5).Select(m=>m.Name).ToList();
                File.WriteAllLines(focusPath,focus);
            }
            var camera=Camera.main;if(!camera)throw new InvalidOperationException("Territory review has no main camera.");
            camera.aspect=Width/(float)Height;
            var readback=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){antiAliasing=1,name="Territory review readback"};readback.Create();
            try
            {
                Vector2 min=MapLayout.PlayableMin,max=MapLayout.PlayableMax;
                view.SetStrategic(true);
                Render(camera,readback,Path.Combine(directory,prefix+"-strategic.png"),new Vector3((min.x+max.x)*.5f,0,(min.y+max.y)*.5f),
                    Mathf.Max((max.y-min.y)*.5f,(max.x-min.x)*.5f*Height/Width)*1.02f,80);
                view.SetStrategic(false);
                foreach(string name in focus)
                {
                    int country=Array.FindIndex(MapLayout.Countries,c=>c.Name==name);
                    var camp=session.Camps.FirstOrDefault(c=>c&&c.Country==country);
                    if(country<0||!camp)continue;
                    if(!CountryBounds(pixels,size,bounds,country,session,out var center,out float extent))continue;
                    camp.Select(true);
                    string safe=new string(name.Select(ch=>char.IsLetterOrDigit(ch)?char.ToLowerInvariant(ch):'-').ToArray());
                    Render(camera,readback,Path.Combine(directory,prefix+"-camp-"+safe+".png"),center,Mathf.Clamp(extent*.7f,14,110),72);
                    // Gameplay camera (55 degrees) close to the territory, with and without the inspection overlay.
                    if(focusOverride!=null)
                    {
                        Render(camera,readback,Path.Combine(directory,prefix+"-tactical-"+safe+".png"),center,Mathf.Clamp(extent*.45f,14,60),55);
                        camp.Select(false);
                        Render(camera,readback,Path.Combine(directory,prefix+"-tactical-"+safe+"-plain.png"),center,Mathf.Clamp(extent*.45f,14,60),55);
                    }
                    camp.Select(false);
                }
                // Free views: --riskai-territory-look "x,z,zoom;x,z,zoom" (world XZ, gameplay camera).
                var looks=Argument("--riskai-territory-look");
                if(looks!=null)
                {
                    int n=0;
                    foreach(var look in looks.Split(';'))
                    {
                        var part=look.Split(',');if(part.Length<3)continue;
                        float lx=float.Parse(part[0],CultureInfo.InvariantCulture),lz=float.Parse(part[1],CultureInfo.InvariantCulture),lzoom=float.Parse(part[2],CultureInfo.InvariantCulture);
                        Render(camera,readback,Path.Combine(directory,prefix+"-look-"+(n++)+".png"),new Vector3(lx,0,lz),lzoom,55);
                    }
                }
                // Strategic readability at an early-game ownership: every country neutral except
                // the first two focus camps (player 0 and 1), full map and a closer strategic zoom.
                var owned=focus.Select(n=>Array.FindIndex(MapLayout.Countries,c=>c.Name==n)).Where(c=>c>=0).Take(2).ToArray();
                foreach(var town in session.Towns)town.State.Owner=owned.Length>0&&town.State.Country==owned[0]?0:owned.Length>1&&town.State.Country==owned[1]?1:-1;
                if(session.Naval)foreach(var port in session.Naval.Harbors)if(!port.IsImportedPort)port.State.Owner=-1;
                atlas.RefreshOwners();view.SetStrategic(true);
                Render(camera,readback,Path.Combine(directory,prefix+"-strategic-neutral.png"),new Vector3((min.x+max.x)*.5f,0,(min.y+max.y)*.5f),
                    Mathf.Max((max.y-min.y)*.5f,(max.x-min.x)*.5f*Height/Width)*1.02f,80);
                if(owned.Length>0&&CountryBounds(pixels,size,bounds,owned[0],session,out var near,out _))
                    Render(camera,readback,Path.Combine(directory,prefix+"-strategic-near.png"),near,view.EnterZoom*1.05f,80);
                view.SetStrategic(false);
            }
            finally{if(RenderTexture.active==readback)RenderTexture.active=null;readback.Release();UnityEngine.Object.DestroyImmediate(readback);}
        }

        static Color32[] ReadTexture(Texture source)
        {
            var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            target.filterMode=FilterMode.Point;
            var previous=RenderTexture.active;
            Graphics.Blit(source,target);
            RenderTexture.active=target;
            var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
            copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply();
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
            var result=copy.GetPixels32();UnityEngine.Object.DestroyImmediate(copy);return result;
        }

        static int CountryOf(Color32 c)=>c.b-1;
        static Vector2 World(Vector4 bounds,int size,int x,int z)=>new Vector2(bounds.x+(x+.5f)/size*bounds.z,bounds.y+(z+.5f)/size*bounds.w);
        static int Pixel(Vector4 bounds,int size,Vector3 world)
        {
            int x=Mathf.Clamp(Mathf.FloorToInt((world.x-bounds.x)/bounds.z*size),0,size-1);
            int z=Mathf.Clamp(Mathf.FloorToInt((world.z-bounds.y)/bounds.w*size),0,size-1);
            return z*size+x;
        }

        static Metrics[] Measure(Color32[] pixels,int size,Vector4 bounds,out float mapAgreement,out int[] reference)
        {
            int n=MapLayout.Countries.Length,count=size*size;
            var metrics=new Metrics[n];
            for(int c=0;c<n;c++)metrics[c]=new Metrics{Name=MapLayout.Countries[c].Name,Country=c};
            var land=new bool[count];for(int i=0;i<count;i++)land[i]=pixels[i].a>127;
            // Distance to water in pixels (two-pass chamfer).
            var coast=new float[count];
            for(int i=0;i<count;i++)coast[i]=land[i]?1e6f:0;
            Chamfer(coast,size);
            float unitsPerPixel=bounds.z/size;
            // Land components, to recognise a coastal strip on a large landmass.
            var landComponent=Components(i=>land[i],size,out var landSizes);
            for(int i=0;i<count;i++)
            {
                int c=CountryOf(pixels[i]);if(c<0||c>=n)continue;
                if(land[i]){metrics[c].LandPixels++;if(coast[i]*unitsPerPixel<=CoastBand)metrics[c].CoastPixels++;}
                else metrics[c].SeaPixels++;
            }
            var hostLand=new Dictionary<int,HashSet<int>>();
            for(int i=0;i<count;i++)
            {
                int c=CountryOf(pixels[i]);if(!land[i]||c<0||c>=n)continue;
                if(!hostLand.TryGetValue(c,out var set))hostLand[c]=set=new HashSet<int>();set.Add(landComponent[i]);
            }
            foreach(var pair in hostLand){long total=0;foreach(int comp in pair.Value)total+=landSizes[comp];metrics[pair.Key].HostLandRatio=metrics[pair.Key].LandPixels>0?total/(float)metrics[pair.Key].LandPixels:0;}
            // Country pieces (8-connected land pixels of one country).
            var piece=Components(i=>land[i]&&CountryOf(pixels[i])>=0,size,out var pieceSizes,(a,b)=>pixels[a].b==pixels[b].b);
            var pieceCountry=new Dictionary<int,int>();var pieceHasCity=new HashSet<int>();
            for(int i=0;i<count;i++)if(piece[i]>=0)pieceCountry[piece[i]]=CountryOf(pixels[i]);
            for(int t=0;t<MapLayout.Towns.Length;t++)
            {
                var town=MapLayout.Towns[t];var m=metrics[town.Country];m.Cities++;
                int p=Pixel(bounds,size,Anchor(town));
                if(!land[p]){m.CitiesOnWater++;p=NearestLand(land,size,p,town.Country,pixels);}
                if(p>=0&&land[p]&&CountryOf(pixels[p])==town.Country){m.CitiesInside++;pieceHasCity.Add(piece[p]);}
            }
            // Port-only islands count as held by their port city.
            foreach(var pair in pieceCountry)
            {
                var m=metrics[pair.Value];m.Pieces++;
                int pixelsInPiece=pieceSizes[pair.Key];
                if(!pieceHasCity.Contains(pair.Key)){m.OrphanPieces++;m.OrphanPixels+=pixelsInPiece;}
                if(pixelsInPiece*unitsPerPixel*unitsPerPixel<12)m.SliverPieces++;
            }
            // Polsby-Popper compactness over the whole territory, with a 4/pi
            // correction for axis-aligned pixel perimeter.
            var perimeter=new int[n];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                int i=z*size+x,c=CountryOf(pixels[i]);if(!land[i]||c<0||c>=n)continue;
                if(x==0||CountryOf(pixels[i-1])!=c||!land[i-1])perimeter[c]++;
                if(x==size-1||CountryOf(pixels[i+1])!=c||!land[i+1])perimeter[c]++;
                if(z==0||CountryOf(pixels[i-size])!=c||!land[i-size])perimeter[c]++;
                if(z==size-1||CountryOf(pixels[i+size])!=c||!land[i+size])perimeter[c]++;
            }
            for(int c=0;c<n;c++)
            {
                float p=perimeter[c]*Mathf.PI/4f;
                metrics[c].Compactness=p>0?4*Mathf.PI*metrics[c].LandPixels/(p*p):0;
            }
            reference=SourceReference(size,bounds,land);
            mapAgreement=-1;
            if(reference!=null)
            {
                var inter=new int[n];var union=new int[n];int agree=0,defined=0;
                for(int i=0;i<count;i++)
                {
                    if(!land[i])continue;
                    int ours=CountryOf(pixels[i]),theirs=reference[i];
                    if(theirs<0)continue;
                    defined++;if(ours==theirs)agree++;
                    if(ours>=0&&ours<n){union[ours]++;if(ours==theirs)inter[ours]++;}
                    if(theirs!=ours)union[theirs]++;
                }
                for(int c=0;c<n;c++)metrics[c].SourceIoU=union[c]>0?inter[c]/(float)union[c]:0;
                mapAgreement=defined>0?agree/(float)defined:0;
            }
            return metrics;
        }

        // A source port (h00O) may stand far out in its amphibious circle; its city is the shore landing.
        static Vector3 Anchor(MapLayout.City town)=>MapLayout.IsImported&&town.IsPort?ImportedPortLayout.Resolve(town.Position,town.ClaimPoint).Shore:town.Position;

        static int NearestLand(bool[] land,int size,int start,int country,Color32[] pixels)
        {
            // A source port stands in its amphibious circle; test the nearest land within 6 pixels.
            int sx=start%size,sz=start/size,best=-1;float bestDistance=float.MaxValue;
            for(int dz=-8;dz<=8;dz++)for(int dx=-8;dx<=8;dx++)
            {
                int x=sx+dx,z=sz+dz;if(x<0||z<0||x>=size||z>=size)continue;
                int i=z*size+x;if(!land[i])continue;
                float d=dx*dx+dz*dz-(CountryOf(pixels[i])==country?.5f:0);
                if(d<bestDistance){bestDistance=d;best=i;}
            }
            return best;
        }

        /// <summary>Warcraft paints every Risk country with one ground tile. Same-tile land
        /// components containing a member city are the original borders.</summary>
        static int[] SourceReference(int size,Vector4 bounds,bool[] land)
        {
            var data=MapLayout.Imported;if(data==null)return null;
            int w=data.width,h=data.height;
            var tile=new int[w*h];for(int i=0;i<tile.Length;i++)tile[i]=ImportedMapData.GroundTileIndex(data.tileSamples[i]);
            var countryTiles=new HashSet<int>();
            foreach(var city in data.cities)if(!city.port)countryTiles.Add(tile[Vertex(data,city.x,city.z)]);
            var component=new int[w*h];for(int i=0;i<component.Length;i++)component[i]=-1;
            int next=0;var queue=new Queue<int>();
            for(int s=0;s<w*h;s++)
            {
                if(component[s]>=0||data.landSamples[s]==0||!countryTiles.Contains(tile[s]))continue;
                component[s]=next;queue.Enqueue(s);
                while(queue.Count>0)
                {
                    int i=queue.Dequeue(),x=i%w,z=i/w;
                    for(int k=0;k<4;k++)
                    {
                        int nx=x+(k==0?1:k==1?-1:0),nz=z+(k==2?1:k==3?-1:0);
                        if(nx<0||nz<0||nx>=w||nz>=h)continue;int j=nz*w+nx;
                        if(component[j]>=0||data.landSamples[j]==0||tile[j]!=tile[s])continue;
                        component[j]=next;queue.Enqueue(j);
                    }
                }
                next++;
            }
            var owner=new int[next];for(int i=0;i<next;i++)owner[i]=-1;
            foreach(var city in data.cities)if(!city.port){int comp=component[Vertex(data,city.x,city.z)];if(comp>=0)owner[comp]=city.country;}
            var result=new int[size*size];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                int i=z*size+x;result[i]=-1;if(!land[i])continue;
                var world=World(bounds,size,x,z);var source=data.SourcePositionAt(world.x,world.y);
                int comp=component[Vertex(data,source.x,source.y)];
                if(comp>=0)result[i]=owner[comp];
            }
            return result;
        }
        static int Vertex(ImportedMapData data,float x,float z)
        {
            int ix=Mathf.Clamp(Mathf.RoundToInt((x-data.originX)/data.cellSize),0,data.width-1);
            int iz=Mathf.Clamp(Mathf.RoundToInt((z-data.originZ)/data.cellSize),0,data.height-1);
            return iz*data.width+ix;
        }

        static int[] Components(Func<int,bool> include,int size,out List<int> sizes,Func<int,int,bool> same=null)
        {
            var label=new int[size*size];for(int i=0;i<label.Length;i++)label[i]=-1;
            sizes=new List<int>();var stack=new Stack<int>();
            for(int s=0;s<label.Length;s++)
            {
                if(label[s]>=0||!include(s))continue;
                int id=sizes.Count,members=0;label[s]=id;stack.Push(s);
                while(stack.Count>0)
                {
                    int i=stack.Pop(),x=i%size,z=i/size;members++;
                    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    {
                        if(dx==0&&dz==0)continue;int nx=x+dx,nz=z+dz;
                        if(nx<0||nz<0||nx>=size||nz>=size)continue;int j=nz*size+nx;
                        if(label[j]>=0||!include(j)||(same!=null&&!same(s,j)))continue;
                        label[j]=id;stack.Push(j);
                    }
                }
                sizes.Add(members);
            }
            return label;
        }

        static void Chamfer(float[] d,int size)
        {
            const float D=1.41421356f;
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                int i=z*size+x;float v=d[i];
                if(x>0)v=Mathf.Min(v,d[i-1]+1);if(z>0)v=Mathf.Min(v,d[i-size]+1);
                if(x>0&&z>0)v=Mathf.Min(v,d[i-size-1]+D);if(x<size-1&&z>0)v=Mathf.Min(v,d[i-size+1]+D);
                d[i]=v;
            }
            for(int z=size-1;z>=0;z--)for(int x=size-1;x>=0;x--)
            {
                int i=z*size+x;float v=d[i];
                if(x<size-1)v=Mathf.Min(v,d[i+1]+1);if(z<size-1)v=Mathf.Min(v,d[i+size]+1);
                if(x<size-1&&z<size-1)v=Mathf.Min(v,d[i+size+1]+D);if(x>0&&z<size-1)v=Mathf.Min(v,d[i+size-1]+D);
                d[i]=v;
            }
        }

        static Color CountryColor(int country)
        {
            if(country<0)return new Color(.35f,.35f,.35f);
            float hue=(country*.61803398875f)%1f;
            return Color.HSVToRGB(hue,.45f+.3f*((country*7)%3)/2f,.72f+.2f*((country*5)%2));
        }

        static void WriteAtlasImage(string path,Color32[] pixels,int size,Vector4 bounds,BattleSession session,int[] reference,bool source)
        {
            var image=new Texture2D(size,size,TextureFormat.RGB24,false);
            var colors=new Color32[size*size];
            int Label(int i)=>source?reference[i]:(pixels[i].a>127?CountryOf(pixels[i]):-2);
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                int i=z*size+x;bool land=pixels[i].a>127;
                if(!land){colors[i]=new Color32(24,48,70,255);continue;}
                int c=Label(i);Color color=CountryColor(c);
                bool edge=(x>0&&pixels[i-1].a>127&&Label(i-1)!=c)||(z>0&&pixels[i-size].a>127&&Label(i-size)!=c);
                colors[i]=edge?new Color32(20,20,20,255):(Color32)color;
            }
            // Member cities: white when inside their own territory, red otherwise. Camps: black squares.
            foreach(var town in MapLayout.Towns)
            {
                int p=Pixel(bounds,size,Anchor(town)),q=p;
                if(pixels[q].a<=127){q=NearestLandPixel(pixels,size,p);}
                bool inside=q>=0&&pixels[q].a>127&&(source?reference[q]:CountryOf(pixels[q]))==town.Country;
                Dot(colors,size,p,inside?new Color32(255,255,255,255):new Color32(230,20,20,255),town.IsPort?2:3,!town.IsPort);
            }
            foreach(var camp in session.Camps)if(camp)Dot(colors,size,Pixel(bounds,size,camp.SpawnPoint),new Color32(0,0,0,255),2,false);
            image.SetPixels32(colors);image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        static int NearestLandPixel(Color32[] pixels,int size,int start)
        {
            int sx=start%size,sz=start/size,best=-1,bestDistance=int.MaxValue;
            for(int dz=-8;dz<=8;dz++)for(int dx=-8;dx<=8;dx++)
            {
                int x=sx+dx,z=sz+dz;if(x<0||z<0||x>=size||z>=size||pixels[z*size+x].a<=127)continue;
                if(dx*dx+dz*dz<bestDistance){bestDistance=dx*dx+dz*dz;best=z*size+x;}
            }
            return best;
        }
        static void Dot(Color32[] colors,int size,int center,Color32 color,int radius,bool round)
        {
            int cx=center%size,cz=center/size;
            for(int dz=-radius;dz<=radius;dz++)for(int dx=-radius;dx<=radius;dx++)
            {
                if(round&&dx*dx+dz*dz>radius*radius+1)continue;
                int x=cx+dx,z=cz+dz;if(x<0||z<0||x>=size||z>=size)continue;colors[z*size+x]=color;
            }
        }

        static bool CountryBounds(Color32[] pixels,int size,Vector4 bounds,int country,BattleSession session,out Vector3 center,out float extent)
        {
            float minX=float.MaxValue,minZ=float.MaxValue,maxX=float.MinValue,maxZ=float.MinValue;
            void Include(Vector2 p){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.y);maxZ=Mathf.Max(maxZ,p.y);}
            for(int z=0;z<size;z+=2)for(int x=0;x<size;x+=2){int i=z*size+x;if(pixels[i].a>127&&CountryOf(pixels[i])==country)Include(World(bounds,size,x,z));}
            foreach(var town in session.Towns)if(town.State.Country==country)Include(new Vector2(town.transform.position.x,town.transform.position.z));
            center=new Vector3((minX+maxX)*.5f,0,(minZ+maxZ)*.5f);extent=Mathf.Max(maxX-minX,(maxZ-minZ)*Width/Height)*.5f+8;
            return minX<=maxX;
        }

        static void WriteJson(string path,ScenarioMap map,Metrics[] metrics,float pixelArea,float atlasMs,float fieldMs,float agreement)
        {
            var s=new StringBuilder();var inv=CultureInfo.InvariantCulture;
            s.Append("{\"map\":\"").Append(map).Append("\",\"atlasMs\":").Append(atlasMs.ToString("F1",inv))
             .Append(",\"fieldMs\":").Append(fieldMs.ToString("F1",inv)).Append(",\"sourceAgreement\":").Append(agreement.ToString("F4",inv)).Append(",\"countries\":[\n");
            for(int i=0;i<metrics.Length;i++)
            {
                var m=metrics[i];
                s.Append("{\"name\":\"").Append(m.Name.Replace("\"","'")).Append("\",\"country\":").Append(m.Country)
                 .Append(",\"cities\":").Append(m.Cities).Append(",\"citiesInside\":").Append(m.CitiesInside).Append(",\"citiesOnWater\":").Append(m.CitiesOnWater)
                 .Append(",\"area\":").Append(m.Area(pixelArea).ToString("F1",inv)).Append(",\"seaFraction\":").Append(m.SeaFraction.ToString("F3",inv))
                 .Append(",\"coastFraction\":").Append(m.CoastFraction.ToString("F3",inv)).Append(",\"hostLandRatio\":").Append(m.HostLandRatio.ToString("F2",inv))
                 .Append(",\"coastalStrip\":").Append(m.CoastalStrip?"true":"false")
                 .Append(",\"pieces\":").Append(m.Pieces).Append(",\"orphanPieces\":").Append(m.OrphanPieces).Append(",\"orphanArea\":").Append((m.OrphanPixels*pixelArea).ToString("F1",inv))
                 .Append(",\"slivers\":").Append(m.SliverPieces).Append(",\"compactness\":").Append(m.Compactness.ToString("F3",inv))
                 .Append(",\"sourceIoU\":").Append(m.SourceIoU.ToString("F3",inv)).Append(",\"badness\":").Append(m.Badness.ToString("F2",inv)).Append('}')
                 .Append(i<metrics.Length-1?",\n":"\n");
            }
            s.Append("]}\n");File.WriteAllText(path,s.ToString());
        }

        /// <summary>Authored-map geography (height, land, territory) with cities, harbours and camps, for layout review.</summary>
        static void WriteLayout(string path,BattleSession session)
        {
            var inv=CultureInfo.InvariantCulture;var s=new StringBuilder();var field=TerritoryField.Current;
            Vector2 min=MapLayout.PlayableMin,max=MapLayout.PlayableMax;const float step=1f;
            int w=Mathf.CeilToInt((max.x-min.x)/step),h=Mathf.CeilToInt((max.y-min.y)/step);
            s.Append("{\"minX\":").Append(min.x.ToString(inv)).Append(",\"minZ\":").Append(min.y.ToString(inv)).Append(",\"step\":").Append(step.ToString(inv))
             .Append(",\"width\":").Append(w).Append(",\"height\":").Append(h).Append(",\"spacing\":").Append(MapLayout.Spacing.ToString(inv)).Append(",\"grid\":[");
            for(int z=0;z<h;z++)for(int x=0;x<w;x++)
            {
                float wx=min.x+(x+.5f)*step,wz=min.y+(z+.5f)*step;
                bool land=MapLayout.IsLand(wx,wz);
                if(z+x>0)s.Append(',');
                s.Append(land?MapLayout.Height(wx,wz).ToString("F2",inv):"-9").Append(',').Append(land?field.CountryAt(wx,wz):-1);
            }
            s.Append("],\"towns\":[");
            for(int i=0;i<MapLayout.Towns.Length;i++)
            {
                var t=MapLayout.Towns[i];if(i>0)s.Append(',');
                s.Append("{\"id\":\"").Append(t.Id).Append("\",\"name\":\"").Append(t.Name).Append("\",\"x\":").Append(t.Position.x.ToString("F2",inv)).Append(",\"z\":").Append(t.Position.z.ToString("F2",inv)).Append(",\"country\":").Append(t.Country).Append('}');
            }
            s.Append("],\"harbors\":[");
            bool first=true;
            if(session.Naval)foreach(var port in session.Naval.Harbors)
            {
                if(!first)s.Append(',');first=false;
                int linked=port.LinkedTown?session.Towns.IndexOf(port.LinkedTown):-1;
                s.Append("{\"name\":\"").Append(port.DisplayName).Append("\",\"x\":").Append(port.Landing.x.ToString("F2",inv)).Append(",\"z\":").Append(port.Landing.z.ToString("F2",inv)).Append(",\"linked\":").Append(linked).Append('}');
            }
            s.Append("],\"camps\":[");
            first=true;
            foreach(var camp in session.Camps){if(!camp)continue;if(!first)s.Append(',');first=false;s.Append("{\"country\":").Append(camp.Country).Append(",\"x\":").Append(camp.SpawnPoint.x.ToString("F2",inv)).Append(",\"z\":").Append(camp.SpawnPoint.z.ToString("F2",inv)).Append('}');}
            s.Append("],\"countries\":[");
            for(int c=0;c<MapLayout.Countries.Length;c++){if(c>0)s.Append(',');s.Append('"').Append(MapLayout.Countries[c].Name).Append('"');}
            s.Append("]}").Append('\n');File.WriteAllText(path,s.ToString());
        }

        static void Render(Camera camera,RenderTexture readback,string path,Vector3 focus,float zoom,float pitch)
        {
            camera.transform.rotation=Quaternion.Euler(pitch,0,0);
            camera.orthographicSize=zoom;camera.transform.position=focus-camera.transform.forward*(zoom/Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad));
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=readback};
            for(int pass=0;pass<3;pass++)RenderPipeline.SubmitRenderRequest(camera,request);
            var previous=RenderTexture.active;RenderTexture.active=readback;var image=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);camera.targetTexture=null;
            Debug.Log("RISKAI_TERRITORY_REVIEW_RENDER: "+path);
        }
    }
}
