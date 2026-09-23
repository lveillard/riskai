using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    [SetUpFixture]
    public sealed class TwoPlayerPlayModeSetUp
    {
        [OneTimeSetUp]
        public void UseTwoPlayersForPlayModeFixtures()
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
