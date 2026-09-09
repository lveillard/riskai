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
            var args=LaunchArguments.FromUrl("https://localhost/?riskai-probe=1&riskai-probe-recruits=32&riskai-players=16&riskai-path-budget=1000");
            CollectionAssert.AreEqual(new[]{"--riskai-probe","--riskai-probe-recruits","32","--riskai-players","16","--riskai-path-budget","1000"},args);
        }
        [Test] public void RestartProbeAndBoundedCyclesAreWhitelistedForBrowserDiagnostics()
        {
            var args=LaunchArguments.FromUrl("https://localhost/?riskai-restart-probe=true&riskai-restart-cycles=4&unrelated=discard");
            CollectionAssert.AreEqual(new[]{"--riskai-restart-probe","--riskai-restart-cycles","4"},args);
        }
        [Test] public void UnitShadowDiagnosticIsOptInAndCannotBeEnabledByAnUnrelatedQuery()
        {
            var args=LaunchArguments.FromUrl("https://localhost/?riskai-probe-no-unit-shadows=1&no-unit-shadows=1");
            CollectionAssert.AreEqual(new[]{"--riskai-probe-no-unit-shadows"},args);
        }
        [Test] public void UnitModelRendererDiagnosticIsOptInAndKeepsTheBrowserContractNarrow()
        {
            var args=LaunchArguments.FromUrl("https://localhost/?riskai-probe-hide-unit-renderers=true&hide-unit-renderers=true");
            CollectionAssert.AreEqual(new[]{"--riskai-probe-hide-unit-renderers"},args);
        }
        [Test] public void UnitAnimationDiagnosticIsOptInAndKeepsTheBrowserContractNarrow()
        {
            var args=LaunchArguments.FromUrl("https://localhost/?riskai-probe-disable-unit-animation=1&disable-unit-animation=1");
            CollectionAssert.AreEqual(new[]{"--riskai-probe-disable-unit-animation"},args);
        }
        [Test] public void UnitSkinBakeDiagnosticIsOptInAndKeepsTheBrowserContractNarrow()
        {
            var args=LaunchArguments.FromUrl("https://localhost/?riskai-probe-bake-unit-skins=true&bake-unit-skins=true");
            CollectionAssert.AreEqual(new[]{"--riskai-probe-bake-unit-skins"},args);
        }
    }
}
