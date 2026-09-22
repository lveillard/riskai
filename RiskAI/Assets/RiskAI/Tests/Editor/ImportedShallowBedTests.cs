using NUnit.Framework;

namespace RiskAI.Tests
{
    public sealed class ImportedShallowBedTests
    {
        static ImportedMapData WetCell(float depth)
        {
            return new ImportedMapData { width=2,height=2,
                heightSamples=new[]{-depth,-depth,-depth,-depth},
                waterSamples=new float[4],landSamples=new int[4] };
        }

        [TestCase(.27f)] [TestCase(.538f)] [TestCase(.75f)] [TestCase(1.5f)]
        public void WetShallowCellsHaveVisualBedWithoutRequiringDryLand(float depth)
        {
            var data=WetCell(depth);
            Assert.That(ImportedTerrain.HasVisualGroundCell(data,0,0),Is.True);
            CollectionAssert.AreEqual(new[]{0,0,0,0},data.landSamples,
                "A visible bed must not turn flooded terrain into dry land.");
        }

        [Test] public void BedEndsBeyondOpticalFadeAndRetainsBoundaryCells()
        {
            var data=WetCell(4.6f);
            Assert.That(ImportedTerrain.HasVisualGroundCell(data,0,0),Is.False);
            data.heightSamples[3]=-.7f;
            Assert.That(ImportedTerrain.HasVisualGroundCell(data,0,0),Is.True,
                "The full transition cell is rendered so the bed cannot end inside transparent water.");
            data.heightSamples[3]=-4.6f;data.landSamples[0]=1;
            Assert.That(ImportedTerrain.HasVisualGroundCell(data,0,0),Is.True);
        }
    }
}
