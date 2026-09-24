using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Reads Resources/Config/units.json (synchronous on every platform, WebGL included) into the
    /// generated DTO: enums as text only, unknown members and missing required members are errors,
    /// then the cross-field invariants of <see cref="UnitConfigValidation"/>.
    /// </summary>
    public static class UnitConfigLoader
    {
        public const string ResourcePath = "Config/units";

        public static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new SchemaContractResolver(),
            Converters = { new StringEnumConverter { AllowIntegerValues = false } },
            FloatParseHandling = FloatParseHandling.Double,
        };

        /// <summary>Parses and validates a units.json document. Throws <see cref="FormatException"/> with every problem.</summary>
        public static UnitsFile Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("units.json is empty.");
            UnitsFile file;
            try { file = JsonConvert.DeserializeObject<UnitsFile>(json, Settings); }
            catch (JsonException error) { throw new FormatException("units.json: " + error.Message, error); }
            var errors = UnitConfigValidation.Errors(file);
            if (errors.Count > 0) throw new FormatException("units.json is invalid:\n  " + string.Join("\n  ", errors));
            return file;
        }

        /// <summary>Loads the shipped units.json from Resources.</summary>
        public static UnitsFile LoadResource()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (!asset) throw new InvalidOperationException("Missing Resources/" + ResourcePath + ".json: the unit catalog cannot load.");
            try { return Parse(asset.text); }
            finally { Resources.UnloadAsset(asset); }
        }

        /// <summary>Player/PlayMode entry: a fresh domain starts unbound, then binds before the first scene.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCatalog() => UnitCatalog.Unbind();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BindCatalog() => UnitCatalog.Bind(LoadResource());

        public static string Serialize(UnitsFile file) => JsonConvert.SerializeObject(file, Formatting.Indented, Settings);

        /// <summary>Maps the generated [UnitConfigField] facts onto Newtonsoft's Required.</summary>
        sealed class SchemaContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                var field = member.GetCustomAttribute<UnitConfigFieldAttribute>();
                if (field != null && field.Required)
                {
                    property.Required = field.Nullable ? Required.AllowNull : Required.Always;
                    if (field.Nullable) property.NullValueHandling = NullValueHandling.Include;
                }
                property.PropertyName = char.ToLowerInvariant(property.PropertyName[0]) + property.PropertyName.Substring(1);
                return property;
            }
        }
    }
}
