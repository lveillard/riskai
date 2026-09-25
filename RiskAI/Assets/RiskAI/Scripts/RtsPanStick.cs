using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>
    /// Touch camera joystick: drag the knob away from the centre to pan the camera that way,
    /// faster the further it goes. Two-finger drag still works; this is the one-thumb option.
    /// </summary>
    public sealed class RtsPanStick : VisualElement
    {
        static readonly Color Rim = new Color(.93f, .84f, .6f, .75f), Base = new Color(.05f, .04f, .025f, .45f), Knob = new Color(.93f, .84f, .6f, .9f);
        const float DeadZone = .12f;
        int pointer = -1;
        Vector2 value;

        /// <summary>Screen-space stick deflection: x right, y up, length 0..1.</summary>
        public Vector2 Value => value;

        public RtsPanStick(float size)
        {
            name = "HUD camera stick";
            style.position = Position.Absolute; style.width = style.height = size;
            generateVisualContent += Paint;
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<PointerCancelEvent>(_ => Release());
            RegisterCallback<PointerCaptureOutEvent>(_ => Release());
        }

        void OnDown(PointerDownEvent e)
        {
            if (pointer >= 0) return;
            pointer = e.pointerId; this.CapturePointer(pointer);
            Deflect(e.localPosition); e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (e.pointerId != pointer) return;
            Deflect(e.localPosition); e.StopPropagation();
        }

        void OnUp(PointerUpEvent e)
        {
            if (e.pointerId != pointer) return;
            this.ReleasePointer(pointer); Release(); e.StopPropagation();
        }

        void Release() { pointer = -1; value = Vector2.zero; MarkDirtyRepaint(); }

        void Deflect(Vector2 local)
        {
            var r = contentRect; float radius = Mathf.Min(r.width, r.height) * .5f;
            if (radius < 1) return;
            var offset = (local - r.center) / radius;
            offset.y = -offset.y;
            if (offset.sqrMagnitude > 1) offset.Normalize();
            value = offset.magnitude < DeadZone ? Vector2.zero : offset;
            MarkDirtyRepaint();
        }

        void Paint(MeshGenerationContext context)
        {
            var p = context.painter2D; var r = contentRect;
            float radius = Mathf.Min(r.width, r.height) * .5f; if (radius < 4) return;
            var centre = r.center;
            p.BeginPath(); p.Arc(centre, radius - 2, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath();
            p.fillColor = Base; p.Fill(); p.strokeColor = Rim; p.lineWidth = 2.5f; p.Stroke();
            var knob = centre + new Vector2(value.x, -value.y) * radius * .55f;
            p.BeginPath(); p.Arc(knob, radius * .36f, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath();
            p.fillColor = Knob; p.Fill();
        }
    }
}
