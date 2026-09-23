using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class EconomyCountryTests
    {
        [Test]
        public void BreakdownMatchesActualRoundPaymentAndDropsLostCountries()
        {
            var economy=new Economy();
            var lost=new TownState("complete",0,0,2);
            economy.Towns.Add(lost);
            economy.Towns.Add(new TownState("incomplete",0,0,3));
            economy.Towns.Add(new TownState("enemy",1,0,3));
            var countries=new System.Collections.Generic.Dictionary<int,int>();
            int total=economy.IncomeBreakdown(0,countries,out int basic);
            Assert.That(basic,Is.EqualTo(4));Assert.That(countries[2],Is.EqualTo(1));Assert.That(countries.ContainsKey(3),Is.False);
            int before=economy.Gold[0];economy.Advance(60);
            Assert.That(economy.Gold[0]-before,Is.EqualTo(total));
            lost.Owner=1;
            Assert.That(economy.IncomeBreakdown(0,countries,out basic),Is.EqualTo(4));
            Assert.That(countries,Is.Empty,"Reusing a breakdown must remove a country that was lost.");
        }

        [Test]
        public void FragmentedCountryPaysOnlyBasicIncomeUntilCompleted()
        {
            var economy = new Economy();
            var first = new TownState("a", 0, 0, 3);
            var second = new TownState("b", 1, 0, 3);
            economy.Towns.Add(first); economy.Towns.Add(second);
            Assert.That(economy.CountryOwner(3), Is.EqualTo(-1));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome));
            second.Owner = 0;
            Assert.That(economy.CountryOwner(3), Is.EqualTo(0));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome + BattleRules.TownIncome * 2));
            second.Owner = 1;
            Assert.That(economy.CountryOwner(3), Is.EqualTo(-1));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome));
        }

        [Test]
        public void MixedAndCountrylessTownsRemainDistinguishable()
        {
            var economy = new Economy();
            economy.Towns.Add(new TownState("countryless", 0, 0));
            economy.Towns.Add(new TownState("mixed-a", 0, 0, 4));
            economy.Towns.Add(new TownState("mixed-b", -1, 0, 4));
            Assert.That(economy.CountryOwner(4), Is.EqualTo(-1));
            Assert.That(economy.Income(0), Is.EqualTo(BattleRules.BaseIncome));
        }

        [Test]
        public void ThreeScatteredCitiesPayFourWhileACompleteThreeCityCountryPaysSeven()
        {
            var economy=new Economy();
            for(int country=0;country<3;country++)
            {
                economy.Towns.Add(new TownState("owned-"+country,0,0,country));
                economy.Towns.Add(new TownState("neutral-"+country,-1,0,country));
            }
            Assert.That(economy.Income(0),Is.EqualTo(4));
            economy.Advance(60);
            Assert.That(economy.Gold[0],Is.EqualTo(8));
            economy.Towns.Clear();
            for(int city=0;city<3;city++)economy.Towns.Add(new TownState("complete-"+city,0,0,0));
            Assert.That(economy.Income(0),Is.EqualTo(7));
            economy.Advance(60);
            Assert.That(economy.Gold[0],Is.EqualTo(15));
        }

        [Test]
        public void CountryQueriesReflectMutableOwnershipAndTopology()
        {
            var economy = new Economy();
            var first = new TownState("a", 0, 0, 5);
            var second = new TownState("b", 0, 0, 5);
            economy.Towns.Add(first);
            economy.Towns.Add(second);

            Assert.That(economy.CountryOwner(5), Is.EqualTo(0));
            second.Owner = 1;
            Assert.That(economy.CountryOwner(5), Is.EqualTo(-1));

            economy.Towns.Remove(second);
            Assert.That(economy.CountryOwner(5), Is.EqualTo(0));
            first.Country = 7;
            Assert.That(economy.CountryOwner(5), Is.EqualTo(-1));
            Assert.That(economy.CountryOwner(7), Is.EqualTo(0));
            economy.Towns.Add(new TownState("c", 1, 0, 6));
            Assert.That(economy.CountryOwner(6), Is.EqualTo(1));
        }

        [Test]
        public void SourceCountryReinforcementUsesCeilingHalfCityCreditAndFivePointCap()
        {
            Assert.That(BattleRules.CountryReinforcementPointCapPerCity, Is.EqualTo(5));
            Assert.That(BattleRules.CountryReinforcementStepSeconds, Is.EqualTo(.5f));
            Assert.That(BattleRules.CountryReinforcementPointsPerRound(0), Is.Zero);
            Assert.That(BattleRules.CountryReinforcementPointsPerRound(1), Is.EqualTo(1));
            Assert.That(BattleRules.CountryReinforcementPointsPerRound(2), Is.EqualTo(1));
            Assert.That(BattleRules.CountryReinforcementPointsPerRound(3), Is.EqualTo(2));
        }

        [Test]
        public void GrantAndRefundValidateTeamsAndAmounts()
        {
            var economy = new Economy();

            Assert.That(economy.Grant(0, 30), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 30));
            Assert.That(economy.Refund(0, 10), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 40));
            Assert.That(economy.Grant(2, 1), Is.False);
            Assert.That(economy.Refund(-1, 1), Is.False);
            Assert.That(economy.Grant(0, -1), Is.False);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 40));
        }

        [Test]
        public void BountyRetainsQuarterPointFractionsUntilWholeGold()
        {
            var economy = new Economy();

            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold));
            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold));
            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 1));
            Assert.That(economy.GrantBounty(0, 3), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 1));
            Assert.That(economy.GrantBounty(0, 1), Is.True);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 2));
            Assert.That(economy.GrantBounty(2, 1), Is.False);
            Assert.That(economy.GrantBounty(0, -1), Is.False);
        }

        [Test]
        public void BountyUsesPointValueIndependentlyOfPurchasePrice()
        {
            // An independent price fixture: point value 2, purchase price 12.
            const int pointValue = 2, price = 12;
            var economy = new Economy();
            economy.GrantBounty(0, pointValue);
            economy.GrantBounty(0, pointValue);
            Assert.That(economy.Gold[0], Is.EqualTo(BattleRules.StartingGold + 1));
            Assert.That(price, Is.EqualTo(12));
        }

        [Test]
        public void SourceGoldCostsAndNavalProfilesAreNotScaledByPrototypeMultiplier()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Footman).Cost, Is.EqualTo(1));
            Assert.That(UnitCatalog.Get(UnitKind.Archer).Cost, Is.EqualTo(1));
            Assert.That(UnitCatalog.Get(UnitKind.Knight).Cost, Is.EqualTo(5));
            Assert.That(UnitCatalog.Get(UnitKind.Mage).Cost, Is.EqualTo(4));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Cost, Is.EqualTo(3));
            Assert.That(UnitCatalog.Get(UnitKind.Medic).Cost, Is.EqualTo(2));
            Assert.That(UnitCatalog.Tower.MaxHealth, Is.EqualTo(550));
            Assert.That(UnitCatalog.Tower.TownWeapon.Base, Is.EqualTo(45));
            Assert.That(UnitCatalog.Tower.TownWeapon.Dice, Is.EqualTo(1));
            Assert.That(UnitCatalog.Tower.TownWeapon.Sides, Is.EqualTo(5));
            Assert.That(UnitCatalog.Tower.TownWeapon.Range, Is.EqualTo(13f));
            Assert.That(UnitCatalog.Tower.TownWeapon.Cooldown, Is.EqualTo(.9f));
            Assert.That(UnitCatalog.Get(NavalUnitKind.Frigate).MaxHealth, Is.EqualTo(400));
            Assert.That(UnitCatalog.Get(NavalUnitKind.Frigate).Weapon.MinimumDamage, Is.EqualTo(31));
            Assert.That(UnitCatalog.Get(NavalUnitKind.Frigate).Weapon.MaximumDamage, Is.EqualTo(45));
            Assert.That(UnitCatalog.Get(NavalUnitKind.Frigate).Weapon.Cooldown, Is.EqualTo(1.5f));
            var first=new System.Random(16016);var replay=new System.Random(16016);
            for(int i=0;i<64;i++)
            {
                float damage=UnitCatalog.Get(NavalUnitKind.Frigate).Weapon.RollDamage(first);
                Assert.That(damage,Is.InRange(31f,45f));
                Assert.That(damage,Is.EqualTo(UnitCatalog.Get(NavalUnitKind.Frigate).Weapon.RollDamage(replay)));
            }
            Assert.That(UnitCatalog.Get(NavalUnitKind.Transport).Cost, Is.EqualTo(2));
            Assert.That(UnitCatalog.Get(NavalUnitKind.Transport).Speed, Is.EqualTo(6.8f));
            Assert.That(UnitCatalog.Get(NavalUnitKind.Transport).Armor, Is.Zero);
        }
    }
}
