using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel; // MouseButton
using UnityEngine.InputSystem.Utilities; // onAnyButtonPress.Call

/// <summary>
/// 포커스와 무관하게 "마지막으로 버튼 입력이 있었던 시각"을 하나만 든다. 내주는 것은 그 뒤로 몇 초가 지났는지뿐이다.
/// 몇 초면 잠드는가 같은 게임 판단은 모른다 — 그것은 <c>StateModule.IsUserIdle</c>이 정한다.
///
/// 창 안은 <see cref="InputSystem.onAnyButtonPress"/>(키·마우스 버튼 모두), 창 밖은 <see cref="OutFocusKeyHook"/>·
/// <see cref="OutFocusMouseHook"/>의 static 이벤트로 받는다. 두 경로는 포커스 유무로 서로 배타적이다.
/// 유저 설정 '관리자 모드'로 창 밖 입력을 거르지 않는다 — 거르면 관리자 모드를 끈 사용자가 다른 창에서 일하는 동안
/// 캐릭터가 계속 잠든다(.claude/rules/unity/character-state-machine.md 수면 절).
///
/// 캐릭터마다 시각을 들지 않고 한 곳에 두는 이유: 도감으로 귀환해 꺼져 있던 캐릭터는 그동안 입력을 받지 못한다.
/// 캐릭터마다 들면 오래 숨어 있던 캐릭터를 소환하자마자 "마지막 입력이 오래전"이라며 잠든다. 한 곳에서 재면 소환 클릭이 곧 마지막 입력이다.
///
/// 초기화와 구독의 단계를 나눈다. OutFocus 훅은 <c>SubsystemRegistration</c>에서 자기 이벤트를 비우므로,
/// 같은 단계에서 구독하면 실행 순서에 따라 구독이 지워진다(.claude/rules/unity/platform.md).
/// </summary>
public static class UserInputClock
{
    private static float _lastInputAt;
    private static IDisposable _anyButtonSubscription;

    /// <summary>마지막 버튼 입력(또는 게임 시작) 뒤로 지난 시간(초). <see cref="Time.unscaledTime"/> 기준.</summary>
    public static float SecondsSinceLastInput => Time.unscaledTime - _lastInputAt;

    // Enter Play Mode에서 도메인 리로드를 끄면 static 값이 이전 Play의 것을 들고 있다. 지난 구독을 풀고 시각을 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _anyButtonSubscription?.Dispose();
        _anyButtonSubscription = null;
        _lastInputAt = 0f;
    }

    // 훅이 이벤트를 비우는 SubsystemRegistration보다 뒤 단계에서 구독한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Subscribe()
    {
        _lastInputAt = Time.unscaledTime; // 게임 시작을 첫 입력으로 친다
        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(OnAnyButton);
        OutFocusKeyHook.KeyPressed += OnOutFocusKey;
        OutFocusMouseHook.ButtonPressed += OnOutFocusMouseButton;
    }

    private static void OnAnyButton(InputControl _) => Record();
    private static void OnOutFocusKey(Key _) => Record();
    private static void OnOutFocusMouseButton(MouseButton _) => Record();

    private static void Record() => _lastInputAt = Time.unscaledTime;
}
