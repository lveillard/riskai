namespace RiskAI.Core
{
    /// <summary>Device-independent arbitration: a secondary tap orders, a drag pans.</summary>
    public sealed class PointerGesture
    {
        public const float DragThreshold = 7;
        float x, y;
        public bool Pending { get; private set; }
        public bool Dragging { get; private set; }
        public void Begin(float px, float py) { x=px; y=py; Pending=true; Dragging=false; }
        public bool Move(float px, float py)
        {
            if (Pending && (px-x)*(px-x)+(py-y)*(py-y)>DragThreshold*DragThreshold) Dragging=true;
            return Dragging;
        }
        public bool Release() { bool click=Pending&&!Dragging; Cancel(); return click; }
        public void Cancel() { Pending=false; Dragging=false; }
    }
}
