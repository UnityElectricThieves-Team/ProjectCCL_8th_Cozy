using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 창이 포커스를 잃은 상태(OutFocus)에서 들어온 키 입력만 <see cref="KeyPressed"/> 이벤트로 보고한다.
///
/// 구현: <c>WH_KEYBOARD_LL</c> 저수준 훅으로 OS-wide 키다운을 받고, 메인 스레드의 <see cref="Update"/>에서
/// <see cref="Application.isFocused"/>가 false일 때만 이벤트를 fire — InFocus 입력은 큐에서 비우기만 하고 무시.
///
/// 포커스 무관 통합 입력 소스는 <see cref="GlobalKeyInput"/> 쪽. 본 컴포넌트는 *OutFocus 전용*이라는 의미를 이름과 동작에서 일치시킨 별개 추상화.
/// 현재 keydown만, 모디파이어/조합키/keyup 미지원.
/// </summary>
public class OutFocusKeyHook : MonoBehaviour
{
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN     = 0x0100;
    const int WM_SYSKEYDOWN  = 0x0104;

    delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    // OS에 함수 포인터로 넘기는 콜백은 static 보관 — 인스턴스 필드만 두면 GC 수거 후 호출 시점에 액세스 위반.
    static LowLevelKeyboardProc _proc;
    static IntPtr _hookId = IntPtr.Zero;

    // 콜백은 메시지 펌프 스레드, dequeue는 메인 스레드 Update — 둘을 가르는 큐.
    // 큐에 담는 값은 (int)Key, 메인에서 다시 Key로 캐스팅.
    static readonly ConcurrentQueue<int> _keyQueue = new ConcurrentQueue<int>();

    /// <summary>OutFocus 상태에서 키가 눌렸을 때 발생. 인자는 매핑된 <see cref="Key"/> (매핑 실패 시 <see cref="Key.None"/>).</summary>
    public event Action<Key> KeyPressed;

    void OnEnable()
    {
#if !UNITY_EDITOR
        _proc   = HookCallback;
        _hookId = SetHook(_proc);
        Debug.Log("[OutFocusKeyHook] LL 훅 등록. hookId=" + _hookId);
#else
        Debug.Log("[OutFocusKeyHook] Editor 모드: LL 훅 스킵 (Editor 자체 입력이 망가지므로)");
#endif
    }

    void OnDisable()         => Unhook();
    void OnDestroy()         => Unhook();
    void OnApplicationQuit() => Unhook();

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

    void Unhook()
    {
#if !UNITY_EDITOR
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            Debug.Log("[OutFocusKeyHook] LL 훅 해제");
        }
#endif
    }

    static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var module  = process.MainModule;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
    }

    // 별 스레드에서 호출됨 — UnityEngine API 호출 금지. Win32KeyMap.ToKey는 순수 함수라 안전.
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
