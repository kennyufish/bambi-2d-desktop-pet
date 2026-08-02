using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace YourCat.DesktopPet.Editor
{
    public static class StandardCatBuilder
    {
        private const string OutputDirectory = "Assets/StandardCat";
        private const string SourceModelPath = OutputDirectory + "/Source/cat_v2_tabby_plush.fbx";
        private const string TabbyTexturePath = OutputDirectory + "/Source/cat_tabby_fur_tile_v2.png";
        private const string TabbyMaterialPath = OutputDirectory + "/StandardCatTabby.mat";
        private const string PrefabPath = OutputDirectory + "/StandardCat.prefab";
        private const string ControllerPath = OutputDirectory + "/StandardCat.controller";

        [MenuItem("Your Cat/Create Standard Cat")]
        public static void Create()
        {
            Directory.CreateDirectory(OutputDirectory);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
            if (source == null)
                throw new FileNotFoundException($"Missing imported cat model: {SourceModelPath}");

            var root = new GameObject("StandardCat");
            var poseRoot = new GameObject("PoseRoot");
            poseRoot.transform.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Model";
            model.transform.SetParent(poseRoot.transform, false);
            NormalizeModel(model, 2.65f);
            ApplyTabbyMaterial(model);
            ConfigureRigPivots(model.transform);

            foreach (var sourceAnimator in model.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(sourceAnimator);

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = CreateAnimatorController(root.transform, poseRoot.transform, model.transform);

            var bounds = CalculateBounds(root);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = new Vector3(bounds.size.x * 0.9f, bounds.size.y * 0.96f, bounds.size.z * 0.9f);

            var morph = root.AddComponent<CatMorphController>();
            var morphObject = new SerializedObject(morph);
            morphObject.FindProperty("body").objectReferenceValue = model.transform;
            morphObject.FindProperty("head").objectReferenceValue = Find(model.transform, "Head");
            morphObject.FindProperty("neck").objectReferenceValue = Find(model.transform, "Neck");
            var legs = morphObject.FindProperty("legs");
            var legNames = new[] { "L_Leg_Upper", "R_Leg_Upper", "L_BLeg_Upper", "R_BLeg_Upper" };
            legs.arraySize = legNames.Length;
            for (var index = 0; index < legNames.Length; index++)
                legs.GetArrayElementAtIndex(index).objectReferenceValue = Find(model.transform, legNames[index]);
            morphObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created anatomically rigged cat prefab at {PrefabPath}");
        }

        private static void ApplyTabbyMaterial(GameObject model)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TabbyTexturePath);
            if (texture == null)
                throw new FileNotFoundException($"Missing generated tabby texture: {TabbyTexturePath}");

            var material = AssetDatabase.LoadAssetAtPath<Material>(TabbyMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = "StandardCatTabby" };
                AssetDatabase.CreateAsset(material, TabbyMaterialPath);
            }

            material.mainTexture = texture;
            material.color = new Color(0.72f, 0.53f, 0.34f, 1f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.22f);
            EditorUtility.SetDirty(material);

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith("Fur_", System.StringComparison.Ordinal))
                    renderer.sharedMaterial = material;
            }
        }

        private static void ConfigureRigPivots(Transform model)
        {
            SetWorldPivot(Find(model, "root"), new Vector3(0f, 1.05f, 0f));
            SetWorldPivot(Find(model, "butt"), new Vector3(0.7f, 1.2f, 0f));
            SetWorldPivot(Find(model, "belly"), new Vector3(0f, 1.2f, 0f));
            SetWorldPivot(Find(model, "chest"), new Vector3(-0.72f, 1.38f, 0f));
            SetWorldPivot(Find(model, "Neck"), new Vector3(-1.02f, 1.72f, 0f));
            SetWorldPivot(Find(model, "Head"), new Vector3(-1.2f, 1.98f, 0f));
            SetWorldPivot(Find(model, "tail1"), new Vector3(1.02f, 1.43f, 0f));
            SetWorldPivot(Find(model, "tail2"), new Vector3(1.82f, 1.2f, 0f));

            foreach (var (prefix, x, z) in new[]
            {
                ("L_Leg", -0.83f, 0.34f),
                ("R_Leg", -0.83f, -0.34f),
                ("L_BLeg", 0.73f, 0.34f),
                ("R_BLeg", 0.73f, -0.34f),
            })
            {
                SetWorldPivot(Find(model, prefix + "_Upper"), new Vector3(x, 1.05f, z));
                SetWorldPivot(Find(model, prefix + "_Lower"), new Vector3(x, 0.58f, z));
            }
        }

        private static void SetWorldPivot(Transform target, Vector3 worldPosition)
        {
            var children = new (Transform transform, Vector3 position, Quaternion rotation)[target.childCount];
            for (var index = 0; index < target.childCount; index++)
            {
                var child = target.GetChild(index);
                children[index] = (child, child.position, child.rotation);
            }

            target.position = worldPosition;
            foreach (var child in children)
                child.transform.SetPositionAndRotation(child.position, child.rotation);
        }

        private static AnimatorController CreateAnimatorController(Transform root, Transform poseRoot, Transform model)
        {
            DeleteAssetIfPresent(ControllerPath);

            var head = Find(model, "Head");
            var neck = Find(model, "Neck");
            var chest = Find(model, "chest");
            var tail1 = Find(model, "tail1");
            var tail2 = Find(model, "tail2");
            var frontLeft = Find(model, "L_Leg_Upper");
            var frontRight = Find(model, "R_Leg_Upper");
            var backLeft = Find(model, "L_BLeg_Upper");
            var backRight = Find(model, "R_BLeg_Upper");
            var frontLeftLower = Find(model, "L_Leg_Lower");
            var frontRightLower = Find(model, "R_Leg_Lower");
            var backLeftLower = Find(model, "L_BLeg_Lower");
            var backRightLower = Find(model, "R_BLeg_Lower");

            var idle = CreateClip("Idle", true, clip =>
            {
                ScaleCurve(clip, root, chest, Vector3.one, new Vector3(1.015f, 1.015f, 1.015f), Vector3.one);
                RotationCurve(clip, root, tail1, Vector3.zero, new Vector3(0f, 0f, 10f), Vector3.zero);
                RotationCurve(clip, root, tail2, Vector3.zero, new Vector3(0f, 0f, 7f), Vector3.zero);
            });

            var walk = CreateClip("Walk", true, clip =>
            {
                RotationCurve(clip, root, frontLeft, new Vector3(0f, 0f, -24f), new Vector3(0f, 0f, 24f), new Vector3(0f, 0f, -24f));
                RotationCurve(clip, root, frontRight, new Vector3(0f, 0f, 24f), new Vector3(0f, 0f, -24f), new Vector3(0f, 0f, 24f));
                RotationCurve(clip, root, backLeft, new Vector3(0f, 0f, 24f), new Vector3(0f, 0f, -24f), new Vector3(0f, 0f, 24f));
                RotationCurve(clip, root, backRight, new Vector3(0f, 0f, -24f), new Vector3(0f, 0f, 24f), new Vector3(0f, 0f, -24f));
                RotationCurve(clip, root, tail1, new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, 12f), new Vector3(0f, 0f, -10f));
                PositionCurve(clip, root, poseRoot, Vector3.zero, new Vector3(0f, 0.06f, 0f), Vector3.zero);
            });

            var sit = CreateClip("Sit", false, clip =>
            {
                PositionCurve(clip, root, poseRoot, Vector3.zero, new Vector3(0f, -0.22f, 0f), new Vector3(0f, -0.22f, 0f));
                RotationCurve(clip, root, poseRoot, Vector3.zero, new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, -12f));
                RotationCurve(clip, root, backLeft, Vector3.zero, new Vector3(0f, 0f, 62f), new Vector3(0f, 0f, 62f));
                RotationCurve(clip, root, backRight, Vector3.zero, new Vector3(0f, 0f, 62f), new Vector3(0f, 0f, 62f));
                RotationCurve(clip, root, backLeftLower, Vector3.zero, new Vector3(0f, 0f, -82f), new Vector3(0f, 0f, -82f));
                RotationCurve(clip, root, backRightLower, Vector3.zero, new Vector3(0f, 0f, -82f), new Vector3(0f, 0f, -82f));
                RotationCurve(clip, root, neck, Vector3.zero, new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, -10f));
            });

            var lieDown = CreateClip("LieDown", false, clip =>
            {
                ScaleCurve(clip, root, poseRoot, Vector3.one, new Vector3(1f, 0.5f, 1f), new Vector3(1f, 0.5f, 1f));
                RotationCurve(clip, root, frontLeft, Vector3.zero, new Vector3(0f, 0f, 68f), new Vector3(0f, 0f, 68f));
                RotationCurve(clip, root, frontRight, Vector3.zero, new Vector3(0f, 0f, 68f), new Vector3(0f, 0f, 68f));
                RotationCurve(clip, root, frontLeftLower, Vector3.zero, new Vector3(0f, 0f, -88f), new Vector3(0f, 0f, -88f));
                RotationCurve(clip, root, frontRightLower, Vector3.zero, new Vector3(0f, 0f, -88f), new Vector3(0f, 0f, -88f));
                RotationCurve(clip, root, backLeft, Vector3.zero, new Vector3(0f, 0f, 52f), new Vector3(0f, 0f, 52f));
                RotationCurve(clip, root, backRight, Vector3.zero, new Vector3(0f, 0f, 52f), new Vector3(0f, 0f, 52f));
            });

            var sleep = CreateClip("Sleep", true, clip =>
            {
                ScaleCurve(clip, root, poseRoot, new Vector3(1f, 0.5f, 1f), new Vector3(1f, 0.52f, 1f), new Vector3(1f, 0.5f, 1f));
                RotationCurve(clip, root, frontLeft, new Vector3(0f, 0f, 68f), new Vector3(0f, 0f, 72f), new Vector3(0f, 0f, 68f));
                RotationCurve(clip, root, frontRight, new Vector3(0f, 0f, 68f), new Vector3(0f, 0f, 72f), new Vector3(0f, 0f, 68f));
                RotationCurve(clip, root, frontLeftLower, new Vector3(0f, 0f, -88f), new Vector3(0f, 0f, -92f), new Vector3(0f, 0f, -88f));
                RotationCurve(clip, root, frontRightLower, new Vector3(0f, 0f, -88f), new Vector3(0f, 0f, -92f), new Vector3(0f, 0f, -88f));
                RotationCurve(clip, root, backLeft, new Vector3(0f, 0f, 52f), new Vector3(0f, 0f, 56f), new Vector3(0f, 0f, 52f));
                RotationCurve(clip, root, backRight, new Vector3(0f, 0f, 52f), new Vector3(0f, 0f, 56f), new Vector3(0f, 0f, 52f));
                RotationCurve(clip, root, neck, new Vector3(0f, 0f, 12f), new Vector3(0f, 0f, 16f), new Vector3(0f, 0f, 12f));
                RotationCurve(clip, root, head, new Vector3(0f, 0f, 8f), new Vector3(0f, 0f, 11f), new Vector3(0f, 0f, 8f));
                RotationCurve(clip, root, tail1, new Vector3(0f, 0f, -8f), new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, -8f));
                foreach (var eyeName in new[]
                {
                    "EyeGreen_L", "EyeGreen_R", "EyeBlack_L", "EyeBlack_R",
                    "EyeHighlight_L", "EyeHighlight_R",
                })
                {
                    ScaleCurve(
                        clip,
                        root,
                        Find(model, eyeName),
                        new Vector3(1f, 1f, 0.08f),
                        new Vector3(1f, 1f, 0.04f),
                        new Vector3(1f, 1f, 0.08f));
                }
            });

            var petted = CreateClip("Petted", false, clip =>
            {
                RotationCurve(clip, root, head, Vector3.zero, new Vector3(0f, 0f, 18f), Vector3.zero);
                RotationCurve(clip, root, tail1, Vector3.zero, new Vector3(0f, 0f, 28f), Vector3.zero);
                RotationCurve(clip, root, tail2, Vector3.zero, new Vector3(0f, 0f, 18f), Vector3.zero);
            });

            var eat = CreateClip("Eat", false, clip =>
            {
                RotationCurve(clip, root, neck, Vector3.zero, new Vector3(0f, 0f, 42f), Vector3.zero);
                RotationCurve(clip, root, head, Vector3.zero, new Vector3(0f, 0f, 28f), Vector3.zero);
                PositionCurve(clip, root, poseRoot, Vector3.zero, new Vector3(0f, -0.12f, 0f), Vector3.zero);
            });

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Sit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("LieDown", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Sleep", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Petted", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Eat", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;
            var idleState = AddState(machine, "Idle", idle);
            machine.defaultState = idleState;
            var walkState = AddState(machine, "Walk", walk);
            var sitState = AddState(machine, "Sit", sit);
            var lieState = AddState(machine, "LieDown", lieDown);
            var sleepState = AddState(machine, "Sleep", sleep);
            var pettedState = AddState(machine, "Petted", petted);
            var eatState = AddState(machine, "Eat", eat);

            AddCondition(idleState, walkState, AnimatorConditionMode.Greater, 0.1f, "Speed");
            AddCondition(walkState, idleState, AnimatorConditionMode.Less, 0.1f, "Speed");
            AddTrigger(machine, sitState, "Sit");
            AddTrigger(machine, lieState, "LieDown");
            AddTrigger(machine, sleepState, "Sleep");
            AddTrigger(machine, pettedState, "Petted");
            AddTrigger(machine, eatState, "Eat");
            AddTimedReturn(sitState, idleState);
            AddTimedReturn(lieState, idleState);
            AddTimedReturn(sleepState, idleState, 5f);
            AddTimedReturn(pettedState, idleState);
            AddTimedReturn(eatState, idleState);
            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            var state = machine.AddState(name);
            state.motion = clip;
            return state;
        }

        private static AnimationClip CreateClip(string name, bool loop, System.Action<AnimationClip> configure)
        {
            var path = $"{OutputDirectory}/{name}.anim";
            DeleteAssetIfPresent(path);
            var clip = new AnimationClip { name = name, frameRate = 30f };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            configure(clip);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void PositionCurve(AnimationClip clip, Transform root, Transform target, Vector3 startOffset, Vector3 middleOffset, Vector3 endOffset)
        {
            var path = AnimationUtility.CalculateTransformPath(target, root);
            var baseValue = target.localPosition;
            SetVectorCurve(clip, path, "m_LocalPosition", baseValue + startOffset, baseValue + middleOffset, baseValue + endOffset);
        }

        private static void ScaleCurve(AnimationClip clip, Transform root, Transform target, Vector3 startFactor, Vector3 middleFactor, Vector3 endFactor)
        {
            var path = AnimationUtility.CalculateTransformPath(target, root);
            var baseValue = target.localScale;
            SetVectorCurve(clip, path, "m_LocalScale", Vector3.Scale(baseValue, startFactor), Vector3.Scale(baseValue, middleFactor), Vector3.Scale(baseValue, endFactor));
        }

        private static void RotationCurve(AnimationClip clip, Transform root, Transform target, Vector3 startDelta, Vector3 middleDelta, Vector3 endDelta)
        {
            var path = AnimationUtility.CalculateTransformPath(target, root);
            var baseRotation = target.localRotation;
            var start = baseRotation * Quaternion.Euler(startDelta);
            var middle = baseRotation * Quaternion.Euler(middleDelta);
            var end = baseRotation * Quaternion.Euler(endDelta);
            SetCurve(clip, path, "m_LocalRotation.x", start.x, middle.x, end.x);
            SetCurve(clip, path, "m_LocalRotation.y", start.y, middle.y, end.y);
            SetCurve(clip, path, "m_LocalRotation.z", start.z, middle.z, end.z);
            SetCurve(clip, path, "m_LocalRotation.w", start.w, middle.w, end.w);
        }

        private static void SetVectorCurve(AnimationClip clip, string path, string prefix, Vector3 start, Vector3 middle, Vector3 end)
        {
            SetCurve(clip, path, prefix + ".x", start.x, middle.x, end.x);
            SetCurve(clip, path, prefix + ".y", start.y, middle.y, end.y);
            SetCurve(clip, path, prefix + ".z", start.z, middle.z, end.z);
        }

        private static void SetCurve(AnimationClip clip, string path, string property, float start, float middle, float end)
        {
            var curve = new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.5f, middle), new Keyframe(1f, end));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static void AddCondition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string parameter)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            transition.AddCondition(mode, threshold, parameter);
        }

        private static void AddTrigger(AnimatorStateMachine machine, AnimatorState state, string parameter)
        {
            var transition = machine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        }

        private static void AddTimedReturn(AnimatorState from, AnimatorState to, float exitTime = 1f)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = 0.12f;
        }

        private static void NormalizeModel(GameObject model, float targetHeight)
        {
            var bounds = CalculateBounds(model);
            var sourceUsesZAsUp = bounds.size.z > bounds.size.y;
            var sourceHeight = sourceUsesZAsUp ? bounds.size.z : bounds.size.y;
            var scale = targetHeight / sourceHeight;
            model.transform.localScale *= scale;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new System.InvalidOperationException("Cat model contains no renderers.");

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                    return transform;
            }

            throw new System.InvalidOperationException($"Cat bone not found: {name}");
        }

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }
}
