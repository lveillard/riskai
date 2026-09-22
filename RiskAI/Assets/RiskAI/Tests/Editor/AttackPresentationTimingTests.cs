using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class AttackPresentationTimingTests
    {
        [Test]
        public void EveryLandAttackSamplesItsContactPoseAtTheSimulationStrike()
        {
            foreach(UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                var profile=BattleRules.Profile(kind);
                float contact=AttackPresentationTiming.ContactNormalizedTime(kind);
                float recovery=AttackPresentationTiming.RecoverySeconds(
                    profile.AttackPoint,profile.Backswing,profile.Cooldown);
                Assert.That(AttackPresentationTiming.NormalizedTime(profile.AttackPoint,profile.AttackPoint,recovery,contact),
                    Is.EqualTo(contact).Within(.0001f),kind.ToString());
                Assert.That(AttackPresentationTiming.ContactPose(
                    AttackPresentationTiming.NormalizedTime(profile.AttackPoint,profile.AttackPoint,recovery,contact),contact),
                    Is.EqualTo(1).Within(.0001f),kind+" must show its contact pose on the damage frame.");
                Assert.That(profile.AttackPoint+recovery,Is.LessThanOrEqualTo(profile.Cooldown+.0001f),
                    kind+" presentation must finish before its next attack can start.");
            }
        }

        [Test]
        public void SourceBackswingControlsRecoveryWithoutChangingCooldown()
        {
            var rifleman=BattleRules.Profile(UnitKind.Archer);
            float recovery=AttackPresentationTiming.RecoverySeconds(
                rifleman.AttackPoint,rifleman.Backswing,rifleman.Cooldown);
            Assert.That(recovery,Is.EqualTo(.7f));
            Assert.That(AttackPresentationTiming.NormalizedTime(
                rifleman.AttackPoint+recovery,rifleman.AttackPoint,recovery,
                AttackPresentationTiming.ContactNormalizedTime(UnitKind.Archer)),Is.EqualTo(1));

            var localSwordsman=BattleRules.Profile(UnitKind.Footman);
            Assert.That(AttackPresentationTiming.Duration(localSwordsman.AttackPoint,
                localSwordsman.Backswing,localSwordsman.Cooldown),Is.EqualTo(.45f).Within(.0001f));
        }

        [Test]
        public void ImportedClipsUseTheirMeasuredContactKeyframes()
        {
            Assert.That(AttackPresentationTiming.Clip(UnitKind.Footman),Is.EqualTo("1H_Melee_Attack_Slice_Horizontal"));
            Assert.That(AttackPresentationTiming.ContactNormalizedTime(UnitKind.Footman),Is.EqualTo(8f/32f));
            Assert.That(AttackPresentationTiming.ContactNormalizedTime(UnitKind.Archer),Is.EqualTo(8f/32f));
            Assert.That(AttackPresentationTiming.Clip(UnitKind.MarinePrivate),Is.EqualTo("1H_Ranged_Shoot"));
            Assert.That(BattleRules.Model(UnitKind.MarinePrivate),Is.EqualTo("MarinePrivate"));
            Assert.That(BattleRules.Model(UnitKind.MarinePrivate),Is.Not.EqualTo(BattleRules.Model(UnitKind.Archer)));
            Assert.That(AttackPresentationTiming.ContactNormalizedTime(UnitKind.Mage),Is.EqualTo(9f/28f));
            Assert.That(AttackPresentationTiming.ContactNormalizedTime(UnitKind.MarineMajor),Is.EqualTo(13f/33f));
            Assert.That(AttackPresentationTiming.Clip(UnitKind.Mortar),Is.Null);
            Assert.That(AttackPresentationTiming.ContactNormalizedTime(UnitKind.Mortar),Is.EqualTo(.5f));
        }
    }
}
