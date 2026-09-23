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
                if(UnitCatalog.Get(kind).Domain!=UnitDomain.Land)continue;
                var profile=UnitCatalog.Get(kind);
                float contact=UnitCatalog.Get(kind).AttackContact;
                float recovery=AttackPresentationTiming.RecoverySeconds(
                    profile.Weapon.AttackPoint,profile.Weapon.Backswing,profile.Weapon.Cooldown);
                Assert.That(AttackPresentationTiming.NormalizedTime(profile.Weapon.AttackPoint,profile.Weapon.AttackPoint,recovery,contact),
                    Is.EqualTo(contact).Within(.0001f),kind.ToString());
                Assert.That(AttackPresentationTiming.ContactPose(
                    AttackPresentationTiming.NormalizedTime(profile.Weapon.AttackPoint,profile.Weapon.AttackPoint,recovery,contact),contact),
                    Is.EqualTo(1).Within(.0001f),kind+" must show its contact pose on the damage frame.");
                Assert.That(profile.Weapon.AttackPoint+recovery,Is.LessThanOrEqualTo(profile.Weapon.Cooldown+.0001f),
                    kind+" presentation must finish before its next attack can start.");
            }
        }

        [Test]
        public void SourceBackswingControlsRecoveryWithoutChangingCooldown()
        {
            var rifleman=UnitCatalog.Get(UnitKind.Archer);
            float recovery=AttackPresentationTiming.RecoverySeconds(
                rifleman.Weapon.AttackPoint,rifleman.Weapon.Backswing,rifleman.Weapon.Cooldown);
            Assert.That(recovery,Is.EqualTo(.7f));
            Assert.That(AttackPresentationTiming.NormalizedTime(
                rifleman.Weapon.AttackPoint+recovery,rifleman.Weapon.AttackPoint,recovery,
                UnitCatalog.Get(UnitKind.Archer).AttackContact),Is.EqualTo(1));

            var localSwordsman=UnitCatalog.Get(UnitKind.Footman);
            Assert.That(AttackPresentationTiming.Duration(localSwordsman.Weapon.AttackPoint,
                localSwordsman.Weapon.Backswing,localSwordsman.Weapon.Cooldown),Is.EqualTo(.45f).Within(.0001f));
        }

        [Test]
        public void ImportedClipsUseTheirMeasuredContactKeyframes()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Footman).AttackClip,Is.EqualTo("1H_Melee_Attack_Slice_Horizontal"));
            Assert.That(UnitCatalog.Get(UnitKind.Footman).AttackContact,Is.EqualTo(8f/32f));
            Assert.That(UnitCatalog.Get(UnitKind.Archer).AttackContact,Is.EqualTo(8f/32f));
            Assert.That(UnitCatalog.Get(UnitKind.MarinePrivate).AttackClip,Is.EqualTo("1H_Ranged_Shoot"));
            Assert.That(UnitCatalog.Get(UnitKind.MarinePrivate).Model,Is.EqualTo("MarinePrivate"));
            Assert.That(UnitCatalog.Get(UnitKind.MarinePrivate).Model,Is.Not.EqualTo(UnitCatalog.Get(UnitKind.Archer).Model));
            Assert.That(UnitCatalog.Get(UnitKind.Mage).AttackContact,Is.EqualTo(9f/28f));
            // Mounted marines use the procedural lance curve (contact halfway), like the Caballero.
            Assert.That(UnitCatalog.Get(UnitKind.MarineMajor).AttackClip,Is.Null);
            Assert.That(UnitCatalog.Get(UnitKind.MarineMajor).AttackContact,Is.EqualTo(.5f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).AttackClip,Is.Null);
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).AttackContact,Is.EqualTo(.5f));
        }
    }
}
