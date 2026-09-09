using System.IO;
using System.Linq;
using RiskAI.Core;
using UnityEditor;
using UnityEngine;

namespace RiskAI.Editor
{
    public static class RiskArtSetup
    {
        const string Folder = "Assets/RiskAI/Art/KayKit/";
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/RiskAI/Resources/Units");
            Directory.CreateDirectory("Assets/RiskAI/Resources/Portraits");
            var prepared=new System.Collections.Generic.HashSet<string>();
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                string name = BattleRules.Model(kind);
                if(!prepared.Add(name))continue;
                if(kind==UnitKind.Mortar)
                {
                    var cart=new GameObject("Mortar portrait model");VisualFactory.MortarModel(cart.transform,VisualFactory.TeamColor(0));
                    RenderPortrait(cart,null,name);Object.DestroyImmediate(cart);continue;
                }
                string model = kind == UnitKind.Guard ? "Knight" : kind==UnitKind.Medic?"Mage":name;
                string path = Folder + model + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if(!importer)throw new System.InvalidOperationException("Missing own unit art importer: "+path);
                if (importer.animationType != ModelImporterAnimationType.Legacy)
                {
                    importer.animationType = ModelImporterAnimationType.Legacy;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.SaveAndReimport();
                }
                string texture = model == "Knight" ? "knight" : model == "Mage" ? "mage" : "rogue";
                string materialPath = Folder + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + texture + "_texture.png");
                material.SetFloat("_Smoothness", .18f);
                material.color = kind == UnitKind.Guard ? new Color(1, .88f, .6f) : Color.white;
                EditorUtility.SetDirty(material);
                var root = new GameObject(name);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * (kind == UnitKind.Guard ? 1.43f : 1.25f);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
                var animation = visual.GetComponent<Animation>();
                if (!animation) animation = visual.AddComponent<Animation>();
                animation.playAutomatically = false;
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"))) animation.AddClip(clip, clip.name);
                foreach (var hand in visual.GetComponentsInChildren<Transform>(true).Where(t => t.name == "handslot.l" || t.name == "handslot.r"))
                {
                    foreach (Transform item in hand)
                    {
                        bool visible = kind == UnitKind.Footman ? item.name == "1H_Sword" || item.name == "Badge_Shield" :
                            kind == UnitKind.Guard ? item.name == "2H_Sword" :
                            kind == UnitKind.Archer ? item.name == "2H_Crossbow" : item.name.ToLowerInvariant().Contains("staff");
                        item.gameObject.SetActive(visible);
                    }
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/RiskAI/Resources/Units/" + name + ".prefab");
                UnitTeamColor.Apply(root,kind,0);
                RenderPortrait(root, animation, name);
                Object.DestroyImmediate(root);
            }
            var mountedRoot=new GameObject("Original mounted portrait");
            MountedKnightView.Create(mountedRoot.transform,0);
            RenderPortrait(mountedRoot,null,"MountedKnight");
            Object.DestroyImmediate(mountedRoot);
            foreach(ShipKind kind in System.Enum.GetValues(typeof(ShipKind)))
            {
                var shipRoot=new GameObject(kind+" portrait");
                NavalArt.CreateShipModel(shipRoot.transform,0,kind);
                RenderPortrait(shipRoot,null,kind.ToString());
                Object.DestroyImmediate(shipRoot);
            }
            AssetDatabase.Refresh(); AssetDatabase.SaveAssets(); Debug.Log("RISKAI_ART_OK");
        }
        static void RenderPortrait(GameObject root, Animation animation, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            root.transform.position = Vector3.down * 1000;
            if (animation && animation["Idle"]) animation["Idle"].clip.SampleAnimation(animation.gameObject, .3f);
            var cameraObject = new GameObject("Portrait camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f,.085f,.09f);
            bool ship=name==ShipKind.Galley.ToString()||name==ShipKind.Transport.ToString();
            camera.orthographic = true; camera.orthographicSize = ship?3.35f:name=="Mortar"?1.35f:name=="MountedKnight"?1.85f:1.4f;
            Vector3 focus = root.transform.position + Vector3.up * (ship?2.15f:name=="Mortar"?1.15f:1.75f);
            camera.transform.position = focus + (ship?new Vector3(4.8f,3.1f,6.8f):new Vector3(2,1,5)); camera.transform.LookAt(focus);
            var keyObject = new GameObject("Portrait light"); var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.8f; key.cullingMask = 1 << 31; key.transform.rotation = Quaternion.Euler(35, -30, 0);
            var output = new RenderTexture(192, 192, 24); camera.targetTexture = output;
            camera.Render();camera.Render();camera.Render(); var previous = RenderTexture.active; RenderTexture.active = output;
            var image = new Texture2D(192, 192, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 192, 192), 0, 0); image.Apply();
            File.WriteAllBytes("Assets/RiskAI/Resources/Portraits/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.DestroyImmediate(image); output.Release(); Object.DestroyImmediate(output);
            Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(keyObject);
        }
    }
}
