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
                UnitKind.EliteRifleman,UnitKind.Roarer,UnitKind.ArmyGeneral,UnitKind.Artillery,UnitKind.Tank},UnitCatalog.CityUnits);
            CollectionAssert.AreEqual(new[]{UnitKind.MarinePrivate,UnitKind.MarineMajor,UnitKind.MarineGeneral},UnitCatalog.HarborUnits);
            CollectionAssert.AreEqual(new[]{NavalUnitKind.Frigate,NavalUnitKind.Transport,NavalUnitKind.Warship,NavalUnitKind.Battleship,NavalUnitKind.ArmoredTransport},UnitCatalog.HarborShips);
            Assert.That((UnitCatalog.Get(UnitKind.Tank).Building==UnitBuilding.Harbor),Is.False);
            Assert.That(UnitCatalog.HarborShips.All(kind=>UnitCatalog.Get(kind).Building==UnitBuilding.Harbor),Is.True);
            Assert.That(UnitCatalog.CityUnits.All(kind=>UnitCatalog.Get(kind).Building==UnitBuilding.City),Is.True);
            Assert.That(UnitCatalog.HarborUnits.All(kind=>UnitCatalog.Get(kind).Building==UnitBuilding.Harbor),Is.True);
            Assert.That((UnitCatalog.Get(UnitKind.MarinePrivate).Building==UnitBuilding.City),Is.False);
            Assert.That((UnitCatalog.Get(UnitKind.Footman).Building==UnitBuilding.Harbor),Is.False);
        }

        [Test]
        public void EveryProductionHotkeyIsUniqueWithinItsBuilding()
        {
            var city=UnitCatalog.CityUnits.Select(ProductionHotkeys.Hotkey).ToList();
            CollectionAssert.AllItemsAreUnique(city);
            var harbor=UnitCatalog.HarborUnits.Select(ProductionHotkeys.Hotkey)
                .Concat(UnitCatalog.HarborShips.Select(ProductionHotkeys.Hotkey)).ToList();
            CollectionAssert.AllItemsAreUnique(harbor);
            Assert.That(city.Concat(harbor).All(key=>!string.IsNullOrEmpty(key)),Is.True);
            // WC3 grid hotkeys: every product key is a command-card cell. Unit order keys
            // (A/S/E...) are only shadowed while a building is selected.
            CollectionAssert.IsSubsetOf(city,ProductionHotkeys.GridKeys);
            CollectionAssert.IsSubsetOf(harbor,ProductionHotkeys.GridKeys);
        }
    }
}
