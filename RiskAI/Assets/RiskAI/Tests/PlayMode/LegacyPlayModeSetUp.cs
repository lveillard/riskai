using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    [SetUpFixture]
    public sealed class LegacyPlayModeSetUp
    {
        [OneTimeSetUp]
        public void UseTwoPlayersForLegacyFixtures()
        {
            BattleSession.PlayerCountForNewMatch = 2;
        }

        [OneTimeTearDown]
        public void RestoreProductionPlayerCount()
        {
            BattleSession.PlayerCountForNewMatch = PlayerRules.MaxPlayers;
        }
    }
}
