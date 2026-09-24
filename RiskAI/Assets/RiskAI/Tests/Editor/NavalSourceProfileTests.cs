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
                Assert.That(UnitCatalog.Get(UnitKind.Transport).SourceRawcode, Is.EqualTo("n008"));
                Assert.That(UnitCatalog.Get(UnitKind.Transport).Transport.Capacity, Is.EqualTo(capacity));
                Assert.That(UnitCatalog.Get(UnitKind.Transport).CanCapture, Is.False);
                Assert.That(UnitCatalog.Get(UnitKind.Transport).Weapon.AverageDamage, Is.Zero);
            }
        }

        // Explicit W3U values, not inherited Warcraft defaults.
        [TestCase(UnitKind.Frigate, "h00W", true, 400f, 5, 5)]
        [TestCase(UnitKind.Transport, "n008", false, 300f, 2, 2)]
        public void PublicKindsResolveToTheirSourceIdentityAndExplicitEconomy(
            UnitKind kind, string rawId, bool canCapture, float health, int gold, int points)
        {
            var profile = UnitCatalog.Get(kind);
            Assert.That(profile.SourceRawcode, Is.EqualTo(rawId));
            Assert.That(profile.CanCapture, Is.EqualTo(canCapture));
            Assert.That(profile.TrainSeconds, Is.EqualTo(1f), "Both W3U ubld overrides are one second.");
            Assert.That(profile.MaxHealth, Is.EqualTo(health));
            Assert.That(profile.Cost, Is.EqualTo(gold));
            Assert.That(profile.Points, Is.EqualTo(points));
            Assert.That(profile.Speed, Is.EqualTo(340f / 50f));
        }

        [Test]
        public void FrigateIsH00WAndDoesNotSilentlySelectClassicH00Q()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).Id, Is.EqualTo("Frigate"));
            Assert.That(UnitCatalog.Get(UnitKind.Transport).Id, Is.EqualTo("Transport"));
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).SourceRawcode, Is.EqualTo("h00W"));
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).Armor, Is.EqualTo(6f), "Classic h00Q overrides armor to four; it is a different unit.");
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).Weapon.Base, Is.EqualTo(30f));
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).Weapon.Range, Is.EqualTo(1000f / 50f));
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).CanAttack,Is.True);
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).CanCapture,Is.True);
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).CanTransport,Is.False);
            Assert.That(UnitCatalog.Get(UnitKind.Transport).CanAttack,Is.False);
            Assert.That(UnitCatalog.Get(UnitKind.Transport).CanTransport,Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitCatalog.Get((UnitKind)999));
        }

        // h00U, h001 and n007 explicit W3U overrides (hp, gold, points, armor, speed, damage, cooldown).
        [TestCase(UnitKind.Warship, "h00U", 1250f, 20, 10f, 450f, 90f, 1.5f)]
        [TestCase(UnitKind.Battleship, "h001", 2350f, 45, 20f, 330f, 130f, 1.4f)]
        [TestCase(UnitKind.ArmoredTransport, "n007", 300f, 6, 30f, 370f, 0f, 0f)]
        public void V030HullsUseTheirExplicitSourceOverrides(UnitKind kind, string rawId, float health, int gold,
            float armor, float nativeSpeed, float baseDamage, float cooldown)
        {
            var profile = UnitCatalog.Get(kind);
            Assert.That(profile.SourceRawcode, Is.EqualTo(rawId));
            Assert.That(profile.MaxHealth, Is.EqualTo(health));
            Assert.That(profile.Cost, Is.EqualTo(gold));
            Assert.That(profile.Points, Is.EqualTo(gold), "upoi equals ugol for every v0.30 hull.");
            Assert.That(profile.Armor, Is.EqualTo(armor));
            Assert.That(profile.Speed, Is.EqualTo(nativeSpeed / 50f).Within(.0001f));
            Assert.That(profile.Weapon.Base, Is.EqualTo(baseDamage));
            Assert.That(profile.Weapon.Cooldown, Is.EqualTo(cooldown).Within(.0001f));
            Assert.That(profile.TrainSeconds, Is.EqualTo(1f));
            if (profile.CanAttack)
            {
                Assert.That(profile.Weapon.Range, Is.EqualTo(1500f / 50f));
                Assert.That(profile.CanCapture, Is.True);
                var weapon = UnitCatalog.Get(kind).Weapon;
                Assert.That(weapon.Delivery, Is.EqualTo(WeaponDelivery.MissileSplash));
                Assert.That(weapon.ProjectileSpeed, Is.EqualTo(1000f / 50f));
                Assert.That(weapon.SmallDamageRadius, Is.EqualTo(1f));
            }
            else
            {
                Assert.That(profile.Transport.Capacity, Is.EqualTo(UnitCatalog.Get(UnitKind.Transport).Transport.Capacity), "n007 attaches the same Sch3 cargo ability as n008.");
                Assert.That(profile.CanTransport, Is.True);
                Assert.That(profile.CanCapture, Is.False);
            }
        }

        [Test]
        public void V030HullsFollowTheClassicHullsInUnitsJson()
        {
            // Order is units.json order (the production grid breaks cost ties by it); no ordinal is an index.
            Assert.That(UnitCatalog.Get(UnitKind.Warship).Index, Is.GreaterThan(UnitCatalog.Get(UnitKind.Transport).Index));
            Assert.That(UnitCatalog.Get(UnitKind.Battleship).Index, Is.GreaterThan(UnitCatalog.Get(UnitKind.Warship).Index));
            Assert.That(UnitCatalog.Get(UnitKind.ArmoredTransport).Index, Is.GreaterThan(UnitCatalog.Get(UnitKind.Battleship).Index));
        }
    }
}
