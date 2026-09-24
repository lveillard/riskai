namespace RiskAI
{
    /// <summary>
    /// One garrison slot. Land and sea actors bind through this; <see cref="CityClaimZone"/>
    /// does not branch on the actor class. What differs is the motor behind
    /// <see cref="TryBindPost"/> (navmesh anchor, or a berth).
    /// </summary>
    public interface IPostClaimant
    {
        /// <summary>Chosen from the land circle. A hull is not; the berth search supplies it.</summary>
        bool ContendsOnFoot { get; }
        /// <summary>Already holding a different post, so this circle must ignore it.</summary>
        bool BlockedByOtherPost(CityClaimZone zone);
        bool TryBindPost(CityClaimZone zone);
        void ReleasePost(CityClaimZone zone);
        /// <summary>
        /// The post should forget this guardian. A foot actor only drops the reference;
        /// a hull also leaves the berth. Returns true when the slot must be cleared.
        /// </summary>
        bool DropStale(CityClaimZone zone);
    }
}
