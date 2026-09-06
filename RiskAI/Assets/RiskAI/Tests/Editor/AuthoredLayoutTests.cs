using System.Linq;
using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    public sealed class AuthoredLayoutTests
    {
        [Test]
        public void RiverlandsCitiesUseAnIrregularAuthoredLayout()
        {
            MapLayout.Configure(ScenarioMap.Riverlands);
            Assert.That(MapLayout.Towns.Length,Is.EqualTo(20));
            Assert.That(MapLayout.Towns.GroupBy(t=>t.Position.x).All(column=>column.Count()==1),Is.True,
                "The authored country map must not render as repeated vertical city columns.");
            Assert.That(MapLayout.Pads.Select(p=>p.x).Distinct().Count(),Is.EqualTo(MapLayout.Pads.Length));
        }
    }
}
