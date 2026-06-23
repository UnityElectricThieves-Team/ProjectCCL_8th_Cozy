using System;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel; // MouseButton (Left/Right/Middle/Forward/Back)

/// <summary>
/// 게임 창이 포커스를 잃은 상태(OutFocus)에서 들어온 마우스 버튼 down만 <see cref="ButtonPressed"/> 이벤트로 보고한다.
///
/// 저수준 훅·전용 스레드·메시지 루프 등 공통 배관은 <see cref="OutFocusLowLevelHook"/>에 있고,
/// 본 클래스는 버튼 down 3종(<c>WM_LBUTTONDOWN</c> / <c>WM_RBUTTONDOWN</c> / <c>WM_MBUTTONDOWN</c>)
/// 필터링과 <see cref="MouseButton"/> 보고만 담당한다. 이동·휠·up·xbutton은 다루지 않는다.
///
/// 키보드 쪽 <see cref="OutFocusKeyHook"/>과 평행한 추상화 — OutFocus 전용임을 이름과 동작에서 일치시킨다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class OutFocusMouseHook : OutFocusLowLevelHook
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

    protected override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    const int WH_MOUSE_LL    = 14;
    const int WM_LBUTTONDOWN = 0x0201;
    const int WM_RBUTTONDOWN = 0x0204;
    const int WM_MBUTTONDOWN = 0x0207;

    /// <summary>OutFocus 상태에서 마우스 버튼이 눌렸을 때 발생. 인자는 어떤 버튼이 눌렸는지(Left/Right/Middle).</summary>
    public event Action<MouseButton> ButtonPressed;

    protected override int HookId => WH_MOUSE_LL;

    // 전용 스레드에서 호출됨 — UnityEngine API 호출 금지. enum 캐스팅·큐 enqueue만.
    protected override void OnHookMessage(IntPtr wParam, IntPtr lParam)
    {
        switch (wParam.ToInt32())
        {
            case WM_LBUTTONDOWN: Enqueue((int)MouseButton.Left);   break;
            case WM_RBUTTONDOWN: Enqueue((int)MouseButton.Right);  break;
            case WM_MBUTTONDOWN: Enqueue((int)MouseButton.Middle); break;
            // 이동/휠/up/xbutton은 무시.
        }
    }

    protected override void OnDequeued(int code) => ButtonPressed?.Invoke((MouseButton)code);
}
