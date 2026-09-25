using UnityEngine.InputSystem;

namespace RiskAI
{
    /// <summary>
    /// The test and probe seam, owned by the probe tooling (Runtime*Probe/Capture). Production
    /// types expose only neutral internals; every test-facing hook lives here instead.
    /// </summary>
    public static class ProbeHooks
    {
        /// <summary>The virtual pen device the bridge feeds into the Input System.</summary>
        public static Pen VirtualPen(WebPenBridge bridge) => bridge.VirtualPen;

        /// <summary>Injected pen row source for tests and probes.</summary>
        public static void SetPenSampleSource(WebPenBridge bridge, WebPenBridge.PenSampleSource source) => bridge.SetSampleSource(source);

        /// <summary>Runs one pen sample outside the Input System update loop.</summary>
        public static bool ProcessPenSample(WebPenBridge bridge) => bridge.ProcessSample();

        /// <summary>Sets the session language without persisting a player preference (tests, probes).</summary>
        public static void SetLanguage(GameLanguage language) => GameText.Language = language;
    }
}
