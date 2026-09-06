using NUnit.Framework;
namespace RiskAI.Tests
{
    public sealed class ImportedGroundDecodingTests
    {
        [Test]
        public void TileSelectionIgnoresVariationWaterAndCliffFlags()
        {
            // A palette index of 6 with every independent texture-variation byte.
            for(int variation=0;variation<256;variation++)
                for(int flags=0;flags<16;flags++)
                {
                    int packed=variation|(0x93<<8)|((6|(flags<<4))<<16)|(1<<24);
                    Assert.That(ImportedMapData.GroundTileIndex(packed),Is.EqualTo(6));
                }
        }
    }
}
