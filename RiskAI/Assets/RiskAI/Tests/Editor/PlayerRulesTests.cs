using System.Linq;
using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class PlayerRulesTests
    {
        [Test]
        public void SixteenPlayerEconomyStartsAndPaysEveryAccount()
        {
            var economy=new Economy(PlayerRules.MaxPlayers);
            Assert.That(economy.Gold.Length,Is.EqualTo(PlayerRules.MaxPlayers));
            Assert.That(economy.Gold.All(gold=>gold==BattleRules.StartingGold),Is.True);
            Assert.That(economy.Grant(15,3),Is.True);
            Assert.That(economy.Grant(16,1),Is.False);
            economy.Advance(BattleRules.RoundSeconds);
            Assert.That(economy.Gold[15],Is.EqualTo(BattleRules.StartingGold+3+BattleRules.BaseIncome));
            Assert.Throws<System.ArgumentOutOfRangeException>(()=>new Economy(PlayerRules.MaxPlayers+1));
        }

        [Test]
        public void IndividualAllocationGivesEveryOneOfSixteenPlayersACity()
        {
            var countries=Enumerable.Range(0,48).Select(index=>index/3).ToArray();
            var plan=StartingAllocation.Generate(4815,countries,PlayerRules.MaxPlayers,StartingAllocationMode.IndividualCities);
            Assert.That(plan.PlayerCount,Is.EqualTo(PlayerRules.MaxPlayers));
            for(int player=0;player<PlayerRules.MaxPlayers;player++)Assert.That(plan.CountCitiesForPlayer(player),Is.EqualTo(3));
        }

        [Test]
        public void NeutralOwnershipMapsToDedicatedCombatTeam()
        {
            Assert.That(PlayerRules.NeutralOwner,Is.EqualTo(-1));
            Assert.That(PlayerRules.ToCombatTeam(PlayerRules.NeutralOwner),Is.EqualTo(PlayerRules.NeutralTeam));
            Assert.That(PlayerRules.ToCombatTeam(15),Is.EqualTo(15));
            Assert.That(PlayerRules.ToCombatTeam(PlayerRules.NeutralTeam),Is.EqualTo(PlayerRules.NeutralTeam));
            Assert.That(PlayerRules.ToCombatTeam(99),Is.EqualTo(PlayerRules.NeutralTeam));
            Assert.That(ClaimRules.IsValidOwner(PlayerRules.NeutralOwner),Is.True);
            Assert.That(ClaimRules.IsValidOwner(15),Is.True);
            Assert.That(ClaimRules.IsValidOwner(PlayerRules.NeutralTeam),Is.False);
        }
    }
}
