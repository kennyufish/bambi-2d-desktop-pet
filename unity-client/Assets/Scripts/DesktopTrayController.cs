using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace YourCat.DesktopPet
{
    public sealed class DesktopTrayController : MonoBehaviour
    {
        private const int GwlpWndProc = -4;
        private const uint WmAppTray = 0x8001;
        private const uint WmRightButtonUp = 0x0205;
        private const uint NifMessage = 0x00000001;
        private const uint NifIcon = 0x00000002;
        private const uint NifTip = 0x00000004;
        private const uint NimAdd = 0x00000000;
        private const uint NimDelete = 0x00000002;
        private const uint MfString = 0x00000000;
        private const uint MfSeparator = 0x00000800;
        private const uint TpmRightButton = 0x0002;
        private const uint TpmReturnCommand = 0x0100;
        private const int CmdPause = 1001;
        private const int CmdSettings = 1000;
        private const int CmdScale75 = 1002;
        private const int CmdScale100 = 1003;
        private const int CmdScale125 = 1004;
        private const int CmdExit = 1099;

        [SerializeField] private DesktopPetBehaviour behaviour;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform cat;
        [SerializeField] private DesktopWindowController desktopWindow;
        [SerializeField] private DesktopSettingsController settings;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IntPtr window;
        private IntPtr originalWindowProcedure;
        private WindowProcedure windowProcedure;
        private NotifyIconData iconData;
#endif
        private int pendingCommand;
        private bool paused;

        private void Start()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            window = FindWindow(null, Application.productName);
            if (window == IntPtr.Zero)
                return;

            windowProcedure = HandleWindowMessage;
            originalWindowProcedure = SetWindowLongPtr(window, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(windowProcedure));
            iconData = new NotifyIconData
            {
                size = Marshal.SizeOf<NotifyIconData>(),
                window = window,
                id = 1,
                flags = NifMessage | NifIcon | NifTip,
                callbackMessage = WmAppTray,
                icon = LoadIcon(IntPtr.Zero, new IntPtr(32512)),
                tip = "Your Cat Desktop Pet"
            };
            ShellNotifyIcon(NimAdd, ref iconData);
            DesktopWindowController.BeforeQuit += RemoveTrayIcon;
#endif
        }

        private void Update()
        {
            if (pendingCommand == 0)
                return;

            var command = pendingCommand;
            pendingCommand = 0;
            switch (command)
            {
                case CmdSettings:
                    settings.Toggle();
                    break;
                case CmdPause:
                    paused = !paused;
                    behaviour.enabled = !paused;
                    animator.speed = paused ? 0f : 1f;
                    break;
                case CmdScale75:
                    cat.localScale = Vector3.one * 0.75f;
                    break;
                case CmdScale100:
                    cat.localScale = Vector3.one;
                    break;
                case CmdScale125:
                    cat.localScale = Vector3.one * 1.25f;
                    break;
                case CmdExit:
                    desktopWindow.QuitApplication();
                    break;
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IntPtr HandleWindowMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == WmAppTray && ((uint)lParam.ToInt64() & 0xffff) == WmRightButtonUp)
                pendingCommand = ShowMenu();

            return CallWindowProc(originalWindowProcedure, hwnd, message, wParam, lParam);
        }

        private int ShowMenu()
        {
            var menu = CreatePopupMenu();
            AppendMenu(menu, MfString, CmdSettings, "打开设置");
            AppendMenu(menu, MfSeparator, 0, string.Empty);
            AppendMenu(menu, MfString, CmdPause, paused ? "继续活动" : "暂停活动");
            AppendMenu(menu, MfSeparator, 0, string.Empty);
            AppendMenu(menu, MfString, CmdScale75, "大小 75%");
            AppendMenu(menu, MfString, CmdScale100, "大小 100%");
            AppendMenu(menu, MfString, CmdScale125, "大小 125%");
            AppendMenu(menu, MfSeparator, 0, string.Empty);
            AppendMenu(menu, MfString, CmdExit, "退出");
            GetCursorPos(out var point);
            SetForegroundWindow(window);
            var command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCommand, point.x, point.y, 0, window, IntPtr.Zero);
            DestroyMenu(menu);
            return command;
        }

        private void RemoveTrayIcon()
        {
            DesktopWindowController.BeforeQuit -= RemoveTrayIcon;
            if (iconData.size != 0)
                ShellNotifyIcon(NimDelete, ref iconData);
            if (window != IntPtr.Zero && originalWindowProcedure != IntPtr.Zero)
                SetWindowLongPtr(window, GwlpWndProc, originalWindowProcedure);
        }

        private delegate IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public int size;
            public IntPtr window;
            public uint id;
            public uint flags;
            public uint callbackMessage;
            public IntPtr icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string tip;
            public uint state;
            public uint stateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string info;
            public uint timeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string infoTitle;
            public uint infoFlags;
            public Guid guid;
            public IntPtr balloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point { public int x; public int y; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr previous, IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);
        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr menu, uint flags, int item, string text);
        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);
        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);
#endif
    }
}
