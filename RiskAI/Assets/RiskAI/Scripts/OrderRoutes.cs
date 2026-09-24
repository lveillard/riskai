using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Queued-order lines for the selection. One pooled renderer per leg and one marker per
    /// waypoint, capped. The active leg uses the NavMesh or sea polyline and starts at the unit,
    /// so corners already passed are not drawn. Later legs are the queued waypoints.
    /// Shift or Encolar keeps the route up. A plain order shows it for <see cref="ConfirmSeconds"/>, fading, then hides it.
    /// </summary>
    public sealed class OrderRoutes : MonoBehaviour
    {
        public const int SegmentCap = 48;
        /// <summary>How long a plain order's route stays up, and the fade across that same window.</summary>
        public const float ConfirmSeconds = 1.2f;
        const float NormalWidth = .08f;
        const float EmphasizedWidth = .16f;
        readonly LineRenderer[] legs = new LineRenderer[SegmentCap];
        readonly LineRenderer[] marks = new LineRenderer[SegmentCap];
        readonly Vector3[] scratch = new Vector3[48];
        struct Stamp { public int Id, Revision, PathCount, Ends; public Vector3 Pos, Path0; public byte Emphasis; }
        readonly Stamp[] stamps = new Stamp[SegmentCap];
        RtsController controller;
        int legCount, markCount;
        float confirmStart = -1f;
        Material fallbackMaterial;
        public int LegCount { get; private set; }

        /// <summary>A plain order's confirmation. Queue mode draws on its own and does not use this.</summary>
        public void Confirm() => confirmStart = Time.unscaledTime;

        /// <summary>1 at the start of the confirmation, 0 once <see cref="ConfirmSeconds"/> has elapsed.</summary>
        public static float ConfirmAlpha(float age)
        {
            if (age >= ConfirmSeconds) return 0f;
            // A same-frame read can land a hair before the stamp. That is still the start of the flash.
            if (age < 0f) age = 0f;
            return 1f - age / ConfirmSeconds;
        }

        /// <summary>Shift/Encolar keeps the route. Otherwise only the confirmation window does.</summary>
        public static bool RouteVisible(bool queue, float age) => queue || ConfirmAlpha(age) > 0f;

        /// <summary>
        /// Drops corners already passed. The first point becomes <paramref name="position"/>
        /// and the polyline continues at the next corner ahead of it.
        /// </summary>
        public static int TrimTravelled(Vector3[] points, int count, Vector3 position)
        {
            if (count <= 0) return 0;
            if (count == 1) { points[0] = position; return 1; }
            int segment = 0;
            float best = float.MaxValue;
            float px = position.x, pz = position.z;
            for (int i = 0; i < count - 1; i++)
            {
                float distance = SegmentDistanceXZ(px, pz, points[i], points[i + 1]);
                if (distance <= best) { best = distance; segment = i; }
            }
            int write = 1;
            for (int i = segment + 1; i < count; i++) points[write++] = points[i];
            points[0] = position;
            return write;
        }

        static float SegmentDistanceXZ(float px, float pz, Vector3 a, Vector3 b)
        {
            float abx = b.x - a.x, abz = b.z - a.z;
            float length = abx * abx + abz * abz;
            float t = length < 1e-8f ? 0f : Mathf.Clamp01(((px - a.x) * abx + (pz - a.z) * abz) / length);
            float dx = px - (a.x + abx * t), dz = pz - (a.z + abz * t);
            return dx * dx + dz * dz;
        }

        void OnEnable()
        {
            controller = GetComponent<RtsController>();
            for (int i = 0; i < stamps.Length; i++) stamps[i].Id = -1;
        }

        public void Refresh() => LateUpdate();
        void LateUpdate()
        {
            var session = BattleSession.Current;
            bool queue = controller && controller.QueueOrders;
            float age = confirmStart < 0f ? ConfirmSeconds : Time.unscaledTime - confirmStart;
            if (!session || controller == null || !RouteVisible(queue, age)) { Hide(); return; }
            bool emphasis = queue;
            float fade = queue ? 1f : ConfirmAlpha(age);
            int legsDrawn = 0, marksDrawn = 0;
            var soldiers = controller.Selection;
            for (int i = 0; i < soldiers.Count; i++)
                if (soldiers[i]) legsDrawn = Draw(soldiers[i], legsDrawn, ref marksDrawn, emphasis, fade, !queue);
            var fleet = controller.Fleet;
            for (int i = 0; i < fleet.Count; i++)
                if (fleet[i]) legsDrawn = Draw(fleet[i], legsDrawn, ref marksDrawn, emphasis, fade, !queue);
            LegCount = legsDrawn;
            for (int i = legsDrawn; i < legCount; i++)
            {
                if (legs[i]) legs[i].enabled = false;
                if (i < stamps.Length) stamps[i].Id = -1;
            }
            for (int i = marksDrawn; i < markCount; i++) if (marks[i]) marks[i].enabled = false;
            legCount = legsDrawn;
            markCount = marksDrawn;
        }

        /// <summary>Quantised attack and follow endpoints. A still hull redraws when only the target moves.</summary>
        static int LiveEnds(IOrderable unit)
        {
            int hash = 1;
            int count = unit.OrderLegCount;
            for (int i = 0; i < count; i++)
            {
                var kind = unit.OrderLegKind(i);
                if (kind != UnitCommandKind.Attack && kind != UnitCommandKind.Follow) continue;
                var point = unit.OrderLegPoint(i);
                hash = unchecked(hash * 31 + Mathf.RoundToInt(point.x * 5f));
                hash = unchecked(hash * 31 + Mathf.RoundToInt(point.z * 5f));
            }
            return hash;
        }

        int Draw(IOrderable unit, int drawn, ref int marksDrawn, bool emphasis, float fade, bool force)
        {
            var body = unit as Component;
            if (!body || !unit.Selected || unit.OrderLegCount <= 0 || drawn >= SegmentCap) return drawn;
            unit.RefreshActivePath();
            int pathCount = unit.ActivePathCount;
            Vector3 from = body.transform.position;
            Vector3 path0 = pathCount > 0 ? unit.ActivePathPoint(0) : from;
            byte emphasisFlag = (byte)(emphasis ? 1 : 0);
            int ends = LiveEnds(unit);
            var stamp = stamps[drawn];
            bool dirty = force || stamp.Id != unit.EntityId || stamp.Revision != unit.Orders.Revision || stamp.PathCount != pathCount
                || stamp.Ends != ends || stamp.Emphasis != emphasisFlag || (from - stamp.Pos).sqrMagnitude > .04f
                || pathCount > 0 && (path0 - stamp.Path0).sqrMagnitude > .04f;
            stamps[drawn] = new Stamp { Id = unit.EntityId, Revision = unit.Orders.Revision, PathCount = pathCount, Ends = ends, Pos = from, Path0 = path0, Emphasis = emphasisFlag };
            if (!dirty)
            {
                int keep = Mathf.Min(unit.OrderLegCount, SegmentCap - drawn);
                drawn += keep;
                marksDrawn = Mathf.Min(SegmentCap, marksDrawn + keep);
                return drawn;
            }
            var to = unit.OrderLegPoint(0);
            var color = ColorOf(unit.OrderLegKind(0), emphasis);
            color.a *= fade;
            if (unit.ActivePathCount >= 2)
            {
                int n = Mathf.Min(unit.ActivePathCount, scratch.Length);
                for (int c = 0; c < n; c++) scratch[c] = unit.ActivePathPoint(c);
                n = TrimTravelled(scratch, n, from);
                if (n >= 2 && unit.OrderLegKind(0) == UnitCommandKind.Attack) scratch[n - 1] = to;
                if (n >= 2) Show(ref legs[drawn], scratch, n, color, emphasis ? EmphasizedWidth : NormalWidth, false);
                else ShowSegment(ref legs[drawn], from, to, color, emphasis);
            }
            else ShowSegment(ref legs[drawn], from, to, color, emphasis);
            drawn++;
            if (marksDrawn < SegmentCap) ShowMark(ref marks[marksDrawn++], to, color, emphasis);
            from = to;
            for (int i = 1; i < unit.OrderLegCount && drawn < SegmentCap; i++)
            {
                to = unit.OrderLegPoint(i);
                color = ColorOf(unit.OrderLegKind(i), emphasis);
                color.a *= fade;
                ShowSegment(ref legs[drawn], from, to, color, emphasis);
                drawn++;
                if (marksDrawn < SegmentCap) ShowMark(ref marks[marksDrawn++], to, color, emphasis);
                from = to;
            }
            return drawn;
        }

        void ShowSegment(ref LineRenderer line, Vector3 from, Vector3 to, Color color, bool emphasis)
        {
            scratch[0] = from; scratch[1] = to;
            Show(ref line, scratch, 2, color, emphasis ? EmphasizedWidth : NormalWidth, false);
        }

        void ShowMark(ref LineRenderer line, Vector3 point, Color color, bool emphasis)
        {
            const float r = .35f;
            scratch[0] = point + new Vector3(r, 0, 0);
            scratch[1] = point + new Vector3(0, 0, r);
            scratch[2] = point + new Vector3(-r, 0, 0);
            scratch[3] = point + new Vector3(0, 0, -r);
            Show(ref line, scratch, 4, color, emphasis ? .1f : .06f, true);
        }

        void Show(ref LineRenderer line, Vector3[] points, int count, Color color, float width, bool loop)
        {
            if (!line)
            {
                var go = new GameObject("Order route");
                go.transform.SetParent(transform, false);
                line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.numCapVertices = 2;
                line.sharedMaterial = Resources.Load<Material>("RiskAIRing");
                if (!line.sharedMaterial)
                {
                    // Only the shared asset path is free; an instance must die with the routes.
                    fallbackMaterial = GeneratedResourceOwner.For(transform).Track(new Material(Shader.Find("Sprites/Default")));
                    line.sharedMaterial = fallbackMaterial;
                }
            }
            line.enabled = true;
            line.loop = loop;
            line.positionCount = count;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            for (int i = 0; i < count; i++)
            {
                var p = points[i];
                p.y += .15f;
                line.SetPosition(i, p);
            }
        }

        public bool TryLegStart(int index, out Vector3 point)
        {
            point = default;
            if (index < 0 || index >= legCount || !legs[index] || !legs[index].enabled) return false;
            point = legs[index].GetPosition(0);
            point.y -= .15f;
            return true;
        }

        public static Color ColorOf(UnitCommandKind kind, bool emphasis)
        {
            Color color;
            switch (kind)
            {
                case UnitCommandKind.Attack:
                case UnitCommandKind.AttackMove: color = new Color(1f, .28f, .16f); break;
                case UnitCommandKind.Patrol: color = new Color(.95f, .82f, .2f); break;
                case UnitCommandKind.Embark: color = new Color(.25f, .85f, .95f); break;
                case UnitCommandKind.Capture: color = new Color(1f, .55f, .12f); break;
                case UnitCommandKind.Unload: color = new Color(.35f, .5f, 1f); break;
                case UnitCommandKind.Follow: color = new Color(.85f, .85f, .9f); break;
                default: color = new Color(.25f, .95f, .4f); break;
            }
            color.a = emphasis ? 1f : .55f;
            return color;
        }

        void Hide()
        {
            LegCount = 0;
            for (int i = 0; i < legCount; i++) if (legs[i]) legs[i].enabled = false;
            for (int i = 0; i < markCount; i++) if (marks[i]) marks[i].enabled = false;
            legCount = markCount = 0;
        }

    }
}
