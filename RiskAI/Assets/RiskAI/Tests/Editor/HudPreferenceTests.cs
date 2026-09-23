using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class HudPreferenceTests
    {
        const string LegacyMinimapPreference = "riskai.hud.minimap";

        [Test]
        public void DesktopMinimapIsVisibleByDefaultAndIgnoresTheLegacyHiddenPreference()
        {
            bool hadCurrent = PlayerPrefs.HasKey(BattleHud.MinimapPreference), hadLegacy = PlayerPrefs.HasKey(LegacyMinimapPreference);
            int current = PlayerPrefs.GetInt(BattleHud.MinimapPreference, 1), legacy = PlayerPrefs.GetInt(LegacyMinimapPreference, 1);
            try
            {
                PlayerPrefs.DeleteKey(BattleHud.MinimapPreference);
                PlayerPrefs.SetInt(LegacyMinimapPreference, 0);
                Assert.That(BattleHud.DesktopMinimapPreference(), Is.True, "A fresh session shows the desktop minimap, whatever an older build saved.");
                PlayerPrefs.SetInt(BattleHud.MinimapPreference, 0);
                Assert.That(BattleHud.DesktopMinimapPreference(), Is.False, "An explicit hide in this build line is honoured.");
                PlayerPrefs.SetInt(BattleHud.MinimapPreference, 1);
                Assert.That(BattleHud.DesktopMinimapPreference(), Is.True);
            }
            finally
            {
                if (hadCurrent) PlayerPrefs.SetInt(BattleHud.MinimapPreference, current); else PlayerPrefs.DeleteKey(BattleHud.MinimapPreference);
                if (hadLegacy) PlayerPrefs.SetInt(LegacyMinimapPreference, legacy); else PlayerPrefs.DeleteKey(LegacyMinimapPreference);
                PlayerPrefs.Save();
            }
        }
    }
}
