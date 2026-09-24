using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class PortraitFramingTests
    {
        [Test]
        public void LandNamesKeepTheV033CameraEvenThoughTheyAreUnitKinds()
        {
            Assert.That(System.Enum.IsDefined(typeof(UnitKind), "Knight"), Is.True);
            foreach (var name in new[] { "Knight", "Mage", "Medic", "MarinePrivate", "EliteRifleman" })
            {
                var frame = PortraitFraming.For(name);
                Assert.That(frame.Ship, Is.False, name);
                Assert.That(frame.Offset, Is.EqualTo(PortraitFraming.LandOffset), name);
                Assert.That(frame.OrthographicSize, Is.EqualTo(1.4f), name);
            }
            Assert.That(PortraitFraming.For("Mortar").OrthographicSize, Is.EqualTo(1.35f));
            Assert.That(PortraitFraming.For("Artillery").OrthographicSize, Is.EqualTo(1.7f));
            Assert.That(PortraitFraming.For("Tank").OrthographicSize, Is.EqualTo(1.7f));
            Assert.That(PortraitFraming.For("RogueHooded").OrthographicSize, Is.EqualTo(1.4f));
            Assert.That(PortraitFraming.For("RoyalGuard").OrthographicSize, Is.EqualTo(1.4f));
        }

        [Test]
        public void SeaNamesKeepTheHullCameras()
        {
            Assert.That(PortraitFraming.For("Frigate").OrthographicSize, Is.EqualTo(3.35f));
            Assert.That(PortraitFraming.For("Frigate").Offset, Is.EqualTo(PortraitFraming.SeaOffset));
            Assert.That(PortraitFraming.For("Warship").OrthographicSize, Is.EqualTo(3.8f));
            Assert.That(PortraitFraming.For("Battleship").OrthographicSize, Is.EqualTo(4.2f));
            Assert.That(PortraitFraming.For("Transport").Ship, Is.True);
            Assert.That(PortraitFraming.For("ArmoredTransport").Ship, Is.True);
            Assert.That(PortraitFraming.For("MountedKnight").OrthographicSize, Is.EqualTo(1.85f));
            Assert.That(PortraitFraming.For("MountedKnight").Refit, Is.True);
            Assert.That(PortraitFraming.For("ArmyGeneral").OrthographicSize, Is.EqualTo(2.1f));
            Assert.That(PortraitFraming.For("Roarer").UpperFraction, Is.EqualTo(.78f));
            Assert.That(PortraitFraming.For("MarineMajor").Refit, Is.True);
            Assert.That(PortraitFraming.For("MarineMajor").OrthographicSize, Is.EqualTo(1.4f));
            Assert.Throws<System.InvalidOperationException>(() => PortraitFraming.For("NotAUnit"));
        }

        [Test]
        public void FramingIgnoresLinesAndTheGroundShadow()
        {
            var root = new GameObject("frame probe");
            var line = root.AddComponent<LineRenderer>();
            var shadow = new GameObject("Soft ground shadow");
            shadow.transform.SetParent(root.transform, false);
            var shadowRenderer = shadow.AddComponent<MeshRenderer>();
            Assert.That(PortraitFraming.FramesRenderer(line), Is.False);
            Assert.That(PortraitFraming.FramesRenderer(shadowRenderer), Is.False);
            Object.DestroyImmediate(root);
        }
    }
}
