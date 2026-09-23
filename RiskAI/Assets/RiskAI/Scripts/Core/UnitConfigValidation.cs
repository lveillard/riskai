using System.Collections.Generic;

namespace RiskAI.Core
{
    /// <summary>
    /// Cross-field invariants of units.json. Mirrors scripts/config/validate.mjs so a bad file
    /// fails at load time in the player too, not only in CI.
    /// </summary>
    public static class UnitConfigValidation
    {
        public static List<string> Errors(UnitsFile file)
        {
            var errors = new List<string>();
            if (file == null) { errors.Add("units.json is empty"); return errors; }
            if (file.Version != 1) errors.Add("units.json version " + file.Version + " is not supported");
            if (file.Units == null || file.Units.Length == 0) { errors.Add("units.json has no units"); return errors; }
            var seen = new HashSet<string>();
            foreach (var unit in file.Units)
            {
                if (unit == null) { errors.Add("null unit entry"); continue; }
                string at = "units[" + unit.Id + "]";
                if (!seen.Add(unit.Id)) errors.Add(at + ": duplicate id");
                var caps = unit.Capabilities;
                if ((caps.Heal != null || caps.Roar != null) && caps.Mana == null) errors.Add(at + ": heal/roar require mana");
                if (caps.Transport != null && unit.Domain != UnitDomain.Sea) errors.Add(at + ": transport is only for sea units");
                if (unit.Domain == UnitDomain.Land && (unit.Collision == null || unit.Body == null)) errors.Add(at + ": land units need collision and body radii");
                if (unit.Domain == UnitDomain.Sea && unit.Hull == null) errors.Add(at + ": sea units need a hull");
                if (unit.HostWeapons != null && unit.Domain != UnitDomain.Static) errors.Add(at + ": hostWeapons are only for static posts");
                bool armed = unit.Weapons.Length > 0 || unit.HostWeapons != null;
                if (unit.Acquisition != null && !armed) errors.Add(at + ": acquisition without a weapon");
                if (unit.Acquisition == null && armed) errors.Add(at + ": a weapon needs acquisition");
                foreach (var weapon in unit.Weapons) Check(unit, weapon, at, errors);
                if (unit.HostWeapons != null) { Check(unit, unit.HostWeapons.Town, at, errors); Check(unit, unit.HostWeapons.Harbor, at, errors); }
            }
            return errors;
        }

        static void Check(UnitConfig unit, UnitWeapon weapon, string at, List<string> errors)
        {
            string w = at + ".weapon[" + weapon.Source + "]";
            if (weapon.RangeMeasure == RangeMeasure.BodyEdges && unit.Body == null) errors.Add(w + ": BodyEdges needs body.radius");
            if (weapon.RangeMeasure == RangeMeasure.BodyEdges && weapon.Reach == null) errors.Add(w + ": BodyEdges needs reach margins");
            if (weapon.Targeting == WeaponTargeting.LaunchPoint && weapon.Delivery != WeaponDelivery.Artillery) errors.Add(w + ": LaunchPoint needs Artillery delivery");
            if ((weapon.Delivery == WeaponDelivery.Instant) != (weapon.ProjectileSpeed == 0)) errors.Add(w + ": projectileSpeed must be 0 exactly for Instant delivery");
            if (weapon.MinRange > weapon.Range) errors.Add(w + ": minRange above range");
            if (weapon.FlightTime != null && weapon.FlightTime.Max.HasValue && weapon.FlightTime.Max.Value < weapon.FlightTime.Min) errors.Add(w + ": flightTime max below min");
            if (weapon.Splash != null)
            {
                var rings = weapon.Splash.Rings;
                if (rings.Length != 3) { errors.Add(w + ": splash needs three rings"); return; }
                if (rings[0].Factor != 1) errors.Add(w + ": the first splash ring deals full damage");
                for (int i = 1; i < rings.Length; i++) if (rings[i].Radius < rings[i - 1].Radius) errors.Add(w + ": splash rings must not shrink");
                if (rings[rings.Length - 1].Radius <= 0) errors.Add(w + ": splash needs a positive outer radius");
            }
        }
    }
}
