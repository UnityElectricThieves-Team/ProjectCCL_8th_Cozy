using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public class WindowAspectFitter : MonoBehaviour
{
    [Header("Aspect Ratio")]
    [SerializeField] private int aspectWidth = 32;
    [SerializeField] private int aspectHeight = 3;

    [Header("Position")]
    [SerializeField] private bool dockToBottom = true;
    [SerializeField] private bool centerHorizontally = true;

    [Header("Safety")]
    [SerializeField] private bool clampToWorkAreaHeight = true;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool reapplyOnFocus = true;
    [SerializeField] private bool debugLogs;

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsSysMenu = 0x00080000L;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    private void Start()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (applyOnStart)
        {
            StartCoroutine(ApplyAfterWindowReady());
        }
#endif
    }

    private void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (hasFocus && reapplyOnFocus)
        {
            ApplyNow();
        }
#endif
    }

    [ContextMenu("Apply Now")]
    public void ApplyNow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetBorderlessStyle(hwnd);

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf(typeof(MonitorInfo)) };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var workWidth = monitorInfo.rcWork.right - monitorInfo.rcWork.left;
        var workHeight = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;

        var safeAspectWidth = Mathf.Max(1, aspectWidth);
        var safeAspectHeight = Mathf.Max(1, aspectHeight);
        var targetHeight = Mathf.RoundToInt((float)workWidth * safeAspectHeight / safeAspectWidth);

        if (clampToWorkAreaHeight && targetHeight > workHeight)
        {
            targetHeight = workHeight;
        }

        var targetWidth = workWidth;

        var x = centerHorizontally
            ? monitorInfo.rcWork.left + (workWidth - targetWidth) / 2
            : monitorInfo.rcWork.left;

        var y = dockToBottom
            ? monitorInfo.rcWork.bottom - targetHeight
            : monitorInfo.rcWork.top;

        SetWindowPos(hwnd, HwndTop, x, y, targetWidth, targetHeight, SwpNoZOrder | SwpNoActivate | SwpFrameChanged);

        if (debugLogs)
        {
            Debug.Log($"WindowAspectFitter applied: {targetWidth}x{targetHeight}, x={x}, y={y}");
        }
#endif
    }

    private IEnumerator ApplyAfterWindowReady()
    {
        for (var i = 0; i < 10; i++)
        {
            yield return null;
        }

        ApplyNow();
    }

    private static IntPtr GetWindowStyle(IntPtr hWnd)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, GwlStyle)
            : new IntPtr(GetWindowLong32(hWnd, GwlStyle));
    }

    private static void SetWindowStyle(IntPtr hWnd, IntPtr style)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hWnd, GwlStyle, style);
            return;
        }

        SetWindowLong32(hWnd, GwlStyle, style.ToInt32());
    }

    private static void SetBorderlessStyle(IntPtr hWnd)
    {
        var currentStyle = GetWindowStyle(hWnd).ToInt64();
        var newStyle = currentStyle & ~WsCaption & ~WsThickFrame & ~WsMinimizeBox & ~WsMaximizeBox & ~WsSysMenu;
        SetWindowStyle(hWnd, new IntPtr(newStyle));
    }
}
