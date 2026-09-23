using System.IO;
using RiskAI.Core;
using UnityEditor;
using UnityEngine;

namespace RiskAI.Editor
{
    /// <summary>
    /// Editor domains (edit-mode tests, art tools, builds) bind the unit catalog from the
    /// same units.json the player loads, and rebind whenever that file is reimported.
    /// </summary>
    [InitializeOnLoad]
    static class UnitCatalogEditorBinding
    {
        public const string AssetPath = "Assets/RiskAI/Resources/Config/units.json";

        static UnitCatalogEditorBinding() => Bind();

        public static void Bind()
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), AssetPath);
            UnitCatalog.Bind(UnitConfigLoader.Parse(File.ReadAllText(path)));
        }

        sealed class Reimport : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (var asset in imported)
                    if (asset == AssetPath) { Bind(); return; }
            }
        }
    }
}
