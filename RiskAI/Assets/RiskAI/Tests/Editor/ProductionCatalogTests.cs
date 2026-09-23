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
            CollectionAssert.AreEqual(new[]{UnitKind.Footman,UnitKind.Archer,UnitKind.Knight,UnitKind.Mage,UnitKind.Mortar,UnitKind.Medic,
                UnitKind.EliteRifleman,UnitKind.Roarer,UnitKind.ArmyGeneral,UnitKind.Artillery,UnitKind.Tank},ProductionCatalog.SettlementUnits);
            CollectionAssert.AreEqual(new[]{UnitKind.MarinePrivate,UnitKind.MarineMajor,UnitKind.MarineGeneral},ProductionCatalog.HarborUnits);
            CollectionAssert.AreEqual(new[]{NavalUnitKind.Frigate,NavalUnitKind.Transport,NavalUnitKind.Warship,NavalUnitKind.Battleship,NavalUnitKind.ArmoredTransport},ProductionCatalog.HarborShips);
            Assert.That(ProductionCatalog.AllowsHarborUnit(UnitKind.Tank),Is.False);
            Assert.That(ProductionCatalog.HarborShips.All(ProductionCatalog.AllowsHarborShip),Is.True);
            Assert.That(ProductionCatalog.SettlementUnits.All(ProductionCatalog.AllowsSettlementUnit),Is.True);
            Assert.That(ProductionCatalog.HarborUnits.All(ProductionCatalog.AllowsHarborUnit),Is.True);
            Assert.That(ProductionCatalog.AllowsSettlementUnit(UnitKind.MarinePrivate),Is.False);
            Assert.That(ProductionCatalog.AllowsHarborUnit(UnitKind.Footman),Is.False);
        }

        [Test]
        public void EveryProductionHotkeyIsUniqueWithinItsBuilding()
        {
            var city=ProductionCatalog.SettlementUnits.Select(ProductionHotkeys.Hotkey).ToList();
            CollectionAssert.AllItemsAreUnique(city);
            var harbor=ProductionCatalog.HarborUnits.Select(ProductionHotkeys.Hotkey)
                .Concat(ProductionCatalog.HarborShips.Select(ProductionHotkeys.Hotkey)).ToList();
            CollectionAssert.AllItemsAreUnique(harbor);
            Assert.That(city.Concat(harbor).All(key=>!string.IsNullOrEmpty(key)),Is.True);
            // WC3 grid hotkeys: every product key is a command-card cell. Unit order keys
            // (A/S/E...) are only shadowed while a building is selected.
            CollectionAssert.IsSubsetOf(city,ProductionHotkeys.GridKeys);
            CollectionAssert.IsSubsetOf(harbor,ProductionHotkeys.GridKeys);
        }
    }
}
