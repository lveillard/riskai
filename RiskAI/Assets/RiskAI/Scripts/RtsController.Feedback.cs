using UnityEngine;

namespace RiskAI
{
    public sealed partial class RtsController
    {
        /// <summary>Jumps the camera to the latest "under attack" alert, if it is recent.</summary>
        public bool FocusLastAlert()
        {
            var alerts = session ? session.Feedback.Alerts : null;
            if (alerts == null || !alerts.CanJump(Time.unscaledTime)) return false;
            Focus(alerts.LastPosition);
            return true;
        }

        // Warcraft-style Space: with nothing selected it returns to the last alert.
        bool FocusLastAlertWhenIdle()
        {
            PurgeStaleSelection();
            if (Selection.Count > 0 || Fleet.Count > 0 || SelectedTown || SelectedHarbor || SelectedCamp) return false;
            return FocusLastAlert();
        }
    }
}
