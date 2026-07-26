using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace YourCat.DesktopPet
{
    public sealed class DesktopSettingsController : MonoBehaviour
    {
        private const string StartupName = "YourCatDesktopPet";
        private static readonly string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        [SerializeField] private CatMorphController morph;
        [SerializeField] private DesktopPetBehaviour behaviour;
        [SerializeField] private Transform cat;

        private readonly CatShape shape = new();
        private Rect panelRect;
        private bool visible;
        private bool startupEnabled;
        private float scale = 1f;
        private float speed = 0.65f;

        private void Start()
        {
            shape.weight = PlayerPrefs.GetFloat("cat.weight", 0.5f);
            shape.faceWidth = PlayerPrefs.GetFloat("cat.faceWidth", 0.5f);
            shape.earSize = PlayerPrefs.GetFloat("cat.earSize", 0.5f);
            shape.legLength = PlayerPrefs.GetFloat("cat.legLength", 0.5f);
            scale = PlayerPrefs.GetFloat("cat.scale", 1f);
            speed = PlayerPrefs.GetFloat("cat.speed", 0.65f);
            startupEnabled = IsStartupEnabled();
            Apply();
        }

        public void Toggle()
        {
            visible = !visible;
        }

        public bool IsScreenPointInside(Vector2 screenPoint)
        {
            if (!visible)
                return false;

            var guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            return panelRect.Contains(guiPoint);
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            var width = 340f;
            var height = 390f;
            panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            panelRect = GUI.Window(8107, panelRect, DrawSettings, "你家猫桌宠设置");
        }

        private void DrawSettings(int id)
        {
            GUILayout.Label($"胖瘦  {shape.weight:0.00}");
            shape.weight = GUILayout.HorizontalSlider(shape.weight, 0f, 1f);
            GUILayout.Label($"脸宽  {shape.faceWidth:0.00}");
            shape.faceWidth = GUILayout.HorizontalSlider(shape.faceWidth, 0f, 1f);
            GUILayout.Label($"耳朵  {shape.earSize:0.00}");
            shape.earSize = GUILayout.HorizontalSlider(shape.earSize, 0f, 1f);
            GUILayout.Label($"腿长  {shape.legLength:0.00}");
            shape.legLength = GUILayout.HorizontalSlider(shape.legLength, 0f, 1f);
            GUILayout.Label($"整体大小  {scale:0.00}");
            scale = GUILayout.HorizontalSlider(scale, 0.65f, 1.35f);
            GUILayout.Label($"移动速度  {speed:0.00}");
            speed = GUILayout.HorizontalSlider(speed, 0.2f, 1.2f);

            var requestedStartup = GUILayout.Toggle(startupEnabled, "登录 Windows 后自动启动");
            if (requestedStartup != startupEnabled)
            {
                startupEnabled = requestedStartup;
                SetStartupEnabled(startupEnabled);
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("应用并保存"))
            {
                Apply();
                Save();
            }
            if (GUILayout.Button("关闭"))
                visible = false;

            GUI.DragWindow(new Rect(0f, 0f, panelRect.width, 28f));
        }

        private void Apply()
        {
            morph.Apply(shape);
            cat.localScale = Vector3.one * scale;
            behaviour.SetWalkSpeed(speed);
        }

        private void Save()
        {
            PlayerPrefs.SetFloat("cat.weight", shape.weight);
            PlayerPrefs.SetFloat("cat.faceWidth", shape.faceWidth);
            PlayerPrefs.SetFloat("cat.earSize", shape.earSize);
            PlayerPrefs.SetFloat("cat.legLength", shape.legLength);
            PlayerPrefs.SetFloat("cat.scale", scale);
            PlayerPrefs.SetFloat("cat.speed", speed);
            PlayerPrefs.Save();
        }

        private static bool IsStartupEnabled()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (RegOpenKeyEx(HkeyCurrentUser, StartupKey, 0, KeyRead, out var key) != 0)
                return false;

            var result = RegQueryValueEx(key, StartupName, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) == 0;
            RegCloseKey(key);
            return result;
#else
            return false;
#endif
        }

        private static void SetStartupEnabled(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (RegOpenKeyEx(HkeyCurrentUser, StartupKey, 0, KeySetValue, out var key) != 0)
                return;

            if (enabled)
            {
                var value = $"\"{Application.dataPath.Replace("_Data", ".exe")}\"";
                RegSetValueEx(key, StartupName, 0, RegSz, value, (value.Length + 1) * 2);
            }
            else
            {
                RegDeleteValue(key, StartupName);
            }
            RegCloseKey(key);
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static readonly IntPtr HkeyCurrentUser = new(unchecked((int)0x80000001));
        private const int KeyRead = 0x20019;
        private const int KeySetValue = 0x0002;
        private const int RegSz = 1;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegOpenKeyEx(IntPtr root, string subKey, int options, int access, out IntPtr result);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegQueryValueEx(IntPtr key, string valueName, IntPtr reserved, IntPtr type, IntPtr data, IntPtr dataSize);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegSetValueEx(IntPtr key, string valueName, int reserved, int type, string data, int dataSize);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegDeleteValue(IntPtr key, string valueName);
        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr key);
#endif
    }
}
