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

        private IEnumerator Start()
        {
            if (!HasSelfTestArgument())
                yield break;

            var results = new List<string> { "Your Cat Desktop Pet self-test" };
            var initialX = transform.position.x;
            yield return new WaitForSeconds(1.5f);
            results.Add(Result("Walk movement", Mathf.Abs(transform.position.x - initialX) > 0.1f));

            yield return VerifyAction(results, "Petted", behaviour.Pet);
            yield return VerifyAction(results, "Eat", behaviour.Feed);
            yield return VerifyAction(results, "Sit", behaviour.SitDown);
            yield return VerifyAction(results, "LieDown", behaviour.LieDownNow);
            yield return VerifyAction(results, "Sleep", behaviour.GoToSleep);

            var passed = results.TrueForAll(line => !line.StartsWith("FAIL"));
            results.Add(passed ? "OVERALL: PASS" : "OVERALL: FAIL");
            File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Application.dataPath), "self-test-report.txt"), results);
            FindAnyObjectByType<DesktopWindowController>().QuitApplication();
        }

        private IEnumerator VerifyAction(List<string> results, string state, System.Action trigger)
        {
            trigger();
            yield return new WaitForSeconds(0.35f);
            results.Add(Result(state, animator.GetCurrentAnimatorStateInfo(0).IsName(state)));
            yield return new WaitForSeconds(0.8f);
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
