namespace RiskAI
{
    public sealed partial class BattleHud
    {
        // Retained modal state. Controls are rendered by BattleHud.Retained.
        int menuTab;

        public void ShowPlayers()
        {
            controller.CancelCursor();
            menuTab = 2;
            controller.HelpVisible = true;
            BuildRetainedUi(false);
        }
    }
}
