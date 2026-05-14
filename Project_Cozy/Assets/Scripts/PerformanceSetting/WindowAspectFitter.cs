using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public class WindowAspectFitter : MonoBehaviour
{
    [Header("Aspect Ratio")]
    [SerializeField] private int _aspectWidth = 32;
    [SerializeField] private int _aspectHeight = 3;

    [Header("Position")]
    [SerializeField] private bool _dockToBottom = true;
    [SerializeField] private bool _centerHorizontally = true;

    [Header("Safety")]
    [SerializeField] private bool _clampToWorkAreaHeight = true;
    [SerializeField] private bool _applyOnStart = true;
    [SerializeField] private bool _reapplyOnFocus = true;
    [SerializeField] private bool _debugLogs;

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

    private const uint MONITOR_DEFAULT_TO_NEAREST = 2;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private const int GWL_STYLE = -16;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const long WS_MINIMIZEBOX = 0x00020000L;
    private const long WS_MAXIMIZEBOX = 0x00010000L;
    private const long WS_SYSMENU = 0x00080000L;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int CbSize;
        public NativeRect RcMonitor;
        public NativeRect RcWork;
        public uint DwFlags;
    }

    private void Start()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_applyOnStart)
        {
            StartCoroutine(ApplyAfterWindowReady());
        }
#endif
    }

    private void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (hasFocus && _reapplyOnFocus)
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

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULT_TO_NEAREST);
        var monitorInfo = new MonitorInfo { CbSize = Marshal.SizeOf(typeof(MonitorInfo)) };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var workWidth = monitorInfo.RcWork.Right - monitorInfo.RcWork.Left;
        var workHeight = monitorInfo.RcWork.Bottom - monitorInfo.RcWork.Top;

        var safeAspectWidth = Mathf.Max(1, _aspectWidth);
        var safeAspectHeight = Mathf.Max(1, _aspectHeight);
        var targetHeight = Mathf.RoundToInt((float)workWidth * safeAspectHeight / safeAspectWidth);

        if (_clampToWorkAreaHeight && targetHeight > workHeight)
        {
            targetHeight = workHeight;
        }

        var targetWidth = workWidth;

        var x = _centerHorizontally
            ? monitorInfo.RcWork.Left + (workWidth - targetWidth) / 2
            : monitorInfo.RcWork.Left;

        var y = _dockToBottom
            ? monitorInfo.RcWork.Bottom - targetHeight
            : monitorInfo.RcWork.Top;

        SetWindowPos(hwnd, HWND_TOP, x, y, targetWidth, targetHeight, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        if (_debugLogs)
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
            ? GetWindowLongPtr64(hWnd, GWL_STYLE)
            : new IntPtr(GetWindowLong32(hWnd, GWL_STYLE));
    }

    private static void SetWindowStyle(IntPtr hWnd, IntPtr style)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hWnd, GWL_STYLE, style);
            return;
        }

        SetWindowLong32(hWnd, GWL_STYLE, style.ToInt32());
    }

    private static void SetBorderlessStyle(IntPtr hWnd)
    {
        var currentStyle = GetWindowStyle(hWnd).ToInt64();
        var newStyle = currentStyle & ~WS_CAPTION & ~WS_THICKFRAME & ~WS_MINIMIZEBOX & ~WS_MAXIMIZEBOX & ~WS_SYSMENU;
        SetWindowStyle(hWnd, new IntPtr(newStyle));
    }
}
