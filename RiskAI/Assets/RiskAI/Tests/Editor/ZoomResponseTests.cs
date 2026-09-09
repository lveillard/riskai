using NUnit.Framework;

namespace RiskAI.Tests
{
    public sealed class ZoomResponseTests
    {
        [Test] public void ZeroDurationAtRestCannotPoisonTheNextZoomFrame()
        {
            float velocity=0;
            Assert.That(RtsCameraPolicy.SmoothZoom(84,84,ref velocity,0),Is.EqualTo(84));
            Assert.That(velocity,Is.Zero);
            float next=RtsCameraPolicy.SmoothZoom(84,100,ref velocity,1f/60);
            Assert.That(next,Is.InRange(84,100));
            Assert.That(float.IsNaN(velocity),Is.False);
        }

        [TestCase(.65f)]
        [TestCase(1.5f)]
        public void SameZoomRatioHasSameResponseAtTacticalAndMapHeights(float ratio)
        {
            float low=34,high=340,lowVelocity=0,highVelocity=0;
            for(int frame=0;frame<60;frame++)
            {
                low=RtsCameraPolicy.SmoothZoom(low,34*ratio,ref lowVelocity,1f/60);
                high=RtsCameraPolicy.SmoothZoom(high,340*ratio,ref highVelocity,1f/60);
                Assert.That(high/340,Is.EqualTo(low/34).Within(.00001f),"Equal wheel/pinch ratios must not slow down above the strategic threshold.");
            }
            Assert.That(low/34,Is.EqualTo(ratio).Within(.0001f));
        }

        [Test] public void ReversingZoomSettlesWithoutOvershootingTheNewTarget()
        {
            float zoom=340,velocity=0;
            for(int frame=0;frame<5;frame++)zoom=RtsCameraPolicy.SmoothZoom(zoom,500,ref velocity,1f/60);
            for(int frame=0;frame<90;frame++)
            {
                zoom=RtsCameraPolicy.SmoothZoom(zoom,250,ref velocity,1f/60);
                Assert.That(zoom,Is.GreaterThanOrEqualTo(249.999f));
            }
            Assert.That(zoom,Is.EqualTo(250).Within(.01f));
        }
    }
}
