using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Queued-order lines for the selection. One pooled renderer per leg and one marker per
    /// waypoint, capped. The active land leg uses the NavMesh polyline once it exists; a ship
    /// leg uses the sea polyline. Later legs are the queued waypoints. Reaching a point drops its leg.
    /// </summary>
    public sealed class OrderRoutes : MonoBehaviour
    {
        public const int SegmentCap = 48;
        const float NormalWidth = .08f;
        const float EmphasizedWidth = .16f;
        readonly LineRenderer[] legs = new LineRenderer[SegmentCap];
        readonly LineRenderer[] marks = new LineRenderer[SegmentCap];
        readonly Vector3[] scratch = new Vector3[48];
        RtsController controller;
        int legCount, markCount;
        public int LegCount { get; private set; }

        void OnEnable() { controller = GetComponent<RtsController>(); }

        public void Refresh() => LateUpdate();
        void LateUpdate()
        {
            var session = BattleSession.Current;
            if (!session) { Hide(); return; }
            bool emphasis = Emphasis();
            int legsDrawn = 0, marksDrawn = 0;
            if (session.Units != null)
                for (int i = 0; i < session.Units.Count; i++)
                    legsDrawn = Draw(session.Units[i], legsDrawn, ref marksDrawn, emphasis);
            var naval = session.Naval;
            if (naval)
                for (int i = 0; i < naval.Ships.Count; i++)
                    legsDrawn = Draw(naval.Ships[i], legsDrawn, ref marksDrawn, emphasis);
            LegCount = legsDrawn;
            for (int i = legsDrawn; i < legCount; i++) if (legs[i]) legs[i].enabled = false;
            for (int i = marksDrawn; i < markCount; i++) if (marks[i]) marks[i].enabled = false;
            legCount = legsDrawn;
            markCount = marksDrawn;
        }

        bool Emphasis() => controller && controller.QueueOrders;

        int Draw(IOrderable unit, int drawn, ref int marksDrawn, bool emphasis)
        {
            var body = unit as Component;
            if (!body || !unit.Selected || unit.OrderLegCount <= 0 || drawn >= SegmentCap) return drawn;
            unit.RefreshActivePath();
            Vector3 from = body.transform.position;
            var to = unit.OrderLegPoint(0);
            var color = ColorOf(unit.OrderLegKind(0), emphasis);
            if (unit.ActivePathCount >= 2)
            {
                int n = Mathf.Min(unit.ActivePathCount, scratch.Length);
                for (int c = 0; c < n; c++) scratch[c] = unit.ActivePathPoint(c);
                Show(ref legs[drawn], scratch, n, color, emphasis ? EmphasizedWidth : NormalWidth, false);
            }
            else ShowSegment(ref legs[drawn], from, to, color, emphasis);
            drawn++;
            if (marksDrawn < SegmentCap) ShowMark(ref marks[marksDrawn++], to, color, emphasis);
            from = to;
            for (int i = 1; i < unit.OrderLegCount && drawn < SegmentCap; i++)
            {
                to = unit.OrderLegPoint(i);
                color = ColorOf(unit.OrderLegKind(i), emphasis);
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
                if (!line.sharedMaterial) line.material = new Material(Shader.Find("Sprites/Default"));
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
