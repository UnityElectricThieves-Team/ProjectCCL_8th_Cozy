using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 창이 포커스를 잃은 상태(OutFocus)에서 들어온 키 입력만 <see cref="KeyPressed"/> 이벤트로 보고한다.
///
/// 구현: <c>WH_KEYBOARD_LL</c> 저수준 훅으로 OS-wide 키다운을 받고, 메인 스레드의 <see cref="Update"/>에서
/// <see cref="Application.isFocused"/>가 false일 때만 이벤트를 fire — InFocus 입력은 큐에서 비우기만 하고 무시.
///
/// LL 훅은 *훅을 설치한 스레드*에서 콜백이 디스패치되므로, 메인(프레임) 스레드에 설치하면 매 입력이
/// 프레임 루프를 기다려 시스템 전역 입력이 끊긴다. 그래서 훅을 전용 스레드에서 설치하고 그 스레드가
/// OS 메시지 루프를 유지한다 — 콜백이 프레임과 무관하게 즉시 처리됨.
///
/// 포커스 무관 통합 입력 소스는 <see cref="GlobalKeyInput"/> 쪽. 본 컴포넌트는 *OutFocus 전용*이라는 의미를 이름과 동작에서 일치시킨 별개 추상화.
/// 현재 keydown만, 모디파이어/조합키/keyup 미지원.
/// </summary>
[DefaultExecutionOrder(-100)]
public class OutFocusKeyHook : MonoBehaviour
{
    /// <summary>씬 단일 인스턴스. WH_KEYBOARD_LL이 OS-wide라 씬당 1개만 존재해야 정상. 중복 부착 시 두 번째는 Awake에서 자기 컴포넌트만 Destroy.</summary>
    public static OutFocusKeyHook Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    const int  WH_KEYBOARD_LL = 13;
    const int  WM_KEYDOWN     = 0x0100;
    const int  WM_SYSKEYDOWN  = 0x0104;
    const uint WM_QUIT        = 0x0012;

    delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

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
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

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

    // OS에 함수 포인터로 넘기는 콜백은 static 보관 — 인스턴스 필드만 두면 GC 수거 후 호출 시점에 액세스 위반.
    static LowLevelKeyboardProc _proc;
    static IntPtr _hookId = IntPtr.Zero;

#if !UNITY_EDITOR
    // 훅을 설치·구동하는 전용 스레드.
    Thread _hookThread;
    uint   _hookThreadId;
#endif

    // 콜백은 전용 스레드, dequeue는 메인 스레드 Update — 둘을 가르는 큐.
    // 큐에 담는 값은 (int)Key, 메인에서 다시 Key로 캐스팅.
    static readonly ConcurrentQueue<int> _keyQueue = new ConcurrentQueue<int>();

    /// <summary>OutFocus 상태에서 키가 눌렸을 때 발생. 인자는 매핑된 <see cref="Key"/> (매핑 실패 시 <see cref="Key.None"/>).</summary>
    public event Action<Key> KeyPressed;

    void OnEnable()
    {
#if !UNITY_EDITOR
        _proc = HookCallback; // GC 방지 static 보관
        _hookThread = new Thread(HookThreadMain) { IsBackground = true, Name = "OutFocusKeyHook" };
        _hookThread.Start();
#else
        Debug.Log("[OutFocusKeyHook] Editor 모드: LL 훅 스킵 (Editor 자체 입력이 망가지므로)");
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
        // OutFocus일 때만 fire — 큐는 InFocus여도 비워준다(누적 방지).
        bool outOfFocus = !Application.isFocused;
        while (_keyQueue.TryDequeue(out int keyCode))
        {
            if (outOfFocus)
                KeyPressed?.Invoke((Key)keyCode);
        }
    }

#if !UNITY_EDITOR
    // 전용 스레드 본체: 이 스레드에서 훅을 설치하고 OS 메시지 루프를 유지한다.
    // WM_QUIT를 받으면 GetMessage가 0을 반환해 루프를 빠져나오고 훅을 해제한다.
    void HookThreadMain()
    {
        _hookThreadId = GetCurrentThreadId();

        var module = System.Diagnostics.Process.GetCurrentProcess().MainModule;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
        Debug.Log("[OutFocusKeyHook] LL 훅 등록(전용 스레드). hookId=" + _hookId);

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
        Debug.Log("[OutFocusKeyHook] LL 훅 해제(전용 스레드 종료)");
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

    // 전용 스레드에서 호출됨 — UnityEngine API 호출 금지. Win32KeyMap.ToKey는 순수 함수라 안전.
    static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);
            _keyQueue.Enqueue((int)Win32KeyMap.ToKey(vkCode));
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
