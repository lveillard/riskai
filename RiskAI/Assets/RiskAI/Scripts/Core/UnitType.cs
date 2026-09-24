using System;

namespace RiskAI.Core
{
    /// <summary>Oriented ship hull from units.json; builds the NavalArt scale and target collider.</summary>
    public readonly struct HullShape
    {
        public readonly float Length, Beam, Height, CenterHeight, Scale, Clearance;
        public readonly bool Warship, Exists;
        public HullShape(float length, float beam, float height, float centerHeight, float scale, bool warship, float clearance)
        { Length = length; Beam = beam; Height = height; CenterHeight = centerHeight; Scale = scale; Warship = warship; Clearance = clearance; Exists = true; }
    }

    /// <summary>Automatic acquisition resolved per owner/order (units.json acquisition).</summary>
    public readonly struct AcquisitionProfile
    {
        public readonly float RadiusHostile, RadiusNeutral, RadiusHold;
        public readonly bool HasLeash;
        public readonly float LeashHostile, LeashNeutral;
        public readonly float QueryPadding, PressureBias, AllyAlertRadius;
        public readonly bool Retaliate;
        public readonly UnitVisibility Visibility;
        public readonly AcquisitionTieBreak TieBreak;
        /// <summary>How the acquisition radius is measured (not necessarily the weapon measure: melee acquires to the approach point).</summary>
        public readonly RangeMeasure Measure;
        public AcquisitionProfile(float hostile, float neutral, float hold, bool hasLeash, float leashHostile, float leashNeutral,
            float queryPadding, float pressureBias, float allyAlertRadius, bool retaliate, RangeMeasure measure, UnitVisibility visibility, AcquisitionTieBreak tieBreak)
        {
            TieBreak = tieBreak; Measure = measure;
            RadiusHostile = hostile; RadiusNeutral = neutral; RadiusHold = hold; HasLeash = hasLeash;
            LeashHostile = leashHostile; LeashNeutral = leashNeutral; QueryPadding = queryPadding;
            PressureBias = pressureBias; AllyAlertRadius = allyAlertRadius; Retaliate = retaliate; Visibility = visibility;
        }
    }

    public readonly struct TransportProfile
    {
        public readonly int Capacity, LoadLimit;
        public readonly float LoadRadius;
        public bool Enabled => Capacity > 0;
        public TransportProfile(int capacity, float loadRadius, int loadLimit) { Capacity = capacity; LoadRadius = loadRadius; LoadLimit = loadLimit; }
    }

    /// <summary>Ahea heal (units.json capabilities.heal).</summary>
    public readonly struct HealProfile
    {
        public readonly float Amount, Range, Cooldown, ManaCost, Rescan, MaxVerticalDelta;
        public readonly bool OrganicOnly, Enabled;
        public HealProfile(float amount, float range, float cooldown, float manaCost, float rescan, float maxVerticalDelta, bool organicOnly)
        { Amount = amount; Range = range; Cooldown = cooldown; ManaCost = manaCost; Rescan = rescan; MaxVerticalDelta = maxVerticalDelta; OrganicOnly = organicOnly; Enabled = true; }
    }

    /// <summary>Aroa roar (units.json capabilities.roar).</summary>
    public readonly struct RoarProfile
    {
        public readonly float Area, Duration, ManaCost, DamageBonus, Evaluation;
        public readonly bool Enabled;
        public RoarProfile(float area, float duration, float manaCost, float damageBonus, float evaluation)
        { Area = area; Duration = duration; ManaCost = manaCost; DamageBonus = damageBonus; Evaluation = evaluation; Enabled = true; }
    }

    /// <summary>
    /// One unit type resolved from units.json into a flat, immutable struct at
    /// <see cref="UnitCatalog.Bind"/>. The simulation reads it by reference; nothing here allocates.
    /// </summary>
    public readonly struct UnitType
    {
        public readonly string Id;
        /// <summary>Dense in-process index assigned by Bind. Never persisted.</summary>
        public readonly int Index;
        public readonly string Name, NameEn, Role, RoleEn;
        public readonly string SourceRawcode, SourceBase, SourceNotes;
        public readonly UnitDomain Domain;
        public readonly UnitBuilding Building;
        public readonly int Cost, Points, Level;
        public readonly float TrainSeconds;
        public readonly float MaxHealth, Armor;
        public readonly ArmorKind ArmorType;
        public readonly bool Mechanical, CanBeAttacked;
        public readonly float Speed, Separation;
        public readonly bool ForestPenalty;
        public readonly float CollisionRadius, BodyRadius, FootprintSize;
        public readonly HullShape Hull;
        public readonly float VisualHeight, VisualRadius, StandingHeight, StandingWidth, MdxHeight, MdxWidth, SpawnRadius;
        public readonly WeaponProfile Weapon, TownWeapon, HarborWeapon;
        public readonly AcquisitionProfile Acquisition;
        public readonly bool CanCapture, CanGarrison, CanEmbark, HarborGuard;
        public readonly TransportProfile Transport;
        public readonly HealProfile Heal;
        public readonly RoarProfile Roar;
        public readonly ManaProfile Mana;
        public readonly string Model, Portrait, PortraitFallback, AttackClip;
        public readonly float AttackContact;
        public readonly UnitSilhouette Silhouette;

        public bool CanAttack => Weapon.IsValid;
        public bool CanTransport => Transport.Enabled;
        /// <summary>The attack type the unit deals (Normal for unarmed types, as before).</summary>
        public AttackKind AttackType => Weapon.DamageType;

        UnitType(UnitConfig c, int index)
        {
            Id = c.Id; Index = index;
            Name = c.Names.Es; NameEn = c.Names.En;
            Role = c.Role?.Es; RoleEn = c.Role?.En;
            SourceRawcode = c.Source.Rawcode; SourceBase = c.Source.Base; SourceNotes = c.Source.Notes;
            Domain = c.Domain; Building = c.Building;
            Cost = c.Cost; Points = c.Points; Level = c.Level; TrainSeconds = c.TrainSeconds;
            MaxHealth = c.MaxHealth; Armor = c.Armor; ArmorType = c.ArmorType;
            Mechanical = c.Mechanical; CanBeAttacked = c.CanBeAttacked;
            Speed = c.Movement.Speed; ForestPenalty = c.Movement.ForestPenalty; Separation = c.Movement.Separation ?? 0;
            CollisionRadius = c.Collision?.Radius ?? 0; BodyRadius = c.Body?.Radius ?? 0; FootprintSize = c.Footprint?.Size ?? 0;
            Hull = c.Hull == null ? default : new HullShape(c.Hull.Length, c.Hull.Beam, c.Hull.Height, c.Hull.CenterHeight, c.Hull.Scale, c.Hull.Warship, c.Hull.Clearance);
            VisualHeight = c.Visual.Height; VisualRadius = c.Visual.Radius;
            StandingHeight = c.Visual.StandingHeight; StandingWidth = c.Visual.StandingWidth;
            MdxHeight = c.Visual.Mdx?.Height ?? 0; MdxWidth = c.Visual.Mdx?.Width ?? 0;
            SpawnRadius = c.SpawnRadius ?? 0;
            Weapon = c.Weapons.Length > 0 ? Resolve(c.Weapons[0]) : default;
            TownWeapon = c.HostWeapons != null ? Resolve(c.HostWeapons.Town) : default;
            HarborWeapon = c.HostWeapons != null ? Resolve(c.HostWeapons.Harbor) : default;
            var a = c.Acquisition;
            Acquisition = a == null ? default : new AcquisitionProfile(a.Radius.Hostile, a.Radius.Neutral, a.Radius.Hold,
                a.Leash != null, a.Leash?.Hostile ?? 0, a.Leash?.Neutral ?? 0, a.QueryPadding, a.PressureBias,
                a.AllyAlertRadius, a.Retaliate, a.Measure, a.Visibility, a.TieBreak);
            var caps = c.Capabilities;
            CanCapture = caps.CanCapture; CanGarrison = caps.CanGarrison; CanEmbark = caps.CanEmbark; HarborGuard = caps.HarborGuard;
            Transport = caps.Transport == null ? default : new TransportProfile(caps.Transport.Capacity, caps.Transport.LoadRadius, caps.Transport.LoadLimit);
            Heal = caps.Heal == null ? default : new HealProfile(caps.Heal.Amount, caps.Heal.Range, caps.Heal.Cooldown, caps.Heal.ManaCost,
                caps.Heal.Rescan, caps.Heal.MaxVerticalDelta, caps.Heal.OrganicOnly);
            Roar = caps.Roar == null ? default : new RoarProfile(caps.Roar.Area, caps.Roar.Duration, caps.Roar.ManaCost, caps.Roar.DamageBonus, caps.Roar.Evaluation);
            Mana = caps.Mana == null ? default : new ManaProfile(caps.Mana.Max, caps.Mana.Initial, caps.Mana.Regen);
            var p = c.Presentation;
            Model = p.Model; Portrait = p.Portrait; PortraitFallback = p.PortraitFallback; AttackClip = p.AttackClip; AttackContact = p.Contact;
            Silhouette = p.Silhouette;
        }

        internal static UnitType From(UnitConfig config, int index) => new UnitType(config, index);

        static WeaponProfile Resolve(UnitWeapon w)
        {
            float full = 0, medium = 0, small = 0, mediumFactor = 0, smallFactor = 0;
            var splashMask = WeaponTargetMask.None;
            if (w.Splash != null)
            {
                full = w.Splash.Rings[0].Radius;
                medium = w.Splash.Rings[1].Radius; mediumFactor = w.Splash.Rings[1].Factor;
                small = w.Splash.Rings[2].Radius; smallFactor = w.Splash.Rings[2].Factor;
                splashMask = Combine(w.Splash.Mask);
            }
            float minFlight = w.FlightTime?.Min ?? 0;
            float maxFlight = w.FlightTime?.Max ?? float.PositiveInfinity;
            return new WeaponProfile(w.Source, w.AttackType, w.Base, w.Dice, w.Sides, w.Cooldown, w.AttackPoint, w.Backswing,
                w.Range, w.MinRange, w.Ranged, w.RangeMeasure, w.Reach?.ApproachMargin ?? 0, w.Reach?.HoldMargin ?? 0,
                w.StrikeTolerance ?? 0, w.Delivery, w.Targeting, w.ProjectileSpeed, full, medium, small, mediumFactor, smallFactor,
                minFlight, maxFlight, splashMask, Combine(w.TargetMask), w.Tracer, w.Sound);
        }

        static WeaponTargetMask Combine(WeaponTargetMask[] flags)
        {
            var mask = WeaponTargetMask.None;
            if (flags != null) for (int i = 0; i < flags.Length; i++) mask |= flags[i];
            return mask;
        }
    }
}
