using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public class UiViewportTests
    {
        [TestCase(360,800,1)]
        [TestCase(800,360,1)]
        [TestCase(1536,2048,2)]
        [TestCase(2048,1536,2)]
        [TestCase(1600,900,1)]
        public void SafeInsetsAndControlsLeaveAUsableWorld(int width,int height,float density)
        {
            var safe=new Rect(12*density,20*density,width-28*density,height-36*density);
            var layout=UiViewport.Calculate(new Vector2(width,height),safe,density,48,260);
            Assert.That(layout.World.xMin,Is.EqualTo(safe.xMin));
            Assert.That(layout.World.xMax,Is.EqualTo(safe.xMax));
            Assert.That(layout.World.yMin,Is.GreaterThan(safe.yMin));
            Assert.That(layout.World.yMax,Is.LessThan(safe.yMax));
            Assert.That(layout.World.height,Is.GreaterThanOrEqualTo(safe.height*.379f));
            Assert.That(layout.World.Contains(new Vector2(safe.center.x,safe.yMin+10)),Is.False);
            Assert.That(layout.World.Contains(layout.World.center),Is.True);
        }
        [Test] public void MissingSafeAreaUsesScreenAndNeverCreatesNegativeWorld()
        {
            var layout=UiViewport.Calculate(new Vector2(100,100),Rect.zero,1,500,500);
            Assert.That(layout.Safe,Is.EqualTo(new Rect(0,0,100,100)));
            Assert.That(layout.World.height,Is.EqualTo(38).Within(.001));
        }
    }
}
