using System;

namespace RiskAI.Core
{
    /// <summary>Portrait camera from units.json. The renderer does not keep a second table of these numbers.</summary>
    public readonly struct PortraitView
    {
        public readonly bool Exists, Ship, Refit;
        public readonly float OrthographicSize, FocusHeight, UpperFraction;
        public readonly float RefitX, RefitY, RefitZ;

        /// <summary>Ship is domain == Sea. The view offset is not stored here; PortraitFraming owns the two constants.</summary>
        public static PortraitView From(PortraitCamera camera, UnitDomain domain)
        {
            if (camera == null) return default;
            var refit = camera.Refit;
            return new PortraitView(true, domain == UnitDomain.Sea, refit != null, camera.OrthographicSize, camera.FocusHeight,
                refit != null ? refit.UpperFraction : 0f, refit != null ? refit.OffsetX : 0f, refit != null ? refit.OffsetY : 0f, refit != null ? refit.OffsetZ : 0f);
        }

        PortraitView(bool exists, bool ship, bool refit, float size, float focus, float upper, float refitX, float refitY, float refitZ)
        {
            Exists = exists; Ship = ship; Refit = refit; OrthographicSize = size; FocusHeight = focus; UpperFraction = upper;
            RefitX = refitX; RefitY = refitY; RefitZ = refitZ;
        }
    }

    /// <summary>Oriented ship hull from units.json; builds the NavalArt scale and target collider.</summary>
    public readonly struct HullShape
    {
        public readonly float Length, Beam, Height, CenterHeight, Scale, Clearance;
        public readonly ShipModel Model;
        public readonly bool Exists;
        public HullShape(float length, float beam, float height, float centerHeight, float scale, ShipModel model, float clearance)
        { Length = length; Beam = beam; Height = height; CenterHeight = centerHeight; Scale = scale; Model = model; Clearance = clearance; Exists = true; }
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
        public readonly WeaponTargetMask Mask;
        public HealProfile(float amount, float range, float cooldown, float manaCost, float rescan, float maxVerticalDelta, bool organicOnly, WeaponTargetMask mask)
        { Amount = amount; Range = range; Cooldown = cooldown; ManaCost = manaCost; Rescan = rescan; MaxVerticalDelta = maxVerticalDelta; OrganicOnly = organicOnly; Mask = mask; Enabled = true; }
    }

    /// <summary>Aroa roar (units.json capabilities.roar).</summary>
    public readonly struct RoarProfile
    {
        public readonly float Area, Duration, ManaCost, DamageBonus, Evaluation;
        public readonly WeaponTargetMask Mask;
        public readonly bool Enabled;
        public RoarProfile(float area, float duration, float manaCost, float damageBonus, float evaluation, WeaponTargetMask mask)
        { Area = area; Duration = duration; ManaCost = manaCost; DamageBonus = damageBonus; Evaluation = evaluation; Mask = mask; Enabled = true; }
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
        public readonly bool CanCapture, CanGarrison, CanEmbark, CanFollow, CanPatrol, HarborGuard, StartingGarrison, ExpeditionTransport;
        public readonly TransportProfile Transport;
        public readonly HealProfile Heal;
        public readonly RoarProfile Roar;
        public readonly ManaProfile Mana;
        public readonly string Model, PortraitName, PortraitFallback, AttackClip;
        public readonly PortraitSource PortraitSource;
        public readonly PortraitView Portrait;
        public readonly bool PortraitLandDefault;
        public readonly float AttackContact;
        public readonly UnitSilhouette Silhouette;
        /// <summary>clips.json id played when this unit dies.</summary>
        public readonly string DeathSound;

        public bool CanAttack => Weapon.IsValid;
        public bool CanTransport => Transport.Enabled;
        /// <summary>Sea motor (SeaNavigation). Land and static types move on the NavMesh.</summary>
        public bool SeaMotor => Domain == UnitDomain.Sea;
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
            Hull = c.Hull == null ? default : new HullShape(c.Hull.Length, c.Hull.Beam, c.Hull.Height, c.Hull.CenterHeight, c.Hull.Scale, c.Hull.Model, c.Hull.Clearance);
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
            CanCapture = caps.CanCapture; CanGarrison = caps.CanGarrison; CanEmbark = caps.CanEmbark;
            CanFollow = caps.CanFollow; CanPatrol = caps.CanPatrol; HarborGuard = caps.HarborGuard;
            StartingGarrison = caps.StartingGarrison == true; ExpeditionTransport = caps.ExpeditionTransport == true;
            Transport = caps.Transport == null ? default : new TransportProfile(caps.Transport.Capacity, caps.Transport.LoadRadius, caps.Transport.LoadLimit);
            Heal = caps.Heal == null ? default : new HealProfile(caps.Heal.Amount, caps.Heal.Range, caps.Heal.Cooldown, caps.Heal.ManaCost,
                caps.Heal.Rescan, caps.Heal.MaxVerticalDelta, caps.Heal.OrganicOnly, Combine(caps.Heal.Mask));
            Roar = caps.Roar == null ? default : new RoarProfile(caps.Roar.Area, caps.Roar.Duration, caps.Roar.ManaCost, caps.Roar.DamageBonus, caps.Roar.Evaluation, Combine(caps.Roar.Mask));
            Mana = caps.Mana == null ? default : new ManaProfile(caps.Mana.Max, caps.Mana.Initial, caps.Mana.Regen);
            var p = c.Presentation;
            Model = p.Model; PortraitName = p.Portrait; PortraitFallback = p.PortraitFallback; AttackClip = p.AttackClip; AttackContact = p.Contact;
            PortraitSource = p.PortraitSource; Portrait = PortraitView.From(p.PortraitCamera, c.Domain); PortraitLandDefault = p.PortraitCamera?.LandDefault == true; Silhouette = p.Silhouette; DeathSound = p.DeathSound;
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
                minFlight, maxFlight, splashMask, Combine(w.TargetMask), w.Projectile, w.Sound);
        }

        static WeaponTargetMask Combine(WeaponTargetMask[] flags)
        {
            var mask = WeaponTargetMask.None;
            if (flags != null) for (int i = 0; i < flags.Length; i++) mask |= flags[i];
            return mask;
        }
    }
}
