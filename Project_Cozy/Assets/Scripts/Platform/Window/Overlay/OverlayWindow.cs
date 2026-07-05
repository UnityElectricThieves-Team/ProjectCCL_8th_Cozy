// ============================================================
// OverlayWindow
//
// WindowState를 실제 OS 창에 적용하는 레이어. "어떻게 적용하는가" 담당.
// HWND를 획득(획득 실패 시 다음 프레임 재시도)하고, 전달받은 WindowState를
// Win32 스타일/확장스타일/ColorKey/Z-order로 실현한다.
//
// per-pixel 클릭 통과는 ColorKey(LWA_COLORKEY)가 OS 레벨에서 자동 처리:
//   - ClickThrough=false → ColorKey 픽셀(빈 공간)만 통과, 캐릭터 픽셀은 클릭됨
//   - ClickThrough=true  → WS_EX_TRANSPARENT로 캐릭터 포함 전부 통과
// 카메라 BackgroundColor를 WindowState.ColorKey와 동일하게 맞춰야 한다.
//
// Win32 호출은 이 클래스와 Win32WindowApi에만 존재한다.
// Editor에서는 동작하지 않는다(Unity Editor 창이 깨짐) — 빌드에서만.
// ============================================================
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class OverlayWindow : MonoBehaviour
{
    [SerializeField, Tooltip("전체화면 시 하단 여백(px) — 작업 표시줄 공간 확보")]
    private int _fullscreenBottomMargin = 90;

    private IntPtr _hwnd = IntPtr.Zero;
    private WindowState? _pending; // HWND 미획득 시 보류된 상태

    /// <summary>네이티브 창 핸들. 획득 전이면 IntPtr.Zero.</summary>
    public IntPtr Hwnd => _hwnd;

    public bool HasWindow => _hwnd != IntPtr.Zero;

    private void Awake()
    {
        Application.runInBackground = true;
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        // 다른 Window 컴포넌트(WindowResizeHandler)가 Start에서 Hwnd를 읽으므로 미리 확보.
        EnsureHwnd();
#endif
    }

    /// <summary>창을 주 모니터 전체 크기로 (0,0)에 배치. 전체화면 오버레이.</summary>
    public void SetFullscreen()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        if (!EnsureHwnd()) return;
        int w = Win32WindowApi.GetSystemMetrics(Win32WindowApi.SM_CXSCREEN);
        int h = Win32WindowApi.GetSystemMetrics(Win32WindowApi.SM_CYSCREEN) - _fullscreenBottomMargin;
        Win32WindowApi.SetWindowPos(_hwnd, Win32WindowApi.HWND_TOPMOST, 0, 0, w, h,
            Win32WindowApi.SWP_FRAMECHANGED);
#endif
    }

    /// <summary>창을 지정 위치·크기로 배치.</summary>
    public void SetRegion(int x, int y, int width, int height)
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        if (!EnsureHwnd()) return;
        Win32WindowApi.SetWindowPos(_hwnd, Win32WindowApi.HWND_TOPMOST, x, y, width, height,
            Win32WindowApi.SWP_FRAMECHANGED);
#endif
    }

    /// <summary>지정 크기로 주 모니터 중앙에 배치.</summary>
    public void SetRegionCentered(int width, int height)
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        if (!EnsureHwnd()) return;
        int sw = Win32WindowApi.GetSystemMetrics(Win32WindowApi.SM_CXSCREEN);
        int sh = Win32WindowApi.GetSystemMetrics(Win32WindowApi.SM_CYSCREEN);
        SetRegion((sw - width) / 2, (sh - height) / 2, width, height);
#endif
    }

    /// <summary>원하는 창 상태를 적용. HWND 미획득 시 획득될 때까지 보류 후 자동 적용.</summary>
    public void Apply(WindowState state)
    {
        if (TryApply(state)) _pending = null;
        else _pending = state;
    }

    private void Update()
    {
        if (_pending!= null && TryApply(_pending))
            _pending = null;
    }

    /// <summary>HWND가 준비됐으면 적용하고 true. 아직이면 false.
    /// Editor/비Windows 빌드에서는 적용을 생략하고 true(보류 불필요).</summary>
    private bool TryApply(WindowState state)
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        if (!EnsureHwnd()) return false;
        ApplyToWindow(state);
#endif
        return true;
    }

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
    private bool EnsureHwnd()
    {
        if (_hwnd != IntPtr.Zero) return true;

        // 반드시 자기 프로세스의 메인 창만 잡는다.
        // GetActiveWindow/GetForegroundWindow는 시작 순간 우리 창이 아직 활성/포그라운드가
        // 아니면 그때 맨 앞이던 다른 앱(예: 전체화면 게임)의 창을 반환할 수 있고,
        // 그 창에 borderless/투명/리사이즈가 적용되어 대상 앱이 망가진다.
        // 시작 직후엔 아직 창이 없어 Zero일 수 있으나, 그 경우 호출 측이 다음 프레임 재시도(_pending).
        // MainWindowHandle은 첫 접근값을 Process 객체에 캐시하므로, 매번 새 Process로 읽어 Zero가 굳지 않게 한다.
        _hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

        return _hwnd != IntPtr.Zero;
    }

    private void ApplyToWindow(WindowState s)
    {
        // (1) 일반 스타일 — borderless(WS_POPUP)
        uint style = Win32WindowApi.GetWindowLong(_hwnd, Win32WindowApi.GWL_STYLE);
        if (s.Borderless)
        {
            style &= ~(Win32WindowApi.WS_CAPTION
                     | Win32WindowApi.WS_THICKFRAME
                     | Win32WindowApi.WS_MINIMIZEBOX
                     | Win32WindowApi.WS_MAXIMIZEBOX
                     | Win32WindowApi.WS_SYSMENU);
            style |= Win32WindowApi.WS_POPUP | Win32WindowApi.WS_VISIBLE;
        }
        Win32WindowApi.SetWindowLong(_hwnd, Win32WindowApi.GWL_STYLE, style);

        // (2) 확장 스타일 — LAYERED(투명) + TRANSPARENT(전체 통과)
        uint ex = Win32WindowApi.GetWindowLong(_hwnd, Win32WindowApi.GWL_EXSTYLE);
        if (s.Transparent) ex |= Win32WindowApi.WS_EX_LAYERED;
        else ex &= ~Win32WindowApi.WS_EX_LAYERED;
        if (s.ClickThrough) ex |= Win32WindowApi.WS_EX_TRANSPARENT;
        else ex &= ~Win32WindowApi.WS_EX_TRANSPARENT;
        Win32WindowApi.SetWindowLong(_hwnd, Win32WindowApi.GWL_EXSTYLE, ex);

        // (3) ColorKey + DWM 프레임 확장 — Transparent일 때만.
        //     Transparent OFF(편집 모드)에서는 둘 다 생략해 창을 불투명하게 유지(영역을 잡을 수 있게).
        if (s.Transparent)
        {
            uint key = Win32WindowApi.ToColorRef(
                (byte)(s.ColorKey.r * 255),
                (byte)(s.ColorKey.g * 255),
                (byte)(s.ColorKey.b * 255));
            Win32WindowApi.SetLayeredWindowAttributes(_hwnd, key, 255, Win32WindowApi.LWA_COLORKEY);

            var margins = new Win32WindowApi.MARGINS { cxLeftWidth = -1 };
            Win32WindowApi.DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        }

        // (4) Z-order + 프레임 변경 통지
        Win32WindowApi.SetWindowPos(
            _hwnd,
            s.TopMost ? Win32WindowApi.HWND_TOPMOST : Win32WindowApi.HWND_NOTOPMOST,
            0, 0, 0, 0,
            Win32WindowApi.SWP_NOMOVE | Win32WindowApi.SWP_NOSIZE | Win32WindowApi.SWP_FRAMECHANGED);
    }
#endif
}
