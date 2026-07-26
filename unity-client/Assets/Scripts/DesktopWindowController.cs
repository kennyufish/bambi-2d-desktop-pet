using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace YourCat.DesktopPet
{
    public sealed class DesktopWindowController : MonoBehaviour
    {
        public static event Action BeforeQuit;

        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsExLayered = 0x00080000;
        private const int WsExTransparent = 0x00000020;
        private const uint LwaAlpha = 0x00000002;
        private static readonly IntPtr HwndTopmost = new(-1);

        [SerializeField] private Camera desktopCamera;
        [SerializeField] private DesktopSettingsController settings;
        private IntPtr window;
        private IntPtr originalStyle;
        private IntPtr originalExtendedStyle;
        private bool clickThrough;
        private bool initialized;

        private void Start()
        {
            Application.runInBackground = true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                QuitApplication();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!initialized)
            {
                TryInitializeWindow();
                return;
            }

            var shouldClickThrough = !CursorIsOverPet();
            if (shouldClickThrough != clickThrough)
            {
                clickThrough = shouldClickThrough;
                var style = GetWindowLongPtr(window, GwlExStyle).ToInt64();
                var updated = clickThrough ? style | WsExTransparent : style & ~WsExTransparent;
                SetWindowLongPtr(window, GwlExStyle, new IntPtr(updated));
            }
#endif
        }

        public void QuitApplication()
        {
            BeforeQuit?.Invoke();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            RestoreWindow();
            System.Environment.Exit(0);
#else
            Application.Quit();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void TryInitializeWindow()
        {
            window = FindWindow(null, Application.productName);
            if (window == IntPtr.Zero)
                return;

            originalStyle = GetWindowLongPtr(window, GwlStyle);
            originalExtendedStyle = GetWindowLongPtr(window, GwlExStyle);
            SetWindowLongPtr(window, GwlStyle, new IntPtr(WsPopup));
            var extendedStyle = originalExtendedStyle.ToInt64();
            SetWindowLongPtr(window, GwlExStyle, new IntPtr(extendedStyle | WsExLayered));
            var margins = new Margins { left = -1 };
            DwmExtendFrameIntoClientArea(window, ref margins);
            SetLayeredWindowAttributes(window, 0, 255, LwaAlpha);
            SetWindowPos(window, HwndTopmost, 0, 0, Display.main.systemWidth, Display.main.systemHeight, 0x0060);
            initialized = true;
        }

        private void OnApplicationQuit()
        {
            RestoreWindow();
        }

        private void RestoreWindow()
        {
            if (!initialized || window == IntPtr.Zero)
                return;

            var margins = new Margins();
            DwmExtendFrameIntoClientArea(window, ref margins);
            SetWindowLongPtr(window, GwlStyle, originalStyle);
            SetWindowLongPtr(window, GwlExStyle, originalExtendedStyle);
            initialized = false;
        }
#endif

        private bool CursorIsOverPet()
        {
            if (!GetCursorPos(out var cursor))
                return false;

            if (!ScreenToClient(window, ref cursor))
                return false;

            var dpiScale = GetDpiForWindow(window) / 96f;
            var point = new Vector3(cursor.x * dpiScale, Screen.height - cursor.y * dpiScale, 0f);
            if (settings != null && settings.IsScreenPointInside(point))
                return true;
            return Physics.Raycast(desktopCamera.ScreenPointToRay(point), 100f);
        }

#if UNITY_STANDALONE_WIN
        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins
        {
            public int left;
            public int right;
            public int top;
            public int bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint colorKey, byte alpha, uint flags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref Point point);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins margins);
#endif
    }
}
