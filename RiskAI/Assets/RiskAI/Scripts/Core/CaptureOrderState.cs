namespace RiskAI.Core
{
    /// <summary>
    /// When a capture order is finished. Reaching the claim point is the motor's job;
    /// the order ends when the post is gone, someone else takes it, we take it, or a
    /// trip that started on our own post has finished its approach.
    /// </summary>
    public struct CaptureOrderState
    {
        int owner;
        bool known;

        public bool Active => known;

        public void Clear() { known = false; }

        public void Begin(bool found, int ownerNow)
        {
            known = found;
            owner = ownerNow;
        }

        /// <param name="approachFinished">Land capture passes true: there is no voyage left after the post is ours. A ship passes true only once the sail and the shore unload are done, so a friendly port still disembarks.</param>
        public bool Done(bool found, int ownerNow, int team, bool approachFinished)
        {
            if (!known || !found) return true;
            if (ownerNow != owner && ownerNow != team) return true;
            if (owner == team || ownerNow == team) return approachFinished;
            return false;
        }
    }
}
