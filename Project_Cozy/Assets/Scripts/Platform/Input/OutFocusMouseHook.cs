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
public class OutFocusMouseHook : OutFocusLowLevelHook
{
    // 씬 단일 인스턴스 추적 — 외부 참조용이 아니라 중복 부착 방지용. 소비자는 static <see cref="ButtonPressed"/> 이벤트를 구독한다.
    // WH_MOUSE_LL이 OS-wide라 씬당 1개만 존재해야 정상. 중복 부착 시 두 번째는 Awake에서 자기 컴포넌트만 Destroy.
    static OutFocusMouseHook _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
    }

    protected override void OnDestroy()
    {
        if (_instance == this) _instance = null;
        base.OnDestroy();
    }

    const int WH_MOUSE_LL    = 14;
    const int WM_LBUTTONDOWN = 0x0201;
    const int WM_RBUTTONDOWN = 0x0204;
    const int WM_MBUTTONDOWN = 0x0207;

    /// <summary>OutFocus 상태에서 마우스 버튼이 눌렸을 때 발생. 인자는 어떤 버튼이 눌렸는지(Left/Right/Middle).
    /// static 이벤트 — 소비자는 인스턴스 참조 없이 <c>OutFocusMouseHook.ButtonPressed += ...</c>로 구독한다.</summary>
    public static event Action<MouseButton> ButtonPressed;

    // 도메인 리로드 끄기 상황에서 static 상태가 세션 간 잔존하는 것을 방지(CharacterNames.ResetState와 같은 패턴).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        ButtonPressed = null;
        _instance = null;
    }

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
