using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace RiskAI
{
    public enum GameLanguage { English, Spanish }

    /// <summary>Single presentation boundary for the two supported UI languages.</summary>
    public static class GameText
    {
        public static GameLanguage Language { get; internal set; } = GameLanguage.English;
        public static bool IsSpanish => Language == GameLanguage.Spanish;
        public static string SwitchLabel => IsSpanish ? "EN" : "ES";

        const string PreferenceKey = "riskai.language";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => Language = GameLanguage.English;

        // Players start in their own language: an explicit toggle (PlayerPrefs) wins,
        // then the browser (navigator.language) or OS language; Spanish for es-*, else English.
        // Editor and batch runs keep the deterministic English default that tests assume.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void DetectOnLoad()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            Language = Initial(StoredPreference(), PlatformPresentation.PrefersSpanish);
        }

        /// <summary>Resolution order for the starting language. A stored choice beats detection.</summary>
        public static GameLanguage Initial(GameLanguage? stored, bool systemPrefersSpanish) =>
            stored ?? (systemPrefersSpanish ? GameLanguage.Spanish : GameLanguage.English);

        static GameLanguage? StoredPreference()
        {
            try
            {
                if (!PlayerPrefs.HasKey(PreferenceKey)) return null;
                return PlayerPrefs.GetInt(PreferenceKey) == (int)GameLanguage.Spanish ? GameLanguage.Spanish : GameLanguage.English;
            }
            catch (Exception) { return null; }
        }

        /// <summary>The player's explicit choice: applied and remembered for later sessions.</summary>
        public static void Toggle()
        {
            Language = IsSpanish ? GameLanguage.English : GameLanguage.Spanish;
            try { PlayerPrefs.SetInt(PreferenceKey, (int)Language); PlayerPrefs.Save(); }
            catch (Exception) { }
        }

        const string TextResource = "Config/text";

        /// <summary>One line of Resources/Config/text.json: the same line in every language.</summary>
        sealed class TextEntry
        {
            [JsonProperty("es", Required = Required.Always)] public string Es;
            [JsonProperty("en", Required = Required.Always)] public string En;
        }

        // text.json is the single source of UI text; unit names and roles come from units.json.
        static Dictionary<string,string> english;
        static Dictionary<string,string> unitText;
        static int unitTextRevision=-1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTables() { english=null; unitText=null; unitTextRevision=-1; }

        static Dictionary<string,string> English => english ??= LoadText();

        static Dictionary<string,string> LoadText()
        {
            var asset=Resources.Load<TextAsset>(TextResource);
            if(!asset)throw new InvalidOperationException("Resources/"+TextResource+".json is missing.");
            var entries=JsonConvert.DeserializeObject<List<TextEntry>>(asset.text,new JsonSerializerSettings{ MissingMemberHandling=MissingMemberHandling.Error });
            var table=new Dictionary<string,string>(entries.Count,StringComparer.Ordinal);
            foreach(var entry in entries)
            {
                if(string.IsNullOrEmpty(entry.Es)||string.IsNullOrEmpty(entry.En))
                    throw new InvalidOperationException("text.json: every line needs both es and en (\""+entry.Es+"\").");
                if(!table.TryAdd(entry.Es,entry.En))throw new InvalidOperationException("text.json: duplicate line \""+entry.Es+"\".");
            }
            return table;
        }

        static Dictionary<string,string> UnitText
        {
            get
            {
                int revision=RiskAI.Core.UnitCatalog.IsBound?RiskAI.Core.UnitCatalog.Revision:-1;
                if(unitText!=null&&unitTextRevision==revision)return unitText;
                unitText=new Dictionary<string,string>(StringComparer.Ordinal);
                if(revision>=0)
                    for(int i=0;i<RiskAI.Core.UnitCatalog.Count;i++)
                    {
                        ref readonly var type=ref RiskAI.Core.UnitCatalog.At(i);
                        if(type.Domain==RiskAI.Core.UnitDomain.Static)continue;
                        AddUnitText(type.Name,type.NameEn);AddUnitText(type.Role,type.RoleEn);
                    }
                unitTextRevision=revision;return unitText;
            }
        }
        static void AddUnitText(string spanish,string english)
        {
            if(!string.IsNullOrEmpty(spanish)&&!string.IsNullOrEmpty(english))unitText[spanish]=english;
        }

        public static string Localize(string source) => IsSpanish ? source : EnglishOf(source);

        /// <summary>
        /// English form of a Spanish line, whatever the current language (chat aliases). Exact lines
        /// only: a line text.json does not know stays whole in Spanish, never half translated.
        /// </summary>
        public static string EnglishOf(string source)
        {
            if(string.IsNullOrEmpty(source))return source;
            if(English.TryGetValue(source,out string line))return line;
            return UnitText.TryGetValue(source,out line)?line:source;
        }

        /// <summary>
        /// A composed line: the template is a text.json line with {0}, {1}… placeholders, and every
        /// text argument (a place, player or unit name) is localized as its own line.
        /// </summary>
        public static string Format(string template,params object[] args)
        {
            var parts=new object[args.Length];
            for(int i=0;i<args.Length;i++)parts[i]=args[i] is string text?Localize(text):args[i];
            return string.Format(CultureInfo.InvariantCulture,Localize(template),parts);
        }
    }
}
