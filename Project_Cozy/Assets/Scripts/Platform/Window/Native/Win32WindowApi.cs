// ============================================================
// Win32WindowApi
//
// user32 / dwmapi P/Invoke 선언과 상수, 구조체만 모은 격리 레이어.
// 로직은 두지 않는다 — 호출 측(OverlayWindow)이 조합한다.
//
// Platform/CLAUDE.md 규칙: OS 호출(P/Invoke)은 이 폴더 안에서만.
// namespace 미사용(팀 컨벤션) — 글로벌 namespace 유지.
// ============================================================
using System;
using System.Runtime.InteropServices;

internal static class Win32WindowApi
{
    // ---- GetWindowLong nIndex ----
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    // ---- Window styles (GWL_STYLE) ----
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_CAPTION = 0x00C00000;
    public const uint WS_THICKFRAME = 0x00040000;
    public const uint WS_MINIMIZEBOX = 0x00020000;
    public const uint WS_MAXIMIZEBOX = 0x00010000;
    public const uint WS_SYSMENU = 0x00080000;

    // ---- Extended styles (GWL_EXSTYLE) ----
    public const uint WS_EX_LAYERED = 0x00080000;
    public const uint WS_EX_TRANSPARENT = 0x00000020;

    // ---- SetLayeredWindowAttributes dwFlags ----
    public const uint LWA_COLORKEY = 0x00000001;
    public const uint LWA_ALPHA = 0x00000002;

    // ---- SetWindowPos uFlags ----
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_FRAMECHANGED = 0x0020;

    // ---- SetWindowPos hWndInsertAfter ----
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    // ---- GetSystemMetrics nIndex (전체화면 사이징) ----
    public const int SM_CXSCREEN = 0; // 주 모니터 너비
    public const int SM_CYSCREEN = 1; // 주 모니터 높이

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    /// <summary>Win32 POINT — 화면/클라이언트 좌표(좌상단 원점, y-down).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    public static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern int SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("Dwmapi.dll")]
    public static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    /// <summary>OS 데스크톱 커서 위치(전역 화면 좌표). 창 포커스·투명 여부와 무관하게 갱신된다.</summary>
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>화면(데스크톱) 좌표를 지정 창의 클라이언트 좌표로 변환. 현재 창 위치·크기 기준이라 리사이즈/이동에 자동 대응.</summary>
    [DllImport("user32.dll")]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    /// <summary>UnityEngine.Color → Win32 COLORREF(0x00BBGGRR).</summary>
    public static uint ToColorRef(byte r, byte g, byte b)
    {
        return (uint)(r | (g << 8) | (b << 16));
    }
}
