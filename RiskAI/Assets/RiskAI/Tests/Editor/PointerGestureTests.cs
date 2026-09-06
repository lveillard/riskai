using NUnit.Framework;
using RiskAI.Core;
namespace RiskAI.Tests
{
    public sealed class PointerGestureTests
    {
        [Test] public void TapOrdersButDragAndCancelledFocusNeverOrder()
        {
            var gesture=new PointerGesture();
            gesture.Begin(10,20); Assert.IsFalse(gesture.Move(13,22)); Assert.IsTrue(gesture.Release());
            gesture.Begin(10,20); Assert.IsTrue(gesture.Move(30,20)); Assert.IsFalse(gesture.Release());
            gesture.Begin(10,20); gesture.Cancel(); Assert.IsFalse(gesture.Release());
            Assert.IsFalse(gesture.Move(100,100));
        }
    }
}
