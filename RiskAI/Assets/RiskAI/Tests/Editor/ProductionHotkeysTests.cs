using System.Linq;
using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class ProductionHotkeysTests
    {
        static readonly string[] Grid={"Q","W","E","R","A","S","D","F","Z","X","C","V"};

        [TearDown]
        public void ResetLanguage() => ProbeHooks.SetLanguage(GameLanguage.English);

        [Test]
        public void GridKeysFollowWarcraftCommandCardRows()
        {
            CollectionAssert.AreEqual(Grid,ProductionHotkeys.GridKeys);
            Assert.That(ProductionHotkeys.Columns,Is.EqualTo(4));
            Assert.That(ProductionHotkeys.Rows,Is.EqualTo(3));
            for(int cell=0;cell<Grid.Length;cell++)
            {
                Assert.That(ProductionHotkeys.KeyForCell(cell),Is.EqualTo(Grid[cell]));
                Assert.That(ProductionHotkeys.CellForKey(Grid[cell].ToLowerInvariant()),Is.EqualTo(cell));
            }
            Assert.That(ProductionHotkeys.CellForKey("T"),Is.EqualTo(-1));
            Assert.That(ProductionHotkeys.KeyForCell(12),Is.Null);
        }

        [TestCase(ProductionBuilding.City)]
        [TestCase(ProductionBuilding.Harbor)]
        public void EveryProductOfABuildingGetsAUniqueGridCell(ProductionBuilding building)
        {
            var layout=ProductionHotkeys.Layout(building);
            int expected=building==ProductionBuilding.City
                ? UnitCatalog.CityUnits.Count
                : UnitCatalog.HarborUnits.Count+UnitCatalog.HarborShips.Count;
            Assert.That(layout.Count,Is.EqualTo(expected),"Every catalog product needs a cell.");
            CollectionAssert.AllItemsAreUnique(layout.Select(slot=>slot.Option));
            CollectionAssert.AllItemsAreUnique(layout.Select(slot=>(slot.Page,slot.Key)));
            for(int i=0;i<layout.Count;i++)
            {
                Assert.That(layout[i].Index,Is.EqualTo(i));
                Assert.That(Grid,Does.Contain(layout[i].Key));
                Assert.That(layout[i].Row,Is.InRange(0,2));
                Assert.That(layout[i].Column,Is.InRange(0,3));
                Assert.That(ProductionHotkeys.TryFind(building,layout[i].Page,layout[i].Cell,out var found),Is.True);
                Assert.That(found.Option,Is.EqualTo(layout[i].Option));
            }
            // Filled in reading order without gaps while one page suffices.
            if(ProductionHotkeys.PageCount(building)==1)
                CollectionAssert.AreEqual(Grid.Take(layout.Count),layout.Select(slot=>slot.Key));
        }

        [Test]
        public void CityCardIsOrderedCheapToExpensive()
        {
            var layout=ProductionHotkeys.Layout(ProductionBuilding.City);
            for(int i=1;i<layout.Count;i++)Assert.That(layout[i].Option.Cost,Is.GreaterThanOrEqualTo(layout[i-1].Option.Cost));
            Assert.That(ProductionHotkeys.Hotkey(UnitKind.Footman),Is.EqualTo("Q"));
            Assert.That(ProductionHotkeys.Hotkey(UnitKind.Archer),Is.EqualTo("W"),"Equal costs keep catalog order.");
            Assert.That(ProductionHotkeys.Hotkey(UnitKind.Tank),Is.EqualTo(Grid[layout.Count-1]),"The most expensive unit takes the last used cell.");
            Assert.That(layout.All(slot=>!slot.Option.SeaMotor),Is.True);
        }

        [Test]
        public void HarborCardListsLandUnitsBeforeShips()
        {
            var layout=ProductionHotkeys.Layout(ProductionBuilding.Harbor);
            int firstShip=layout.ToList().FindIndex(slot=>slot.Option.SeaMotor);
            Assert.That(firstShip,Is.EqualTo(UnitCatalog.HarborUnits.Count));
            Assert.That(layout.Skip(firstShip).All(slot=>slot.Option.SeaMotor),Is.True);
            for(int i=1;i<firstShip;i++)Assert.That(layout[i].Option.Cost,Is.GreaterThanOrEqualTo(layout[i-1].Option.Cost));
            for(int i=firstShip+1;i<layout.Count;i++)Assert.That(layout[i].Option.Cost,Is.GreaterThanOrEqualTo(layout[i-1].Option.Cost));
            Assert.That(ProductionHotkeys.Hotkey(UnitKind.MarinePrivate),Is.EqualTo("Q"));
            Assert.That(ProductionHotkeys.Hotkey(UnitKind.Transport),Is.EqualTo(Grid[firstShip]),"The cheapest hull opens the naval block.");
            foreach(var ship in UnitCatalog.HarborShips)Assert.That(ProductionHotkeys.Hotkey(ship),Is.Not.Null);
            Assert.That(ProductionHotkeys.Hotkey(UnitKind.Footman),Is.EqualTo("Q"),"Cards are independent: each building restarts at Q.");
        }

        [Test]
        public void NewProductsReceiveTheNextCellAndOverflowPages()
        {
            var options=UnitCatalog.CityUnits.Select(ProductionOption.For).ToList();
            var layout=ProductionHotkeys.Arrange(options);
            Assert.That(layout.All(slot=>slot.Page==0),Is.True);
            options.Add(ProductionOption.For(UnitKind.Frigate));
            layout=ProductionHotkeys.Arrange(options);
            Assert.That(layout[layout.Length-1].Key,Is.EqualTo(Grid[options.Count-1]),"An appended kind gets the next free cell.");
            Assert.That(ProductionHotkeys.PageCount(options.Count),Is.EqualTo(1));

            options.Add(ProductionOption.For(UnitKind.Transport));
            options.Add(ProductionOption.For(UnitKind.Warship));
            layout=ProductionHotkeys.Arrange(options);
            Assert.That(ProductionHotkeys.PageCount(options.Count),Is.EqualTo(2));
            Assert.That(layout.Count(slot=>slot.Page==0),Is.EqualTo(ProductionHotkeys.CellsPerPage-1),"V is kept for the page switch.");
            Assert.That(layout.Any(slot=>slot.Cell==ProductionHotkeys.PageCell),Is.False);
            Assert.That(ProductionHotkeys.PageKey,Is.EqualTo("V"));
            Assert.That(layout.Where(slot=>slot.Page==1).Select(slot=>slot.Key),Is.EqualTo(new[]{"Q","W","E"}));
        }

        [Test]
        public void IncomeCountdownReadsAsTimeToNextIncome()
        {
            Assert.That(IncomeCountdown.SecondsRemaining(2f,60f),Is.EqualTo(58));
            Assert.That(IncomeCountdown.SecondsRemaining(0f,60f),Is.EqualTo(60));
            Assert.That(IncomeCountdown.SecondsRemaining(59.5f,60f),Is.EqualTo(1));
            Assert.That(IncomeCountdown.SecondsRemaining(75f,60f),Is.EqualTo(0));
            Assert.That(IncomeCountdown.Progress(15f,60f),Is.EqualTo(.25f).Within(1e-5f));
            Assert.That(IncomeCountdown.Progress(-1f,60f),Is.EqualTo(0));
            Assert.That(IncomeCountdown.Progress(90f,60f),Is.EqualTo(1));

            ProbeHooks.SetLanguage(GameLanguage.Spanish);
            Assert.That(GameText.Localize(IncomeCountdown.Label(1,2f,false,60f)),Is.EqualTo("R1 · Ingreso en 58 s"));
            Assert.That(GameText.Localize(IncomeCountdown.Label(1,2f,true,60f)),Is.EqualTo("58 s"));
            ProbeHooks.SetLanguage(GameLanguage.English);
            Assert.That(GameText.Localize(IncomeCountdown.Label(3,2f,false,60f)),Is.EqualTo("R3 · Income in 58 s"));
            Assert.That(GameText.Localize(IncomeCountdown.Label(3,2f,false,60f)),Does.Not.Contain("ROUND"),"The countdown is not elapsed round time.");
            Assert.That(GameText.Localize(IncomeCountdown.Detail(3,2f,7,60f)),Is.EqualTo("Round 3 · next income +7 gold in 58 s"));
        }
    }

    public sealed class LanguageDetectionTests
    {
        [Test]
        public void StoredChoiceWinsOverSystemLanguage()
        {
            Assert.That(GameText.Initial(null,true),Is.EqualTo(GameLanguage.Spanish));
            Assert.That(GameText.Initial(null,false),Is.EqualTo(GameLanguage.English));
            Assert.That(GameText.Initial(GameLanguage.English,true),Is.EqualTo(GameLanguage.English));
            Assert.That(GameText.Initial(GameLanguage.Spanish,false),Is.EqualTo(GameLanguage.Spanish));
        }
    }
}
