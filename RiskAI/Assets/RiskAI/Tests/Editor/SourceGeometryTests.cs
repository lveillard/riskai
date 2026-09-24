using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class SourceGeometryTests
    {
        [Test]
        public void VerifiedW3uAndSlkCollisionSizesUseSharedNativeConversion()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Archer).CollisionRadius, Is.EqualTo(.32f));
            Assert.That(UnitCatalog.Get(UnitKind.Medic).CollisionRadius, Is.EqualTo(.32f));
            Assert.That(UnitCatalog.Get(UnitKind.MarinePrivate).CollisionRadius, Is.EqualTo(.32f));
            Assert.That(UnitCatalog.Get(UnitKind.MarineMajor).CollisionRadius, Is.EqualTo(.64f));
            Assert.That(UnitCatalog.Get(UnitKind.MarineGeneral).CollisionRadius, Is.EqualTo(.64f));
            Assert.That(UnitCatalog.Get(UnitKind.Knight).CollisionRadius, Is.EqualTo(.64f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).CollisionRadius, Is.EqualTo(.64f));
            Assert.That(UnitCatalog.Get(UnitKind.EliteRifleman).CollisionRadius, Is.EqualTo(.32f));
            Assert.That(UnitCatalog.Get(UnitKind.Roarer).CollisionRadius, Is.EqualTo(.32f));
            Assert.That(UnitCatalog.Get(UnitKind.ArmyGeneral).CollisionRadius, Is.EqualTo(.72f));
            Assert.That(UnitCatalog.Get(UnitKind.Artillery).CollisionRadius, Is.EqualTo(.96f));
            Assert.That(UnitCatalog.Get(UnitKind.Tank).CollisionRadius, Is.EqualTo(.8f));
        }

        [Test]
        public void LocallyAdaptedKindsDoNotClaimUnverifiedSourceCollisionSizes()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Footman).CollisionRadius, Is.EqualTo(.24f));
            Assert.That(UnitCatalog.Get(UnitKind.Mage).CollisionRadius, Is.EqualTo(.24f));
        }
    }
}
