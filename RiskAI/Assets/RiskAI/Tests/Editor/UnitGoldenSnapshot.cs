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
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind))) land.Add(Land(kind));
            root.Add("land", land);
            var naval = new Arr();
            foreach (NavalUnitKind kind in Enum.GetValues(typeof(NavalUnitKind))) naval.Add(Naval(kind));
            root.Add("naval", naval);
            root.Add("tower", Tower());
            var production = new Obj();
            production.Add("city", Layout(ProductionBuilding.City));
            production.Add("harbor", Layout(ProductionBuilding.Harbor));
            root.Add("production", production);
            root.Add("rules", Rules());
            root.Add("behaviour", Behaviour());
            var text = new StringBuilder();
            Write(text, root, 0);
            text.Append('\n');
            return text.ToString();
        }

        static Obj Land(UnitKind kind)
        {
            var profile = BattleRules.Profile(kind);
            var unit = new Obj();
            unit.Add("id", kind.ToString());
            unit.Add("names", Names(BattleRules.Name(kind)));
            unit.Add("role", Names(BattleRules.Role(kind)));
            unit.Add("source", profile.Source);
            unit.Add("sourceRawId", BattleRules.SourceRawId(kind));
            unit.Add("model", BattleRules.Model(kind));
            unit.Add("cost", BattleRules.Cost(kind));
            unit.Add("points", BattleRules.PointValue(kind));
            unit.Add("level", BattleRules.RequiredLevel(kind));
            unit.Add("trainSeconds", BattleRules.TrainTime(kind));
            unit.Add("maxHealth", BattleRules.Health(kind));
            unit.Add("armor", profile.Armor);
            unit.Add("armorType", profile.Defense.ToString());
            unit.Add("mechanical", BattleRules.Mechanical(kind));
            unit.Add("speed", BattleRules.Speed(kind));

            var weapon = SourceWeapons.For(kind, profile.Attack);
            var attack = Weapon(weapon);
            attack.Add("attackType", profile.Attack.ToString());
            attack.Add("base", profile.BaseDamage);
            attack.Add("dice", profile.Dice);
            attack.Add("sides", profile.Sides);
            attack.Add("minimumDamage", profile.MinimumDamage);
            attack.Add("maximumDamage", profile.MaximumDamage);
            attack.Add("averageDamage", BattleRules.Damage(kind));
            attack.Add("damageRange", BattleRules.DamageRange(kind));
            attack.Add("cooldown", BattleRules.AttackInterval(kind));
            attack.Add("attackPoint", BattleRules.AttackPoint(kind));
            attack.Add("backswing", profile.Backswing);
            attack.Add("range", BattleRules.Range(kind));
            attack.Add("minRange", BattleRules.MinimumRange(kind));
            attack.Add("ranged", BattleRules.Ranged(kind));
            // CombatWorld draws an instant tracer only for these firearm/crossbow kinds.
            attack.Add("instantTracer", kind == UnitKind.Archer || kind == UnitKind.MarinePrivate || kind == UnitKind.EliteRifleman);
            unit.Add("weapon", attack);

            // Soldier.Acquire / AutonomousLeash / AttackDistance, resolved per team relation.
            float source = SourceWeapons.AcquisitionRange(kind);
            float range = BattleRules.Range(kind);
            bool ranged = BattleRules.Ranged(kind);
            var acquisition = new Obj();
            acquisition.Add("sourceRange", source);
            var radius = new Obj();
            radius.Add("hostile", source > 0 ? source : 7.5f);
            radius.Add("neutral", source > 0 ? source : 5f);
            radius.Add("hold", source > 0 ? source : range);
            acquisition.Add("radius", radius);
            var leash = new Obj();
            leash.Add("hostile", Mathf.Max(ranged ? range + 2 : 11, source));
            leash.Add("neutral", Mathf.Max(ranged ? range + 2 : 7, source));
            acquisition.Add("leash", leash);
            acquisition.Add("pressureBias", kind == UnitKind.Footman ? .48f : .1f);
            acquisition.Add("measure", ranged ? "centerToApproach" : "bodyEdges");
            acquisition.Add("visibility", ranged ? "terrainRay" : "navMeshRay");
            acquisition.Add("meleeEngageDistanceVsFootman", ranged ? 0 : Soldier.MeleeEngageDistance(kind, SourceGeometry.AgentRadius(UnitKind.Footman)));
            unit.Add("acquisition", acquisition);

            var geometry = new Obj();
            geometry.Add("collisionRadius", SourceGeometry.AgentRadius(kind));
            geometry.Add("bodyRadius", SourceGeometry.AgentRadius(kind));
            geometry.Add("sourceStandingHeight", SourceGeometry.StandingHeight(kind));
            geometry.Add("sourceStandingWidth", SourceGeometry.StandingWidth(kind));
            geometry.Add("standingHeight", VisualMetrics.StandingHeightTarget(kind));
            geometry.Add("standingWidth", VisualMetrics.StandingWidthTarget(kind));
            geometry.Add("visualHeight", VisualMetrics.HeightFor(kind));
            geometry.Add("visualRadius", VisualMetrics.RadiusFor(kind));
            geometry.Add("spawnRadius", VisualMetrics.SpawnRadiusFor(kind));
            unit.Add("geometry", geometry);

            var abilities = new Obj();
            abilities.Add("heal", SupportAbilities.CanHeal(kind));
            abilities.Add("roar", SupportAbilities.CanRoar(kind));
            var mana = SupportAbilities.Mana(kind);
            var manaObj = new Obj();
            manaObj.Add("max", mana.Maximum);
            manaObj.Add("initial", mana.Initial);
            manaObj.Add("regen", mana.Regeneration);
            abilities.Add("mana", manaObj);
            abilities.Add("canCapture", true);
            abilities.Add("canEmbark", true);
            unit.Add("abilities", abilities);

            var production = new Obj();
            production.Add("building", ProductionCatalog.AllowsSettlementUnit(kind) ? "city" : ProductionCatalog.AllowsHarborUnit(kind) ? "harbor" : "none");
            production.Add("hotkey", ProductionHotkeys.Hotkey(kind));
            unit.Add("production", production);

            var presentation = new Obj();
            presentation.Add("attackClip", AttackPresentationTiming.Clip(kind));
            presentation.Add("contact", AttackPresentationTiming.ContactNormalizedTime(kind));
            presentation.Add("portrait", UnitVariantViews.PortraitName(kind));
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

        static Obj Naval(NavalUnitKind kind)
        {
            var profile = NavalProfiles.Profile(kind);
            var unit = new Obj();
            unit.Add("id", kind.ToString());
            unit.Add("names", Names(profile.Name));
            unit.Add("sourceRawId", profile.SourceRawId);
            unit.Add("cost", profile.Cost);
            unit.Add("points", profile.PointValue);
            unit.Add("trainSeconds", profile.TrainSeconds);
            unit.Add("maxHealth", profile.Health);
            unit.Add("armor", profile.Armor);
            unit.Add("armorType", profile.Defense.ToString());
            unit.Add("speed", profile.Speed);
            unit.Add("canAttack", profile.CanAttack);
            unit.Add("canCapture", profile.CanCapture);
            unit.Add("canTransport", profile.CanTransport);
            unit.Add("capacity", profile.Capacity);

            var weapon = Weapon(SourceWeapons.For(kind, profile.Attack));
            weapon.Add("attackType", profile.Attack.ToString());
            weapon.Add("base", profile.BaseDamage);
            weapon.Add("dice", profile.Dice);
            weapon.Add("sides", profile.Sides);
            weapon.Add("minimumDamage", profile.MinimumDamage);
            weapon.Add("maximumDamage", profile.MaximumDamage);
            weapon.Add("averageDamage", profile.Damage);
            weapon.Add("damageText", profile.DamageText);
            weapon.Add("cooldown", profile.Cooldown);
            weapon.Add("range", profile.Range);
            unit.Add("weapon", weapon);

            // Ship.FindNearbyEnemy / RangeTo: only enemies already inside weapon range, XZ to their hull.
            var acquisition = new Obj();
            acquisition.Add("radius", profile.Range);
            acquisition.Add("queryPadding", 6f);
            acquisition.Add("measure", "toHull");
            acquisition.Add("visibility", "terrainRay");
            unit.Add("acquisition", acquisition);

            bool war = NavalArt.IsWarship(kind);
            float scale = NavalArt.HullScale(kind);
            var hull = new Obj();
            hull.Add("warship", war);
            hull.Add("scale", scale);
            hull.Add("length", (war ? 7.2f : 5.15f) * scale * .82f);
            hull.Add("beam", (war ? 1.65f : 2.65f) * scale);
            hull.Add("height", 3f);
            hull.Add("centerHeight", 1.3f);
            unit.Add("hull", hull);

            var production = new Obj();
            production.Add("building", ProductionCatalog.AllowsHarborShip(kind) ? "harbor" : "none");
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
            var post = UnitCatalog.CapturableTower;
            tower.Add("healthSource", UnitCatalog.Tower.Source);
            tower.Add("maxHealth", BattleRules.TowerHealth);
            var attack = new Obj();
            attack.Add("source", post.Source);
            attack.Add("attackType", post.Attack.ToString());
            attack.Add("base", post.BaseDamage);
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
            weapons.Add("town", Weapon(SourceWeapons.MilitaryBase));
            weapons.Add("harbor", Weapon(SourceWeapons.Shipyard));
            tower.Add("hostWeapons", weapons);
            var acquisition = new Obj();
            acquisition.Add("radius", UnitCatalog.CapturableTower.Range);
            acquisition.Add("measure", "centerToCenter");
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
                item.Add("product", slot.Option.IsShip ? slot.Option.Ship.ToString() : slot.Option.Unit.ToString());
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
            rules.Add("meleeReachMargin", Soldier.MeleeReachMargin);
            rules.Add("meleeApproachMargin", Soldier.MeleeApproachMargin);
            rules.Add("meleeStrikeTolerance", .55f);
            rules.Add("allyAlertRadius", 5f);
            rules.Add("unitOrderQueueLimit", 35);
            rules.Add("commandInboxLimit", 1024);
            rules.Add("transportLoadRadius", Ship.LoadRadius);
            rules.Add("transportLoadOrderLimit", Ship.LoadOrderLimit);
            rules.Add("shipSeparation", 2.8f);
            rules.Add("hullClearance", SeaNavigation.HullClearance);
            var heal = new Obj();
            heal.Add("range", SupportAbilities.HealRange);
            heal.Add("amount", SupportAbilities.HealAmount);
            heal.Add("cooldown", SupportAbilities.HealCooldown);
            heal.Add("manaCost", SupportAbilities.HealManaCost);
            heal.Add("maxVerticalDelta", MedicSupport.MaxVerticalDelta);
            rules.Add("heal", heal);
            var roar = new Obj();
            roar.Add("area", SupportAbilities.RoarArea);
            roar.Add("duration", SupportAbilities.RoarDuration);
            roar.Add("manaCost", SupportAbilities.RoarManaCost);
            roar.Add("damageBonus", SupportAbilities.RoarDamageBonus);
            roar.Add("evaluation", SupportAbilities.RoarEvaluationInterval);
            roar.Add("multiplierRoaring", SupportAbilities.DamageMultiplier(true));
            rules.Add("roar", roar);
            return rules;
        }

        // Conducts that are code today and must survive the refactor unchanged
        // (except plan §7). Recorded as the inventory the later steps are checked against.
        static Obj Behaviour()
        {
            var behaviour = new Obj();
            var measure = new Obj();
            measure.Add("bodyEdges", "3D distance to target.ApproachPoint minus own and target body radius (soldiers only)");
            measure.Add("centerToApproach", "3D distance from pivot to target.ApproachPoint");
            measure.Add("toHull", "XZ distance from pivot to target.ApproachPoint (a ship's oriented hull)");
            measure.Add("centerToCenter", "XZ distance between pivots");
            behaviour.Add("measure", measure);
            var queue = new Obj();
            queue.Add("appendable", "Move, AttackMove, Patrol; only when the unit is not Idle/Hold");
            queue.Add("clearing", "Attack, Follow, Stop, Hold clear the queue");
            queue.Add("patrol", "completing a patrol swaps its ends and never drains the queue");
            queue.Add("shipAttackMove", "a ship attack-move resumes its destination when its target dies");
            queue.Add("soldierAttackMove", "a soldier attack-move resumes its path when its target dies");
            queue.Add("ships", "ships receive direct orders, no queue");
            behaviour.Add("queue", queue);
            var click = new Arr();
            click.Add("enemy ship with an attack-capable fleet beats the harbor");
            click.Add("harbor with a fleet: dock/land");
            click.Add("town with a port and a fleet: dock at its port");
            click.Add("enemy target: soldiers Attack via inbox, attack ships Attack directly");
            click.Add("own transport with a selection: embark");
            click.Add("harbor with a fleet: dock");
            click.Add("ally soldier not selected: Follow");
            click.Add("enemy tower: attack-move to its claim point / landing");
            click.Add("ground: move (attack-move to a hostile town/harbor point)");
            behaviour.Add("click", click);
            return behaviour;
        }

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
