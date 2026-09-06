using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskAI.Editor
{
    public static class RiskWorldPreview
    {
        const int Width = 1920;
        const int Height = 1080;

        public static void Capture()
        {
            Scene original = default;
            bool haveOriginal = false;
            RenderTexture output = null;
            Camera camera = null;
            try
            {
                RiskProjectSetup.Prepare();
                original = SceneManager.GetActiveScene();
                haveOriginal = original.IsValid() && !string.IsNullOrEmpty(original.path);

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var bootstrapObject = new GameObject("RiskAI · Visual preview bootstrap");
                var bootstrap = bootstrapObject.AddComponent<RiskBootstrap>();
                if (!BattleSession.Current)
                    typeof(RiskBootstrap).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(bootstrap, null);

                SampleIdlePoses();
                camera = Camera.main;
                if (!camera) throw new InvalidOperationException("RiskAI visual preview: no main camera was created.");
                output = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                Render(camera, output, "Screenshots/v11-world-overview.png");

                var focus = MapLayout.Point(-32*MapLayout.Spacing,2*MapLayout.Spacing);
                camera.orthographicSize = 20;
                camera.transform.position = focus - camera.transform.forward * (20/Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad));
                Render(camera, output, "Screenshots/v11-world-bastion.png");
                focus=MapLayout.Point(26*MapLayout.Spacing,4*MapLayout.Spacing);
                camera.transform.position=focus-camera.transform.forward*(25/Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad));
                Render(camera,output,"Screenshots/v11-world-highlands.png");
                var town=bootstrapObject.GetComponent<BattleSession>().Towns[1];
                focus=town.Defense.transform.position;
                camera.transform.position=focus-camera.transform.forward*(10/Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad));
                Render(camera,output,"Screenshots/v11-world-tower.png");
                focus=MapLayout.Point(33*MapLayout.Spacing,50*MapLayout.Spacing);
                camera.transform.position=focus-camera.transform.forward*(22/Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad));
                Render(camera,output,"Screenshots/v11-world-estuary.png");
                Debug.Log("RISKAI_WORLD_PREVIEW_OK: Screenshots/v11-world-overview.png, Screenshots/v11-world-bastion.png");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                throw;
            }
            finally
            {
                if (camera) camera.targetTexture = null;
                if (RenderTexture.active == output) RenderTexture.active = null;
                if (output)
                {
                    output.Release();
                    UnityEngine.Object.DestroyImmediate(output);
                }
                if (haveOriginal && File.Exists(original.path))
                    EditorSceneManager.OpenScene(original.path, OpenSceneMode.Single);
            }
        }

        static void SampleIdlePoses()
        {
            foreach (var animation in UnityEngine.Object.FindObjectsByType<Animation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (animation["Idle"] && animation["Idle"].clip)
                    animation["Idle"].clip.SampleAnimation(animation.gameObject, .3f);
        }

        static void Render(Camera camera, RenderTexture output, string relativePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(relativePath));
            var previous = RenderTexture.active;
            camera.targetTexture = output;
            // Warm the render pipeline and material buffers before recording the frame.
            camera.Render();
            camera.Render();
            camera.Render();
            RenderTexture.active = output;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(relativePath, image.EncodeToPNG());
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}

