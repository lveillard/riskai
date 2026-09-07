using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
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

        [Test]
        public void DirectPointerDefersSingleTapUntilDoubleWindowExpires()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(4,new PointerPoint(10,20),1);gesture.End(4,new PointerPoint(10,20),1.01f);
            gesture.Drain(actions.Add);Assert.That(actions,Is.Empty);
            gesture.Advance(1.249f);gesture.Drain(actions.Add);Assert.That(actions,Is.Empty);
            gesture.Advance(1.25f);gesture.Drain(actions.Add);
            Assert.That(actions.Select(action=>action.Kind),Is.EqualTo(new[]{DirectPointerActionKind.PrimaryTap}));
        }

        [Test]
        public void DirectPointerDoubleTapBecomesOneContextWithoutPrimaryTap()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,20),1);gesture.End(1,new PointerPoint(10,20),1.02f);
            gesture.Begin(2,new PointerPoint(12,21),1.10f);gesture.End(2,new PointerPoint(12,21),1.12f);gesture.Advance(2);
            gesture.Drain(actions.Add);
            Assert.That(actions.Select(action=>action.Kind),Is.EqualTo(new[]{DirectPointerActionKind.Context}));
        }

        [Test]
        public void DirectPointerDragCreatesAreaAndNeverTap()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(0,0),1);gesture.Move(1,new PointerPoint(20,0));gesture.End(1,new PointerPoint(30,0),1.1f);gesture.Advance(2);
            gesture.Drain(actions.Add);
            Assert.That(actions.First().Kind,Is.EqualTo(DirectPointerActionKind.AreaBegin));
            Assert.That(actions.Last().Kind,Is.EqualTo(DirectPointerActionKind.AreaEnd));
            Assert.That(actions.Any(action=>action.Kind==DirectPointerActionKind.PrimaryTap),Is.False);
        }

        [Test]
        public void TwoFingerTapIsOneContextAndLiftDoesNotBecomePrimary()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,10),1);gesture.Begin(2,new PointerPoint(30,10),1.01f);
            gesture.End(1,new PointerPoint(10,10),1.02f);gesture.Move(2,new PointerPoint(50,10));gesture.End(2,new PointerPoint(50,10),1.03f);gesture.Advance(2);
            gesture.Drain(actions.Add);
            Assert.That(actions.Any(action=>action.Kind==DirectPointerActionKind.PrimaryTap),Is.False);
            Assert.That(actions.Count(action=>action.Kind==DirectPointerActionKind.Context),Is.EqualTo(1));
        }

        [Test]
        public void ThirdFingerCancelsGestureWithoutPrimaryOrContext()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,10),1);gesture.Begin(2,new PointerPoint(30,10),1.01f);gesture.Begin(3,new PointerPoint(50,10),1.02f);
            gesture.Advance(2);gesture.Drain(actions.Add);
            Assert.That(actions,Is.Empty);Assert.That(gesture.Active,Is.True,"A third active contact must block the recognizer until all contacts lift.");
        }

        [Test]
        public void TwoFingerMoveProducesPanAndPinchButNoContext()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,10),1);gesture.Begin(2,new PointerPoint(30,10),1.01f);gesture.Move(2,new PointerPoint(50,10));
            gesture.End(1,new PointerPoint(10,10),1.03f);gesture.End(2,new PointerPoint(50,10),1.04f);gesture.Drain(actions.Add);
            Assert.That(actions.Any(action=>action.Kind==DirectPointerActionKind.Pan),Is.True);
            Assert.That(actions.Any(action=>action.Kind==DirectPointerActionKind.Pinch),Is.True);
            Assert.That(actions.Any(action=>action.Kind==DirectPointerActionKind.Context||action.Kind==DirectPointerActionKind.PrimaryTap),Is.False);
        }

        [Test]
        public void SecondFingerCancelsAnActiveAreaSelection()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,10),1);gesture.Move(1,new PointerPoint(30,10));gesture.Drain(actions.Add);
            gesture.Begin(2,new PointerPoint(40,10),1.1f);gesture.Drain(actions.Add);
            Assert.That(actions.Select(action=>action.Kind),Is.EqualTo(new[]{DirectPointerActionKind.AreaBegin,DirectPointerActionKind.AreaUpdate,DirectPointerActionKind.AreaCancel}));
        }

        [Test]
        public void AreaDragEmitsUpdatesForEveryFurtherMove()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(0,0),1);gesture.Move(1,new PointerPoint(20,0));gesture.Move(1,new PointerPoint(30,0));gesture.End(1,new PointerPoint(40,0),1.1f);gesture.Drain(actions.Add);
            Assert.That(actions.Count(action=>action.Kind==DirectPointerActionKind.AreaUpdate),Is.EqualTo(3));
            Assert.That(actions.Last(action=>action.Kind==DirectPointerActionKind.AreaUpdate).Position.X,Is.EqualTo(40));
        }

        [Test]
        public void HeldSecondTapDoesNotCommitFirstTapOrBecomeContext()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,10),1);gesture.End(1,new PointerPoint(10,10),1.01f);
            gesture.Begin(2,new PointerPoint(11,10),1.1f);gesture.Advance(1.5f);gesture.Drain(actions.Add);
            Assert.That(actions,Is.Empty,"A second candidate held past the double window cannot select or clear anything.");
            gesture.End(2,new PointerPoint(11,10),1.5f);gesture.Advance(1.75f);gesture.Drain(actions.Add);
            Assert.That(actions.Select(action=>action.Kind),Is.EqualTo(new[]{DirectPointerActionKind.PrimaryTap}));
        }

        [Test]
        public void SmallTwoFingerJitterRemainsAContextTap()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,10),1);gesture.Begin(2,new PointerPoint(30,10),1.01f);gesture.Move(2,new PointerPoint(31,10));
            gesture.End(1,new PointerPoint(10,10),1.02f);gesture.End(2,new PointerPoint(31,10),1.03f);gesture.Drain(actions.Add);
            Assert.That(actions.Select(action=>action.Kind),Is.EqualTo(new[]{DirectPointerActionKind.Context}));
        }

        [Test]
        public void CancellingOneFingerCancelsTheEntireTwoFingerGesture()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,10),1);gesture.Begin(2,new PointerPoint(30,10),1.01f);
            gesture.End(1,new PointerPoint(10,10),1.02f,true);gesture.End(2,new PointerPoint(30,10),1.03f);gesture.Drain(actions.Add);
            Assert.That(actions,Is.Empty);
        }

        [Test]
        public void ThirdFingerBlocksNewGesturesUntilEveryContactLifts()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,10),1);gesture.Begin(2,new PointerPoint(30,10),1.01f);gesture.Begin(3,new PointerPoint(50,10),1.02f);
            gesture.End(3,new PointerPoint(50,10),1.03f);gesture.Begin(4,new PointerPoint(70,10),1.04f);gesture.End(4,new PointerPoint(70,10),1.05f);
            gesture.End(2,new PointerPoint(30,10),1.06f);gesture.End(1,new PointerPoint(10,10),1.07f);
            Assert.That(gesture.Active,Is.False);
            gesture.Begin(5,new PointerPoint(20,20),2);gesture.End(5,new PointerPoint(20,20),2.01f);gesture.Advance(2.3f);gesture.Drain(actions.Add);
            Assert.That(actions.Select(action=>action.Kind),Is.EqualTo(new[]{DirectPointerActionKind.PrimaryTap}));
        }

        [Test]
        public void ReplacementFingerAfterPanStaysBlockedUntilOriginalPairReleases()
        {
            var gesture=new DirectPointerGesture();var actions=new List<DirectPointerAction>();
            gesture.Begin(1,new PointerPoint(10,10),1);gesture.Begin(2,new PointerPoint(30,10),1.01f);gesture.Move(2,new PointerPoint(50,10));
            gesture.End(1,new PointerPoint(10,10),1.02f);gesture.Begin(3,new PointerPoint(30,10),1.03f);gesture.End(3,new PointerPoint(30,10),1.04f);gesture.End(2,new PointerPoint(50,10),1.05f);gesture.Drain(actions.Add);
            Assert.That(actions.Any(action=>action.Kind==DirectPointerActionKind.Context),Is.False);
        }
    }
}