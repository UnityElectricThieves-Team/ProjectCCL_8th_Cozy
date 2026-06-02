using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
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

    const int WH_MOUSE_LL    = 14;
    const int WM_LBUTTONDOWN = 0x0201;
    const int WM_RBUTTONDOWN = 0x0204;
    const int WM_MBUTTONDOWN = 0x0207;

    delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    // GC 방지 static 보관 — OutFocusKeyHook과 동일 패턴.
    static LowLevelMouseProc _proc;
    static IntPtr _hookId = IntPtr.Zero;

    // 큐에는 (int)MouseButton을 담아 메인 스레드에서 enum으로 캐스팅.
    static readonly ConcurrentQueue<int> _buttonQueue = new ConcurrentQueue<int>();

    /// <summary>OutFocus 상태에서 마우스 버튼이 눌렸을 때 발생. 인자는 어떤 버튼이 눌렸는지(Left/Right/Middle).</summary>
    public event Action<MouseButton> ButtonPressed;

    void OnEnable()
    {
#if !UNITY_EDITOR
        _proc   = HookCallback;
        _hookId = SetHook(_proc);
        Debug.Log("[OutFocusMouseHook] LL 훅 등록. hookId=" + _hookId);
#else
        Debug.Log("[OutFocusMouseHook] Editor 모드: LL 훅 스킵");
#endif
    }

    void OnDisable()         => Unhook();
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Unhook();
    }
    void OnApplicationQuit() => Unhook();

    void Update()
    {
        bool outOfFocus = !Application.isFocused;
        while (_buttonQueue.TryDequeue(out int btn))
        {
            if (outOfFocus)
                ButtonPressed?.Invoke((MouseButton)btn);
        }
    }

    void Unhook()
    {
#if !UNITY_EDITOR
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            Debug.Log("[OutFocusMouseHook] LL 훅 해제");
        }
#endif
    }

    static IntPtr SetHook(LowLevelMouseProc proc)
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var module  = process.MainModule;
        return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(module.ModuleName), 0);
    }

    // 별 스레드에서 호출됨 — UnityEngine API 호출 금지. enum 캐스팅·큐 enqueue만.
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
