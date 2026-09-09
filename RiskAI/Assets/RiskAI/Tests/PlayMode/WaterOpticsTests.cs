using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class WaterOpticsTests
    {
        [UnityTest]
        public IEnumerator OceanColorIgnoresSourceBedTilesButContactAndAboveWaterRemainTransparent()
        {
            var shader=Shader.Find("Hidden/RiskAI/Tests/WaterOpticsProbe");Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);
            var target=RenderTexture.GetTemporary(1,1,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var readback=new Texture2D(1,1,TextureFormat.RGBA32,false,true);
            var previousTarget=RenderTexture.active;bool previousSrgb=GL.sRGBWrite;
            try
            {
                // These are common actual source sea depths, not arbitrary deep-water limits.
                var shallowBed=Render(material,target,readback,.538f,0,Color.red);
                var normalBed=Render(material,target,readback,1.793f,0,Color.blue);
                var deepBed=Render(material,target,readback,4.608f,0,Color.white);
                AssertColor(shallowBed,normalBed);AssertColor(normalBed,deepBed);
                Assert.That(normalBed.a,Is.EqualTo(1).Within(.005f));
                var contactRed=Render(material,target,readback,.02f,1,Color.red);
                var contactBlue=Render(material,target,readback,.02f,1,Color.blue);
                Assert.That(contactRed.r-contactBlue.r,Is.GreaterThan(.5f),"Contact shallows must retain the actual opaque shore color.");
                Assert.That(contactBlue.b-contactRed.b,Is.GreaterThan(.5f));
                Assert.That(contactRed.a,Is.InRange(.001f,.1f),"The waterline retains its soft transparency.");
                var waterline=Render(material,target,readback,0,1,Color.red);
                var aboveWaterHull=Render(material,target,readback,-.3f,1,Color.red);
                Assert.That(waterline.a,Is.Zero);
                Assert.That(aboveWaterHull.a,Is.Zero,"Opaque geometry above the water must not acquire water effects.");
                var coastal=Render(material,target,readback,.538f,1,Color.blue);
                Assert.That(coastal.r+coastal.g+coastal.b,Is.GreaterThan(normalBed.r+normalBed.g+normalBed.b),"The continuous coastal field retains readable shallows.");
            }
            finally
            {
                GL.sRGBWrite=previousSrgb;RenderTexture.active=previousTarget;
                RenderTexture.ReleaseTemporary(target);Object.Destroy(readback);Object.Destroy(material);
            }
            yield return null;
        }
        static Color Render(Material material,RenderTexture target,Texture2D readback,float depth,float coast,Color bottom)
        {
            material.SetVector("_Optics",new Vector4(depth,coast,.5f,0));material.SetVector("_Bottom",bottom);
            GL.sRGBWrite=false;Graphics.Blit(Texture2D.whiteTexture,target,material);RenderTexture.active=target;
            readback.ReadPixels(new Rect(0,0,1,1),0,0);readback.Apply();return readback.GetPixel(0,0);
        }
        static void AssertColor(Color actual,Color expected)
        {
            Assert.That(actual.r,Is.EqualTo(expected.r).Within(.005f));
            Assert.That(actual.g,Is.EqualTo(expected.g).Within(.005f));
            Assert.That(actual.b,Is.EqualTo(expected.b).Within(.005f));
        }
    }
}
