using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel; // MouseButton (Left/Right/Middle/Forward/Back)

/// <summary>
/// 게임 창이 포커스를 잃은 상태(OutFocus)에서 들어온 마우스 버튼 down만 <see cref="ButtonPressed"/> 이벤트로 보고한다.
///
/// 구현: <c>WH_MOUSE_LL</c> 저수준 훅으로 OS-wide 마우스 메시지를 받되 down 3종
/// (<c>WM_LBUTTONDOWN</c> / <c>WM_RBUTTONDOWN</c> / <c>WM_MBUTTONDOWN</c>)만 큐에 적재.
/// 이동(<c>WM_MOUSEMOVE</c>)·휠·up·xbutton은 무시. 메인 스레드 <see cref="Update"/>에서
/// <see cref="Application.isFocused"/>가 false일 때만 fire.
///
/// LL 훅은 *훅을 설치한 스레드*에서 콜백이 디스패치되므로, 메인(프레임) 스레드에 설치하면
/// 매 입력이 프레임 루프를 기다려 시스템 전역 입력이 끊긴다. 그래서 훅을 전용 스레드에서
/// 설치하고 그 스레드가 OS 메시지 루프를 유지한다 — 콜백이 프레임과 무관하게 즉시 처리됨.
///
/// 키보드 쪽 <see cref="OutFocusKeyHook"/>과 평행한 추상화 — OutFocus 전용임을 이름과 동작에서 일치시킨다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class OutFocusMouseHook : MonoBehaviour
{
    /// <summary>씬 단일 인스턴스. WH_MOUSE_LL이 OS-wide라 씬당 1개만 존재해야 정상. 중복 부착 시 두 번째는 Awake에서 자기 컴포넌트만 Destroy.</summary>
    public static OutFocusMouseHook Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    const int  WH_MOUSE_LL    = 14;
    const int  WM_LBUTTONDOWN = 0x0201;
    const int  WM_RBUTTONDOWN = 0x0204;
    const int  WM_MBUTTONDOWN = 0x0207;
    const uint WM_QUIT        = 0x0012;

    delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG
    {
        public IntPtr hwnd;
        public uint   message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint   time;
        public POINT  pt;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    // 전용 스레드의 메시지 루프 + 종료 신호용.
    [DllImport("user32.dll")] static extern int    GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] static extern bool   TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] static extern bool   PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    // GC 방지 static 보관 — OutFocusKeyHook과 동일 패턴.
    static LowLevelMouseProc _proc;
    static IntPtr _hookId = IntPtr.Zero;

#if !UNITY_EDITOR
    // 훅을 설치·구동하는 전용 스레드.
    Thread _hookThread;
    uint   _hookThreadId;
#endif

    // 큐에는 (int)MouseButton을 담아 메인 스레드에서 enum으로 캐스팅.
    static readonly ConcurrentQueue<int> _buttonQueue = new ConcurrentQueue<int>();

    /// <summary>OutFocus 상태에서 마우스 버튼이 눌렸을 때 발생. 인자는 어떤 버튼이 눌렸는지(Left/Right/Middle).</summary>
    public event Action<MouseButton> ButtonPressed;

    void OnEnable()
    {
#if !UNITY_EDITOR
        _proc = HookCallback; // GC 방지 static 보관
        _hookThread = new Thread(HookThreadMain) { IsBackground = true, Name = "OutFocusMouseHook" };
        _hookThread.Start();
#else
        Debug.Log("[OutFocusMouseHook] Editor 모드: LL 훅 스킵");
#endif
    }

    void OnDisable() => StopHookThread();
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        StopHookThread();
    }
    void OnApplicationQuit() => StopHookThread();

    void Update()
    {
        bool outOfFocus = !Application.isFocused;
        while (_buttonQueue.TryDequeue(out int btn))
        {
            if (outOfFocus)
                ButtonPressed?.Invoke((MouseButton)btn);
        }
    }

#if !UNITY_EDITOR
    // 전용 스레드 본체: 이 스레드에서 훅을 설치하고 OS 메시지 루프를 유지한다.
    // WM_QUIT를 받으면 GetMessage가 0을 반환해 루프를 빠져나오고 훅을 해제한다.
    void HookThreadMain()
    {
        _hookThreadId = GetCurrentThreadId();

        var module = System.Diagnostics.Process.GetCurrentProcess().MainModule;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(module.ModuleName), 0);
        Debug.Log("[OutFocusMouseHook] LL 훅 등록(전용 스레드). hookId=" + _hookId);

        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        Debug.Log("[OutFocusMouseHook] LL 훅 해제(전용 스레드 종료)");
    }
#endif

    void StopHookThread()
    {
#if !UNITY_EDITOR
        if (_hookThread != null && _hookThread.IsAlive)
        {
            // 전용 스레드의 GetMessage 루프를 깨워 종료시킨다.
            PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _hookThread.Join(1000); // 타임아웃 — WM_QUIT 누락 시 데드락 방지(IsBackground라 프로세스 종료 시 자동 회수)
        }
        _hookThread = null;
#endif
    }

    // 전용 스레드에서 호출됨 — UnityEngine API 호출 금지. enum 캐스팅·큐 enqueue만.
    static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            switch (msg)
            {
                case WM_LBUTTONDOWN: _buttonQueue.Enqueue((int)MouseButton.Left);   break;
                case WM_RBUTTONDOWN: _buttonQueue.Enqueue((int)MouseButton.Right);  break;
                case WM_MBUTTONDOWN: _buttonQueue.Enqueue((int)MouseButton.Middle); break;
                // 이동/휠/up/xbutton은 무시.
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
