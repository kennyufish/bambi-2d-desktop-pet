using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace YourCat.DesktopPet.Editor
{
    public static class StandardCatBuilder
    {
        private const string OutputDirectory = "Assets/StandardCat";
        private const string PrefabPath = OutputDirectory + "/StandardCat.prefab";
        private const string ControllerPath = OutputDirectory + "/StandardCat.controller";

        [MenuItem("Your Cat/Create Standard Cat")]
        public static void Create()
        {
            Directory.CreateDirectory(OutputDirectory);

            var root = new GameObject("StandardCat");
            var fur = CreateMaterial("StandardCatFur", new Color(0.58f, 0.42f, 0.30f));
            var dark = CreateMaterial("StandardCatDark", new Color(0.12f, 0.09f, 0.07f));
            var eye = CreateMaterial("StandardCatEyes", new Color(0.35f, 0.72f, 0.32f));

            var torso = Part(root.transform, "Torso", PrimitiveType.Sphere, new Vector3(0f, 0.85f, 0f), new Vector3(1.25f, 0.85f, 0.72f), fur);
            var chest = Part(torso.transform, "Chest", PrimitiveType.Sphere, new Vector3(0.44f, 0.22f, 0f), new Vector3(0.72f, 0.78f, 0.68f), fur);
            var head = Part(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0.88f, 1.42f, 0f), new Vector3(0.68f, 0.62f, 0.62f), fur);

            Ear(head.transform, "LeftEar", new Vector3(0f, 0.48f, 0.30f), fur);
            Ear(head.transform, "RightEar", new Vector3(0f, 0.48f, -0.30f), fur);
            Eye(head.transform, "LeftEye", new Vector3(0.48f, 0.10f, 0.21f), eye, dark);
            Eye(head.transform, "RightEye", new Vector3(0.48f, 0.10f, -0.21f), eye, dark);
            Part(head.transform, "Muzzle", PrimitiveType.Sphere, new Vector3(0.51f, -0.10f, 0f), new Vector3(0.24f, 0.20f, 0.32f), fur);
            Part(head.transform, "Nose", PrimitiveType.Sphere, new Vector3(0.65f, -0.07f, 0f), new Vector3(0.10f, 0.08f, 0.12f), dark);

            Leg(root.transform, "FrontLeftLeg", new Vector3(0.58f, 0.36f, 0.31f), fur);
            Leg(root.transform, "FrontRightLeg", new Vector3(0.58f, 0.36f, -0.31f), fur);
            Leg(root.transform, "BackLeftLeg", new Vector3(-0.45f, 0.36f, 0.31f), fur);
            Leg(root.transform, "BackRightLeg", new Vector3(-0.45f, 0.36f, -0.31f), fur);
            Tail(root.transform, fur);

            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.85f, 0f);
            collider.radius = 0.72f;
            collider.height = 2.2f;
            collider.direction = 0;

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = CreateAnimatorController();

            var morph = root.AddComponent<CatMorphController>();
            var morphObject = new SerializedObject(morph);
            morphObject.FindProperty("torso").objectReferenceValue = torso.transform;
            morphObject.FindProperty("head").objectReferenceValue = head.transform;
            morphObject.FindProperty("leftEar").objectReferenceValue = head.transform.Find("LeftEar");
            morphObject.FindProperty("rightEar").objectReferenceValue = head.transform.Find("RightEar");
            var legs = morphObject.FindProperty("legs");
            legs.arraySize = 4;
            legs.GetArrayElementAtIndex(0).objectReferenceValue = root.transform.Find("FrontLeftLeg");
            legs.GetArrayElementAtIndex(1).objectReferenceValue = root.transform.Find("FrontRightLeg");
            legs.GetArrayElementAtIndex(2).objectReferenceValue = root.transform.Find("BackLeftLeg");
            legs.GetArrayElementAtIndex(3).objectReferenceValue = root.transform.Find("BackRightLeg");
            morphObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created {PrefabPath}");
        }

        private static AnimatorController CreateAnimatorController()
        {
            DeleteAssetIfPresent(ControllerPath);
            var idle = CreateClip("Idle", true, clip =>
            {
                PositionCurve(clip, "Torso", "m_LocalPosition.y", 0.85f, 0.88f, 0.85f);
                RotationCurve(clip, "Tail", "localEulerAnglesRaw.z", 0f, 8f, 0f);
            });
            var walk = CreateClip("Walk", true, clip =>
            {
                PositionCurve(clip, "Torso", "m_LocalPosition.y", 0.85f, 0.92f, 0.85f);
                RotationCurve(clip, "FrontLeftLeg", "localEulerAnglesRaw.z", -18f, 18f, -18f);
                RotationCurve(clip, "FrontRightLeg", "localEulerAnglesRaw.z", 18f, -18f, 18f);
                RotationCurve(clip, "BackLeftLeg", "localEulerAnglesRaw.z", 18f, -18f, 18f);
                RotationCurve(clip, "BackRightLeg", "localEulerAnglesRaw.z", -18f, 18f, -18f);
                RotationCurve(clip, "Tail", "localEulerAnglesRaw.z", -8f, 12f, -8f);
            });
            var sit = CreateClip("Sit", false, clip =>
            {
                PositionCurve(clip, "Torso", "m_LocalPosition.y", 0.85f, 0.62f, 0.62f);
                RotationCurve(clip, "Torso", "localEulerAnglesRaw.z", 0f, -12f, -12f);
                RotationCurve(clip, "BackLeftLeg", "localEulerAnglesRaw.z", 0f, 65f, 65f);
                RotationCurve(clip, "BackRightLeg", "localEulerAnglesRaw.z", 0f, 65f, 65f);
            });
            var lieDown = CreateClip("LieDown", false, clip =>
            {
                PositionCurve(clip, "Torso", "m_LocalPosition.y", 0.85f, 0.38f, 0.38f);
                RotationCurve(clip, "FrontLeftLeg", "localEulerAnglesRaw.z", 0f, -72f, -72f);
                RotationCurve(clip, "FrontRightLeg", "localEulerAnglesRaw.z", 0f, -72f, -72f);
                RotationCurve(clip, "BackLeftLeg", "localEulerAnglesRaw.z", 0f, 72f, 72f);
                RotationCurve(clip, "BackRightLeg", "localEulerAnglesRaw.z", 0f, 72f, 72f);
            });
            var sleep = CreateClip("Sleep", true, clip =>
            {
                PositionCurve(clip, "Torso", "m_LocalPosition.y", 0.38f, 0.41f, 0.38f);
                RotationCurve(clip, "Head", "localEulerAnglesRaw.z", 0f, -8f, 0f);
                RotationCurve(clip, "Tail", "localEulerAnglesRaw.z", 0f, 5f, 0f);
            });
            var petted = CreateClip("Petted", false, clip =>
            {
                PositionCurve(clip, "Head", "m_LocalPosition.y", 1.42f, 1.52f, 1.42f);
                RotationCurve(clip, "Head", "localEulerAnglesRaw.z", 0f, 12f, 0f);
                RotationCurve(clip, "Tail", "localEulerAnglesRaw.z", -5f, 22f, -5f);
            });
            var eat = CreateClip("Eat", false, clip =>
            {
                PositionCurve(clip, "Head", "m_LocalPosition.y", 1.42f, 0.92f, 1.42f);
                RotationCurve(clip, "Head", "localEulerAnglesRaw.z", 0f, 32f, 0f);
                PositionCurve(clip, "Torso", "m_LocalPosition.y", 0.85f, 0.78f, 0.85f);
            });

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Sit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("LieDown", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Sleep", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Petted", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Eat", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            machine.defaultState = idleState;
            var walkState = machine.AddState("Walk");
            walkState.motion = walk;
            var sitState = machine.AddState("Sit");
            sitState.motion = sit;
            var lieState = machine.AddState("LieDown");
            lieState.motion = lieDown;
            var sleepState = machine.AddState("Sleep");
            sleepState.motion = sleep;
            var pettedState = machine.AddState("Petted");
            pettedState.motion = petted;
            var eatState = machine.AddState("Eat");
            eatState.motion = eat;

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

        private static void PositionCurve(AnimationClip clip, string path, string property, float start, float middle, float end)
        {
            SetCurve(clip, path, property, start, middle, end);
        }

        private static void RotationCurve(AnimationClip clip, string path, string property, float start, float middle, float end)
        {
            SetCurve(clip, path, property, start, middle, end);
        }

        private static void SetCurve(AnimationClip clip, string path, string property, float start, float middle, float end)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, start),
                new Keyframe(0.5f, middle),
                new Keyframe(1f, end));
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

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        private static void Ear(Transform parent, string name, Vector3 position, Material material)
        {
            var ear = Part(parent, name, PrimitiveType.Sphere, position, new Vector3(0.24f, 0.48f, 0.18f), material);
            ear.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
        }

        private static void Eye(Transform parent, string name, Vector3 position, Material iris, Material pupil)
        {
            Part(parent, name, PrimitiveType.Sphere, position, new Vector3(0.10f, 0.15f, 0.13f), iris);
            Part(parent, name + "Pupil", PrimitiveType.Sphere, position + new Vector3(0.085f, 0f, 0f), new Vector3(0.035f, 0.09f, 0.055f), pupil);
        }

        private static void Leg(Transform parent, string name, Vector3 position, Material material)
        {
            var leg = Part(parent, name, PrimitiveType.Capsule, position, new Vector3(0.25f, 0.42f, 0.25f), material);
            Part(leg.transform, "Paw", PrimitiveType.Sphere, new Vector3(0.10f, -0.48f, 0f), new Vector3(0.32f, 0.20f, 0.28f), material);
        }

        private static void Tail(Transform parent, Material material)
        {
            var pivot = new GameObject("Tail").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(-0.70f, 1.00f, 0f);
            for (var index = 0; index < 5; index++)
            {
                var segment = Part(pivot, $"TailSegment{index + 1}", PrimitiveType.Capsule,
                    new Vector3(-0.12f * index, 0.18f * index, 0f), new Vector3(0.16f, 0.31f, 0.16f), material);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, -42f);
            }
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var path = $"{OutputDirectory}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
