using UnityEditor;
using UnityEngine;

namespace RiskAI.Editor
{
    public static class RiskWorldArtSetup
    {
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            foreach(string name in new[]{"GroundAtlas","ArchitectureAtlas","StrategicAtlas","BiomeAtlas-v06","CliffAtlas-v07","FirBough-v07b","FoliageAtlas-v08"})
            {
                string path="Assets/RiskAI/Resources/Painted/"+name+".png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(!importer)throw new System.Exception("Missing painted texture: "+path);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;
                importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;
                importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            Create("PaintedSurface","RiskAI/PaintedSurface","ArchitectureAtlas");
            Create("Meadow","RiskAI/Meadow","StrategicAtlas");
            var meadow=AssetDatabase.LoadAssetAtPath<Material>("Assets/RiskAI/Resources/Meadow.mat");
            meadow.SetTexture("_Biomes",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RiskAI/Resources/Painted/BiomeAtlas-v06.png"));EditorUtility.SetDirty(meadow);
            meadow.SetTexture("_Cliffs",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RiskAI/Resources/Painted/CliffAtlas-v07.png"));
            Create("FirFoliage","RiskAI/FirFoliage","FirBough-v07b");
            Create("BiomeFoliage","RiskAI/BiomeFoliage","FoliageAtlas-v08");
            Create("UnitTeam","RiskAI/UnitTeam",null);
            Create("RiverWater","RiskAI/RiverWater",null);
            Create("GroundShade","RiskAI/GroundShade",null);
            Create("Atmosphere","RiskAI/Atmosphere",null);
            Create("Cascade","RiskAI/Cascade",null);
            AssetDatabase.SaveAssets();Debug.Log("RISKAI_PAINTED_ART_OK");
        }
        static void Create(string name,string shaderName,string texture)
        {
            string path="Assets/RiskAI/Resources/"+name+".mat";
            var shader=Shader.Find(shaderName);if(!shader)throw new System.Exception("Missing shader: "+shaderName);
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
            if(texture!=null)material.SetTexture("_Atlas",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RiskAI/Resources/Painted/"+texture+".png"));
            EditorUtility.SetDirty(material);
        }
    }
}
