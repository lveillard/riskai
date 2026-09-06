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
        const string FrontEndScenePath="Assets/RiskAI/Scenes/FrontEnd.unity";
        const string ScenePath="Assets/RiskAI/Scenes/LasMarcas.unity";
        [MenuItem("RiskAI/Prepare playable scene")]
        public static void Prepare()
        {
            RiskWorldArtSetup.Prepare();
            RiskArtSetup.Prepare();
            Directory.CreateDirectory("Assets/RiskAI/Scenes");
            EnsureBattlefieldScene();
            EnsureFrontEndScene();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(FrontEndScenePath,true),new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName="RiskAI";PlayerSettings.productName="RiskAI — Las Marcas v0.18";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.lveillard.riskai");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.lveillard.riskai");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS,"com.lveillard.riskai");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.WindowsStoreApps,"com.lveillard.riskai");
            PlayerSettings.bundleVersion="0.18.0";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
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
            EnsureMaterial("Assets/RiskAI/Resources/TerritoryOverlay.mat","RiskAI/TerritoryOverlay");
            EnsureMaterial("Assets/RiskAI/Resources/StrategicTerritory.mat","RiskAI/StrategicTerritory");
            EnsureMaterial("Assets/RiskAI/Resources/Meadow.mat","RiskAI/Meadow");
            var names=new[]{"Universal Render Pipeline/Lit","Universal Render Pipeline/Particles/Unlit"};
            for(int i=0;i<names.Length;i++) EnsureMaterial("Assets/RiskAI/Resources/"+(i==0?"RiskAILit":"RiskAIRing")+".mat",names[i]);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(FrontEndScenePath);Debug.Log("RISKAI_SETUP_OK: v0.18 frontend and battlefield prepared.");
        }
        static void EnsureBattlefieldScene()
        {
            if(File.Exists(ScenePath))return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("RiskAI · Bootstrap").AddComponent<RiskBootstrap>();
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        static void EnsureFrontEndScene()
        {
            if(File.Exists(FrontEndScenePath))return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("RiskAI · Front End").AddComponent<FrontEndController>();
            EditorSceneManager.SaveScene(scene,FrontEndScenePath);
        }
        static void EnsureMaterial(string path,string shaderName)
        {
            if(AssetDatabase.LoadAssetAtPath<Material>(path))return;
            var shader=Shader.Find(shaderName);if(!shader)throw new System.Exception("Required shader unavailable: "+shaderName);
            AssetDatabase.CreateAsset(new Material(shader),path);
        }
        [MenuItem("RiskAI/Build Windows prototype")]
        public static void BuildWindows()
        {
            Prepare();Directory.CreateDirectory("../Builds/Windows-v0.18");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{FrontEndScenePath,ScenePath},locationPathName="../Builds/Windows-v0.18/RiskAI.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Build failed: "+report.summary.result);
            File.Copy("../THIRD_PARTY_NOTICES.md","../Builds/Windows-v0.18/THIRD_PARTY_NOTICES.md",true);
            File.Copy("Assets/RiskAI/Art/KayKit/LICENSE.txt","../Builds/Windows-v0.18/KayKit-LICENSE.txt",true);
            Debug.Log("RISKAI_BUILD_OK: "+report.summary.totalSize+" bytes");
        }
    }
}
