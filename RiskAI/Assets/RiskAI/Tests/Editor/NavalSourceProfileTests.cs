using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class NavalSourceProfileTests
    {
        [Test]
        public void TransportCapacityMatchesAttachedSch3AbilityBinaryOverride()
        {
            // Exact 24-byte modification extracted from war3map.w3a@0x395.
            // n008 attaches Sch3 explicitly at war3map.w3u uabi@0x3588.
            string path = Path.Combine(Application.dataPath, "RiskAI/Tests/Editor/Fixtures/SaranSch3CargoModification.bytes");
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                Assert.That(Encoding.ASCII.GetString(reader.ReadBytes(4)), Is.EqualTo("Car1"));
                Assert.That(reader.ReadInt32(), Is.Zero, "W3A integer value type");
                Assert.That(reader.ReadInt32(), Is.EqualTo(1), "Ability level");
                Assert.That(reader.ReadInt32(), Is.EqualTo(1), "Ability data pointer");
                int capacity = reader.ReadInt32();
                Assert.That(Encoding.ASCII.GetString(reader.ReadBytes(4)), Is.EqualTo("Sch3"));
                Assert.That(reader.BaseStream.Position, Is.EqualTo(reader.BaseStream.Length));
                Assert.That(NavalProfiles.Transport.SourceRawId, Is.EqualTo("n008"));
                Assert.That(NavalProfiles.Transport.Capacity, Is.EqualTo(capacity));
                Assert.That(NavalProfiles.Transport.CanCapture, Is.False);
                Assert.That(NavalProfiles.Transport.Damage, Is.Zero);
            }
        }

        // Explicit W3U values, not inherited Warcraft defaults.
        [TestCase(NavalUnitKind.Frigate, "h00W", true, 400f, 5, 5)]
        [TestCase(NavalUnitKind.Transport, "n008", false, 300f, 2, 2)]
        public void PublicKindsResolveToTheirSourceIdentityAndExplicitEconomy(
            NavalUnitKind kind, string rawId, bool canCapture, float health, int gold, int points)
        {
            ShipProfile profile = NavalProfiles.Profile(kind);
            Assert.That(profile.SourceRawId, Is.EqualTo(rawId));
            Assert.That(profile.CanCapture, Is.EqualTo(canCapture));
            Assert.That(profile.TrainSeconds, Is.EqualTo(1f), "Both W3U ubld overrides are one second.");
            Assert.That(profile.Health, Is.EqualTo(health));
            Assert.That(profile.Cost, Is.EqualTo(gold));
            Assert.That(profile.PointValue, Is.EqualTo(points));
            Assert.That(profile.Speed, Is.EqualTo(340f / 50f));
        }

        [Test]
        public void FrigateIsH00WAndDoesNotSilentlySelectClassicH00Q()
        {
            Assert.That((int)NavalUnitKind.Frigate, Is.Zero);
            Assert.That((int)NavalUnitKind.Transport, Is.EqualTo(1));
            Assert.That(NavalProfiles.Frigate.SourceRawId, Is.EqualTo("h00W"));
            Assert.That(NavalProfiles.Frigate.Armor, Is.EqualTo(6f), "Classic h00Q overrides armor to four; it is a different unit.");
            Assert.That(NavalProfiles.Frigate.BaseDamage, Is.EqualTo(30f));
            Assert.That(NavalProfiles.Frigate.Range, Is.EqualTo(1000f / 50f));
            Assert.That(NavalProfiles.Frigate.CanAttack,Is.True);
            Assert.That(NavalProfiles.Frigate.CanCapture,Is.True);
            Assert.That(NavalProfiles.Frigate.CanTransport,Is.False);
            Assert.That(NavalProfiles.Transport.CanAttack,Is.False);
            Assert.That(NavalProfiles.Transport.CanTransport,Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => NavalProfiles.Profile((NavalUnitKind)999));
        }

        // h00U, h001 and n007 explicit W3U overrides (hp, gold, points, armor, speed, damage, cooldown).
        [TestCase(NavalUnitKind.Warship, "h00U", 1250f, 20, 10f, 450f, 90f, 1.5f)]
        [TestCase(NavalUnitKind.Battleship, "h001", 2350f, 45, 20f, 330f, 130f, 1.4f)]
        [TestCase(NavalUnitKind.ArmoredTransport, "n007", 300f, 6, 30f, 370f, 0f, 0f)]
        public void V030HullsUseTheirExplicitSourceOverrides(NavalUnitKind kind, string rawId, float health, int gold,
            float armor, float nativeSpeed, float baseDamage, float cooldown)
        {
            var profile = NavalProfiles.Profile(kind);
            Assert.That(profile.SourceRawId, Is.EqualTo(rawId));
            Assert.That(profile.Health, Is.EqualTo(health));
            Assert.That(profile.Cost, Is.EqualTo(gold));
            Assert.That(profile.PointValue, Is.EqualTo(gold), "upoi equals ugol for every v0.30 hull.");
            Assert.That(profile.Armor, Is.EqualTo(armor));
            Assert.That(profile.Speed, Is.EqualTo(nativeSpeed / 50f).Within(.0001f));
            Assert.That(profile.BaseDamage, Is.EqualTo(baseDamage));
            Assert.That(profile.Cooldown, Is.EqualTo(cooldown).Within(.0001f));
            Assert.That(profile.TrainSeconds, Is.EqualTo(1f));
            if (profile.CanAttack)
            {
                Assert.That(profile.Range, Is.EqualTo(1500f / 50f));
                Assert.That(profile.CanCapture, Is.True);
                var weapon = SourceWeapons.For(kind, profile.Attack);
                Assert.That(weapon.Delivery, Is.EqualTo(WeaponDelivery.MissileSplash));
                Assert.That(weapon.ProjectileSpeed, Is.EqualTo(1000f / 50f));
                Assert.That(weapon.SmallDamageRadius, Is.EqualTo(1f));
            }
            else
            {
                Assert.That(profile.Capacity, Is.EqualTo(NavalProfiles.Transport.Capacity), "n007 attaches the same Sch3 cargo ability as n008.");
                Assert.That(profile.CanTransport, Is.True);
                Assert.That(profile.CanCapture, Is.False);
            }
        }

        [Test]
        public void V030HullOrdinalsAreAppended()
        {
            Assert.That((int)NavalUnitKind.Warship, Is.EqualTo(2));
            Assert.That((int)NavalUnitKind.Battleship, Is.EqualTo(3));
            Assert.That((int)NavalUnitKind.ArmoredTransport, Is.EqualTo(4));
        }
    }
}
