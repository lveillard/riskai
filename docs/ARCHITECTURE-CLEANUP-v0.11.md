# Architecture hygiene cleanup v0.11

Audit date: 2026-09-06. This note records the bounded package, template, and application-identity cleanup. It does not claim a runtime performance improvement. Unity Package Manager has regenerated the lockfile and the project now compiles after restoring explicit built-in module dependencies; EditMode validation is 26/26, while PlayMode validation remains in progress.

## Package evidence

The project source and serialized assets had no use of Visual Scripting, Timeline, or Collab Proxy. The only Timeline hit was Unity's default `TimelineAsset` dependency type in `SceneTemplateSettings.json`; there were no Timeline assets, PlayableDirector components, tracks, or Timeline API calls. Removing that default entry was attempted, but Unity regenerated it during validation. The editor-owned default remains; it does not reintroduce the Timeline package or a game dependency. Collab hits were only empty editor settings fields, and Visual Scripting had no project references.

The following direct manifest entries were removed from [`RiskAI/Packages/manifest.json`](../RiskAI/Packages/manifest.json):

- `com.unity.collab-proxy`
- `com.unity.timeline`
- `com.unity.visualscripting`

The package lock was left for Unity Package Manager to recompute, and `packages-lock.json` has now been regenerated. Removing Timeline also removed built-in module entries that the project still uses, so `com.unity.modules.animation`, `com.unity.modules.particlesystem`, and `com.unity.modules.audio` are explicit manifest dependencies. `com.unity.pipeline` remains in the manifest as a separate candidate because it is experimental; removing it requires a separate Package Manager/Editor validation pass. AI Navigation, Input System, URP, and Test Framework remain because they have direct source, asset, or test usage. UGUI remains because it is present in the resolved project dependencies even though the game has no direct `UnityEngine.UI` code.

## Template/reference evidence

The only enabled build scene was [`LasMarcas.unity`](../RiskAI/ProjectSettings/EditorBuildSettings.asset). [`RiskProjectSetup.Prepare`](../RiskAI/Assets/RiskAI/Editor/RiskProjectSetup.cs) also creates/opens only that scene. No project or source reference remained for the old `Assets/Scenes/SampleScene.unity`, its `.meta`, or `Assets/Settings/SampleSceneProfile.asset`; the profile's GUID was not referenced elsewhere. The stale `templateDefaultScene` value was updated to `Assets/RiskAI/Scenes/LasMarcas.unity` in [`ProjectSettings.asset`](../RiskAI/ProjectSettings/ProjectSettings.asset).

The default URP readme and TutorialInfo folder were template-only. `Assets/Readme.asset` named “URP Empty Template,” and its only consumers were the deleted TutorialInfo editor/readme scripts and styles. The old Readme, TutorialInfo files, SampleScene, and SampleSceneProfile were removed. The active URP assets remain: [`PC_RPAsset.asset`](../RiskAI/Assets/Settings/PC_RPAsset.asset), [`Mobile_RPAsset.asset`](../RiskAI/Assets/Settings/Mobile_RPAsset.asset), [`DefaultVolumeProfile.asset`](../RiskAI/Assets/Settings/DefaultVolumeProfile.asset), and the global URP settings referenced by [`GraphicsSettings.asset`](../RiskAI/ProjectSettings/GraphicsSettings.asset).

No reference, map, build folder, or active LasMarcas scene was deleted. Empty filesystem directories left by the removed template files are untracked and do not participate in Unity's asset database.

## Application identity

The template application identifiers in [`ProjectSettings.asset`](../RiskAI/ProjectSettings/ProjectSettings.asset) were replaced with `com.lveillard.riskai` for Android, Standalone, iPhone, and the Windows Store package fields. [`RiskProjectSetup.Prepare`](../RiskAI/Assets/RiskAI/Editor/RiskProjectSetup.cs) now writes the same identifier for those build targets so rerunning the project setup does not restore template IDs.

## Validation limits

Validation covered repository-wide reference searches, JSON parsing of the edited manifest and scene-template settings, exact target inspection before deletion, Unity Package Manager lock regeneration, and Editor compilation. After the explicit built-in animation, particle-system, and audio modules were restored, EditMode validation passed 26/26. PlayMode subsequently passed 64/64 and the Windows build succeeded (195,763,014 bytes reported by BuildReport). See [validation](VALIDATION-v0.11.md) for Player checks. No quantified performance improvement is claimed without a measured baseline.
