using NUnit.Framework;

namespace RiskAI.Tests
{
    public sealed class TerritoryMarkersTests
    {
        [Test]
        public void InternalPostsHideWhenAdjacentCitiesShareAnOwner()
        {
            Assert.That(TerritoryMarkers.IsOwnershipBoundary(0,0),Is.False);
            Assert.That(TerritoryMarkers.IsOwnershipBoundary(14,14),Is.False);
            Assert.That(TerritoryMarkers.IsOwnershipBoundary(0,1),Is.True);
        }
    }
}
