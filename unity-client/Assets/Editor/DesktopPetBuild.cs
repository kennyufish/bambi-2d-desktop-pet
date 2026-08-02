using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace YourCat.DesktopPet.Editor
{
    public static class DesktopPetBuild
    {
        private const string ScenePath = "Assets/DesktopPet.unity";
        private const string PrefabPath = "Assets/StandardCat/StandardCat.prefab";
        private const string BuildPath = "Build/DesktopPetV21/YourCatDesktopPet.exe";

        [MenuItem("Your Cat/Build Windows Desktop Pet")]
        public static void BuildWindows()
        {
            StandardCatBuilder.Create();
            CreateScene();

            PlayerSettings.productName = "Your Cat Desktop Pet";
            PlayerSettings.companyName = "Your Cat";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.useFlipModelSwapchain = false;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var result = BuildPipeline.BuildPlayer(
                new[] { ScenePath },
                BuildPath,
                BuildTarget.StandaloneWindows64,
                BuildOptions.Development);

            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException($"Desktop pet build failed: {result.summary.result}");

            Debug.Log($"Built {BuildPath}");
        }

        private static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var cat = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            cat.name = "DesktopCat";
            cat.transform.position = new Vector3(0f, -2.15f, 0f);

            var animator = cat.GetComponent<Animator>();
            var behaviour = cat.AddComponent<DesktopPetBehaviour>();
            var behaviourObject = new SerializedObject(behaviour);
            behaviourObject.FindProperty("animator").objectReferenceValue = animator;
            behaviourObject.ApplyModifiedPropertiesWithoutUndo();

            var selfTest = cat.AddComponent<DesktopPetSelfTest>();
            var selfTestObject = new SerializedObject(selfTest);
            selfTestObject.FindProperty("behaviour").objectReferenceValue = behaviour;
            selfTestObject.FindProperty("animator").objectReferenceValue = animator;
            selfTestObject.FindProperty("morph").objectReferenceValue = cat.GetComponent<CatMorphController>();
            selfTestObject.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("DesktopCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var window = cameraObject.AddComponent<DesktopWindowController>();
            var settings = cameraObject.AddComponent<DesktopSettingsController>();
            var settingsObject = new SerializedObject(settings);
            settingsObject.FindProperty("morph").objectReferenceValue = cat.GetComponent<CatMorphController>();
            settingsObject.FindProperty("behaviour").objectReferenceValue = behaviour;
            settingsObject.FindProperty("cat").objectReferenceValue = cat.transform;
            settingsObject.ApplyModifiedPropertiesWithoutUndo();

            var windowObject = new SerializedObject(window);
            windowObject.FindProperty("desktopCamera").objectReferenceValue = camera;
            windowObject.FindProperty("settings").objectReferenceValue = settings;
            windowObject.ApplyModifiedPropertiesWithoutUndo();

            var tray = cameraObject.AddComponent<DesktopTrayController>();
            var trayObject = new SerializedObject(tray);
            trayObject.FindProperty("behaviour").objectReferenceValue = behaviour;
            trayObject.FindProperty("animator").objectReferenceValue = animator;
            trayObject.FindProperty("cat").objectReferenceValue = cat.transform;
            trayObject.FindProperty("desktopWindow").objectReferenceValue = window;
            trayObject.FindProperty("settings").objectReferenceValue = settings;
            trayObject.ApplyModifiedPropertiesWithoutUndo();

            selfTestObject.FindProperty("settings").objectReferenceValue = settings;
            selfTestObject.FindProperty("tray").objectReferenceValue = tray;
            selfTestObject.ApplyModifiedPropertiesWithoutUndo();

            var lightObject = new GameObject("KeyLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
