using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YourCat.DesktopPet.Editor
{
    public static class CatVisualRegressionTest
    {
        public static void Render()
        {
            StandardCatBuilder.Create();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StandardCat/StandardCat.prefab");
            GameObject cat = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var bounds = CalculateBounds(cat);
            foreach (var transform in cat.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name is "Model" or "butt" or "belly" or "root" or "L_BLeg_Upper" or "L_BLeg_Lower")
                    Debug.Log($"CAT_BONE {transform.name} position={transform.localPosition} rotation={transform.localEulerAngles} scale={transform.localScale}");
            }
            var cameraObject = new GameObject("TestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.18f;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 8f);

            var lightObject = new GameObject("TestLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

            var texture = new RenderTexture(800, 800, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(800, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 800f, 800f), 0, 0);
            image.Apply();

            var outputDirectory = Path.GetFullPath("Build/TestResults");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllBytes(Path.Combine(outputDirectory, "cat-appearance.png"), image.EncodeToPNG());

            foreach (var state in new[] { "Walk", "Petted", "Eat", "Sit", "LieDown", "Sleep" })
            {
                Object.DestroyImmediate(cat);
                cat = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/StandardCat/{state}.anim");
                clip.SampleAnimation(cat, 0.85f);
                camera.Render();
                image.ReadPixels(new Rect(0f, 0f, 800f, 800f), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(outputDirectory, $"cat-{state}.png"), image.EncodeToPNG());
            }

            Object.DestroyImmediate(image);
            texture.Release();
            Object.DestroyImmediate(texture);
            Debug.Log($"CAT_VISUAL_BOUNDS center={bounds.center} size={bounds.size}");
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }
    }
}
