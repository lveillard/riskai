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
                "9B0000","0000C3","00EAFF","BE00FE"
            };

            CollectionAssert.AreEqual(expected,Enumerable.Range(0,expected.Length)
                .Select(team=>ColorUtility.ToHtmlStringRGB(VisualFactory.TeamColor(team))).ToArray());
            StringAssert.Contains("Rojo",VisualFactory.TeamName(0));
            StringAssert.Contains("Azul",VisualFactory.TeamName(1));
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
                Assert.That(renderer.sharedMaterial.GetColor("_TeamColor"),Is.EqualTo(canonical));
                Assert.That(WorldArt.RoofMaterial(12).GetColor("_Tint"),Is.EqualTo(canonical));
                Assert.That(canonical.maxColorComponent,Is.LessThan(VisualFactory.TeamColor(0).maxColorComponent-.3f),
                    "WC3 red and maroon must retain their visible brightness gap on every faction surface.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
