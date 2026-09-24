using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    /// <summary>units.json ↔ generated DTO: loading, strictness and schema drift.</summary>
    public sealed class UnitConfigLoaderTests
    {
        static string ShippedJson => File.ReadAllText(Path.Combine(Application.dataPath, "RiskAI/Resources/Config/units.json"));
        static string SchemaPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "scripts", "config", "units.schema.json"));

        static string Edit(Action<JObject> change)
        {
            var root = JObject.Parse(ShippedJson);
            change(root);
            return root.ToString();
        }
        static JObject UnitNamed(JObject root, string id) => (JObject)root["units"].First(unit => (string)unit["id"] == id);

        [Test]
        public void ShippedResourceLoadsEveryUnitType()
        {
            var file = UnitConfigLoader.LoadResource();
            var ids = file.Units.Select(unit => unit.Id).ToList();
            // UnitKind is generated from units.json: same ids, same order (build.mjs --check keeps it current).
            CollectionAssert.AreEqual(ids, Enum.GetNames(typeof(UnitKind)));
            Assert.That(ids, Does.Contain("Tower"));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test]
        public void EnumsAreReadAsText()
        {
            var file = UnitConfigLoader.Parse(ShippedJson);
            var knight = file.Units.Single(unit => unit.Id == "Knight");
            Assert.That(knight.ArmorType, Is.EqualTo(ArmorKind.Heavy));
            Assert.That(knight.Weapons[0].AttackType, Is.EqualTo(AttackKind.Normal));
            var mortar = file.Units.Single(unit => unit.Id == "Mortar").Weapons[0];
            Assert.That(mortar.Targeting, Is.EqualTo(WeaponTargeting.LaunchPoint));
            Assert.That(mortar.Splash.Mask, Is.EquivalentTo(new[] { WeaponTargetMask.Tree, WeaponTargetMask.Ground, WeaponTargetMask.Structure }));
        }

        [Test]
        public void IntegerEnumsAreRejected()
        {
            string json = Edit(root => UnitNamed(root, "Knight")["armorType"] = 3);
            Assert.Throws<FormatException>(() => UnitConfigLoader.Parse(json));
        }

        [Test]
        public void UnknownEnumMembersAreRejected()
        {
            string json = Edit(root => UnitNamed(root, "Knight")["armorType"] = "Plate");
            Assert.Throws<FormatException>(() => UnitConfigLoader.Parse(json));
        }

        [Test]
        public void OptionalFieldsAndExplicitNullsSurvive()
        {
            var file = UnitConfigLoader.Parse(ShippedJson);
            var footman = file.Units.Single(unit => unit.Id == "Footman");
            Assert.That(footman.Source.Rawcode, Is.Null);
            Assert.That(footman.Weapons[0].FlightTime, Is.Null);
            Assert.That(footman.Capabilities.Mana, Is.Null);
            var mage = file.Units.Single(unit => unit.Id == "Mage").Weapons[0];
            Assert.That(mage.FlightTime.Min, Is.EqualTo(.15f));
            Assert.That(mage.FlightTime.Max, Is.EqualTo(.6f));
            var mortar = file.Units.Single(unit => unit.Id == "Mortar").Weapons[0];
            Assert.That(mortar.FlightTime, Is.Null);
            Assert.That(file.Units.Single(unit => unit.Id == "Frigate").Presentation.Model, Is.Null);
        }

        [Test]
        public void UnknownFieldsAreRejected()
        {
            string json = Edit(root => UnitNamed(root, "Footman")["speedBonus"] = 2);
            var error = Assert.Throws<FormatException>(() => UnitConfigLoader.Parse(json));
            Assert.That(error.Message, Does.Contain("speedBonus"));
        }

        [Test]
        public void MissingRequiredFieldsAreRejected()
        {
            Assert.Throws<FormatException>(() => UnitConfigLoader.Parse(Edit(root => UnitNamed(root, "Footman").Remove("maxHealth"))));
            // A required-nullable member must still be present.
            Assert.Throws<FormatException>(() => UnitConfigLoader.Parse(Edit(root => ((JObject)UnitNamed(root, "Footman")["source"]).Remove("rawcode"))));
        }

        [Test]
        public void AMissingPresentationBlockIsAReadableError()
        {
            string json = Edit(root => UnitNamed(root, "Footman").Remove("presentation"));
            var error = Assert.Throws<FormatException>(() => UnitConfigLoader.Parse(json));
            Assert.That(error.Message, Does.Contain("presentation").IgnoreCase);
            Assert.That(error.Message, Does.Not.Contain("NullReference"));
            var file = UnitConfigLoader.Parse(ShippedJson);
            file.Units.Single(unit => unit.Id == "Footman").Presentation = null;
            List<string> errors = null;
            Assert.DoesNotThrow(() => errors = UnitConfigValidation.Errors(file));
            Assert.That(errors, Has.Some.Contain("landDefault"));
        }

        [Test]
        public void InvariantsAreEnforcedAtLoad()
        {
            string json = Edit(root => ((JObject)UnitNamed(root, "Medic")["capabilities"]).Remove("mana"));
            var error = Assert.Throws<FormatException>(() => UnitConfigLoader.Parse(json));
            Assert.That(error.Message, Does.Contain("require mana"));
            json = Edit(root => UnitNamed(root, "Mortar")["weapons"][0]["delivery"] = "Missile");
            error = Assert.Throws<FormatException>(() => UnitConfigLoader.Parse(json));
            Assert.That(error.Message, Does.Contain("LaunchPoint"));
        }

        [Test]
        public void SerializingTheDtoRoundTrips()
        {
            var file = UnitConfigLoader.Parse(ShippedJson);
            string written = UnitConfigLoader.Serialize(file);
            string difference = Difference(JObject.Parse(ShippedJson), JObject.Parse(written), "");
            Assert.That(difference, Is.Null, "units.json → DTO → JSON must be lossless");
        }

        // Numbers compare as the float the game reads; everything else structurally.
        static string Difference(JToken expected, JToken actual, string path)
        {
            if (expected.Type == JTokenType.Object && actual.Type == JTokenType.Object)
            {
                var a = (JObject)expected; var b = (JObject)actual;
                foreach (var property in a.Properties())
                {
                    if (!b.TryGetValue(property.Name, out var other)) return path + "/" + property.Name + " missing";
                    var inner = Difference(property.Value, other, path + "/" + property.Name);
                    if (inner != null) return inner;
                }
                foreach (var property in b.Properties()) if (a[property.Name] == null) return path + "/" + property.Name + " extra";
                return null;
            }
            if (expected.Type == JTokenType.Array && actual.Type == JTokenType.Array)
            {
                var a = (JArray)expected; var b = (JArray)actual;
                if (a.Count != b.Count) return path + " length " + a.Count + " vs " + b.Count;
                for (int i = 0; i < a.Count; i++) { var inner = Difference(a[i], b[i], path + "[" + i + "]"); if (inner != null) return inner; }
                return null;
            }
            bool numeric(JToken t) => t.Type == JTokenType.Integer || t.Type == JTokenType.Float;
            if (numeric(expected) && numeric(actual))
                return (float)expected == (float)actual ? null : path + ": " + expected + " vs " + actual;
            return JToken.DeepEquals(expected, actual) ? null : path + ": " + expected + " vs " + actual;
        }

    }
}
