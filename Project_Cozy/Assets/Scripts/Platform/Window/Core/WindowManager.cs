using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 데스크톱 펫 윈도우의 OS-레벨 설정을 한 곳에서 관리.
///
/// 인스펙터 토글:
///   - alwaysOnTop
///   - hoverAwareClickThrough  (콜라이더·UGUI 위가 아니면 뒤의 창으로 클릭 통과)
///   - borderless              (테두리 + 타이틀바 제거)
///   - maximizeToWorkArea      (taskbar 위 작업영역 가득 채우기)
///   - resizable               (네 변·네 모서리 드래그로 크기 조절 + 상단 중앙 캡션 드래그로 창 이동.
///                              maximizeToWorkArea=true면 무시)
///
/// 고정 기능 (항상 ON):
///   - DWM 투명 배경 (WS_EX_LAYERED + DwmExtendFrameIntoClientArea)
///     ※ Unity 카메라 clear color가 검정(0,0,0)이어야 작동.
///
/// 토글 변경은 부팅 시 1회만 반영 (런타임 변경 미지원).
/// 예외 — 런타임 영역 API: ApplyRegion / ApplyMonitorFullscreen / TryGetMonitorRect /
///   SetClickThroughSuspended. ViewportScreenSettings(정책)가 뷰포트↔편집 전환에 사용한다.
/// 빌드 전용 — Editor에서는 모든 Win32 호출 스킵 (Editor 창 보호).
/// </summary>
[DisallowMultipleComponent]
public class WindowManager : MonoBehaviour
{
    // ===== 인스펙터 =====
    [Header("Toggles")]
    [SerializeField] private bool _alwaysOnTop            = true;
    [SerializeField] private bool _hoverAwareClickThrough = true;
    [SerializeField] private bool _borderless             = true;
    [SerializeField] private bool _maximizeToWorkArea     = false;
    [SerializeField] private bool _resizable              = true;

    [Header("Resize (when resizable)")]
    [SerializeField] private Vector2Int _minSize         = new Vector2Int(200, 200);
    [SerializeField] private Vector2Int _maxSize         = new Vector2Int(1920, 1080);
    [SerializeField] private int        _edgeThicknessPx = 6;
    [SerializeField] private int        _cornerSizePx    = 12;
    [SerializeField, Tooltip("상단 중앙 이동 핸들 두께(px). 0이면 이동 핸들 없음.")]
    private int _captionHeightPx = 28;
    [SerializeField, Tooltip("상단 중앙 이동 핸들 폭(px). 0 이하면 상단 전체 폭.")]
    private int _captionWidthPx  = 220;

    [Header("Click-Through (when hoverAwareClickThrough)")]
    [Tooltip("폴링에 사용할 카메라. 비워두면 Camera.main을 lazy하게 사용.")]
    [SerializeField] private Camera _pollingCamera;

    [Header("Debug")]
    [SerializeField] private bool _debugLogs;

    // ===== Win32 상수 =====
    const int  GWL_STYLE          = -16;
    const int  GWL_EXSTYLE        = -20;
    const int  GWLP_WNDPROC       = -4;

    const long WS_CAPTION         = 0x00C00000L;
    const long WS_THICKFRAME      = 0x00040000L;
    const long WS_MINIMIZEBOX     = 0x00020000L;
    const long WS_MAXIMIZEBOX     = 0x00010000L;
    const long WS_SYSMENU         = 0x00080000L;

    const uint WS_EX_LAYERED      = 0x00080000;
    const uint WS_EX_TRANSPARENT  = 0x00000020;

    const uint SWP_NOMOVE         = 0x0002;
    const uint SWP_NOSIZE         = 0x0001;
    const uint SWP_NOZORDER       = 0x0004;
    const uint SWP_NOACTIVATE     = 0x0010;
    const uint SWP_SHOWWINDOW     = 0x0040;
    const uint SWP_FRAMECHANGED   = 0x0020;

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    static readonly IntPtr HWND_TOP     = IntPtr.Zero;

    const uint MONITOR_DEFAULT_TO_NEAREST = 2;

    const uint WM_NCHITTEST       = 0x0084;
    const uint WM_GETMINMAXINFO   = 0x0024;
    const uint WM_EXITSIZEMOVE    = 0x0232;

    // ===== Win32 구조체 =====
    [StructLayout(LayoutKind.Sequential)]
    struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MONITORINFO
    {
        public int  cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // ===== P/Invoke =====
    [DllImport("user32.dll")] static extern uint   GetWindowLong(IntPtr hwnd, int nIndex);
    [DllImport("user32.dll")] static extern int    SetWindowLong(IntPtr hwnd, int nIndex, uint dwNewLong);
    [DllImport("user32.dll")] static extern bool   SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool   GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool   GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("dwmapi.dll")] static extern int    DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    // ===== WndProc 서브클래싱용 static 상태 =====
    // OS에 함수 포인터로 넘기는 델리게이트와 그 콜백이 접근하는 데이터는 static이어야 한다.
    // 인스턴스 필드만 두면 GC가 델리게이트를 수거한 뒤 OS가 호출할 때 액세스 위반.
    delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    static WndProcDelegate _newProcDelegate;
    static IntPtr _originalProc = IntPtr.Zero;
    static IntPtr _hwndStatic   = IntPtr.Zero;

    // 메시지 펌프 스레드의 WndProc에서 참조하는 인스펙터 값들의 미러 (단일 인스턴스 가정)
    static int  _sEdge, _sCorner, _sCaptionH, _sCaptionW;
    static int  _sMinW, _sMinH, _sMaxW, _sMaxH;
    static bool _sAlwaysOnTopForExitSizeMove;
    static volatile bool _sResizeSuspended; // 편집 모드 등에서 가장자리 리사이즈 히트테스트 정지
    static volatile bool _sSizeMoveEnded;   // 메시지 스레드 → 메인 스레드 신호 (WM_EXITSIZEMOVE)

    // ===== 런타임 상태 =====
    IntPtr _hwnd             = IntPtr.Zero;
    bool   _wndProcInstalled = false;
    bool   _isClickThroughOn = false; // Win32의 WS_EX_TRANSPARENT 비트와 동기화된 캐시
    bool   _clickThroughSuspended = false; // 편집 모드 등에서 hover 폴링을 잠시 정지
    Camera _camera;

    void Awake()
    {
#if !UNITY_EDITOR
        _hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        _hwndStatic = _hwnd;

        if (_hwnd == IntPtr.Zero)
        {
            Debug.LogError("[WindowManager] HWND 획득 실패 — 윈도우 설정 스킵");
            return;
        }

        if (_borderless) ApplyBorderless();

        // DWM 투명 배경 (고정 ON). Borderless 다음, AOT 이전에 적용.
        ApplyTransparentBackground();

        if (_alwaysOnTop) ApplyAlwaysOnTop();

        // Maximize는 윈도우가 실제로 표시된 뒤 적용해야 안정적이라 코루틴으로 지연.
        // (WindowAspectFitter의 경험적 노하우 — 즉시 호출 시 위치/크기가 잡히지 않는 케이스 있음.)
        if (_maximizeToWorkArea) StartCoroutine(ApplyMaximizeAfterReady());

        bool wantResizable = _resizable && !_maximizeToWorkArea;
        if (_resizable && _maximizeToWorkArea && _debugLogs)
            Debug.LogWarning("[WindowManager] resizable + maximizeToWorkArea 동시 ON — resizable 무시");

        _sAlwaysOnTopForExitSizeMove = _alwaysOnTop;
        if (wantResizable) InstallWndProc();

        if (_debugLogs) Debug.Log("[WindowManager] 적용 완료");
#else
        if (_debugLogs) Debug.Log("[WindowManager] Editor 모드: 모든 Win32 호출 스킵");
#endif
    }

    /// <summary>사용자가 창을 드래그(이동/리사이즈)한 직후 메인 스레드에서 발행.
    /// 뷰포트 정책(ViewportScreenSettings)이 창 rect → 뷰포트 역동기화에 사용.</summary>
    public event Action WindowRectChangedByUser;

    void Update()
    {
#if !UNITY_EDITOR
        // WndProc(메시지 스레드 가능)에서 세운 플래그를 메인 스레드에서 이벤트로 변환.
        if (_sSizeMoveEnded)
        {
            _sSizeMoveEnded = false;
            WindowRectChangedByUser?.Invoke();
        }

        if (!_hoverAwareClickThrough || _clickThroughSuspended || _hwnd == IntPtr.Zero) return;

        // Camera.main이 씬 로드 직후엔 null일 수 있어 lazy로 가져옴.
        if (_camera == null)
        {
            _camera = _pollingCamera != null ? _pollingCamera : Camera.main;
            if (_camera == null) return;
        }

        PollClickThrough();
#endif
    }

    void OnDestroy()         => UninstallWndProc();
    void OnApplicationQuit() => UninstallWndProc();

    // ===== Borderless =====
    void ApplyBorderless()
    {
        // 마스크 방식: 기존 스타일 비트를 보존하면서 캡션/리사이즈 보더/시스템 메뉴만 제거.
        // (GWL_STYLE을 통째 덮어쓰는 옛 방식은 Unity가 켜둔 비트까지 날려서 위험.)
        long current  = (long)GetWindowLong(_hwnd, GWL_STYLE);
        long newStyle = current & ~WS_CAPTION & ~WS_THICKFRAME & ~WS_MINIMIZEBOX & ~WS_MAXIMIZEBOX & ~WS_SYSMENU;
        SetWindowLong(_hwnd, GWL_STYLE, (uint)newStyle);

        SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }

    // ===== DWM 투명 배경 =====
    void ApplyTransparentBackground()
    {
        // WS_EX_LAYERED는 Win8+ DWM에서 DwmExtendFrame과 양립 가능하며,
        // hoverAwareClickThrough(WS_EX_TRANSPARENT 토글)의 전제이기도 하다.
        uint exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

        // 모든 변을 -1로 두면 클라이언트 전체에 DWM 프레임이 확장되어
        // 백버퍼의 알파 < 1 픽셀이 데스크톱과 합성된다.
        MARGINS m = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref m);
    }

    // ===== Always On Top =====
    void ApplyAlwaysOnTop()
    {
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    }

    // ===== Maximize To Work Area =====
    IEnumerator ApplyMaximizeAfterReady()
    {
        for (int i = 0; i < 10; i++) yield return null;
        ApplyMaximizeToWorkArea();
    }

    void ApplyMaximizeToWorkArea()
    {
        IntPtr monitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULT_TO_NEAREST);
        MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
        if (!GetMonitorInfo(monitor, ref mi))
        {
            if (_debugLogs) Debug.LogWarning("[WindowManager] GetMonitorInfo 실패 — maximize 스킵");
            return;
        }

        int x = mi.rcWork.Left;
        int y = mi.rcWork.Top;
        int w = mi.rcWork.Right  - mi.rcWork.Left;
        int h = mi.rcWork.Bottom - mi.rcWork.Top;

        SetWindowPos(_hwnd, HWND_TOP, x, y, w, h,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    // ===== 런타임 영역 API (뷰포트 정책용) =====
    // _hwnd는 빌드에서만 획득되므로(Awake의 #if) Editor에서는 전부 자연 no-op — 별도 #if 불필요.

    /// <summary>창을 지정 스크린 좌표 rect로 배치. 평시 "창 = 뷰포트" 적용 경로.</summary>
    public void ApplyRegion(int x, int y, int width, int height)
    {
        if (_hwnd == IntPtr.Zero) return;
        SetWindowPos(_hwnd, IntPtr.Zero, x, y, width, height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    /// <summary>창을 현재 모니터 전체(rcMonitor)로 확장. 화면 설정(뷰포트 편집) 진입용.</summary>
    public void ApplyMonitorFullscreen()
    {
        if (_hwnd == IntPtr.Zero) return;
        if (!TryGetMonitorInfo(out MONITORINFO mi)) return;
        SetWindowPos(_hwnd, IntPtr.Zero,
            mi.rcMonitor.Left, mi.rcMonitor.Top,
            mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    /// <summary>현재 창이 있는 모니터의 전체 rect(스크린 좌표, 픽셀).
    /// 베이스 공간 크기의 원천. Editor/획득 실패 시 false.</summary>
    public bool TryGetMonitorRect(out RectInt monitorRect)
    {
        monitorRect = default;
        if (_hwnd == IntPtr.Zero) return false;
        if (!TryGetMonitorInfo(out MONITORINFO mi)) return false;
        monitorRect = new RectInt(
            mi.rcMonitor.Left, mi.rcMonitor.Top,
            mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top);
        return true;
    }

    /// <summary>가장자리 리사이즈(WndProc NCHITTEST)를 잠시 정지/재개. 뷰포트 편집 중에는
    /// 창이 모니터 전체 + 뷰포트 조정은 핸들 UI 담당이라 OS 리사이즈가 무의미하고,
    /// 가장자리 클릭이 리사이즈로 오인되면 편집 핸들 조작과 충돌한다.</summary>
    public void SetResizeSuspended(bool suspended) => _sResizeSuspended = suspended;

    /// <summary>hover 클릭 통과 폴링을 잠시 정지/재개. 뷰포트 편집 중에는 빈 공간에서도
    /// 핸들 드래그가 잡혀야 하므로 정지하고, 통과 상태면 즉시 해제한다.</summary>
    public void SetClickThroughSuspended(bool suspended)
    {
        _clickThroughSuspended = suspended;
        if (suspended && _isClickThroughOn && _hwnd != IntPtr.Zero)
        {
            ApplyClickThrough(false);
            _isClickThroughOn = false;
        }
    }

    /// <summary>현재 창 rect(스크린 좌표, 픽셀). Editor/획득 실패 시 false.</summary>
    public bool TryGetWindowRect(out RectInt windowRect)
    {
        windowRect = default;
        if (_hwnd == IntPtr.Zero) return false;
        if (!GetWindowRect(_hwnd, out RECT r)) return false;
        windowRect = new RectInt(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        return true;
    }

    /// <summary>
    /// 현재 OS 커서를 Unity 화면 좌표(좌하단 원점)로 변환한다.
    /// 클릭 통과 중 Input System의 포인터 좌표가 멈출 수 있는 소비자용 API.
    /// </summary>
    public bool TryGetUnityScreenCursorPosition(out Vector2 position)
    {
        position = default;
        if (!TryGetCursorContext(out _, out _, out Vector2 cursorPosition)) return false;
        position = cursorPosition;
        return true;
    }

    bool TryGetMonitorInfo(out MONITORINFO mi)
    {
        mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
        IntPtr monitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULT_TO_NEAREST);
        return GetMonitorInfo(monitor, ref mi);
    }

    // ===== Click-Through 폴링 =====
    void PollClickThrough()
    {
        // (A) 매 프레임 — 가벼운 판단
        if (!TryGetCursorContext(out POINT screenPt, out RECT win, out Vector2 cursorPosition)) return;

        int unityX = Mathf.RoundToInt(cursorPosition.x);
        int unityY = Mathf.RoundToInt(cursorPosition.y);

        Vector3 worldPt = _camera.ScreenToWorldPoint(new Vector3(unityX, unityY, 0f));
        bool overInteractable = Physics2D.OverlapPoint(new Vector2(worldPt.x, worldPt.y)) != null;

        // UGUI(버튼·패널) 위에서도 클릭이 잡혀야 한다. Input System의 포인터는 클릭 통과 ON일 때
        // 갱신이 멈출 수 있으므로 IsPointerOverGameObject 대신 좌표를 직접 넣어 레이캐스트.
        bool overUI = !overInteractable && IsPointerOverUI(unityX, unityY);

        // Resizable이 같이 켜진 경우, 리사이즈/이동 핫존 위에서는 click-through OFF로 유지
        // (그렇지 않으면 OS가 마우스 메시지를 통과시켜 NCHITTEST가 안 옴 → 리사이즈·이동 불가).
        bool overResizeZone = false;
        if (_wndProcInstalled && !_sResizeSuspended)
        {
            var zone = HitTestCalculator.Calculate(
                screenPt.x, screenPt.y,
                win.Left, win.Top, win.Right, win.Bottom,
                _edgeThicknessPx, _cornerSizePx, _captionHeightPx, _captionWidthPx);
            overResizeZone = (zone != ResizeHitZone.None);
        }

        bool shouldBeOn = !overInteractable && !overUI && !overResizeZone;

        // (B) 변할 때만 — Win32 콜
        if (shouldBeOn != _isClickThroughOn)
        {
            ApplyClickThrough(shouldBeOn);
            _isClickThroughOn = shouldBeOn;
        }
    }

    private bool TryGetCursorContext(out POINT screenPoint, out RECT windowRect, out Vector2 unityPosition)
    {
        screenPoint = default;
        windowRect = default;
        unityPosition = default;

        if (_hwnd == IntPtr.Zero || !GetCursorPos(out screenPoint) || !GetWindowRect(_hwnd, out windowRect))
            return false;

        int clientX = screenPoint.x - windowRect.Left;
        int clientYFromTop = screenPoint.y - windowRect.Top;
        int windowHeight = windowRect.Bottom - windowRect.Top;
        unityPosition = new Vector2(clientX, windowHeight - clientYFromTop);
        return true;
    }

    // UI 레이캐스트 재사용 버퍼 — 매 프레임 할당 방지
    static readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();
    PointerEventData _uiPointerData;

    bool IsPointerOverUI(float screenX, float screenY)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        if (_uiPointerData == null) _uiPointerData = new PointerEventData(es);
        _uiPointerData.position = new Vector2(screenX, screenY);

        _uiRaycastResults.Clear();
        es.RaycastAll(_uiPointerData, _uiRaycastResults);
        bool over = _uiRaycastResults.Count > 0;
        _uiRaycastResults.Clear();
        return over;
    }

    void ApplyClickThrough(bool on)
    {
        uint exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        if (on) exStyle |=  WS_EX_TRANSPARENT;
        else    exStyle &= ~WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
    }

    // ===== WndProc 서브클래싱 (Resizable) =====
    void InstallWndProc()
    {
        _sEdge     = _edgeThicknessPx;
        _sCorner   = _cornerSizePx;
        _sCaptionH = _captionHeightPx;
        _sCaptionW = _captionWidthPx;
        _sMinW   = _minSize.x; _sMinH = _minSize.y;
        _sMaxW   = _maxSize.x; _sMaxH = _maxSize.y;

        _newProcDelegate = SubclassedWndProc;
        IntPtr newProcPtr = Marshal.GetFunctionPointerForDelegate(_newProcDelegate);
        _originalProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, newProcPtr);
        _wndProcInstalled = true;
    }

    void UninstallWndProc()
    {
#if !UNITY_EDITOR
        if (!_wndProcInstalled || _originalProc == IntPtr.Zero || _hwndStatic == IntPtr.Zero) return;

        SetWindowLongPtr(_hwndStatic, GWLP_WNDPROC, _originalProc);
        _wndProcInstalled = false;
        // _originalProc/_newProcDelegate는 의도적으로 유지 — 원복 이후에도 이미 디스패치된
        // 메시지가 우리 프로시저로 들어올 수 있어, Zero화하면 CallWindowProc(Zero)로
        // 종료 시 간헐 액세스 위반이 난다 (리뷰 합의). 프로세스 종료가 정리를 맡는다.
#endif
    }

    // ---- 이하 static. 메시지 펌프 스레드에서 호출될 수 있으므로 인스턴스 멤버 접근 금지. ----

    static IntPtr SubclassedWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                if (_sResizeSuspended) break; // 정지 중엔 기본 처리(HTCLIENT)로 — 리사이즈 히트존 무시
                return HandleNCHitTest(lParam);

            case WM_GETMINMAXINFO:
                HandleGetMinMaxInfo(lParam);
                break;

            case WM_EXITSIZEMOVE:
                // 메인 스레드(Update)가 WindowRectChangedByUser 이벤트로 변환 — 뷰포트 역동기화용.
                _sSizeMoveEnded = true;
                // 리사이즈 종료 후 드물게 Topmost가 풀리는 케이스 보정.
                // (invariant의 주인이 직접 보정 — 단일 클래스 안이라 책임 누수 아님.)
                if (_sAlwaysOnTopForExitSizeMove)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
                break;
        }

        return CallWindowProc(_originalProc, hwnd, msg, wParam, lParam);
    }

    static IntPtr HandleNCHitTest(IntPtr lParam)
    {
        // lParam 인코딩: 하위 16비트 = X, 상위 16비트 = Y (스크린 좌표).
        // 멀티 모니터에서 음수 좌표 가능 → signed short로 캐스트 필수.
        long lp = lParam.ToInt64();
        int mouseX = (short)(lp & 0xFFFF);
        int mouseY = (short)((lp >> 16) & 0xFFFF);

        if (!GetWindowRect(_hwndStatic, out RECT rect))
            return (IntPtr)ResizeHitZoneExtensions.HTCLIENT;

        var zone = HitTestCalculator.Calculate(
            mouseX, mouseY,
            rect.Left, rect.Top, rect.Right, rect.Bottom,
            _sEdge, _sCorner, _sCaptionH, _sCaptionW);

        return (IntPtr)zone.ToHitTestCode();
    }

    static void HandleGetMinMaxInfo(IntPtr lParam)
    {
        var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        info.ptMinTrackSize = new POINT { x = _sMinW, y = _sMinH };
        info.ptMaxTrackSize = new POINT { x = _sMaxW, y = _sMaxH };
        Marshal.StructureToPtr(info, lParam, false);
    }
}
