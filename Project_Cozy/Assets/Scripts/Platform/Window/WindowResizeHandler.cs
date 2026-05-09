using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 마우스 드래그로 borderless 창의 크기를 조절한다.
///
/// 동작 원리: Unity 창의 WindowProc을 서브클래싱(원래 함수 포인터를 백업하고 우리 함수로 교체)한 뒤,
/// WM_NCHITTEST 메시지에서 마우스가 모서리/변 근처면 HTLEFT/HTRIGHT/... 등의 코드를 반환한다.
/// 이 코드를 받은 OS는 시스템 리사이즈 커서, 마우스 캡처, 드래그 추적, ESC 취소 등을
/// 알아서 수행한다 — 우리는 영역 라벨만 답해주는 역할.
///
/// 빌드 전용 (#if !UNITY_EDITOR). Editor에서 켜면 Unity Editor 메인 창의 WndProc이 망가진다.
///
/// TODO: WndProc 서브클래싱 인프라와 리사이즈 응용을 분리할지 — 두 번째 메시지 소비자(예: WM_DISPLAYCHANGE,
/// WM_DPICHANGED) 등장 시점에 검토. 그때 WndProc 라우터를 별도 컴포넌트로 추출하면 자연스러움.
/// </summary>
[RequireComponent(typeof(BorderlessWindow))]
public class WindowResizeHandler : MonoBehaviour
{
    // === Win32 메시지 ID (winuser.h) ===
    // OS가 우리 WindowProc에 전달하는 메시지 종류. 16진수 값들은 OS 정의값이라 변경 불가.
    const uint WM_NCHITTEST      = 0x0084; // 마우스 hover 위치가 어느 영역인지 묻는 메시지 (가장 중요)
    const uint WM_GETMINMAXINFO  = 0x0024; // 리사이즈 직전 OS가 min/max 크기를 묻는 메시지
    const uint WM_EXITSIZEMOVE   = 0x0232; // 리사이즈/이동 드래그 종료 시 (Topmost 재확정용)

    // === Win32 GWLP_* (winuser.h) ===
    // SetWindowLongPtr 의 nIndex 인자. 어떤 슬롯을 갈아끼울지 지정.
    const int GWLP_WNDPROC = -4; // WindowProc 함수 포인터 슬롯 (이걸 우리 함수로 교체하는 게 서브클래싱)

    // === SetWindowPos 플래그 (Topmost 재확정용) ===
    const uint SWP_NOMOVE       = 0x0002;
    const uint SWP_NOSIZE       = 0x0001;
    const uint SWP_NOACTIVATE   = 0x0010;
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    /// <summary>WM_GETMINMAXINFO 의 lParam이 가리키는 구조체. 일부 필드만 채우면 된다.</summary>
    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize; // 사용자 드래그로 줄일 수 있는 최소 크기
        public POINT ptMaxTrackSize; // 사용자 드래그로 키울 수 있는 최대 크기
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    // WindowProc 시그니처. Win32의 WNDPROC 콜백과 정확히 일치해야 한다.
    delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);


    [DllImport("user32.dll")]
    static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);


    // === 인스펙터 노출 ===
    [Header("Resize Hot Zones (px)")]
    [SerializeField] int edgeThicknessPx = 6;  // 변 핫존 두께
    [SerializeField] int cornerSizePx    = 12; // 모서리 핫존 크기

    [Header("Window Size Limits (px)")]
    [SerializeField] Vector2Int minSize = new Vector2Int(200, 200);
    [SerializeField] Vector2Int maxSize = new Vector2Int(1920, 1080);

    public int EdgeThicknessPx { get; private set; }
    public int CornerSizePx    { get; private set; }
    public Vector2Int MinSize  { get; private set; }
    public Vector2Int MaxSize  { get; private set; }

    /// <summary>현재 마우스가 위치한 핫존. 메인 스레드에서 갱신되며 UI 호버 피드백용으로 구독 가능.</summary>
    public ResizeHitZone CurrentHover { get; private set; } = ResizeHitZone.None;
    public event Action<ResizeHitZone> HoverChanged;

    // === 서브클래싱 상태 (static — GC 수거 방지) ===
    // WndProc 콜백으로 등록한 델리게이트는 OS 측에서 함수 포인터로만 들고 있다.
    // C# 측의 인스턴스 참조가 사라지면 GC가 수거하고, OS가 그 함수를 호출하는 순간 액세스 위반.
    // static 필드로 보관해 GC root를 살려둔다.
    static WndProcDelegate _newProcDelegate;
    static IntPtr _originalProc = IntPtr.Zero;
    static IntPtr _hwnd         = IntPtr.Zero;

    // 단일 인스턴스 가드 — static 상태(_originalProc 등)를 공유하므로 두 번째 인스턴스가 생기면
    // 첫 번째 핸들러의 WndProc 복원이 깨진다.
    static WindowResizeHandler _instance;

    // 인스턴스 설정값을 static 콜백에서 읽기 위한 미러. 단일 인스턴스 가정.
    static int _sEdgeThickness;
    static int _sCornerSize;
    static int _sMinW, _sMinH, _sMaxW, _sMaxH;

    // WndProc 스레드 → 메인 스레드 호버 전달용 큐. (Win32 메시지 펌프는 보통 메인 스레드와 같지만,
    //  Unity API 호출은 항상 Update에서 하는 게 안전하므로 큐로 격리한다.)
    static readonly ConcurrentQueue<ResizeHitZone> _hoverQueue = new ConcurrentQueue<ResizeHitZone>();

    void Start()
    {
        // 단일 인스턴스 가드 — 두 번째 인스턴스는 자기 자신만 destroy하고 빠진다.
        // (첫 번째 핸들러가 이미 잡은 _originalProc/_newProcDelegate를 덮어쓰지 않도록.)
        if (_instance != null && _instance != this)
        {
            Debug.LogError("[WindowResizeHandler] 이미 다른 인스턴스가 등록됨 — 자기 자신 destroy");
            Destroy(this);
            return;
        }
        _instance = this;

        // 인스펙터 값을 프로퍼티/static 미러로 동기화
        EdgeThicknessPx = edgeThicknessPx;
        CornerSizePx    = cornerSizePx;
        MinSize         = minSize;
        MaxSize         = maxSize;

        _sEdgeThickness = edgeThicknessPx;
        _sCornerSize    = cornerSizePx;
        _sMinW = minSize.x; _sMinH = minSize.y;
        _sMaxW = maxSize.x; _sMaxH = maxSize.y;

#if !UNITY_EDITOR
        var manager = GetComponent<BorderlessWindow>();
        _hwnd = manager.Hwnd;

        // Guard
        if (_hwnd == IntPtr.Zero)
        {
            Debug.LogError("[WindowResizeHandler] HWND를 얻지 못함 — BorderlessWindow.Awake 실행 순서 확인");
            return;
        }

        // 델리게이트를 static 필드에 보관해야 GC로부터 안전하다.
        _newProcDelegate = SubclassedWndProc;
        IntPtr newProcPtr = Marshal.GetFunctionPointerForDelegate(_newProcDelegate);
        _originalProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, newProcPtr);

        Debug.Log("[WindowResizeHandler] WindowProc 서브클래싱 완료");
#else
        Debug.Log("[WindowResizeHandler] Editor 모드: 서브클래싱 스킵 (Editor 보호)");
#endif
    }

    void Update()
    {
        // 메시지 펌프에서 enqueue된 호버 변화를 메인 스레드에서 안전하게 발행
        while (_hoverQueue.TryDequeue(out var zone))
        {
            if (zone != CurrentHover)
            {
                CurrentHover = zone;
                HoverChanged?.Invoke(zone);
            }
        }
    }

    void OnDestroy()
    {
        RestoreWndProc();
        if (_instance == this) _instance = null;
    }
    void OnApplicationQuit() => RestoreWndProc();

    void RestoreWndProc()
    {
#if !UNITY_EDITOR
        // Guard: 이미 복원되었거나 등록되지 않은 경우 skip (idempotent)
        if (_originalProc == IntPtr.Zero || _hwnd == IntPtr.Zero) return;

        SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _originalProc);
        _originalProc    = IntPtr.Zero;
        _newProcDelegate = null;
        Debug.Log("[WindowResizeHandler] WindowProc 원상복구 완료");
#endif
    }

    // ---- 이하 모두 static. 메시지 펌프 스레드에서 호출될 수 있으므로 인스턴스 멤버 접근 금지. ----

    /// <summary>
    /// 우리 WindowProc. 관심있는 메시지만 처리하고 나머지는 원래 WndProc(Unity가 등록한 것)에 위임한다.
    /// </summary>
    static IntPtr SubclassedWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                return HandleNCHitTest(lParam);

            case WM_GETMINMAXINFO:
                HandleGetMinMaxInfo(lParam);
                break; // OS 기본 처리도 거치도록 fall-through

            case WM_EXITSIZEMOVE:
                // HACK: BorderlessWindow의 Topmost invariant를 여기서 보정 — 책임 누수.
                // WndProc 라우터를 별도 컴포넌트로 분리하면 BorderlessWindow가 자기 영역에서 직접 처리 가능.
                // 현재는 WndProc이 여기 있어 부득이.
                // 동작: 리사이즈 종료 직후 드물게 Topmost가 풀리는 케이스 보정.
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                break;
        }

        return CallWindowProc(_originalProc, hwnd, msg, wParam, lParam);
    }

    static IntPtr HandleNCHitTest(IntPtr lParam)
    {
        // lParam 인코딩: 하위 16비트 = X(스크린 좌표), 상위 16비트 = Y.
        // 멀티 모니터에서 좌상단 모니터 외 영역은 음수 좌표가 가능하므로 signed short로 캐스트해야 한다.
        long lp = lParam.ToInt64();
        int mouseX = (short)(lp & 0xFFFF);
        int mouseY = (short)((lp >> 16) & 0xFFFF);

        if (!GetWindowRect(_hwnd, out RECT rect))
            return (IntPtr)ResizeHitZoneExtensions.HTCLIENT;

        var zone = HitTestCalculator.Calculate(
            mouseX, mouseY,
            rect.Left, rect.Top, rect.Right, rect.Bottom,
            _sEdgeThickness, _sCornerSize);

        // 메인 스레드에 호버 상태 전달 (UI 페이드용)
        _hoverQueue.Enqueue(zone);

        return (IntPtr)zone.ToHitTestCode();
    }

    static void HandleGetMinMaxInfo(IntPtr lParam)
    {
        // lParam이 가리키는 메모리(MINMAXINFO 구조체)를 직접 읽고 쓴다.
        // C로 치면 (MINMAXINFO*)lParam 으로 접근하는 것과 동일.
        var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        info.ptMinTrackSize = new POINT { x = _sMinW, y = _sMinH };
        info.ptMaxTrackSize = new POINT { x = _sMaxW, y = _sMaxH };
        Marshal.StructureToPtr(info, lParam, false);
    }
}
