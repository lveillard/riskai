using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskAI
{
    public static class RtsPicking
    {
        // Screen-space padding stays useful when the player zooms out.
        public static CombatTarget Target(BattleSession battle, Camera camera, Vector2 pointer, int relation = 0)
        {
            CombatTarget best = null; float bestScore = float.MaxValue;
            foreach (var candidate in battle.Targets)
            {
                if (!candidate || !candidate.IsAlive) continue;
                if (relation == 1 && candidate.Team != 0 || relation == -1 && candidate.Team == 0) continue;
                Rect bounds = Bounds(camera, candidate);
                if (bounds.width <= 0) continue;
                float dx = Mathf.Max(bounds.xMin - pointer.x, 0, pointer.x - bounds.xMax);
                float dy = Mathf.Max(bounds.yMin - pointer.y, 0, pointer.y - bounds.yMax);
                float outside = Mathf.Sqrt(dx * dx + dy * dy);
                if (outside > 9) continue;
                float score = outside * 8 + Vector2.Distance(pointer, bounds.center) * .2f;
                if (score < bestScore) { best = candidate; bestScore = score; }
            }
            return best;
        }
        public static Rect Bounds(Camera camera, CombatTarget target)
        {
            float height = target is Ship ? 4.8f : target is DefenseTower ? VisualMetrics.TowerHeight : target is Soldier soldier?VisualMetrics.HeightFor(soldier.Kind):VisualMetrics.UnitHeight;
            float radius = target is Ship ? 1.8f : target is DefenseTower ? VisualMetrics.TowerRadius : target is Soldier unit?VisualMetrics.RadiusFor(unit.Kind):VisualMetrics.UnitRadius;
            Vector3 foot = camera.WorldToScreenPoint(target.transform.position);
            Vector3 head = camera.WorldToScreenPoint(target.transform.position + Vector3.up * height);
            if (head.z <= 0) return Rect.zero;
            float halfWidth = Mathf.Max(8, Mathf.Abs(camera.WorldToScreenPoint(target.transform.position + Vector3.right * radius).x - foot.x));
            return Rect.MinMaxRect(foot.x - halfWidth, Mathf.Min(foot.y, head.y) - 2, foot.x + halfWidth, Mathf.Max(foot.y, head.y) + 3);
        }
        public static Settlement Town(BattleSession battle, Camera camera, Vector2 pointer)
        {
            foreach (var town in battle.Towns)
            {
                if (!town.Selected && !(Keyboard.current != null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed))) continue;
                Vector3 label = camera.WorldToScreenPoint(town.transform.position + Vector3.up * 4.8f);
                float s=BattleHud.Scale;
                if (label.z > 0 && new Rect(label.x - 88*s, label.y - 22*s, 176*s, 28*s).Contains(pointer)) return town;
            }
            var hits = Physics.RaycastAll(camera.ScreenPointToRay(pointer), camera.farClipPlane, ~0, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                var town = hit.collider.GetComponentInParent<Settlement>();
                if (town) return town;
            }
            return null;
        }
        public static Harbor Harbor(BattleSession battle, Camera camera, Vector2 pointer)
        {
            if(battle==null)return null;
            var hits=Physics.RaycastAll(camera.ScreenPointToRay(pointer),camera.farClipPlane,~0,QueryTriggerInteraction.Collide);
            foreach(var hit in hits)
            {
                var harbor=hit.collider.GetComponentInParent<Harbor>();
                if(harbor)return harbor;
            }
            return null;
        }
    }
}
