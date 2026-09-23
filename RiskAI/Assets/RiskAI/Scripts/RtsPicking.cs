using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskAI
{
    public static class RtsPicking
    {
        static Rect ScreenRect(Camera camera, Bounds bounds)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var screen = camera.WorldToScreenPoint(corner);
                if (screen.z <= 0) return Rect.zero;
                xMin = Mathf.Min(xMin, screen.x); xMax = Mathf.Max(xMax, screen.x); yMin = Mathf.Min(yMin, screen.y); yMax = Mathf.Max(yMax, screen.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
        // Screen-space padding stays useful when the player zooms out.
        public static CombatTarget Target(BattleSession battle, Camera camera, Vector2 pointer, int relation = 0)
        {
            if (StrategicMapView.Active) return null;
            CombatTarget best = null; float bestScore = float.MaxValue;
            foreach (var candidate in battle.Targets)
            {
                if (!candidate || !candidate.IsAlive) continue;
                if (relation == 1 && candidate.Team != 0 || relation == -1 && candidate.Team == 0) continue;
                Rect bounds = Bounds(camera, candidate);
                if (bounds.width <= 0) continue;
                float dx = Mathf.Max(0f, Mathf.Max(bounds.xMin - pointer.x, pointer.x - bounds.xMax));
                float dy = Mathf.Max(0f, Mathf.Max(bounds.yMin - pointer.y, pointer.y - bounds.yMax));
                float outside = Mathf.Sqrt(dx * dx + dy * dy);
                if (outside > 9 * UiViewport.Scale) continue;
                float score = outside * 8 + Vector2.Distance(pointer, bounds.center) * .2f;
                if (score < bestScore) { best = candidate; bestScore = score; }
            }
            return best;
        }
        public static Rect Bounds(Camera camera, CombatTarget target)
        {
            // Warships and battleships are long hulls: a pivot-centred 1.8 m box
            // missed clicks on the bow or stern, which then became a plain move
            // order that sailed the fleet into its target.
            if (target is Ship ship && ship.TryGetHullBounds(out var hull)) return ScreenRect(camera, hull);
            float height = target is Ship ? 4.8f : target is DefenseTower ? VisualMetrics.TowerHeight : target is Soldier soldier?VisualMetrics.HeightFor(soldier.Kind):VisualMetrics.UnitHeight;
            if(target is DefenseTower tower && BuildingVariants.IsIntegrated(tower.VisualVariant))height=VisualMetrics.IntegratedTowerTopHeight;
            float radius = target is Ship ? 1.8f : target is DefenseTower ? VisualMetrics.TowerRadius : target is Soldier unit?VisualMetrics.RadiusFor(unit.Kind):VisualMetrics.UnitRadius;
            Vector3 foot = camera.WorldToScreenPoint(target.transform.position);
            Vector3 head = camera.WorldToScreenPoint(target.transform.position + Vector3.up * height);
            if (head.z <= 0) return Rect.zero;
            float halfWidth = Mathf.Max(8 * UiViewport.Scale, Mathf.Abs(camera.WorldToScreenPoint(target.transform.position + Vector3.right * radius).x - foot.x));
            return Rect.MinMaxRect(foot.x - halfWidth, Mathf.Min(foot.y, head.y) - 2, foot.x + halfWidth, Mathf.Max(foot.y, head.y) + 3);
        }
        public static Settlement Town(BattleSession battle, Camera camera, Vector2 pointer)
        {
            if(StrategicMapView.Active)
            {
                Settlement closest=null;float score=144*UiViewport.Scale*UiViewport.Scale;
                foreach(var city in battle.Towns)
                {var p=camera.WorldToScreenPoint(city.transform.position);float d=((Vector2)p-pointer).sqrMagnitude;if(p.z>0&&d<score){score=d;closest=city;}}
                return closest;
            }
            foreach (var town in battle.Towns)
            {
                if (!town.Selected && !(Keyboard.current != null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed))) continue;
                Vector3 label = camera.WorldToScreenPoint(town.transform.position + Vector3.up * BuildingSelection.LabelHeight(town));
                float s=BattleHud.Scale;
                // The name plate sits just above its anchor (BattleHud.DrawWorld).
                if (label.z > 0 && new Rect(label.x - 88*s, label.y - 2*s, 176*s, 28*s).Contains(pointer)) return town;
            }
            var ray=camera.ScreenPointToRay(pointer);Settlement best=null;float nearest=camera.farClipPlane;
            foreach (var town in battle.Towns)
            {
                if(!town)continue;float distance=BuildingSelection.HitDistance(town,ray);
                if(distance<nearest){nearest=distance;best=town;}
            }
            return best;
        }
        public static Harbor Harbor(BattleSession battle, Camera camera, Vector2 pointer)
        {
            if(battle==null)return null;
            if(StrategicMapView.Active&&battle.Naval)
            {
                Harbor closest=null;float score=144*UiViewport.Scale*UiViewport.Scale;
                foreach(var port in battle.Naval.Harbors)
                {var p=camera.WorldToScreenPoint(port.Landing);float d=((Vector2)p-pointer).sqrMagnitude;if(p.z>0&&d<score){score=d;closest=port;}}
                return closest;
            }
            var ray=camera.ScreenPointToRay(pointer);Harbor best=null;float nearest=camera.farClipPlane;
            if(!battle.Naval)return null;
            foreach(var harbor in battle.Naval.Harbors)
            {
                if(!harbor)continue;float distance=BuildingSelection.HitDistance(harbor,ray);
                if(distance<nearest){nearest=distance;best=harbor;}
            }
            return best;
        }
    }
}
