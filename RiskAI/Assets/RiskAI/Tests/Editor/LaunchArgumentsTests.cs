using NUnit.Framework;

namespace RiskAI.Tests
{
    public class LaunchArgumentsTests
    {
        [Test] public void BrowserDiagnosticsRequireAnExplicitFlagAndIgnoreUnrelatedQueryData()
        {
            var args=LaunchArguments.FromUrl("https://localhost/game/?riskai-map=europe&riskai-seed=16&riskai-probe=0&logFile=private&riskai-probe-seconds=60");
            CollectionAssert.AreEqual(new[]{"--riskai-map","europe","--riskai-seed","16","--riskai-probe-seconds","60"},args);
        }
        [Test] public void ExplicitProbeUsesTheSameOptionsAsDesktop()
        {
            var args=LaunchArguments.FromUrl("https://localhost/?riskai-probe=1&riskai-probe-recruits=32&riskai-players=16");
            CollectionAssert.AreEqual(new[]{"--riskai-probe","--riskai-probe-recruits","32","--riskai-players","16"},args);
        }
        [Test] public void RestartProbeAndBoundedCyclesAreWhitelistedForBrowserDiagnostics()
        {
            var args=LaunchArguments.FromUrl("https://localhost/?riskai-restart-probe=true&riskai-restart-cycles=4&unrelated=discard");
            CollectionAssert.AreEqual(new[]{"--riskai-restart-probe","--riskai-restart-cycles","4"},args);
        }
    }
}
