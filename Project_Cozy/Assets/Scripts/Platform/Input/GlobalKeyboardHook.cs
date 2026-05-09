using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities; // .Call() extension method

/// <summary>
/// 포커스 상태와 무관하게 키 입력을 감지한다.
///
/// 두 경로를 합쳐서 사용한다 (포커스 상태에 따라 어느 한쪽만 fire되어 상호 배타적):
///   1) WH_KEYBOARD_LL 훅 — 게임 창이 포커스를 잃은 상태에서 들어오는 키 입력
///   2) InputSystem.onAnyButtonPress — 게임 창이 포커스를 가진 상태에서 들어오는 키 입력 (키마다 개별 fire)
///
/// Unity 6 + Input System Package 조합에서, 포그라운드 창이 자기 자신일 때 OS가 Raw Input
/// 경로를 우선해 LL 훅을 우회시키는 동작이 관찰되어, 두 경로를 결합해 항상 입력을 받도록 한다.
/// </summary>
public class GlobalKeyboardHook : MonoBehaviour
{
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN     = 0x0100;
    const int WM_SYSKEYDOWN  = 0x0104;

    delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    // GC가 델리게이트를 수거하지 못하도록 인스턴스 참조를 보관
    static LowLevelKeyboardProc _proc;
    static IntPtr _hookId = IntPtr.Zero;

    // 두 경로 모두에서 enqueue되어 Update가 메인 스레드에서 소비.
    // LL 훅은 메시지 펌프 스레드, InputSystem은 input update 시점이라 둘 다 큐로 모아 일관 처리.
    static readonly ConcurrentQueue<int> _keyQueue = new ConcurrentQueue<int>();

    // InputSystem.onAnyButtonPress 구독 핸들. OnDisable에서 해제하지 않으면 hot-reload 시 이중 등록.
    IDisposable _anyButtonSubscription;

    // 메인 스레드에서 구독하는 이벤트
    public event Action KeyPressed;

    void OnEnable()
    {
        // 두 입력 경로(LL 훅 + InputSystem)를 모두 OnEnable에서 켜고 OnDisable에서 끈다 — lifecycle 일관성.
#if !UNITY_EDITOR
        _proc   = HookCallback;
        _hookId = SetHook(_proc);
        Debug.Log("[GlobalKeyboardHook] LL 훅 등록 완료. hookId=" + _hookId);
#else
        Debug.Log("[GlobalKeyboardHook] Editor 모드: LL 훅 스킵 (Input System 경로는 Editor에서도 동작)");
#endif

        // InputSystem.onAnyButtonPress.Call(action)은 IDisposable을 반환한다.
        // 키마다 개별 fire되므로 한 프레임에 여러 키가 들어와도 모두 카운트된다.
        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(ctrl =>
        {
            if (ctrl.device is Keyboard)
                _keyQueue.Enqueue(1);
        });
    }

    void OnDisable()
    {
        _anyButtonSubscription?.Dispose();
        _anyButtonSubscription = null;
        Unhook();
    }

    void Update()
    {
        // 두 경로(LL 훅 / InputSystem.onAnyButtonPress)가 모두 _keyQueue에 enqueue.
        // 포커스 상태에 따라 어느 한쪽만 fire되어 사실상 상호 배타적.
        while (_keyQueue.TryDequeue(out _))
            KeyPressed?.Invoke();
    }

    void OnDestroy()   => Unhook();
    void OnApplicationQuit() => Unhook();

    void Unhook()
    {
#if !UNITY_EDITOR
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            Debug.Log("[GlobalKeyboardHook] 훅 해제 완료");
        }
#endif
    }

    static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var module  = process.MainModule;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
    }

    // 이 콜백은 별도 스레드에서 호출된다 — Unity API 호출 금지
    static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            _keyQueue.Enqueue(1);

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
