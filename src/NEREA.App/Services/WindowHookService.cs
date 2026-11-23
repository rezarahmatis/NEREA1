using System.Runtime.InteropServices;
using NEREA.Audio;

namespace NEREA.App.Services;

/// <summary>
/// Win32 window hook via comctl32 subclassing. Provides: media keyboard keys
/// (WM_APPCOMMAND), a minimum window size (WM_GETMINMAXINFO), cursor hiding
/// for cinematic/ambient mode (WM_SETCURSOR) and the system tray icon.
/// Everything is defensive: if subclassing fails, the app keeps working.
/// </summary>
public sealed class WindowHookService : IDisposable
{
    public const int TrayCallback = 0x0500 + 777;

    private readonly PlaybackController _playback;
    private readonly MainViewModelProxy _proxy;

    private IntPtr _hwnd;
    private readonly SUBCLASSPROC _proc;
    private bool _subclassed;
    private bool _trayCreated;
    private uint _trayId = 1;
    private bool _hideCursorRequested;
    private IntPtr _trayIconHandle;

    // Win32
    private const uint WM_GETMINMAXINFO = 0x0024;
    private const uint WM_APPCOMMAND = 0x0319;
    private const uint WM_SETCURSOR = 0x0020;
    private const uint WM_USER_TRAY = 0x0500;
    private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
    private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 13;
    private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;
    private const int APPCOMMAND_MEDIA_STOP = 15;
    private const int APPCOMMAND_VOLUME_MUTE = 8;
    private const int APPCOMMAND_VOLUME_DOWN = 9;
    private const int APPCOMMAND_VOLUME_UP = 10;

    public WindowHookService(PlaybackController playback, MainViewModelProxy proxy)
    {
        _playback = playback;
        _proxy = proxy;
        _proc = WndProc;
    }

    /// <summary>Simple bridge so the hook can reach the main view model.</summary>
    public sealed class MainViewModelProxy
    {
        public Action? ToggleCinematic { get; set; }
        public Func<bool>? CursorHidden { get; set; }
    }

    public void Attach(IntPtr hwnd, string iconPath)
    {
        _hwnd = hwnd;
        try
        {
            _subclassed = SetWindowSubclass(hwnd, _proc, 97531, IntPtr.Zero);
        }
        catch
        {
            _subclassed = false;
        }
        try
        {
            _trayCreated = CreateTrayIcon(iconPath);
        }
        catch
        {
            _trayCreated = false;
        }
    }

    public void SetCursorHidden(bool hidden) => _hideCursorRequested = hidden;

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr idSubclass, IntPtr refData)
    {
        switch (msg)
        {
            case WM_GETMINMAXINFO:
                try
                {
                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.ptMinTrackSize = new POINT(960, 600);
                    Marshal.StructureToPtr(mmi, lParam, false);
                    return IntPtr.Zero; // handled
                }
                catch
                {
                    break;
                }

            case WM_APPCOMMAND:
                // GET_APPCOMMAND_LPARAM: (short)HIWORD(lParam) & 0xF000
                int appCommand = ((int)((long)lParam >> 16)) & 0xF000;
                switch (appCommand)
                {
                    case APPCOMMAND_MEDIA_PLAY_PAUSE << 12:
                        _playback.PlayPause();
                        return 1;
                    case APPCOMMAND_MEDIA_NEXTTRACK << 12:
                        _playback.Next();
                        return 1;
                    case APPCOMMAND_MEDIA_PREVIOUSTRACK << 12:
                        _playback.Previous();
                        return 1;
                    case APPCOMMAND_MEDIA_STOP << 12:
                        _playback.Pause();
                        return 1;
                    case APPCOMMAND_VOLUME_MUTE << 12:
                        return 0; // let Windows handle master mute
                }
                break;

            case WM_SETCURSOR:
                if (_hideCursorRequested)
                {
                    SetCursor(IntPtr.Zero);
                    return 1;
                }
                break;

            case WM_USER_TRAY + TrayCallback:
                HandleTrayMessage((uint)lParam);
                return IntPtr.Zero;

            case 0x0111: // WM_COMMAND (tray menu selections)
                HandleTrayCommand((int)((long)wParam & 0xFFFF));
                break;
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    public event Action? TrayShowRequested;
    public event Action? TrayExitRequested;

    private void HandleTrayMessage(uint lParamMsg)
    {
        const uint WM_LBUTTONDBLCLK = 0x0203;
        const uint WM_RBUTTONUP = 0x0205;
        if (lParamMsg == WM_LBUTTONDBLCLK)
        {
            TrayShowRequested?.Invoke();
        }
        else if (lParamMsg == WM_RBUTTONUP)
        {
            ShowTrayMenu();
        }
    }

    private void ShowTrayMenu()
    {
        try
        {
            IntPtr menu = CreatePopupMenu();
            AppendMenu(menu, 0x0000, 1, "Show NEREA");
            AppendMenu(menu, 0x0800, 0, null); // separator
            AppendMenu(menu, 0x0000, 2, "Exit");
            GetCursorPos(out var pt);
            SetForegroundWindow(_hwnd);
            TrackPopupMenu(menu, 0x0080, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
            DestroyMenu(menu);
        }
        catch
        {
            // Menu failure is cosmetic only.
        }
    }

    public void HandleTrayCommand(int commandId)
    {
        if (commandId == 1) TrayShowRequested?.Invoke();
        if (commandId == 2) TrayExitRequested?.Invoke();
    }

    private bool CreateTrayIcon(string iconPath)
    {
        _trayIconHandle = LoadImage(IntPtr.Zero, iconPath, 1 /*IMAGE_ICON*/, 16, 16, 0x10 /*LR_LOADFROMFILE*/);
        if (_trayIconHandle == IntPtr.Zero) return false;

        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = _trayId,
            uFlags = 0x01 | 0x02 | 0x04, // MESSAGE | ICON | TIP
            uCallbackMessage = WM_USER_TRAY + TrayCallback,
            hIcon = _trayIconHandle,
            szTip = "NEREA"
        };
        return Shell_NotifyIconW(0 /*NIM_ADD*/, ref data);
    }

    public void RemoveTrayIcon()
    {
        if (!_trayCreated) return;
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = _trayId
        };
        Shell_NotifyIconW(2 /*NIM_DELETE*/, ref data);
        _trayCreated = false;
    }

    // ── Interop ──────────────────────────────────────────────────────────────
    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetCursor(IntPtr cursor);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string? text);

    [DllImport("user32.dll")]
    private static extern bool TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATAW data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hinst, string name, uint type, int cx, int cy, uint load);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; public POINT(int x, int y) { X = x; Y = y; } }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
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
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    public void Dispose()
    {
        RemoveTrayIcon();
        // Subclasses dissolve with the window; no explicit removal API needed here.
    }
}
