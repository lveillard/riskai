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
    }
}
