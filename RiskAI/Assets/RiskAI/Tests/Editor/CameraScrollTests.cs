using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class CameraScrollTests
    {
        static readonly Vector2 Point=new Vector2(400,300);
        static CameraScrollSample Sample(bool native,Vector2 pixels,double time=1,bool control=false,float density=1,Vector2? point=null)
        {
            var position=point??Point*density;
            if(native)return CameraScrollSample.FromUnity(new Vector2(pixels.x,-pixels.y)/100,position,control,density,time,600*density);
            return CameraScrollSample.FromBrowser(new[]{pixels.x,pixels.y,0f,control?1:0,position.x/(800*density),position.y/(600*density),1f/800,1f/600,0},new Vector2(800,600)*density,time);
        }

        [TestCase(false,1)] [TestCase(true,1)] [TestCase(false,2)] [TestCase(true,2)]
        public void BothTransportsPanBothAxesWithoutZoom(bool native,float density)
        {
            var gesture=new CameraScrollInterpreter();gesture.Push(Sample(native,new Vector2(10,5),density:density),1);
            var result=gesture.Consume(1);
            Assert.That(result.PanDelta,Is.EqualTo(new Vector2(-10,5)*density));
            Assert.That(result.Position,Is.EqualTo(Point*density));Assert.That(result.ZoomSteps,Is.Zero);
            Assert.That(gesture.Consume(1).PanDelta,Is.EqualTo(Vector2.zero),"Consume exactly once.");
        }

        [TestCase(false)] [TestCase(true)]
        public void BurstKeepsPerEventMeaningAndAcceleratedMomentumDoesNotZoom(bool native)
        {
            var gesture=new CameraScrollInterpreter();
            for(int i=0;i<20;i++)gesture.Push(Sample(native,new Vector2(0,5)),1);
            Assert.That(gesture.Consume(1).PanDelta.y,Is.EqualTo(100).Within(.001));
            gesture.Push(Sample(native,new Vector2(0,120),1.02),1.02);
            var fast=gesture.Consume(1.02);
            Assert.That(fast.ZoomSteps,Is.Zero);Assert.That(fast.PanDelta.y,Is.EqualTo(120).Within(.001));
            gesture.Push(Sample(native,new Vector2(0,-5),1.04),1.04);
            Assert.That(gesture.Consume(1.04).PanDelta.y,Is.EqualTo(-5).Within(.001));
            gesture.Push(Sample(native,new Vector2(0,100),2),2);
            var mouse=gesture.Consume(2);Assert.That(mouse.ZoomSteps,Is.EqualTo(-1));Assert.That(mouse.PanDelta,Is.EqualTo(Vector2.zero));
        }

        [TestCase(false)] [TestCase(true)]
        public void PinchAndWheelShareZoomDirectionButNeverPan(bool native)
        {
            var gesture=new CameraScrollInterpreter();
            gesture.Push(Sample(native,new Vector2(10,-20),control:true),1);
            var pinch=gesture.Consume(1);
            Assert.That(pinch.PanDelta,Is.EqualTo(Vector2.zero));
            Assert.That(RtsCameraPolicy.WheelZoomMultiplier(pinch.ZoomSteps),Is.EqualTo(Mathf.Exp(-.2f)).Within(.00001f));
            gesture.Push(Sample(native,new Vector2(0,20),control:true),1);
            Assert.That(RtsCameraPolicy.WheelZoomMultiplier(gesture.Consume(1).ZoomSteps+pinch.ZoomSteps),Is.EqualTo(1).Within(.00001f));
            gesture.Push(Sample(native,new Vector2(0,-120),2),2);
            Assert.That(gesture.Consume(2).ZoomSteps,Is.EqualTo(1.2f).Within(.00001f));
        }

        [TestCase(false)] [TestCase(true)]
        public void CancellationExpiryAndPointerChangesCannotReplayPanelScroll(bool native)
        {
            var gesture=new CameraScrollInterpreter();
            gesture.Push(Sample(native,new Vector2(0,10)),1);gesture.Reset();
            Assert.That(gesture.Consume(1).PanDelta,Is.EqualTo(Vector2.zero));
            gesture.Push(Sample(native,new Vector2(0,10)),1);
            Assert.That(gesture.Consume(1.2).PanDelta,Is.EqualTo(Vector2.zero));
            gesture.Push(Sample(native,new Vector2(0,40),2,point:Vector2.zero),2);
            gesture.Push(Sample(native,new Vector2(0,5),2),2);
            var result=gesture.Consume(2);
            Assert.That(result.PanDelta.y,Is.EqualTo(5));Assert.That(result.Position,Is.EqualTo(Point));
            gesture.Push(Sample(native,new Vector2(0,100),1),2);
            Assert.That(gesture.Consume(2).ZoomSteps,Is.Zero,"Expired transport events are discarded before classification.");
        }

        [TestCase(CameraScrollUnit.Lines,3,-1)] [TestCase(CameraScrollUnit.Pages,-1,1)]
        public void BrowserUnitsOnlyChangeTransportConversion(CameraScrollUnit unit,float delta,float expected)
        {
            var gesture=new CameraScrollInterpreter();
            gesture.Push(new CameraScrollSample(new Vector2(0,delta),unit,false,Point,Vector2.one,1,600),1);
            var result=gesture.Consume(1);Assert.That(result.ZoomSteps,Is.EqualTo(expected));Assert.That(result.PanDelta,Is.EqualTo(Vector2.zero));
        }

        [Test] public void SafariScaleUsesTheSameRatioAsDirectTouch()
        {
            var gesture=new CameraScrollInterpreter();
            gesture.Push(new CameraScrollSample(new Vector2(0,1.5f),CameraScrollUnit.Scale,true,Point,Vector2.one,1,600),1);
            Assert.That(RtsCameraPolicy.WheelZoomMultiplier(gesture.Consume(1).ZoomSteps),Is.EqualTo(1/1.5f).Within(.00001));
        }

        [Test] public void InvalidSamplesAndExtremeZoomAreBounded()
        {
            var gesture=new CameraScrollInterpreter();
            gesture.Push(Sample(false,new Vector2(float.NaN,float.PositiveInfinity)),1);
            Assert.That(gesture.Consume(1).ZoomSteps,Is.Zero);
            gesture.Push(Sample(false,new Vector2(0,10000)),1);
            Assert.That(gesture.Consume(1).ZoomSteps,Is.EqualTo(-RtsCameraPolicy.MaximumWheelStepsPerFrame));
        }
    }
}
