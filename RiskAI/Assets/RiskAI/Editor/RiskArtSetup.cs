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
                if(UnitCatalog.Get(kind).Domain!=UnitDomain.Land)continue;
                if(UnitVariantViews.HasVariantPortrait(kind))continue; // rendered below from their variant views
                string name = UnitCatalog.Get(kind).Model;
                if(!prepared.Add(name))continue;
                if(kind==UnitKind.Mortar)
                {
                    var cart=new GameObject("Mortar portrait model");VisualFactory.MortarModel(cart.transform,VisualFactory.TeamColor(0));
                    RenderPortrait(cart,null,name);Object.DestroyImmediate(cart);continue;
                }
                string model = kind == UnitKind.Knight ? "Knight" : kind==UnitKind.Medic?"Mage":kind==UnitKind.MarinePrivate?"RogueHooded":name;
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
                material.color = kind == UnitKind.Knight ? new Color(1, .88f, .6f) : Color.white;
                EditorUtility.SetDirty(material);
                var root = new GameObject(name);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * (kind == UnitKind.Knight ? 1.43f : 1.25f);
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
                            kind == UnitKind.Knight ? item.name == "2H_Sword" :
                            kind == UnitKind.Archer ? item.name == "2H_Crossbow" : item.name.ToLowerInvariant().Contains("staff");
                        item.gameObject.SetActive(visible);
                    }
                }
                if(kind==UnitKind.MarinePrivate)
                    foreach(var part in visual.GetComponentsInChildren<Transform>(true))
                        if(part.name=="Rogue_Cape"||part.name=="Rogue_Head_Hooded")part.gameObject.SetActive(false);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/RiskAI/Resources/Units/" + name + ".prefab");
                UnitTeamColor.Apply(root,kind,0);
                if(kind==UnitKind.Archer)CrossbowView.Apply(visual);
                if(kind==UnitKind.MarinePrivate)MarinePrivateView.Apply(visual,0);
                RenderPortrait(root, animation, name);
                Object.DestroyImmediate(root);
            }
            RenderVariantPortraitsOnly();
            foreach(UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                if(UnitCatalog.Get(kind).Domain!=UnitDomain.Sea)continue;
                var shipRoot=new GameObject(kind+" portrait");
                NavalArt.CreateShipModel(shipRoot.transform,0,kind);
                RenderPortrait(shipRoot,null,kind.ToString());
                Object.DestroyImmediate(shipRoot);
            }
            AssetDatabase.Refresh(); AssetDatabase.SaveAssets(); Debug.Log("RISKAI_ART_OK");
        }
        /// <summary>
        /// Re-renders only the procedural/variant portraits (mounted knights, Roarer, General,
        /// siege) without re-saving unit prefabs. Batch: -executeMethod RiskAI.Editor.RiskArtSetup.RenderVariantPortraits
        /// </summary>
        public static void RenderVariantPortraits()
        {
            RenderVariantPortraitsOnly();
            AssetDatabase.Refresh(); AssetDatabase.SaveAssets(); Debug.Log("RISKAI_VARIANT_PORTRAITS_OK");
        }
        static void RenderVariantPortraitsOnly()
        {
            var mountedRoot=new GameObject("Original mounted portrait");
            MountedKnightView.Create(mountedRoot.transform,0);
            RenderPortrait(mountedRoot,null,"MountedKnight");
            Object.DestroyImmediate(mountedRoot);
            foreach(UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                if(!UnitVariantViews.HasVariantPortrait(kind))continue;
                var variantRoot=new GameObject(kind+" portrait model");Animation variantAnimation=null;
                if(MountedKnightView.IsMounted(kind))MountedKnightView.CreateVariant(variantRoot.transform,0,kind);
                else if(kind==UnitKind.Artillery)UnitVariantViews.ArtilleryModel(variantRoot.transform,VisualFactory.TeamColor(0));
                else if(kind==UnitKind.Tank)UnitVariantViews.TankModel(variantRoot.transform,VisualFactory.TeamColor(0));
                else
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RiskAI/Resources/Units/"+UnitCatalog.Get(kind).Model+".prefab");
                    if(!prefab)throw new System.InvalidOperationException("Missing base prefab for "+kind);
                    var visual=(GameObject)Object.Instantiate(prefab,variantRoot.transform,false);
                    UnitTeamColor.Apply(visual,kind,0);UnitVariantViews.Decorate(visual,kind,0);
                    variantAnimation=visual.GetComponentInChildren<Animation>();
                }
                RenderPortrait(variantRoot,variantAnimation,UnitCatalog.Get(kind).Portrait);
                Object.DestroyImmediate(variantRoot);
            }
        }
        /// <summary>Frames the whole model (upper part only when <paramref name="upperFraction"/> &lt; 1) with a small margin.</summary>
        static void FitPortrait(Camera camera, GameObject root, float upperFraction, Vector3 view)
        {
            var bounds = new Bounds(); bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is LineRenderer || renderer.name == "Soft ground shadow") continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) return;
            if (upperFraction < 1)
            {
                float bottom = bounds.max.y - bounds.size.y * upperFraction;
                bounds.SetMinMax(new Vector3(bounds.min.x, bottom, bounds.min.z), bounds.max);
            }
            var direction = view.normalized;
            camera.transform.position = bounds.center + direction * 8; camera.transform.LookAt(bounds.center);
            float halfX = 0, halfY = 0;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x, (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                var local = camera.transform.InverseTransformPoint(point);
                halfX = Mathf.Max(halfX, Mathf.Abs(local.x)); halfY = Mathf.Max(halfY, Mathf.Abs(local.y));
            }
            camera.orthographicSize = Mathf.Max(halfX, halfY) * 1.06f;
        }
        static void RenderPortrait(GameObject root, Animation animation, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            root.transform.position = Vector3.down * 1000;
            if (animation && animation["Idle"]) animation["Idle"].clip.SampleAnimation(animation.gameObject, .3f);
            var cameraObject = new GameObject("Portrait camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f,.085f,.09f);
            bool ship=System.Enum.IsDefined(typeof(UnitKind),name);
            bool siege=name=="Mortar"||name=="Artillery"||name=="Tank";
            camera.orthographic = true; camera.orthographicSize = ship?(name==UnitKind.Battleship.ToString()?4.2f:name==UnitKind.Warship.ToString()?3.8f:3.35f):siege?(name=="Mortar"?1.35f:1.7f):name=="MountedKnight"?1.85f:name=="ArmyGeneral"?2.1f:1.4f;
            Vector3 focus = root.transform.position + Vector3.up * (ship?2.15f:siege?1.15f:name=="ArmyGeneral"?1.95f:1.75f);
            camera.transform.position = focus + (ship?new Vector3(4.8f,3.1f,6.8f):new Vector3(2,1,5)); camera.transform.LookAt(focus);
            if (name == "MountedKnight" || name == "ArmyGeneral" || name == "MarineMajor" || name == "MarineGeneral" || name == "Roarer")
                FitPortrait(camera, root, name == "Roarer" ? .78f : .8f, name == "Roarer" ? new Vector3(2, 1, 5) : new Vector3(4.5f, 1.6f, 3));
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
