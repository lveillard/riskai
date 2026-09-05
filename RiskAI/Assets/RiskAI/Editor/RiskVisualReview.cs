using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RiskAI
{
    internal static class RiskVisualReview
    {
        static bool bastionFocusPending;
        static double bastionFocusAt;

        [MenuItem("RiskAI/Review/Overview")]
        static void Overview()
        {
            if (!TryGetSession(out var battle, out var controller)) return;
            PauseForReview(battle);
            controller.SelectAll();
            controller.CameraRig.ResetView();
        }

        [MenuItem("RiskAI/Review/Bastion")]
        static void Bastion()
        {
            if (!TryGetSession(out var battle, out var controller)) return;
            PauseForReview(battle);
            var capital = battle.Towns.FirstOrDefault(t => t.State.Owner == 0 && t.IsCapital);
            if (!capital)
            {
                Debug.LogWarning("RiskAI visual review: blue capital was not found.");
                return;
            }

            controller.SelectTown(capital);
            var rig = controller.CameraRig;
            rig.ResetView();
            rig.ZoomAt(4, new Vector2(Screen.width * .5f, Screen.height * .5f));
            bastionFocusPending = false;
            bastionFocusAt = EditorApplication.timeSinceStartup + .8;
            EditorApplication.update -= FocusBastionAfterZoom;
            EditorApplication.update += FocusBastionAfterZoom;
        }

        [MenuItem("RiskAI/Review/Resume")]
        static void Resume()
        {
            if (!TryGetSession(out var battle, out var controller)) return;
            if (battle.Winner >= 0)
            {
                Debug.Log("RiskAI visual review: the battle has ended; resume is unavailable.");
                return;
            }

            bastionFocusPending = false;
            EditorApplication.update -= FocusBastionAfterZoom;
            controller.SelectAll();
            controller.CameraRig.ResetView();
            battle.AiEnabled = true;
            if (battle.Paused) battle.TogglePause();
        }

        static void FocusBastionAfterZoom()
        {
            if (EditorApplication.timeSinceStartup < bastionFocusAt) return;
            if (bastionFocusPending) return;
            bastionFocusPending = true;
            EditorApplication.update -= FocusBastionAfterZoom;
            if (!TryGetSession(out _, out var controller)) return;
            controller.Focus(new Vector3(-25, 0, 13));
        }

        static void PauseForReview(BattleSession battle)
        {
            battle.AiEnabled = false;
            if (!battle.Paused) battle.TogglePause();
        }

        static bool TryGetSession(out BattleSession battle, out RtsController controller)
        {
            battle = BattleSession.Current;
            controller = Object.FindFirstObjectByType<RtsController>();
            if (!EditorApplication.isPlaying || !battle || !controller || !controller.CameraRig)
            {
                Debug.LogWarning("RiskAI visual review requires the LasMarcas scene to be running in Play Mode.");
                battle = null;
                controller = null;
                return false;
            }
            return true;
        }
    }
}
