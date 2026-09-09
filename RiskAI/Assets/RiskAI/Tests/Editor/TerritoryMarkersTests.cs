using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class TerritoryMarkersTests
    {
        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(3f)]
        public void StrategicDiamondStaysOnItsProjectedGroundPointAtEveryDensity(float density)
        {
            var hud=Matrix4x4.Scale(Vector3.one*density);
            foreach(var point in new[]{new Vector2(80,90),new Vector2(200,350),new Vector2(320,650)})
            {
                var symbol=BattleHud.StrategicSymbolMatrix(hud,point);
                var expected=hud.MultiplyPoint3x4(point);
                Assert.That(Vector3.Distance(symbol.MultiplyPoint3x4(point),expected),Is.LessThan(.001f),
                    "Zoom and camera rotation change the projected point; neither may separate the diamond from that point.");
                var corner=symbol.MultiplyPoint3x4(point+Vector2.right*5)-expected;
                Assert.That(corner.x,Is.EqualTo(5*density/Mathf.Sqrt(2)).Within(.001f));
                Assert.That(corner.y,Is.EqualTo(corner.x).Within(.001f));
            }
        }

        [Test]
        public void InternalPostsHideWhenAdjacentCitiesShareAnOwner()
        {
            Assert.That(TerritoryMarkers.IsOwnershipBoundary(0,0),Is.False);
            Assert.That(TerritoryMarkers.IsOwnershipBoundary(14,14),Is.False);
            Assert.That(TerritoryMarkers.IsOwnershipBoundary(0,1),Is.True);
        }
    }
}
