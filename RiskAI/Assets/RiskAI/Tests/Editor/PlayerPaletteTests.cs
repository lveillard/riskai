using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class PlayerPaletteTests
    {
        [Test]
        public void UsesWarcraftThreeExtendedPaletteInCanonicalPlayerOrder()
        {
            var expected=new[]
            {
                "FF0303","0042FF","1CE6B9","540081",
                "FFFC01","FE8A0E","20C000","E55BB0",
                "959697","7EBFF1","106246","4E2A04",
                "800048","0000C3","00EAFF","BE00FE" // player 13: local burgundy, see VisualFactory
            };

            CollectionAssert.AreEqual(expected,Enumerable.Range(0,expected.Length)
                .Select(team=>ColorUtility.ToHtmlStringRGB(VisualFactory.TeamColor(team))).ToArray());
            Assert.That(VisualFactory.ColourName(0),Is.EqualTo("Rojo"));
            Assert.That(VisualFactory.ColourName(1),Is.EqualTo("Azul"));
        }

        [Test]
        public void UnitClothAndRoofsReadTheSameCanonicalTeamColour()
        {
            var root=new GameObject("Palette material probe");
            try
            {
                var renderer=root.AddComponent<MeshRenderer>();
                var source=Resources.Load<Material>("RiskAILit");
                Assert.That(source,Is.Not.Null);
                renderer.sharedMaterial=source;
                UnitTeamColor.Apply(root,RiskAI.Core.UnitKind.Archer,12);
                var canonical=VisualFactory.TeamColor(12);
                var surface=VisualFactory.TeamMaterialColor(12);
                Assert.That(Vector4.Distance(renderer.sharedMaterial.GetColor("_TeamColor"),surface),Is.LessThan(.00001f));
                Assert.That(Vector4.Distance(WorldArt.RoofMaterial(12).GetColor("_Tint"),surface),Is.LessThan(.00001f));
                Assert.That(canonical.maxColorComponent,Is.LessThan(VisualFactory.TeamColor(0).maxColorComponent-.3f),
                    "Red and burgundy must retain their visible brightness gap on every faction surface.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MaterialColoursAreNotLinearisedTwice()
        {
            // Material.SetColor already converts sRGB to linear in a Linear project.
            // A pre-linearised input turned orange FE8A0E into red-orange FD4101.
            for(int team=0;team<16;team++)
                Assert.That(ColorUtility.ToHtmlStringRGB(VisualFactory.TeamMaterialColor(team)),
                    Is.EqualTo(ColorUtility.ToHtmlStringRGB(VisualFactory.TeamColor(team))),"team "+team);
        }

        [Test]
        public void ClaimCirclesStayWhiteIndependentlyOfPlayerColour()
        {
            Assert.That(CityClaimZone.VisibleRingColor(false),Is.EqualTo(Color.white));
            Assert.That(CityClaimZone.VisibleRingColor(true),Is.EqualTo(CityClaimZone.ContestedRingColor));
        }
    }
}
