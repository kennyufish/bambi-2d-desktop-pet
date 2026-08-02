using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YourCat.DesktopPet
{
    public sealed class DesktopPetSelfTest : MonoBehaviour
    {
        [SerializeField] private DesktopPetBehaviour behaviour;
        [SerializeField] private Animator animator;
        [SerializeField] private CatMorphController morph;
        [SerializeField] private DesktopSettingsController settings;
        [SerializeField] private DesktopTrayController tray;

        private IEnumerator Start()
        {
            if (!HasSelfTestArgument())
                yield break;

            var outputDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "self-test-images");
            Directory.CreateDirectory(outputDirectory);
            var results = new List<string> { "Your Cat Desktop Pet self-test" };
            var renderers = GetComponentsInChildren<Renderer>(true);
            results.Add(Result("Visible cat renderer", renderers.Length > 0 && renderers[0].enabled));

            morph.Apply(new CatShape { weight = 0f, faceWidth = 0f, earSize = 0f, legLength = 0f });
            var lowShape = morph.GetVisualSignature();
            morph.Apply(new CatShape { weight = 1f, faceWidth = 1f, earSize = 1f, legLength = 1f });
            var highShape = morph.GetVisualSignature();
            results.Add(Result("Weight slider morph", highShape.x > lowShape.x));
            results.Add(Result("Face width slider morph", highShape.y > lowShape.y));
            results.Add(Result("Ear size slider morph", highShape.z > lowShape.z));
            results.Add(Result("Leg length slider morph", highShape.w > lowShape.w));
            morph.Apply(new CatShape());

            tray.ToggleSettingsForTest();
            results.Add(Result("Settings panel open", settings.VisibleForTest));
            yield return Capture(outputDirectory, "Settings.png");
            tray.ToggleSettingsForTest();
            results.Add(Result("Settings panel close", !settings.VisibleForTest));

            tray.TogglePauseForTest();
            results.Add(Result("Tray pause", tray.PausedForTest && !behaviour.enabled && animator.speed == 0f));
            tray.TogglePauseForTest();
            results.Add(Result("Tray resume", !tray.PausedForTest && behaviour.enabled && animator.speed == 1f));
            tray.SetScaleForTest(75);
            results.Add(Result("Tray scale 75%", Mathf.Approximately(transform.localScale.x, 0.375f)));
            tray.SetScaleForTest(125);
            results.Add(Result("Tray scale 125%", Mathf.Approximately(transform.localScale.x, 0.625f)));
            tray.SetScaleForTest(100);

            var savedShape = new CatShape
            {
                weight = PlayerPrefs.GetFloat("cat.weight", 0.5f),
                faceWidth = PlayerPrefs.GetFloat("cat.faceWidth", 0.5f),
                earSize = PlayerPrefs.GetFloat("cat.earSize", 0.5f),
                legLength = PlayerPrefs.GetFloat("cat.legLength", 0.5f)
            };
            var savedScale = PlayerPrefs.GetFloat("cat.scale", 1f);
            var savedSpeed = PlayerPrefs.GetFloat("cat.speed", 0.65f);
            settings.ApplyAndSaveForTest(new CatShape { weight = 0.7f, faceWidth = 0.6f, earSize = 0.8f, legLength = 0.9f }, 1.2f, 0.9f);
            results.Add(Result("Settings apply scale", Mathf.Approximately(transform.localScale.x, 0.6f)));
            results.Add(Result("Settings apply speed", Mathf.Approximately(behaviour.WalkSpeedForTest, 0.9f)));
            results.Add(Result("Settings save", Mathf.Approximately(PlayerPrefs.GetFloat("cat.scale"), 1.2f) && Mathf.Approximately(PlayerPrefs.GetFloat("cat.speed"), 0.9f)));
            settings.ApplyAndSaveForTest(savedShape, savedScale, savedSpeed);

            var startupWasEnabled = settings.StartupEnabledForTest;
            settings.SetStartupForTest(!startupWasEnabled);
            results.Add(Result("Startup setting toggle", settings.StartupEnabledForTest != startupWasEnabled));
            settings.SetStartupForTest(startupWasEnabled);
            results.Add(Result("Startup setting restore", settings.StartupEnabledForTest == startupWasEnabled));

            var collider = GetComponent<Collider>();
            var ray = new Ray(collider.bounds.center + Vector3.back * 10f, Vector3.forward);
            results.Add(Result("Pointer collider", Physics.Raycast(ray, out var hit, 20f) && hit.collider == collider));

            var dragStart = transform.position;
            behaviour.BeginDragForTest(dragStart);
            behaviour.MoveDragForTest(dragStart + new Vector3(0.6f, 0.35f, 0f));
            yield return null;
            results.Add(Result("Drag movement", Vector3.Distance(transform.position, dragStart) > 0.5f));
            behaviour.EndDragForTest();

            var initialX = transform.position.x;
            yield return new WaitForSeconds(1.5f);
            results.Add(Result("Walk movement", Mathf.Abs(transform.position.x - initialX) > 0.1f));
            yield return Capture(outputDirectory, "00-walk.png");

            yield return VerifyAction(results, outputDirectory, "Petted", behaviour.Pet);
            yield return VerifyAction(results, outputDirectory, "Eat", behaviour.Feed);
            yield return VerifyAction(results, outputDirectory, "Sit", behaviour.SitDown);
            yield return VerifyAction(results, outputDirectory, "LieDown", behaviour.LieDownNow);
            yield return VerifyAction(results, outputDirectory, "Sleep", behaviour.GoToSleep);

            var passed = results.TrueForAll(line => !line.StartsWith("FAIL"));
            results.Add(passed ? "OVERALL: PASS" : "OVERALL: FAIL");
            File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Application.dataPath), "self-test-report.txt"), results);
            FindAnyObjectByType<DesktopWindowController>().QuitApplication();
        }

        private IEnumerator VerifyAction(List<string> results, string outputDirectory, string state, System.Action trigger)
        {
            trigger();
            yield return new WaitForSeconds(0.75f);
            results.Add(Result(state, animator.GetCurrentAnimatorStateInfo(0).IsName(state)));
            yield return Capture(outputDirectory, state + ".png");
            yield return new WaitForSeconds(0.8f);
        }

        private static IEnumerator Capture(string outputDirectory, string fileName)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, fileName));
            yield return new WaitForSeconds(0.2f);
        }

        private static string Result(string name, bool passed) => $"{(passed ? "PASS" : "FAIL")}: {name}";

        private static bool HasSelfTestArgument()
        {
            foreach (var argument in System.Environment.GetCommandLineArgs())
            {
                if (argument == "--self-test")
                    return true;
            }

            return false;
        }
    }
}
