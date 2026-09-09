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
        [TestCase(NavalUnitKind.Galley, "h00W", false, 400f, 5, 5)]
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
        public void GalleyAliasRemainsH00WAndDoesNotSilentlySelectClassicH00Q()
        {
            Assert.That((int)NavalUnitKind.Galley, Is.Zero);
            Assert.That((int)NavalUnitKind.Transport, Is.EqualTo(1));
            Assert.That(NavalProfiles.Frigate.SourceRawId, Is.EqualTo("h00W"));
            Assert.That(NavalProfiles.Galley.Armor, Is.EqualTo(6f), "Classic h00Q overrides armor to four; it is a different unit.");
            Assert.That(NavalProfiles.Galley.BaseDamage, Is.EqualTo(30f));
            Assert.That(NavalProfiles.Galley.Range, Is.EqualTo(1000f / 50f));
            Assert.Throws<ArgumentOutOfRangeException>(() => NavalProfiles.Profile((NavalUnitKind)999));
        }
    }
}
