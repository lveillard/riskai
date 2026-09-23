using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class HudPreferenceTests
    {
        [Test]
        public void DesktopMinimapIsVisibleByDefaultAndHonoursAnExplicitChoice()
        {
            bool hadCurrent = PlayerPrefs.HasKey(BattleHud.MinimapPreference);
            int current = PlayerPrefs.GetInt(BattleHud.MinimapPreference, 1);
            try
            {
                PlayerPrefs.DeleteKey(BattleHud.MinimapPreference);
                Assert.That(BattleHud.DesktopMinimapPreference(), Is.True, "A fresh session shows the desktop minimap.");
                PlayerPrefs.SetInt(BattleHud.MinimapPreference, 0);
                Assert.That(BattleHud.DesktopMinimapPreference(), Is.False, "An explicit hide is honoured.");
                PlayerPrefs.SetInt(BattleHud.MinimapPreference, 1);
                Assert.That(BattleHud.DesktopMinimapPreference(), Is.True);
            }
            finally
            {
                if (hadCurrent) PlayerPrefs.SetInt(BattleHud.MinimapPreference, current); else PlayerPrefs.DeleteKey(BattleHud.MinimapPreference);
                PlayerPrefs.Save();
            }
        }
    }
}
