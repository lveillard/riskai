namespace RiskAI.Core
{
    /// <summary>
    /// When a capture order is finished. The approach is the sail and the shore unload.
    /// A combat target is not part of it. Land passes approach-finished as true.
    /// An actor that cannot claim (a transport) is done when that approach finishes,
    /// even if the port is still hostile. Neutral keeps the order going. Another player ends it at once.
    /// </summary>
    public struct CaptureOrderState
    {
        int owner;
        bool known;
        int zoneId;

        public bool Active => known;
        public int ZoneId => zoneId;

        public void Clear() { known = false; zoneId = 0; }

        public void Begin(bool found, int ownerNow, int zoneInstanceId = 0)
        {
            known = found;
            owner = ownerNow;
            zoneId = zoneInstanceId;
        }

        public bool Done(bool found, int ownerNow, int team, bool approachFinished, bool canClaim)
        {
            if (!known || !found) return true;
            if (ownerNow != owner && PlayerRules.IsPlayer(ownerNow) && ownerNow != team) return true;
            if (!canClaim) return approachFinished;
            if (owner == team || ownerNow == team) return approachFinished;
            return false;
        }
    }
}
