using System.IO;
using System.Text;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class SourceTrainingTests
    {
        [Test]
        public void EveryRepresentedSourceUnitUsesItsOriginalBinaryTrainingTime()
        {
            // Concatenated original 16-byte W3U modifications, offsets
            // 552, 6B6, 85F, A09, 5165, 5381, 54C1 (hex).
            var cases=new[]{
                ("h00B",UnitKind.Archer),("h00E",UnitKind.Medic),("h00G",UnitKind.Guard),
                ("h00H",UnitKind.Mortar),("h012",UnitKind.MarinePrivate),
                ("h014",UnitKind.MarineMajor),("h015",UnitKind.MarineGeneral)};
            string path=Path.Combine(Application.dataPath,"RiskAI/Tests/Editor/Fixtures/SaranTrainingModifications.bytes");
            using(var reader=new BinaryReader(File.OpenRead(path)))
            {
                Assert.That(reader.BaseStream.Length,Is.EqualTo(cases.Length*16));
                foreach(var entry in cases)
                {
                    Assert.That(Encoding.ASCII.GetString(reader.ReadBytes(4)),Is.EqualTo("ubld"));
                    Assert.That(reader.ReadInt32(),Is.Zero);
                    int seconds=reader.ReadInt32();
                    Assert.That(reader.ReadInt32(),Is.Zero,"Original modification terminator");
                    Assert.That(BattleRules.TrainTime(entry.Item2),Is.EqualTo(seconds),entry.Item1);
                }
            }
        }
    }
}
