using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// OutFocus(게임 창이 포커스를 잃은 상태) 전용 저수준 OS 훅의 공통 배관.
/// <c>WH_KEYBOARD_LL</c> / <c>WH_MOUSE_LL</c> 훅을 전용 스레드에서 설치하고 OS 메시지 루프를 유지하며,
/// 콜백이 큐에 적재한 입력을 메인 스레드 <see cref="Update"/>에서 <see cref="Application.isFocused"/>가
/// false일 때만 보고한다.
///
/// 키/마우스 두 훅의 차이는 (1) 설치할 훅 ID (2) 콜백 필터링 (3) dequeue 시 타입 캐스팅뿐이라,
/// 그 3가지만 추상 멤버(<see cref="HookId"/> / <see cref="OnHookMessage"/> / <see cref="OnDequeued"/>)로
/// 노출하고 나머지(전용 스레드·메시지 루프·생명주기·GC 보관·종료)는 여기서 처리한다.
///
/// LL 훅은 *훅을 설치한 스레드*에서 콜백이 디스패치되므로, 메인(프레임) 스레드에 설치하면 매 입력이
/// 프레임 루프를 기다려 시스템 전역 입력이 끊긴다. 그래서 훅을 전용 스레드에서 설치하고 그 스레드가
/// OS 메시지 루프를 유지한다 — 콜백이 프레임과 무관하게 즉시 처리됨.
///
/// 싱글톤 <c>Instance</c>·공개 이벤트는 타입별로 달라서 각 서브클래스가 보유한다.
/// </summary>
public abstract class OutFocusLowLevelHook : MonoBehaviour
{
    const uint WM_QUIT = 0x0012;

    delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

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
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

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

    // OS에 함수 포인터로 넘기는 콜백 — GC 수거를 막으려면 보관해야 한다.
    // *인스턴스* 필드인 이유: 이 베이스를 키/마우스 두 서브클래스가 상속하므로 static이면 두 서브클래스가
    // 같은 저장소를 공유해 한쪽이 다른 쪽 델리게이트를 덮어쓴다(덮인 델리게이트는 GC → 콜백 시 액세스 위반).
    // 인스턴스 필드는 씬당 1개인 싱글톤 컴포넌트가 살아있는 한 루팅되어 GC되지 않는다.
    LowLevelHookProc _proc;
    IntPtr _hookId = IntPtr.Zero;

    // 콜백은 전용 스레드, dequeue는 메인 스레드 Update — 둘을 가르는 큐. 값의 의미는 OnDequeued가 해석.
    readonly ConcurrentQueue<int> _queue = new ConcurrentQueue<int>();

#if !UNITY_EDITOR
    // 훅을 설치·구동하는 전용 스레드.
    Thread _hookThread;
    uint   _hookThreadId;
#endif

    /// <summary>설치할 LL 훅 ID (<c>WH_KEYBOARD_LL</c> / <c>WH_MOUSE_LL</c>).</summary>
    protected abstract int HookId { get; }

    /// <summary>전용 스레드에서 호출됨 — UnityEngine API 호출 금지. 필요한 메시지만 골라 <see cref="Enqueue"/>.</summary>
    protected abstract void OnHookMessage(IntPtr wParam, IntPtr lParam);

    /// <summary>메인 스레드 Update에서 OutFocus일 때만 호출 — 큐에서 꺼낸 코드를 캐스팅해 이벤트로 보고.</summary>
    protected abstract void OnDequeued(int code);

    /// <summary>콜백(전용 스레드)에서 입력 코드를 큐에 적재. 메인 스레드 Update가 <see cref="OnDequeued"/>로 소비.</summary>
    protected void Enqueue(int code) => _queue.Enqueue(code);

    protected virtual void OnEnable()
    {
#if !UNITY_EDITOR
        _proc = HookCallback; // GC 방지 보관
        _hookThread = new Thread(HookThreadMain) { IsBackground = true, Name = GetType().Name };
        _hookThread.Start();
#else
        Debug.Log($"[{GetType().Name}] Editor 모드: LL 훅 스킵 (Editor 자체 입력이 망가지므로)");
#endif
    }

    protected virtual void OnDisable() => StopHookThread();

    protected virtual void OnDestroy() => StopHookThread();

    void OnApplicationQuit() => StopHookThread();

    void Update()
    {
        // OutFocus일 때만 보고 — 큐는 InFocus여도 비워준다(누적 방지).
        bool outOfFocus = !Application.isFocused;
        while (_queue.TryDequeue(out int code))
        {
            if (outOfFocus)
                OnDequeued(code);
        }
    }

#if !UNITY_EDITOR
    // 전용 스레드 본체: 이 스레드에서 훅을 설치하고 OS 메시지 루프를 유지한다.
    // WM_QUIT를 받으면 GetMessage가 0을 반환해 루프를 빠져나오고 훅을 해제한다.
    void HookThreadMain()
    {
        _hookThreadId = GetCurrentThreadId();

        var module = System.Diagnostics.Process.GetCurrentProcess().MainModule;
        _hookId = SetWindowsHookEx(HookId, _proc, GetModuleHandle(module.ModuleName), 0);
        Debug.Log($"[{GetType().Name}] LL 훅 등록(전용 스레드). hookId=" + _hookId);

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
        Debug.Log($"[{GetType().Name}] LL 훅 해제(전용 스레드 종료)");
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

    // 전용 스레드에서 호출됨 — UnityEngine API 호출 금지. nCode 가드만 공통 처리하고 필터는 OnHookMessage에 위임.
    IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
            OnHookMessage(wParam, lParam);
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
