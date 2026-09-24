using System.Collections.Generic;
using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    /// <summary>
    /// v0.33 prepared a base prefab for every land model that was not in PortraitKinds,
    /// and rendered those seven kinds from their variant views. Both sets come from
    /// presentation.portraitSource; this test runs the same selection the art setup uses.
    /// </summary>
    public sealed class PortraitPreparationTests
    {
        static readonly UnitKind[] V033Variants =
        {
            UnitKind.EliteRifleman, UnitKind.Roarer, UnitKind.ArmyGeneral,
            UnitKind.MarineMajor, UnitKind.MarineGeneral, UnitKind.Artillery, UnitKind.Tank
        };

        // First claimant of each model, catalog order. Mortar is the procedural cart.
        static readonly string[] V033Models = { "Knight", "RogueHooded", "RoyalGuard", "Mage", "Mortar", "Medic", "MarinePrivate" };
        static readonly UnitKind[] V033Claimants =
        {
            UnitKind.Footman, UnitKind.Archer, UnitKind.Knight, UnitKind.Mage,
            UnitKind.Mortar, UnitKind.Medic, UnitKind.MarinePrivate
        };

        [Test]
        public void VariantSetAndBaseModelsMatchV033()
        {
            var variants = new List<UnitKind>();
            var kinds = new List<UnitKind>();
            var models = new List<string>();
            UnitVariantViews.CollectVariantPortraits(variants);
            UnitVariantViews.CollectBasePreparations(kinds, models);

            CollectionAssert.AreEquivalent(V033Variants, variants);
            CollectionAssert.AreEqual(V033Models, models);
            CollectionAssert.AreEqual(V033Claimants, kinds);

            Assert.That(models[kinds.IndexOf(UnitKind.Knight)], Is.EqualTo("RoyalGuard"), "Knight prepares the RoyalGuard prefab.");
            Assert.That(kinds, Does.Contain(UnitKind.Mortar), "Mortar stays on the model loop and renders the procedural cart.");
            Assert.That(models[kinds.IndexOf(UnitKind.Mage)], Is.EqualTo("Mage"));
            Assert.That(models[kinds.IndexOf(UnitKind.Medic)], Is.EqualTo("Medic"));
            Assert.That(models[kinds.IndexOf(UnitKind.MarinePrivate)], Is.EqualTo("MarinePrivate"));
            Assert.That(UnitVariantViews.HasVariantPortrait(UnitKind.Mage), Is.False);
            Assert.That(UnitVariantViews.HasVariantPortrait(UnitKind.Mortar), Is.False);
            Assert.That(UnitVariantViews.HasVariantPortrait(UnitKind.Medic), Is.False);
            Assert.That(UnitVariantViews.HasVariantPortrait(UnitKind.MarinePrivate), Is.False);
            Assert.That(UnitVariantViews.HasVariantPortrait(UnitKind.Tank), Is.True);
            Assert.That(UnitVariantViews.HasVariantPortrait(UnitKind.Knight), Is.False);
            CollectionAssert.DoesNotContain(variants, UnitKind.Frigate);
            CollectionAssert.DoesNotContain(kinds, UnitKind.Frigate);
        }
    }
}
