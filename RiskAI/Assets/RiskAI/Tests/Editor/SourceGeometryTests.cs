using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class SourceGeometryTests
    {
        [Test]
        public void VerifiedW3uAndSlkCollisionSizesUseSharedNativeConversion()
        {
            Assert.That(SourceGeometry.NativePerUnity, Is.EqualTo(50f));
            Assert.That(SourceGeometry.AgentRadius(UnitKind.Archer), Is.EqualTo(.32f));
            Assert.That(SourceGeometry.AgentRadius(UnitKind.Medic), Is.EqualTo(.32f));
            Assert.That(SourceGeometry.AgentRadius(UnitKind.Guard), Is.EqualTo(.64f));
            Assert.That(SourceGeometry.AgentRadius(UnitKind.Mortar), Is.EqualTo(.64f));
        }

        [Test]
        public void LocallyAdaptedKindsDoNotClaimUnverifiedSourceCollisionSizes()
        {
            Assert.That(SourceGeometry.AgentRadius(UnitKind.Footman), Is.EqualTo(.24f));
            Assert.That(SourceGeometry.AgentRadius(UnitKind.Mage), Is.EqualTo(.24f));
        }
    }
}
