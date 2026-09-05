using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class RulesTests
    {
        [Test] public void IncomeIncludesOnlyCompleteRegions()
        {
            var economy=new Economy();economy.Towns.Add(new TownState("a",0,0));economy.Towns.Add(new TownState("b",-1,0));
            Assert.That(economy.Income(0),Is.EqualTo(20));economy.Towns[1].Owner=0;
            Assert.That(economy.Income(0),Is.EqualTo(36));economy.Towns[1].Owner=1;
            Assert.That(economy.Income(0),Is.EqualTo(20));
        }
        [Test] public void IncomePaysAtRoundBoundaryAndHandlesLongFrames()
        {
            var economy=new Economy();Assert.That(economy.Advance(59.9f),Is.Zero);Assert.That(economy.Gold[0],Is.EqualTo(120));
            Assert.That(economy.Advance(.2f),Is.EqualTo(1));Assert.That(economy.Gold[0],Is.EqualTo(132));
            Assert.That(economy.Advance(120),Is.EqualTo(2));Assert.That(economy.Round,Is.EqualTo(4));
            Assert.That(economy.Gold[1],Is.EqualTo(156));
        }
        [Test] public void InvalidTimeAndPurchasesCannotCreateGold()
        {
            var economy=new Economy();economy.Advance(float.NaN);economy.Advance(-100);
            Assert.That(economy.Spend(0,-1),Is.False);Assert.That(economy.Spend(0,121),Is.False);Assert.That(economy.Spend(3,1),Is.False);
            Assert.That(economy.Spend(0,120),Is.True);Assert.That(economy.Gold[0],Is.Zero);Assert.That(economy.Round,Is.EqualTo(1));
        }
    }
}
