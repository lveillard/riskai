namespace RiskAI.Core
{
    /// <summary>
    /// When a capture order is finished. The approach is the sail and the shore unload.
    /// A combat target is not part of it. Land passes approach-finished as true.
    /// An actor that cannot claim (a transport) is done only when that approach finishes,
    /// even if the port is still hostile or another player takes it.
    /// A warship on a hostile port with a living guardian keeps the order until the owner changes (v0.33).
    /// Neutral keeps the order going. Another player ends a claiming capture at once.
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

        public bool Done(bool found, int ownerNow, int team, bool approachFinished, bool canClaim)
        {
            if (!known || !found) return true;
            // A transport cannot claim. The order ends only when the sail and the unload
            // are finished, even if another player takes the port on the way.
            if (!canClaim) return approachFinished;
            if (ownerNow != owner && PlayerRules.IsPlayer(ownerNow) && ownerNow != team) return true;
            if (owner == team || ownerNow == team) return approachFinished;
            return false;
        }
    }
}
