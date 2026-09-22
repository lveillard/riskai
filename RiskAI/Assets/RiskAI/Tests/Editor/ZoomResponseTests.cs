using NUnit.Framework;
using UnityEngine;

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

        [TestCase(0,-100,0,0,1)]
        [TestCase(0,100,0,0,-1)]
        [TestCase(0,0,-3,0,1)]
        [TestCase(0,0,3,0,-1)]
        [TestCase(0,0,0,-1,1)]
        [TestCase(0,0,0,1,-1)]
        public void BrowserPixelLineAndPageUnitsKeepDirection(float touchpadPixels,float wheelPixels,float lines,float pages,float expected)
        {
            Assert.That(RtsCameraPolicy.NormalizeWebWheelDeltas(touchpadPixels,wheelPixels,lines,pages),Is.EqualTo(expected).Within(.00001f));
        }

        [Test] public void SmallTouchpadBurstIsProportionalToItsPixelDistanceNotItsEventCount()
        {
            float pixels=0;
            for(int eventIndex=0;eventIndex<40;eventIndex++)pixels-=.5f;
            float steps=RtsCameraPolicy.NormalizeWebWheelDeltas(pixels,0,0,0);
            Assert.That(steps,Is.EqualTo(.05f).Within(.00001f));
            float oldExponent=Mathf.Abs(-pixels/RtsCameraPolicy.WebPixelUnitsPerStep*RtsCameraPolicy.WheelZoomExponent);
            float newExponent=Mathf.Abs(Mathf.Log(RtsCameraPolicy.WheelZoomMultiplier(steps)));
            Assert.That(oldExponent/newExponent,Is.EqualTo(4).Within(.0001f),"Fine continuous input must be four times less aggressive than the old pixel response.");
        }

        [Test] public void EqualWheelUnitsAreFrameIndependentAndReverseExactly()
        {
            float burst=1;
            for(int frame=0;frame<10;frame++)burst*=RtsCameraPolicy.WheelZoomMultiplier(.1f);
            Assert.That(burst,Is.EqualTo(RtsCameraPolicy.WheelZoomMultiplier(1)).Within(.00001f));
            Assert.That(RtsCameraPolicy.WheelZoomMultiplier(1)*RtsCameraPolicy.WheelZoomMultiplier(-1),Is.EqualTo(1).Within(.00001f));
            Assert.That(RtsCameraPolicy.NormalizeWebWheelDeltas(0,-120,0,0),Is.InRange(1f,1.25f),"Common 100/120 px mouse wheels remain about one notch.");
        }

        [Test] public void InvalidAndExtremeBrowserDeltasAreFiniteAndBounded()
        {
            Assert.That(RtsCameraPolicy.NormalizeWebWheelDeltas(float.NaN,float.PositiveInfinity,float.NegativeInfinity,float.NaN),Is.Zero);
            Assert.That(RtsCameraPolicy.NormalizeWebWheelDeltas(0,-10000,0,0),Is.EqualTo(RtsCameraPolicy.MaximumWheelStepsPerFrame));
            Assert.That(RtsCameraPolicy.NormalizeWebWheelDeltas(0,10000,0,0),Is.EqualTo(-RtsCameraPolicy.MaximumWheelStepsPerFrame));
        }
    }
}
