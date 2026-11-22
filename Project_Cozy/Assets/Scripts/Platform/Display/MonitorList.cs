using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>모니터 하나. 좌표는 가상 바탕화면 기준 스크린 px(주 모니터 왼쪽 위가 0,0, Y 아래)이다.</summary>
public readonly struct MonitorInfo
{
    /// <summary>Windows 장치 이름(예: <c>\\.\DISPLAY2</c>). 선택한 모니터를 다시 찾는 첫째 단서다.</summary>
    public readonly string DeviceName;
    /// <summary>모니터 전체 rect. 드롭다운에 보이는 해상도이자 다시 찾는 둘째 단서다.</summary>
    public readonly RectInt Bounds;
    /// <summary>작업표시줄을 뺀 작업 영역. 창을 놓을 자리다.</summary>
    public readonly RectInt WorkArea;
    public readonly bool IsPrimary;

    public MonitorInfo(string deviceName, RectInt bounds, RectInt workArea, bool isPrimary)
    {
        DeviceName = deviceName;
        Bounds = bounds;
        WorkArea = workArea;
        IsPrimary = isPrimary;
    }
}

/// <summary>
/// 연결된 모니터 목록. 주 모니터가 1번(목록의 0번째)이고, 나머지는 왼쪽 → 오른쪽(같으면 위 → 아래) 순서다.
/// 실행할 때마다 번호가 바뀌면 헷갈리므로 열거 순서가 아니라 위치로 정렬한다(Docs/Development/MultiMonitorPreparation.md D1).
///
/// 에디터에서는 Win32를 부르지 않고 지금 화면 하나만 돌려준다(Platform 규약의 에디터 보호).
/// 열거는 드롭다운을 만들 때와 모니터를 고를 때만 부른다 — 상주 확인은 하지 않는다.
/// </summary>
public static class MonitorList
{
    public static List<MonitorInfo> Enumerate()
    {
        var result = new List<MonitorInfo>();
#if UNITY_EDITOR
        var screen = new RectInt(0, 0, Mathf.Max(Screen.currentResolution.width, 1), Mathf.Max(Screen.currentResolution.height, 1));
        result.Add(new MonitorInfo("Editor", screen, screen, true));
#else
        s_collecting = result;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, s_callback, IntPtr.Zero);
        s_collecting = null;
        result.Sort(CompareOrder);
#endif
        return result;
    }

    // 주 모니터 먼저, 그다음 왼쪽 → 오른쪽, 위 → 아래.
    private static int CompareOrder(MonitorInfo a, MonitorInfo b)
    {
        if (a.IsPrimary != b.IsPrimary) return a.IsPrimary ? -1 : 1;
        if (a.Bounds.x != b.Bounds.x) return a.Bounds.x.CompareTo(b.Bounds.x);
        return a.Bounds.y.CompareTo(b.Bounds.y);
    }

#if !UNITY_EDITOR
    private const uint MONITORINFOF_PRIMARY = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    // OS에 넘기는 콜백은 static 필드에 보관한다 — GC가 거두면 호출 시점에 액세스 위반이 난다(Platform 규약).
    // EnumDisplayMonitors는 부른 스레드에서 동기로 콜백하므로 메인 스레드 마샬링은 필요 없다.
    private static readonly MonitorEnumProc s_callback = OnMonitor;
    private static List<MonitorInfo> s_collecting;

    private static bool OnMonitor(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(MONITORINFOEX)) };
        if (GetMonitorInfo(hMonitor, ref mi))
        {
            s_collecting?.Add(new MonitorInfo(
                mi.szDevice,
                ToRect(mi.rcMonitor),
                ToRect(mi.rcWork),
                (mi.dwFlags & MONITORINFOF_PRIMARY) != 0));
        }
        return true; // 계속 열거
    }

    private static RectInt ToRect(RECT r) => new RectInt(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
#endif
}
