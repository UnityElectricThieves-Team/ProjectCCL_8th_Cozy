using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;  // KeyControl
using UnityEngine.InputSystem.Utilities; // .Call() extension method
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// 포커스 상태와 무관한 전역 키 입력 소스. 두 OS-level 구현을 단일 이벤트(<see cref="KeyPressed"/>)로 통합한 추상화 컴포넌트.
///
/// 내부적으로 두 경로를 합쳐서 사용한다 (포커스 상태에 따라 어느 한쪽만 fire되어 상호 배타적):
///   1) WH_KEYBOARD_LL 훅 — 게임 창이 포커스를 잃은 상태에서 들어오는 키 입력
///   2) InputSystem.onAnyButtonPress — 게임 창이 포커스를 가진 상태에서 들어오는 키 입력 (키마다 개별 fire)
///
/// Unity 6 + Input System Package 조합에서, 포그라운드 창이 자기 자신일 때 OS가 Raw Input
/// 경로를 우선해 LL 훅을 우회시키는 동작이 관찰되어, 두 경로를 결합해 항상 입력을 받도록 한다.
///
/// 어느 경로로 들어왔든 입력 키를 <see cref="Key"/>로 정규화해 <see cref="KeyPressed"/>(Key)로 보고한다.
/// LL 훅의 Win32 vkCode는 <see cref="Win32KeyMap"/>로 변환하고, 매핑되지 않는 키는 <c>Key.None</c>으로 보고하되 이벤트 자체는 발생시킨다(입력 횟수 카운트는 항상 유효).
/// 현재는 keydown만 보고하며 모디파이어/조합키/keyup은 다루지 않는다 — 필요 시 풍부한 KeyEvent 형태로 확장.
///
/// 클래스명 이력: 과거에는 <c>GlobalKeyboardHook</c> 였다. 이름이 구현 디테일(WH_KEYBOARD_LL "hook")을 노출해
/// 소비자가 추상화를 인식하기 어렵다는 이유로 <c>GlobalKeyInput</c>으로 개명 — <see cref="MovedFromAttribute"/>로 prefab/씬의 missing script 자동 매핑.
/// </summary>
[MovedFrom(false, sourceClassName: "GlobalKeyboardHook")]
public class GlobalKeyInput : MonoBehaviour
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
    // 큐에 담는 값은 (int)Key — Update에서 다시 Key로 캐스팅해 invoke한다.
    static readonly ConcurrentQueue<int> _keyQueue = new ConcurrentQueue<int>();

    // InputSystem.onAnyButtonPress 구독 핸들. OnDisable에서 해제하지 않으면 hot-reload 시 이중 등록.
    IDisposable _anyButtonSubscription;

    // 메인 스레드에서 구독하는 이벤트. 인자는 입력된 키 (매핑 실패 시 Key.None — 그래도 이벤트는 발생).
    public event Action<Key> KeyPressed;

    void OnEnable()
    {
        // 두 입력 경로(LL 훅 + InputSystem)를 모두 OnEnable에서 켜고 OnDisable에서 끈다 — lifecycle 일관성.
#if !UNITY_EDITOR
        _proc   = HookCallback;
        _hookId = SetHook(_proc);
        Debug.Log("[GlobalKeyInput] LL 훅 등록 완료. hookId=" + _hookId);
#else
        Debug.Log("[GlobalKeyInput] Editor 모드: LL 훅 스킵 (Input System 경로는 Editor에서도 동작)");
#endif

        // InputSystem.onAnyButtonPress.Call(action)은 IDisposable을 반환한다.
        // 키마다 개별 fire되므로 한 프레임에 여러 키가 들어와도 모두 카운트된다.
        // KeyControl만 받아 — Keyboard.anyKey(AnyKeyControl)는 제외 → 중복 카운트 방지 + 어떤 키인지(keyCode) 확보.
        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(ctrl =>
        {
            if (ctrl is KeyControl key)
                _keyQueue.Enqueue((int)key.keyCode);
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
        while (_keyQueue.TryDequeue(out int keyCode))
            KeyPressed?.Invoke((Key)keyCode);
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
            Debug.Log("[GlobalKeyInput] 훅 해제 완료");
        }
#endif
    }

    static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var module  = process.MainModule;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
    }

    // 이 콜백은 별도 스레드에서 호출된다 — Unity API 호출 금지 (Win32KeyMap.ToKey는 순수 함수라 안전).
    static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            // KBDLLHOOKSTRUCT의 첫 DWORD가 vkCode. Win32 가상 키코드를 Unity Key로 매핑해 큐에 넣는다.
            int vkCode = Marshal.ReadInt32(lParam);
            _keyQueue.Enqueue((int)Win32KeyMap.ToKey(vkCode));
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
