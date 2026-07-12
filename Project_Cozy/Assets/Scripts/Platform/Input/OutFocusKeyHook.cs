using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 창이 포커스를 잃은 상태(OutFocus)에서 들어온 키 입력만 <see cref="KeyPressed"/> 이벤트로 보고한다.
///
/// 저수준 훅·전용 스레드·메시지 루프 등 공통 배관은 <see cref="OutFocusLowLevelHook"/>에 있고,
/// 본 클래스는 keydown 필터링과 <see cref="Key"/> 매핑/보고만 담당한다.
///
/// 포커스 무관 통합 입력 소스는 <see cref="GlobalKeyInput"/> 쪽. 본 컴포넌트는 *OutFocus 전용*이라는 의미를 이름과 동작에서 일치시킨 별개 추상화.
/// 현재 keydown만, 모디파이어/조합키/keyup 미지원.
/// </summary>
public class OutFocusKeyHook : OutFocusLowLevelHook
{
    // 씬 단일 인스턴스 추적 — 외부 참조용이 아니라 중복 부착 방지용. 소비자는 static <see cref="KeyPressed"/> 이벤트를 구독한다.
    // WH_KEYBOARD_LL이 OS-wide라 씬당 1개만 존재해야 정상. 중복 부착 시 두 번째는 Awake에서 자기 컴포넌트만 Destroy.
    static OutFocusKeyHook _instance;

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

    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN     = 0x0100;
    const int WM_SYSKEYDOWN  = 0x0104;

    /// <summary>OutFocus 상태에서 키가 눌렸을 때 발생. 인자는 매핑된 <see cref="Key"/> (매핑 실패 시 <see cref="Key.None"/>).
    /// static 이벤트 — 소비자는 인스턴스 참조 없이 <c>OutFocusKeyHook.KeyPressed += ...</c>로 구독한다.</summary>
    public static event Action<Key> KeyPressed;

    // 도메인 리로드 끄기 상황에서 static 상태가 세션 간 잔존하는 것을 방지(CharacterNames.ResetState와 같은 패턴).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        KeyPressed = null;
        _instance = null;
    }

    protected override int HookId => WH_KEYBOARD_LL;

    // 전용 스레드에서 호출됨 — UnityEngine API 호출 금지. Win32KeyMap.ToKey는 순수 함수라 안전.
    protected override void OnHookMessage(IntPtr wParam, IntPtr lParam)
    {
        if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            Enqueue((int)Win32KeyMap.ToKey(vkCode));
        }
    }

    protected override void OnDequeued(int code) => KeyPressed?.Invoke((Key)code);
}
