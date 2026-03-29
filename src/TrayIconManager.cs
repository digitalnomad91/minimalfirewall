using System;
using System.Runtime.InteropServices;

namespace MinimalFirewall
{
    /// <summary>
    /// Manages the system tray icon using Win32 Shell_NotifyIcon.
    /// </summary>
    public enum TrayIconState { Locked, Unlocked, Alert }

    public sealed class TrayIconManager : IDisposable
    {
        #region Native constants and structs

        private const int WM_APP = 0x8000;
        private const int WM_TRAYICON = WM_APP + 1;
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIM_SETVERSION = 0x00000004;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NOTIFYICON_VERSION_4 = 4;
        private const uint WM_LBUTTONDBLCLK = 0x0203;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_CONTEXTMENU = 0x007B;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const uint MF_STRING = 0x00000000;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_NONOTIFY = 0x0080;

        private const uint IDM_SHOW = 1;
        private const uint IDM_LOCKDOWN = 2;
        private const uint IDM_EXIT = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        #endregion

        private readonly IntPtr _hwnd;
        private NOTIFYICONDATA _nid;
        private bool _added;
        private IntPtr _lockedIcon = IntPtr.Zero;
        private IntPtr _unlockedIcon = IntPtr.Zero;
        private IntPtr _alertIcon = IntPtr.Zero;
        private bool _isLocked;

        private readonly Action _onShow;
        private readonly Action _onToggleLockdown;
        private readonly Action _onExit;

        // Win32 window subclass for tray messages
        private WndProcHook? _hook;

        public TrayIconManager(Action onShow, Action onToggleLockdown, Action onExit)
        {
            _onShow = onShow;
            _onToggleLockdown = onToggleLockdown;
            _onExit = onExit;

            LoadIcons();
            CreateMessageWindow();
            AddTrayIcon();
        }

        private void LoadIcons()
        {
            // Load icons from assembly resources
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("MinimalFirewall.logo.ico");
            if (stream != null)
            {
                using var ms = new System.IO.MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                // Create HICON from stream bytes using Win32
                _lockedIcon = LoadIconFromBytes(ms.ToArray());
                _unlockedIcon = RecolorIcon(_lockedIcon, System.Drawing.Color.ForestGreen);
                _alertIcon = RecolorIcon(_lockedIcon, System.Drawing.Color.Orange);
            }
        }

        private static IntPtr LoadIconFromBytes(byte[] iconBytes)
        {
            // Write to temp file and load
            var tempPath = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllBytes(tempPath, iconBytes);
            var icon = LoadImage(IntPtr.Zero, tempPath, 1, 0, 0, 0x0010 | 0x0040); // LR_LOADFROMFILE | LR_DEFAULTSIZE
            try { System.IO.File.Delete(tempPath); } catch { }
            return icon;
        }

        private static IntPtr RecolorIcon(IntPtr srcIcon, System.Drawing.Color tint)
        {
            if (srcIcon == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                using var bmp = System.Drawing.Icon.FromHandle(srcIcon).ToBitmap();
                using var colored = TintBitmap(bmp, tint);
                return colored.GetHicon();
            }
            catch { return srcIcon; }
        }

        private static System.Drawing.Bitmap TintBitmap(System.Drawing.Bitmap src, System.Drawing.Color tint)
        {
            var result = new System.Drawing.Bitmap(src.Width, src.Height);
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    var c = src.GetPixel(x, y);
                    if (c.A > 10)
                        result.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, tint.R, tint.G, tint.B));
                    else
                        result.SetPixel(x, y, c);
                }
            return result;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        private void CreateMessageWindow()
        {
            _hook = new WndProcHook(OnWndProc);
            _hwnd = _hook.Handle;
        }

        private void AddTrayIcon()
        {
            _nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _unlockedIcon != IntPtr.Zero ? _unlockedIcon : _lockedIcon,
                szTip = "Minimal Firewall"
            };
            _added = Shell_NotifyIcon(NIM_ADD, ref _nid);

            // Set version for Win7+
            _nid.uVersion = NOTIFYICON_VERSION_4;
            Shell_NotifyIcon(NIM_SETVERSION, ref _nid);
        }

        public void SetIcon(TrayIconState state, bool isLocked)
        {
            _isLocked = isLocked;
            IntPtr icon = state switch
            {
                TrayIconState.Locked => _lockedIcon,
                TrayIconState.Alert => _alertIcon != IntPtr.Zero ? _alertIcon : _lockedIcon,
                _ => _unlockedIcon != IntPtr.Zero ? _unlockedIcon : _lockedIcon
            };

            if (_added)
            {
                _nid.hIcon = icon;
                _nid.uFlags = NIF_ICON;
                Shell_NotifyIcon(NIM_MODIFY, ref _nid);
            }
        }

        private IntPtr OnWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                uint notification = (uint)(lParam.ToInt64() & 0xFFFF);
                if (notification == WM_LBUTTONDBLCLK)
                {
                    _onShow();
                }
                else if (notification == WM_RBUTTONUP || notification == WM_CONTEXTMENU)
                {
                    ShowContextMenu();
                }
            }
            return WndProcHook.DefaultWndProc(hwnd, msg, wParam, lParam);
        }

        private void ShowContextMenu()
        {
            SetForegroundWindow(_hwnd);
            GetCursorPos(out var pt);
            IntPtr hMenu = CreatePopupMenu();
            try
            {
                AppendMenu(hMenu, MF_STRING, IDM_SHOW, "Show");
                AppendMenu(hMenu, MF_STRING, IDM_LOCKDOWN, _isLocked ? "Disable Lockdown" : "Enable Lockdown");
                AppendMenu(hMenu, MF_STRING, IDM_EXIT, "Exit");

                uint cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_NONOTIFY, pt.X, pt.Y, _hwnd, IntPtr.Zero);
                switch (cmd)
                {
                    case IDM_SHOW: _onShow(); break;
                    case IDM_LOCKDOWN: _onToggleLockdown(); break;
                    case IDM_EXIT: _onExit(); break;
                }
            }
            finally
            {
                DestroyMenu(hMenu);
            }
        }

        public void Dispose()
        {
            if (_added)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _nid);
                _added = false;
            }
            if (_unlockedIcon != IntPtr.Zero && _unlockedIcon != _lockedIcon) DestroyIcon(_unlockedIcon);
            if (_alertIcon != IntPtr.Zero && _alertIcon != _lockedIcon) DestroyIcon(_alertIcon);
            if (_lockedIcon != IntPtr.Zero) DestroyIcon(_lockedIcon);
            _hook?.Dispose();
        }
    }

    /// <summary>
    /// Minimal hidden Win32 window for receiving tray messages.
    /// </summary>
    internal sealed class WndProcHook : IDisposable
    {
        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate? _wndProcDelegate;
        private IntPtr _hwnd;
        private readonly Func<IntPtr, uint, IntPtr, IntPtr, IntPtr> _handler;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        public IntPtr Handle => _hwnd;

        public WndProcHook(Func<IntPtr, uint, IntPtr, IntPtr, IntPtr> handler)
        {
            _handler = handler;
            _wndProcDelegate = WndProc;

            string className = "MFW_TrayMsg_" + Guid.NewGuid().ToString("N")[..8];
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                lpszClassName = className,
                hInstance = IntPtr.Zero
            };
            RegisterClassEx(ref wc);
            _hwnd = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
            => _handler(hwnd, msg, wParam, lParam);

        public static IntPtr DefaultWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
            => DefWindowProc(hwnd, msg, wParam, lParam);

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
