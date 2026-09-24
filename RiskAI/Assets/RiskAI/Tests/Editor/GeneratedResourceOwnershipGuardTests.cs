using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    /// <summary>
    /// Single-source resource ownership guard, enforced by scanning the runtime sources on disk
    /// (plain file IO; no Unity asset IO). GeneratedResourceOwner is the one owner of runtime-created
    /// native assets, so every generated Mesh/Material/Texture/RenderTexture/buffer must be created
    /// inside GeneratedResourceOwner...Track(...) on the same statement, or be an explicitly tagged
    /// bounded global cache (// RISKAI_SHARED_ASSET: &lt;reason&gt;, on the statement line or the line
    /// above). StaticBatchingUtility.Combine may only run inside GeneratedResourceOwner, where the
    /// combined mesh is tracked. The scan is line-based and deliberately simple: one regex list.
    /// </summary>
    public sealed class GeneratedResourceOwnershipGuardTests
    {
        // One regex list: every runtime constructor of a generated native asset.
        static readonly Regex[] GeneratedAssetConstructors =
        {
            new Regex(@"\bnew\s+Mesh\s*[{(]"),
            new Regex(@"\bnew\s+Material\s*\("),
            new Regex(@"\bnew\s+Texture2D\s*\("),
            new Regex(@"\bnew\s+Texture3D\s*\("),
            new Regex(@"\bnew\s+RenderTexture\s*\("),
            new Regex(@"\bnew\s+ComputeBuffer\s*\("),
            new Regex(@"\bnew\s+GraphicsBuffer\s*\("),
        };
        static readonly Regex StaticBatchCombine = new Regex(@"StaticBatchingUtility\.Combine\s*\(");
        static readonly Regex OwnerTrack = new Regex(@"\bTrack\s*\(");
        static readonly Regex SharedAssetTag = new Regex(@"RISKAI_SHARED_ASSET");

        static string ScriptsRoot => Path.Combine(Application.dataPath, "RiskAI", "Scripts");

        static IEnumerable<string> RuntimeSources()
        {
            Assert.That(Directory.Exists(ScriptsRoot), Is.True, "Runtime sources must exist under " + ScriptsRoot);
            foreach (var file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
                yield return file;
        }

        [Test]
        public void StaticBatchingUtilityCombineOnlyRunsInsideGeneratedResourceOwner()
        {
            foreach (var file in RuntimeSources())
            {
                if (Path.GetFileName(file) == "GeneratedResourceOwner.cs") continue;
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    Assert.That(StaticBatchCombine.IsMatch(lines[i]), Is.False,
                        file + ":" + (i + 1) + " calls StaticBatchingUtility.Combine directly. " +
                        "Every static batch must go through GeneratedResourceOwner.CombineStaticBatches, " +
                        "which tracks the combined mesh the call creates.\n" + lines[i].Trim());
            }
        }

        [Test]
        public void EveryGeneratedAssetIsTrackedAtCreationOrExplicitlyTagged()
        {
            foreach (var file in RuntimeSources())
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    bool createsAsset = false;
                    foreach (var constructor in GeneratedAssetConstructors)
                        if (constructor.IsMatch(lines[i])) { createsAsset = true; break; }
                    if (!createsAsset) continue;
                    bool tracked = OwnerTrack.IsMatch(lines[i]);
                    bool tagged = SharedAssetTag.IsMatch(lines[i]) || (i > 0 && SharedAssetTag.IsMatch(lines[i - 1]));
                    Assert.That(tracked || tagged, Is.True,
                        file + ":" + (i + 1) + " creates a generated native asset without an owner. " +
                        "Wrap it in GeneratedResourceOwner.For(root).Track(...) on the same statement, " +
                        "or mark it a bounded global cache with // RISKAI_SHARED_ASSET: <reason>.\n" + lines[i].Trim());
                }
            }
        }
    }
}
