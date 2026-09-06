using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI.Editor
{
    public static class RiskProjectSetup
    {
        const string ScenePath="Assets/RiskAI/Scenes/LasMarcas.unity";
        [MenuItem("RiskAI/Prepare playable scene")]
        public static void Prepare()
        {
            RiskWorldArtSetup.Prepare();
            RiskArtSetup.Prepare();
            Directory.CreateDirectory("Assets/RiskAI/Scenes");
            if(!File.Exists(ScenePath))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                new GameObject("RiskAI · Bootstrap").AddComponent<RiskBootstrap>();
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName="RiskAI";PlayerSettings.productName="RiskAI — Las Marcas v0.12";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.lveillard.riskai");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.lveillard.riskai");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS,"com.lveillard.riskai");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.WindowsStoreApps,"com.lveillard.riskai");
            PlayerSettings.bundleVersion="0.12.0";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=false;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.colorSpace=ColorSpace.Linear;
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            var pipeline=GraphicsSettings.defaultRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if(pipeline){pipeline.shadowDistance=120;pipeline.msaaSampleCount=4;EditorUtility.SetDirty(pipeline);}
            // These shaders are retained through Resources materials, which permits normal URP variant stripping.
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var included=graphics.FindProperty("m_AlwaysIncludedShaders");
            for(int i=included.arraySize-1;i>=0;i--)
            {
                var shader=included.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if(shader&&(shader.name=="Universal Render Pipeline/Lit"||shader.name=="Universal Render Pipeline/Particles/Unlit"))
                { included.GetArrayElementAtIndex(i).objectReferenceValue=null;included.DeleteArrayElementAtIndex(i); }
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Assets/RiskAI/Resources");
            if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/RiskAI/Resources/TerritoryOverlay.mat"))
                AssetDatabase.CreateAsset(new Material(Shader.Find("RiskAI/TerritoryOverlay")),"Assets/RiskAI/Resources/TerritoryOverlay.mat");
            if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/RiskAI/Resources/Meadow.mat"))
                AssetDatabase.CreateAsset(new Material(Shader.Find("RiskAI/Meadow")),"Assets/RiskAI/Resources/Meadow.mat");
            var names=new[]{"Universal Render Pipeline/Lit","Universal Render Pipeline/Particles/Unlit"};
            for(int i=0;i<names.Length;i++)
            {
                var shader=Shader.Find(names[i]);if(!shader)throw new System.Exception("Required shader unavailable: "+names[i]);
                string path="Assets/RiskAI/Resources/"+(i==0?"RiskAILit":"RiskAIRing")+".mat";
                if(!AssetDatabase.LoadAssetAtPath<Material>(path))AssetDatabase.CreateAsset(new Material(shader),path);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);Debug.Log("RISKAI_SETUP_OK: playable scene prepared.");
        }
        [MenuItem("RiskAI/Build Windows prototype")]
        public static void BuildWindows()
        {
            Prepare();Directory.CreateDirectory("../Builds/Windows-v0.12");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{ScenePath},locationPathName="../Builds/Windows-v0.12/RiskAI.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Build failed: "+report.summary.result);
            File.Copy("../THIRD_PARTY_NOTICES.md","../Builds/Windows-v0.12/THIRD_PARTY_NOTICES.md",true);
            File.Copy("Assets/RiskAI/Art/KayKit/LICENSE.txt","../Builds/Windows-v0.12/KayKit-LICENSE.txt",true);
            Debug.Log("RISKAI_BUILD_OK: "+report.summary.totalSize+" bytes");
        }
    }
}

