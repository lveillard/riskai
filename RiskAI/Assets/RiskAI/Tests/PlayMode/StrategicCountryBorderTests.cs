using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI.Tests
{
    public sealed class StrategicCountryBorderTests
    {
        [Test]
        public void CountryBordersIgnoreCityOwnershipAndNeverOutlineWater()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Requires graphics device for strategic shader regression.");
            var material=new Material(Shader.Find("RiskAI/StrategicTerritory"));
            var regions=new Texture2D(32,32,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var palette=new Texture2D(2,1,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point};
            var readback=new Texture2D(128,128,TextureFormat.RGBA32,false,true);
            var target=RenderTexture.GetTemporary(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var mesh=new Mesh();var previous=RenderTexture.active;
            var surface=new GameObject("Country border test surface");surface.layer=29;
            var cameraObject=new GameObject("Country border test camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;camera.aspect=1;
            camera.transform.SetPositionAndRotation(new Vector3(0,2,0),Quaternion.Euler(90,0,0));
            camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.cullingMask=1<<29;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.12f,.19f,1);
            camera.allowHDR=false;camera.allowMSAA=false;
            try
            {
                mesh.vertices=new[]{new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,-1),new Vector3(1,0,1)};
                mesh.normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up};mesh.triangles=new[]{0,1,2,1,3,2};
                surface.AddComponent<MeshFilter>().sharedMesh=mesh;surface.AddComponent<MeshRenderer>().sharedMaterial=material;
                material.SetTexture("_Regions",regions);material.SetTexture("_Palette",palette);
                material.SetVector("_MapBounds",new Vector4(-1,-1,2,2));material.SetFloat("_PaletteWidth",2);
                material.SetFloat("_Overview",1);material.SetFloat("_SelectedCountry",-1);material.SetFloat("_ZWrite",1);
                palette.SetPixels(new[]{Color.red,Color.red});palette.Apply();
                var pixels=new Color32[32*32];
                void Fill(byte rightCountry,byte rightAlpha)
                {
                    for(int z=0;z<32;z++)for(int x=0;x<32;x++)pixels[z*32+x]=new Color32((byte)(x<16?0:1),0,x<16?(byte)1:rightCountry,x<16?(byte)255:rightAlpha);
                    regions.SetPixels32(pixels);regions.Apply();
                }
                void Render()
                {
                    // Use the real pipeline so camera constant buffers and render-graph
                    // state match the game, rather than legacy immediate matrices.
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination=target });
                    RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,128,128),0,0);readback.Apply();
                }
                Fill(1,255);Render();float interior=readback.GetPixel(40,64).r;
                Assert.That(interior,Is.GreaterThan(.2f),"The terrain must actually render.");
                Assert.That(readback.GetPixel(63,64).r,Is.EqualTo(interior).Within(.02f),"Two cities in one camp have no group border.");
                Fill(2,255);Render();
                Assert.That(readback.GetPixel(63,64).r,Is.LessThan(interior-.1f),"Different camps remain separated even with identical owners.");
                palette.SetPixels(new[]{Color.red,Color.green});palette.Apply();Fill(1,255);Render();
                Assert.That(readback.GetPixel(63,64).r,Is.EqualTo(interior).Within(.02f),"Changing ownership must not introduce a camp border.");
                Fill(2,0);Render();
                Assert.That(readback.GetPixel(63,64).r,Is.EqualTo(interior).Within(.02f),"Water must not add a country outline along the coast.");
                Assert.That(readback.GetPixel(85,64).r,Is.LessThan(.08f),"Water stays transparent to the background.");
            }
            finally
            {
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(surface);Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);Object.DestroyImmediate(regions);
                Object.DestroyImmediate(palette);Object.DestroyImmediate(readback);
            }
        }
    }
}
