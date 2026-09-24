using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    /// <summary>
    /// v0.34 golden inventory: every unit field and per-type rule the game consumes, read from
    /// the live code paths. The committed Fixtures/unit-golden.json is frozen; the reading code
    /// follows the API as it moves, the expected values never do (plan §5 step 1, §6 parity).
    /// </summary>
    public static class UnitGoldenSnapshot
    {
        public const string FixturePath = "RiskAI/Tests/Editor/Fixtures/unit-golden.json";

        public static string Capture()
        {
            var root = new Obj();
            root.Add("version", 1);
            var land = new Arr();
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind))) if (UnitCatalog.Get(kind).Domain == UnitDomain.Land) land.Add(Land(kind));
            root.Add("land", land);
            var naval = new Arr();
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind))) if (UnitCatalog.Get(kind).Domain == UnitDomain.Sea) naval.Add(Naval(kind));
            root.Add("naval", naval);
            root.Add("tower", Tower());
            var production = new Obj();
            production.Add("city", Layout(ProductionBuilding.City));
            production.Add("harbor", Layout(ProductionBuilding.Harbor));
            root.Add("production", production);
            root.Add("rules", Rules());

            var text = new StringBuilder();
            Write(text, root, 0);
            text.Append('\n');
            return text.ToString();
        }

        static Obj Land(UnitKind kind)
        {
            var profile = UnitCatalog.Get(kind);
            var unit = new Obj();
            unit.Add("id", kind.ToString());
            unit.Add("names", Names(UnitCatalog.Get(kind).Name));
            unit.Add("role", Names(UnitCatalog.Get(kind).Role));
            unit.Add("source", profile.SourceBase);
            unit.Add("sourceRawId", UnitCatalog.Get(kind).SourceRawcode);
            unit.Add("model", UnitCatalog.Get(kind).Model);
            unit.Add("cost", UnitCatalog.Get(kind).Cost);
            unit.Add("points", UnitCatalog.Get(kind).Points);
            unit.Add("level", UnitCatalog.Get(kind).Level);
            unit.Add("trainSeconds", UnitCatalog.Get(kind).TrainSeconds);
            unit.Add("maxHealth", UnitCatalog.Get(kind).MaxHealth);
            unit.Add("armor", profile.Armor);
            unit.Add("armorType", profile.ArmorType.ToString());
            unit.Add("mechanical", UnitCatalog.Get(kind).Mechanical);
            unit.Add("speed", UnitCatalog.Get(kind).Speed);

            var weapon = UnitCatalog.Get(kind).Weapon;
            var attack = Weapon(weapon);
            attack.Add("attackType", profile.AttackType.ToString());
            attack.Add("base", profile.Weapon.Base);
            attack.Add("dice", profile.Weapon.Dice);
            attack.Add("sides", profile.Weapon.Sides);
            attack.Add("minimumDamage", profile.Weapon.MinimumDamage);
            attack.Add("maximumDamage", profile.Weapon.MaximumDamage);
            attack.Add("averageDamage", UnitCatalog.Get(kind).Weapon.AverageDamage);
            attack.Add("damageRange", UnitCatalog.Get(kind).Weapon.DamageText);
            attack.Add("cooldown", UnitCatalog.Get(kind).Weapon.Cooldown);
            attack.Add("attackPoint", UnitCatalog.Get(kind).Weapon.AttackPoint);
            attack.Add("backswing", profile.Weapon.Backswing);
            attack.Add("range", UnitCatalog.Get(kind).Weapon.Range);
            attack.Add("minRange", UnitCatalog.Get(kind).Weapon.MinRange);
            attack.Add("ranged", UnitCatalog.Get(kind).Weapon.Ranged);
            attack.Add("instantTracer", weapon.Tracer);
            unit.Add("weapon", attack);

            // Soldier.Acquire / AutonomousLeash / AttackDistance, resolved per team relation.
            var a = profile.Acquisition;
            bool ranged = weapon.Ranged;
            var acquisition = new Obj();
            // A source uacq applies to every order; the local default differs by owner/order.
            acquisition.Add("sourceRange", a.RadiusHostile == a.RadiusNeutral && a.RadiusHostile == a.RadiusHold ? a.RadiusHostile : 0);
            var radius = new Obj();
            radius.Add("hostile", a.RadiusHostile);
            radius.Add("neutral", a.RadiusNeutral);
            radius.Add("hold", a.RadiusHold);
            acquisition.Add("radius", radius);
            var leash = new Obj();
            leash.Add("hostile", a.LeashHostile);
            leash.Add("neutral", a.LeashNeutral);
            acquisition.Add("leash", leash);
            acquisition.Add("pressureBias", a.PressureBias);
            acquisition.Add("measure", Measure(weapon.Measure));
            acquisition.Add("visibility", a.Visibility == UnitVisibility.TerrainRay ? "terrainRay" : "navMeshRay");
            acquisition.Add("meleeEngageDistanceVsFootman", ranged ? 0 : UnitRules.EngageDistance(UnitCatalog.Get(kind), UnitCatalog.Get(UnitKind.Footman).BodyRadius));
            unit.Add("acquisition", acquisition);

            var geometry = new Obj();
            geometry.Add("collisionRadius", UnitCatalog.Get(kind).CollisionRadius);
            geometry.Add("bodyRadius", UnitCatalog.Get(kind).BodyRadius);
            geometry.Add("sourceStandingHeight", UnitCatalog.Get(kind).MdxHeight);
            geometry.Add("sourceStandingWidth", UnitCatalog.Get(kind).MdxWidth);
            geometry.Add("standingHeight", UnitCatalog.Get(kind).StandingHeight);
            geometry.Add("standingWidth", UnitCatalog.Get(kind).StandingWidth);
            geometry.Add("visualHeight", UnitCatalog.Get(kind).VisualHeight);
            geometry.Add("visualRadius", UnitCatalog.Get(kind).VisualRadius);
            geometry.Add("spawnRadius", UnitCatalog.Get(kind).SpawnRadius);
            unit.Add("geometry", geometry);

            var abilities = new Obj();
            abilities.Add("heal", UnitCatalog.Get(kind).Heal.Enabled);
            abilities.Add("roar", UnitCatalog.Get(kind).Roar.Enabled);
            var mana = UnitCatalog.Get(kind).Mana;
            var manaObj = new Obj();
            manaObj.Add("max", mana.Maximum);
            manaObj.Add("initial", mana.Initial);
            manaObj.Add("regen", mana.Regeneration);
            abilities.Add("mana", manaObj);
            abilities.Add("canCapture", profile.CanCapture);
            abilities.Add("canEmbark", profile.CanEmbark);
            unit.Add("abilities", abilities);

            var production = new Obj();
            production.Add("building", (UnitCatalog.Get(kind).Building==UnitBuilding.City) ? "city" : (UnitCatalog.Get(kind).Building==UnitBuilding.Harbor) ? "harbor" : "none");
            production.Add("hotkey", ProductionHotkeys.Hotkey(kind));
            unit.Add("production", production);

            var presentation = new Obj();
            presentation.Add("attackClip", UnitCatalog.Get(kind).AttackClip);
            presentation.Add("contact", UnitCatalog.Get(kind).AttackContact);
            presentation.Add("portrait", profile.PortraitName);
            presentation.Add("portraitResource", UnitVariantViews.PortraitResource(kind));
            unit.Add("presentation", presentation);

            var traits = AiUnitAnalysis.For(kind);
            var ai = new Obj();
            ai.Add("role", traits.Role.ToString());
            ai.Add("cost", traits.Cost);
            ai.Add("level", traits.Level);
            ai.Add("dps", traits.Dps);
            ai.Add("effectiveHealth", traits.EffectiveHealth);
            ai.Add("range", traits.Range);
            ai.Add("ranged", traits.Ranged);
            ai.Add("splash", traits.Splash);
            ai.Add("healer", traits.Healer);
            ai.Add("value", traits.Value);
            unit.Add("ai", ai);
            return unit;
        }

        static Obj Naval(UnitKind kind)
        {
            var profile = UnitCatalog.Get(kind);
            var unit = new Obj();
            unit.Add("id", kind.ToString());
            unit.Add("names", Names(profile.Name));
            unit.Add("sourceRawId", profile.SourceRawcode);
            unit.Add("cost", profile.Cost);
            unit.Add("points", profile.Points);
            unit.Add("trainSeconds", profile.TrainSeconds);
            unit.Add("maxHealth", profile.MaxHealth);
            unit.Add("armor", profile.Armor);
            unit.Add("armorType", profile.ArmorType.ToString());
            unit.Add("speed", profile.Speed);
            unit.Add("canAttack", profile.CanAttack);
            unit.Add("canCapture", profile.CanCapture);
            unit.Add("canTransport", profile.CanTransport);
            unit.Add("capacity", profile.Transport.Capacity);

            var weapon = Weapon(UnitCatalog.Get(kind).Weapon);
            weapon.Add("attackType", profile.AttackType.ToString());
            weapon.Add("base", profile.Weapon.Base);
            weapon.Add("dice", profile.Weapon.Dice);
            weapon.Add("sides", profile.Weapon.Sides);
            weapon.Add("minimumDamage", profile.Weapon.MinimumDamage);
            weapon.Add("maximumDamage", profile.Weapon.MaximumDamage);
            weapon.Add("averageDamage", profile.Weapon.AverageDamage);
            weapon.Add("damageText", profile.Weapon.DamageText);
            weapon.Add("cooldown", profile.Weapon.Cooldown);
            weapon.Add("range", profile.Weapon.Range);
            unit.Add("weapon", weapon);

            // Ship.FindNearbyEnemy / RangeTo: only enemies already inside weapon range, XZ to their hull.
            var acquisition = new Obj();
            acquisition.Add("radius", profile.CanAttack ? profile.Acquisition.RadiusHostile : 0);
            // Unarmed hulls never search; step 1 recorded the shared 6 m hull padding for them too.
            acquisition.Add("queryPadding", profile.CanAttack ? profile.Acquisition.QueryPadding : 6f);
            acquisition.Add("measure", profile.CanAttack ? Measure(profile.Weapon.Measure) : "toHull");
            acquisition.Add("visibility", "terrainRay");
            unit.Add("acquisition", acquisition);

            var shape = profile.Hull;
            var hull = new Obj();
            hull.Add("warship", shape.Warship);
            hull.Add("scale", shape.Scale);
            hull.Add("length", shape.Length);
            hull.Add("beam", shape.Beam);
            hull.Add("height", shape.Height);
            hull.Add("centerHeight", shape.CenterHeight);
            unit.Add("hull", hull);

            var production = new Obj();
            production.Add("building", (UnitCatalog.Get(kind).Building==UnitBuilding.Harbor) ? "harbor" : "none");
            production.Add("hotkey", ProductionHotkeys.Hotkey(kind));
            unit.Add("production", production);
            var presentation = new Obj();
            presentation.Add("portraitResource", UnitVariantViews.PortraitResource(kind));
            unit.Add("presentation", presentation);
            var ai = new Obj();
            ai.Add("shipValue", AiUnitAnalysis.ShipValue(profile));
            unit.Add("ai", ai);
            return unit;
        }

        static Obj Tower()
        {
            var tower = new Obj();
            // Only consumed fields: health from the o000 bunker, the weapon from the h00N/h00O post.
            var post = UnitCatalog.Get(UnitKind.Tower).TownWeapon;
            tower.Add("healthSource", UnitCatalog.Get(UnitKind.Tower).SourceBase);
            tower.Add("maxHealth", UnitCatalog.Get(UnitKind.Tower).MaxHealth);
            var attack = new Obj();
            attack.Add("source", UnitCatalog.Get(UnitKind.Tower).SourceNotes);
            attack.Add("attackType", post.DamageType.ToString());
            attack.Add("base", post.Base);
            attack.Add("dice", post.Dice);
            attack.Add("sides", post.Sides);
            attack.Add("averageDamage", post.AverageDamage);
            attack.Add("cooldown", post.Cooldown);
            attack.Add("range", post.Range);
            attack.Add("attackPoint", post.AttackPoint);
            attack.Add("backswing", post.Backswing);
            tower.Add("attack", attack);
            var probe = new GameObject("golden tower probe");
            try
            {
                var actor = probe.AddComponent<DefenseTower>();
                tower.Add("attackType", actor.AttackType.ToString());
                tower.Add("armorType", actor.ArmorType.ToString());
                tower.Add("armor", actor.Armor);
                tower.Add("canBeAttacked", actor.CanBeAttacked);
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            var weapons = new Obj();
            weapons.Add("town", Weapon(UnitCatalog.Get(UnitKind.Tower).TownWeapon));
            weapons.Add("harbor", Weapon(UnitCatalog.Get(UnitKind.Tower).HarborWeapon));
            tower.Add("hostWeapons", weapons);
            var acquisition = new Obj();
            acquisition.Add("radius", UnitCatalog.Get(UnitKind.Tower).Acquisition.RadiusHostile);
            acquisition.Add("measure", Measure(UnitCatalog.Get(UnitKind.Tower).TownWeapon.Measure));
            acquisition.Add("visibility", "terrainRay");
            tower.Add("acquisition", acquisition);
            var ai = new Obj();
            ai.Add("valueVs300Hp", AiUnitAnalysis.TowerValue(300, null));
            tower.Add("ai", ai);
            return tower;
        }

        static Obj Weapon(WeaponProfile weapon)
        {
            var result = new Obj();
            result.Add("valid", weapon.IsValid);
            result.Add("damageType", weapon.DamageType.ToString());
            result.Add("delivery", weapon.Delivery.ToString());
            result.Add("targeting", weapon.Targeting.ToString());
            result.Add("projectileSpeed", weapon.ProjectileSpeed);
            result.Add("fullRadius", weapon.FullDamageRadius);
            result.Add("mediumRadius", weapon.MediumDamageRadius);
            result.Add("smallRadius", weapon.SmallDamageRadius);
            result.Add("mediumFactor", weapon.MediumDamageFactor);
            result.Add("smallFactor", weapon.SmallDamageFactor);
            result.Add("minFlight", weapon.MinimumFlightTime);
            result.Add("maxFlight", weapon.MaximumFlightTime);
            result.Add("splashMask", weapon.SplashTargets.ToString());
            result.Add("hasSplash", weapon.HasSplash);
            result.Add("weaponSource", weapon.SourceRawId);
            return result;
        }

        static Arr Layout(ProductionBuilding building)
        {
            var slots = new Arr();
            foreach (var slot in ProductionHotkeys.Layout(building))
            {
                var item = new Obj();
                item.Add("product", slot.Option.IsShip ? slot.Option.Kind.ToString() : slot.Option.Kind.ToString());
                item.Add("index", slot.Index);
                item.Add("page", slot.Page);
                item.Add("cell", slot.Cell);
                item.Add("key", slot.Key);
                item.Add("cost", slot.Option.Cost);
                slots.Add(item);
            }
            return slots;
        }

        static Obj Rules()
        {
            var rules = new Obj();
            ref readonly var footman = ref UnitCatalog.Get(UnitKind.Footman);
            rules.Add("meleeReachMargin", footman.Weapon.HoldMargin);
            rules.Add("meleeApproachMargin", footman.Weapon.ApproachMargin);
            rules.Add("meleeStrikeTolerance", footman.Weapon.StrikeTolerance);
            rules.Add("allyAlertRadius", footman.Acquisition.AllyAlertRadius);
            rules.Add("unitOrderQueueLimit", OrderQueue.Limit);
            rules.Add("commandInboxLimit", RiskAI.BattleCommands.InboxLimit);
            rules.Add("transportLoadRadius", UnitCatalog.TransportLoadRadius);
            rules.Add("transportLoadOrderLimit", UnitCatalog.TransportLoadLimit);
            rules.Add("shipSeparation", UnitCatalog.Get(UnitKind.Frigate).Separation);
            rules.Add("hullClearance", SeaNavigation.HullClearance);
            var heal = new Obj();
            heal.Add("range", UnitCatalog.Get(UnitKind.Medic).Heal.Range);
            heal.Add("amount", UnitCatalog.Get(UnitKind.Medic).Heal.Amount);
            heal.Add("cooldown", UnitCatalog.Get(UnitKind.Medic).Heal.Cooldown);
            heal.Add("manaCost", UnitCatalog.Get(UnitKind.Medic).Heal.ManaCost);
            heal.Add("maxVerticalDelta", UnitCatalog.Get(UnitKind.Medic).Heal.MaxVerticalDelta);
            rules.Add("heal", heal);
            var roar = new Obj();
            roar.Add("area", UnitCatalog.Get(UnitKind.Roarer).Roar.Area);
            roar.Add("duration", UnitCatalog.Get(UnitKind.Roarer).Roar.Duration);
            roar.Add("manaCost", UnitCatalog.Get(UnitKind.Roarer).Roar.ManaCost);
            roar.Add("damageBonus", UnitCatalog.Get(UnitKind.Roarer).Roar.DamageBonus);
            roar.Add("evaluation", UnitCatalog.Get(UnitKind.Roarer).Roar.Evaluation);
            roar.Add("multiplierRoaring", 1 + UnitCatalog.Get(UnitKind.Roarer).Roar.DamageBonus);
            rules.Add("roar", roar);
            return rules;
        }

        static string Measure(RangeMeasure measure) =>
            measure == RangeMeasure.BodyEdges ? "bodyEdges" : measure == RangeMeasure.CenterToApproach ? "centerToApproach" :
            measure == RangeMeasure.ToHull ? "toHull" : "centerToCenter";

        static Obj Names(string spanish)
        {
            var names = new Obj();
            names.Add("es", spanish);
            names.Add("en", GameText.EnglishOf(spanish));
            return names;
        }

        // ---- deterministic JSON writer (ordered keys, shortest round-trip floats) ----
        sealed class Obj { public readonly List<KeyValuePair<string, object>> Items = new List<KeyValuePair<string, object>>(); public void Add(string key, object value) => Items.Add(new KeyValuePair<string, object>(key, value)); }
        sealed class Arr { public readonly List<object> Items = new List<object>(); public void Add(object value) => Items.Add(value); }

        static void Write(StringBuilder text, object value, int indent)
        {
            switch (value)
            {
                case null: text.Append("null"); return;
                case string s: text.Append(Quote(s)); return;
                case bool b: text.Append(b ? "true" : "false"); return;
                case int i: text.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case float f: text.Append(Number(f)); return;
                case Obj obj:
                    if (obj.Items.Count == 0) { text.Append("{}"); return; }
                    text.Append("{\n");
                    for (int k = 0; k < obj.Items.Count; k++)
                    {
                        text.Append(' ', indent + 2).Append(Quote(obj.Items[k].Key)).Append(": ");
                        Write(text, obj.Items[k].Value, indent + 2);
                        text.Append(k + 1 < obj.Items.Count ? ",\n" : "\n");
                    }
                    text.Append(' ', indent).Append('}');
                    return;
                case Arr arr:
                    if (arr.Items.Count == 0) { text.Append("[]"); return; }
                    text.Append("[\n");
                    for (int k = 0; k < arr.Items.Count; k++)
                    {
                        text.Append(' ', indent + 2);
                        Write(text, arr.Items[k], indent + 2);
                        text.Append(k + 1 < arr.Items.Count ? ",\n" : "\n");
                    }
                    text.Append(' ', indent).Append(']');
                    return;
                default: throw new ArgumentException("Unsupported golden value " + value.GetType());
            }
        }

        /// <summary>Shortest decimal that parses back to the same float; non-finite values as strings.</summary>
        public static string Number(float value)
        {
            if (float.IsPositiveInfinity(value)) return "\"inf\"";
            if (float.IsNegativeInfinity(value)) return "\"-inf\"";
            if (float.IsNaN(value)) return "\"nan\"";
            // Start at the integer digit count so 200 prints as 200, not 2E+02.
            int start = value == 0 ? 1 : Math.Max(1, Math.Min(9, (int)Math.Floor(Math.Log10(Math.Abs(value))) + 1));
            for (int digits = start; digits <= 9; digits++)
            {
                string candidate = value.ToString("G" + digits, CultureInfo.InvariantCulture);
                if (float.Parse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture) == value) return candidate;
            }
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }

        static string Quote(string value)
        {
            var text = new StringBuilder(value.Length + 2).Append('"');
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') text.Append('\\').Append(c);
                else if (c == '\n') text.Append("\\n");
                else if (c < ' ') text.Append("\\u").Append(((int)c).ToString("x4"));
                else text.Append(c);
            }
            return text.Append('"').ToString();
        }
    }
}
