using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class MinimapMarkerRasterTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void CameraOutlineClipsBothEndsInsideOffsetMinimap(bool reverse)
        {
            var a=new Vector2(90,190);var b=new Vector2(180,280);
            if(reverse)(a,b)=(b,a);
            Assert.That(RtsSkin.ClipLine(new Rect(100,200,60,40),ref a,ref b),Is.True);
            Assert.That(Vector2.Distance(a,reverse?new Vector2(140,240):new Vector2(100,200)),Is.LessThan(.001f));
            Assert.That(Vector2.Distance(b,reverse?new Vector2(100,200):new Vector2(140,240)),Is.LessThan(.001f));
        }

        [Test]
        public void CameraOutlineRejectsParallelEdgesOutsideMinimap()
        {
            var a=new Vector2(90,190);var b=new Vector2(180,190);
            Assert.That(RtsSkin.ClipLine(new Rect(100,200,60,40),ref a,ref b),Is.False);
        }

        static Color32 At(MinimapMarkerRaster raster, int x, int topY) => raster.Pixels[(raster.Height - 1 - topY) * raster.Width + x];

        [Test]
        public void TopDownMapCoordinatesAreStoredInTextureBottomUpOrder()
        {
            var raster = new MinimapMarkerRaster(8, 8, new Vector2(8, 8));
            raster.Fill(new Rect(3, 1, 2, 2), Color.blue);
            Assert.That(At(raster, 3, 1), Is.EqualTo((Color32)Color.blue));
            Assert.That(At(raster, 4, 2), Is.EqualTo((Color32)Color.blue));
            Assert.That(At(raster, 3, 6).a, Is.Zero, "Northern units must not be mirrored into the south.");
        }

        [Test]
        public void FractionalUnitMarkersKeepEdgeCoverageAndTeamHue()
        {
            var raster = new MinimapMarkerRaster(8, 8, new Vector2(8, 8));
            raster.Fill(new Rect(1, 1, 2.5f, 2.5f), Color.red);
            Assert.That(At(raster, 1, 1), Is.EqualTo((Color32)Color.red));
            Assert.That((int)At(raster, 3, 1).a, Is.EqualTo(128).Within(1));
            Assert.That((int)At(raster, 3, 3).a, Is.EqualTo(64).Within(1));
            Assert.That(At(raster, 3, 3).r, Is.EqualTo(255), "Transparent edges keep unpremultiplied team colour.");
            Assert.That(At(raster, 4, 1).a, Is.Zero);
        }

        [Test]
        public void LaterMarkersCompositeOverEarlierMarkersAndSelectionRemainsWhite()
        {
            var raster = new MinimapMarkerRaster(8, 8, new Vector2(8, 8));
            raster.Fill(new Rect(0, 0, 8, 8), Color.red);
            raster.Fill(new Rect(1.5f, 1, 2, 2), Color.blue);
            var edge = At(raster, 1, 1);
            Assert.That((int)edge.r, Is.EqualTo(128).Within(1));
            Assert.That((int)edge.b, Is.EqualTo(128).Within(1));
            Assert.That(edge.a, Is.EqualTo(255));
            raster.Fill(new Rect(2, 1, 1, 1), Color.white);
            Assert.That(At(raster, 2, 1), Is.EqualTo((Color32)Color.white));
        }

        [Test]
        public void HarborAndDefenderOutlinesKeepTheirHollowCentersAndOneLogicalPixelEdges()
        {
            var raster = new MinimapMarkerRaster(16, 16, new Vector2(8, 8));
            raster.Outline(new Rect(2, 2, 3, 3), Color.yellow);
            Assert.That(At(raster, 4, 4), Is.EqualTo((Color32)Color.yellow));
            Assert.That(At(raster, 5, 5), Is.EqualTo((Color32)Color.yellow));
            Assert.That(At(raster, 7, 7).a, Is.Zero);
            Assert.That(At(raster, 10, 7), Is.EqualTo((Color32)Color.yellow));
            Assert.That(At(raster, 11, 7), Is.EqualTo((Color32)Color.yellow));
            Assert.That(At(raster, 12, 7).a, Is.Zero);
        }

        [Test]
        public void ClippingCannotWrapMarkersAndClearReusesThePixelBuffer()
        {
            var raster = new MinimapMarkerRaster(8, 8, new Vector2(8, 8));
            var pixels = raster.Pixels;
            raster.Fill(new Rect(-1, -1, 2, 2), Color.green);
            Assert.That(At(raster, 0, 0), Is.EqualTo((Color32)Color.green));
            int painted = 0;
            foreach (var pixel in pixels) if (pixel.a != 0) painted++;
            Assert.That(painted, Is.EqualTo(1));
            raster.Fill(new Rect(20, 20, 5, 5), Color.white);
            raster.Clear();
            Assert.That(raster.Pixels, Is.SameAs(pixels));
            foreach (var pixel in pixels) Assert.That(pixel.a, Is.Zero);
        }
    }
}
