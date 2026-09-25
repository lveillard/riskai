using System;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class AiPlanningTests
    {
        static AiArmyCensus Census(int frontline,int rangedCount,int splash,int healer)
        {
            var census=new AiArmyCensus();
            census.Add(AiUnitRole.Frontline,frontline);census.Add(AiUnitRole.Ranged,rangedCount);
            census.Add(AiUnitRole.Splash,splash);census.Add(AiUnitRole.Healer,healer);
            return census;
        }

        [Test]
        public void EveryDifficultyHasAProfileAndHarderLevelsDecideFasterWithLargerWaves()
        {
            var levels=(BattleSession.AiDifficulty[])Enum.GetValues(typeof(BattleSession.AiDifficulty));
            CollectionAssert.AreEqual(new[]{BattleSession.AiDifficulty.Relaxed,BattleSession.AiDifficulty.Standard,BattleSession.AiDifficulty.Hard},levels);
            foreach(var level in levels)Assert.That(AiDifficultyProfile.For((int)level).Level,Is.EqualTo((int)level));
            var relaxed=AiDifficultyProfile.Relaxed;var standard=AiDifficultyProfile.Standard;var hard=AiDifficultyProfile.Hard;
            Assert.That(relaxed.DecisionInterval,Is.EqualTo(12f),"Relaxed keeps its original cadence.");
            Assert.That(standard.DecisionInterval,Is.EqualTo(7f),"Standard keeps its original cadence.");
            Assert.That(hard.DecisionInterval,Is.LessThan(standard.DecisionInterval));
            Assert.That(hard.MicroInterval,Is.LessThan(standard.MicroInterval));
            Assert.That(standard.MicroInterval,Is.LessThan(relaxed.MicroInterval));
            Assert.That(hard.MaximumWave,Is.GreaterThan(standard.MaximumWave));
            Assert.That(standard.MaximumWave,Is.GreaterThan(relaxed.MaximumWave));
            Assert.That(hard.AttackMargin,Is.GreaterThan(standard.AttackMargin),"Hard waits for larger coordinated waves.");
            Assert.That(relaxed.PurchasesPerDecision,Is.EqualTo(1),"Relaxed openings spend one unit per decision.");
            Assert.That(AiDifficultyProfile.For(99).Level,Is.EqualTo(2));
            Assert.That(AiDifficultyProfile.For(-1).Level,Is.EqualTo(0));
        }

        [Test]
        public void DifficultyLaunchArgumentsAndLabelsCoverAllLevels()
        {
            Assert.That(BattleSession.TryParseDifficulty("hard",out var hard),Is.True);Assert.That(hard,Is.EqualTo(BattleSession.AiDifficulty.Hard));
            Assert.That(BattleSession.TryParseDifficulty("Difícil",out var dificil),Is.True);Assert.That(dificil,Is.EqualTo(BattleSession.AiDifficulty.Hard));
            Assert.That(BattleSession.TryParseDifficulty("1",out var standard),Is.True);Assert.That(standard,Is.EqualTo(BattleSession.AiDifficulty.Standard));
            Assert.That(BattleSession.TryParseDifficulty("relaxed",out var relaxed),Is.True);Assert.That(relaxed,Is.EqualTo(BattleSession.AiDifficulty.Relaxed));
            Assert.That(BattleSession.TryParseDifficulty("impossible",out _),Is.False);
            Assert.That(BattleSession.DifficultyLabel(BattleSession.AiDifficulty.Hard),Is.EqualTo("Difícil"));
            var args=LaunchArguments.FromUrl("https://localhost/?riskai-difficulty=hard&difficulty=relaxed");
            CollectionAssert.AreEqual(new[]{"--riskai-difficulty","hard"},args);
        }

        [Test]
        public void RolesAreDerivedFromCatalogDataIncludingFutureKinds()
        {
            Assert.That(AiUnitAnalysis.For(UnitKind.Footman).Role,Is.EqualTo(AiUnitRole.Frontline));
            Assert.That(AiUnitAnalysis.For(UnitKind.Archer).Role,Is.EqualTo(AiUnitRole.Ranged));
            Assert.That(AiUnitAnalysis.For(UnitKind.Mortar).Role,Is.EqualTo(AiUnitRole.Splash));
            Assert.That(AiUnitAnalysis.For(UnitKind.Mage).Role,Is.EqualTo(AiUnitRole.Splash));
            Assert.That(AiUnitAnalysis.For(UnitKind.Medic).Role,Is.EqualTo(AiUnitRole.Healer));
            Assert.That(AiUnitAnalysis.For(UnitKind.Knight).Value,Is.GreaterThan(AiUnitAnalysis.For(UnitKind.Footman).Value));
            // Every catalog entry, including kinds appended later, has usable traits.
            foreach(UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                if(UnitCatalog.Get(kind).Domain!=UnitDomain.Land)continue;
                var traits=AiUnitAnalysis.For(kind);
                Assert.That(traits.Cost,Is.GreaterThanOrEqualTo(1),kind.ToString());
                Assert.That(traits.Value,Is.GreaterThan(0),kind.ToString());
                Assert.That(traits.Ranged,Is.EqualTo(UnitCatalog.Get(kind).Weapon.Ranged));
            }
            var unknown=AiUnitAnalysis.For((UnitKind)999);
            Assert.That(unknown.Role,Is.EqualTo(AiUnitRole.Frontline));
            Assert.That(unknown.Cost,Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void OpeningBuysACheapCombatUnitAndNeverSaves()
        {
            var census=new AiArmyCensus();
            for(int step=0;step<3;step++)
            {
                var decision=AiCompositionPlanner.Choose(UnitCatalog.CityUnits,census,null,4-step,4,1,false,1,true);
                Assert.That(decision.Buy,Is.True);Assert.That(decision.Save,Is.False);
                var traits=AiUnitAnalysis.For(decision.Kind);
                Assert.That(traits.Cost,Is.EqualTo(1),"Four starting gold must turn into cheap mobile units, not one specialist.");
                Assert.That(traits.Role==AiUnitRole.Frontline||traits.Role==AiUnitRole.Ranged,Is.True);
                census.Add(decision.Kind);
            }
            Assert.That(census.Count(AiUnitRole.Frontline),Is.GreaterThan(0),"The opening mixes a frontline in.");
            Assert.That(census.Count(AiUnitRole.Ranged),Is.GreaterThan(0));
            var marine=AiCompositionPlanner.Choose(UnitCatalog.HarborUnits,new AiArmyCensus(),null,4,4,1,false,1,true);
            Assert.That(marine.Buy,Is.True);Assert.That((UnitCatalog.Get(marine.Kind).Building==UnitBuilding.Harbor),Is.True);
        }

        [Test]
        public void GrowingArmySavesForSupportUnlessThreatened()
        {
            // Balanced frontline/ranged core, no area damage yet: the next desired role is Splash.
            var census=Census(9,11,0,2);
            var saving=AiCompositionPlanner.Choose(UnitCatalog.CityUnits,census,null,1,12,1,false,1,true);
            Assert.That(saving.Save,Is.True,"An affordable-next-round specialist is worth saving for.");
            Assert.That(saving.Buy,Is.False);
            var urgent=AiCompositionPlanner.Choose(UnitCatalog.CityUnits,census,null,1,12,1,true,1,true);
            Assert.That(urgent.Buy,Is.True,"A threatened commander spends immediately.");
            Assert.That(AiUnitAnalysis.For(urgent.Kind).Cost,Is.LessThanOrEqualTo(1));
            var rich=AiCompositionPlanner.Choose(UnitCatalog.CityUnits,census,null,40,12,1,false,1,true);
            Assert.That(rich.Buy,Is.True);Assert.That(AiUnitAnalysis.For(rich.Kind).Role,Is.EqualTo(AiUnitRole.Splash));
            var noSaving=AiCompositionPlanner.Choose(UnitCatalog.CityUnits,census,null,1,0,1,false,1,true);
            Assert.That(noSaving.Save,Is.False,"Never save for a unit the next income round cannot pay for.");
        }

        [Test]
        public void LargeArmyWithoutHealersRecruitsAHealer()
        {
            var census=Census(9,9,3,0);
            var decision=AiCompositionPlanner.Choose(UnitCatalog.CityUnits,census,null,50,20,1,false,1,true);
            Assert.That(decision.Buy,Is.True);
            Assert.That(AiUnitAnalysis.For(decision.Kind).Healer,Is.True);
            Assert.That(AiCompositionPlanner.TargetShare(AiUnitRole.Healer,3,null),Is.Zero,"No healers in a tiny opening army.");
        }

        [Test]
        public void CounterPicksFollowTheDamageTable()
        {
            var heavy=new AiForceMix();heavy.Add(ArmorKind.Heavy,AttackKind.Normal,10);
            var light=new AiForceMix();light.Add(ArmorKind.Light,AttackKind.Piercing,10);
            float mageVsHeavy=AiCompositionPlanner.Score(UnitKind.Mage,20,heavy,1)/AiCompositionPlanner.Score(UnitKind.Mortar,20,heavy,1);
            float mageVsLight=AiCompositionPlanner.Score(UnitKind.Mage,20,light,1)/AiCompositionPlanner.Score(UnitKind.Mortar,20,light,1);
            Assert.That(mageVsHeavy,Is.GreaterThan(mageVsLight),"Magic (x2 vs Heavy) must gain on Siege against a heavy army.");
            var lightSwords=new AiForceMix();lightSwords.Add(ArmorKind.Light,AttackKind.Normal,10);
            float archerVsLight=AiCompositionPlanner.Score(UnitKind.Archer,20,lightSwords,1);
            float archerVsHeavy=AiCompositionPlanner.Score(UnitKind.Archer,20,heavy,1);
            Assert.That(archerVsLight,Is.GreaterThan(archerVsHeavy),"Piercing deals double damage to Light armor.");
            Assert.That(AiCompositionPlanner.Score(UnitKind.Archer,20,light,0),Is.EqualTo(AiCompositionPlanner.Score(UnitKind.Archer,20,heavy,0)),
                "A zero counter weight ignores the enemy mix.");
            var towerWeapon=UnitCatalog.Get(UnitKind.Tower).TownWeapon;
            Assert.That(AiUnitAnalysis.TowerValue(towerWeapon,200,light),Is.GreaterThan(AiUnitAnalysis.TowerValue(towerWeapon,200,heavy)),"Post towers punish Light armies.");
        }

        [Test]
        public void TargetValueRewardsCompletionDenialAndProximity()
        {
            float fresh=AiTargetScoring.CityValue(5,0,false,false);
            float progress=AiTargetScoring.CityValue(5,2,false,false);
            float completes=AiTargetScoring.CityValue(5,4,false,false);
            float denies=AiTargetScoring.CityValue(5,0,true,false);
            Assert.That(progress,Is.GreaterThan(fresh));
            Assert.That(completes,Is.GreaterThan(progress));
            Assert.That(denies,Is.GreaterThan(fresh),"Breaking an enemy's complete country removes its income.");
            Assert.That(AiTargetScoring.Score(2,10,1),Is.GreaterThan(AiTargetScoring.Score(2,90,1)));
            Assert.That(AiTargetScoring.Feasibility(100,0,1.2f),Is.GreaterThan(1),"Undefended posts are a bonus.");
            Assert.That(AiTargetScoring.Feasibility(50,200,1.2f),Is.LessThan(AiTargetScoring.Feasibility(150,200,1.2f)));
            Assert.That(AiTargetScoring.Feasibility(500,200,1.2f),Is.EqualTo(1));
        }

        [Test]
        public void WavesWaitForTheGroupThenLaunchOrAbort()
        {
            Assert.That(AiTargetScoring.Wave(300,200,6,8,5,30),Is.EqualTo(AiWaveDecision.Launch),"Enough power and most of the group assembled.");
            Assert.That(AiTargetScoring.Wave(300,200,2,8,5,30),Is.EqualTo(AiWaveDecision.Wait),"Wait for stragglers early on.");
            Assert.That(AiTargetScoring.Wave(100,200,6,6,5,30),Is.EqualTo(AiWaveDecision.Wait),"Too weak: wait for reinforcements.");
            Assert.That(AiTargetScoring.Wave(130,200,6,6,31,30),Is.EqualTo(AiWaveDecision.Launch),"Timeout with a reasonable force.");
            Assert.That(AiTargetScoring.Wave(50,200,2,6,61,30),Is.EqualTo(AiWaveDecision.Abort));
            Assert.That(AiTargetScoring.Wave(10,0,1,1,0,30),Is.EqualTo(AiWaveDecision.Launch),"Undefended objectives need no wave.");
            Assert.That(AiTargetScoring.ShouldRetreat(40,200,.4f),Is.True);
            Assert.That(AiTargetScoring.ShouldRetreat(40,200,0),Is.False,"Relaxed never retreats a wave.");
            Assert.That(AiTargetScoring.DefenseShortfall(50,200,1.2f,true),Is.EqualTo(1),"Contested posts always get a token defender.");
            Assert.That(AiTargetScoring.DefenseShortfall(50,200,1.2f,false),Is.LessThan(0));
            Assert.That(AiTargetScoring.DefenseShortfall(300,100,1f,false),Is.EqualTo(200));
        }
    }
}
