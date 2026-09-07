using System.Linq;
using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class ProductionCatalogTests
    {
        [Test]
        public void CatalogSeparatesRegularCityMarinePortAndShipProduction()
        {
            CollectionAssert.AreEqual(new[]{UnitKind.Footman,UnitKind.Archer,UnitKind.Guard,UnitKind.Mage,UnitKind.Mortar,UnitKind.Medic},ProductionCatalog.SettlementUnits);
            CollectionAssert.AreEqual(new[]{UnitKind.MarinePrivate,UnitKind.MarineMajor,UnitKind.MarineGeneral},ProductionCatalog.HarborUnits);
            CollectionAssert.AreEqual(new[]{NavalUnitKind.Galley,NavalUnitKind.Transport},ProductionCatalog.HarborShips);
            Assert.That(ProductionCatalog.SettlementUnits.All(ProductionCatalog.AllowsSettlementUnit),Is.True);
            Assert.That(ProductionCatalog.HarborUnits.All(ProductionCatalog.AllowsHarborUnit),Is.True);
            Assert.That(ProductionCatalog.AllowsSettlementUnit(UnitKind.MarinePrivate),Is.False);
            Assert.That(ProductionCatalog.AllowsHarborUnit(UnitKind.Footman),Is.False);
        }
    }
}
