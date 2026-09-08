namespace RiskAI.Core
{
    /// <summary>Shared ownership contract for matches with human and AI players.</summary>
    public static class PlayerRules
    {
        public const int MaxPlayers = 16;
        public const int MinimumCitiesPerPlayer = 3;
        public static int MaximumPlayersForCityCount(int cityCount) => System.Math.Min(MaxPlayers, System.Math.Max(0, cityCount) / MinimumCitiesPerPlayer);
        public const int NeutralTeam = MaxPlayers;
        public const int NeutralOwner = -1;
        public static bool IsPlayer(int team) => team >= 0 && team < MaxPlayers;
        public static int ToCombatTeam(int owner) => IsPlayer(owner) ? owner : NeutralTeam;
    }
}
