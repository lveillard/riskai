using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    /// <summary>
    /// The v0.34 arbiter: the live unit catalog must reproduce Fixtures/unit-golden.json exactly.
    /// Only the intentional changes of plan §7 may edit the fixture, in the same commit.
    /// </summary>
    public sealed class UnitGoldenParityTests
    {
        [Test]
        public void LiveCatalogMatchesTheGoldenFixture()
        {
            string expectedPath = Path.Combine(Application.dataPath, UnitGoldenSnapshot.FixturePath);
            string actual = UnitGoldenSnapshot.Capture();
            string actualPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "unit-golden.actual.json"));
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            Assert.That(File.Exists(expectedPath), Is.True, "Missing golden fixture " + expectedPath + "; the capture was written to " + actualPath);
            string expected = File.ReadAllText(expectedPath).Replace("\r\n", "\n");
            if (expected == actual) return;
            var want = expected.Split('\n');
            var got = actual.Split('\n');
            var report = new StringBuilder("Unit catalog differs from the golden fixture (capture: " + actualPath + "):\n");
            int shown = 0;
            for (int i = 0; i < Mathf.Max(want.Length, got.Length) && shown < 25; i++)
            {
                string a = i < want.Length ? want[i] : "<none>", b = i < got.Length ? got[i] : "<none>";
                if (a == b) continue;
                report.Append("line ").Append(i + 1).Append(":\n  golden: ").Append(a).Append("\n  live:   ").Append(b).Append('\n');
                shown++;
            }
            Assert.Fail(report.ToString());
        }
    }
}
