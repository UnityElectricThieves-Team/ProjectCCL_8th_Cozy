using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;  // KeyControl
using UnityEngine.InputSystem.LowLevel;  // MouseButton
using UnityEngine.InputSystem.Utilities; // .Call() extension

/// <summary>
/// 캐릭터 개체별 수면 정책. 무입력 시간이 <see cref="_idleThresholdSeconds"/>를 넘기면
/// <see cref="_sleepCheckInterval"/>마다 <see cref="_sleepProbabilityPerCheck"/> 확률로 sleep 진입을 시도한다.
/// 어떤 입력이라도 감지되면 즉시 <see cref="CharacterBasicAI2D.RequestWakeUp"/>.
///
/// 입력 4채널을 직접 구독 — <see cref="InputCounter"/>와 동일 패턴:
///  InFocus → <c>InputSystem.onAnyButtonPress</c> + <c>Mouse.current</c> 폴링,
///  OutFocus → <see cref="OutFocusKeyHook"/> + <see cref="OutFocusMouseHook"/>.
///
/// 개체차(예: Cat은 잘 안 자고 잠만보는 자주 잠)는 인스펙터의 세 정책 수치로 표현.
/// 씬 전역 일괄 sleep이 필요한 옛 시나리오는 deprecated인 <see cref="SleepController"/>가 담당했음.
/// </summary>
public class CharacterSleepPolicy : MonoBehaviour
{
    [SerializeField] private CharacterBasicAI2D _character;
    [SerializeField] private OutFocusKeyHook _outFocusKey;
    [SerializeField] private OutFocusMouseHook _outFocusMouse;

    [Header("Sleep Policy")]
    [Tooltip("이 시간 동안 무입력이 누적되어야 sleep 검사가 시작된다.")]
    [SerializeField] private float _idleThresholdSeconds = 30f;

    [Tooltip("threshold 이후 sleep 검사가 발동되는 주기.")]
    [SerializeField] private float _sleepCheckInterval = 5f;

    [Range(0f, 1f)]
    [Tooltip("매 검사마다 sleep에 들어갈 확률 (0 = 절대 안 잠, 1 = 항상 잠).")]
    [SerializeField] private float _sleepProbabilityPerCheck = 0.3f;

    private IDisposable _anyButtonSubscription;
    private float _lastInputAt;
    private float _lastCheckAt;

    private void Awake()
    {
        if (_character == null) _character = GetComponent<CharacterBasicAI2D>();
        _lastInputAt = Time.time;
        _lastCheckAt = Time.time;
    }

    private void OnEnable()
    {
        if (_character == null)
            Debug.LogError($"[{nameof(CharacterSleepPolicy)}] CharacterBasicAI2D 참조가 없습니다.", this);
        if (_outFocusKey == null)
            Debug.LogError($"[{nameof(CharacterSleepPolicy)}] OutFocusKeyHook 참조가 없습니다.", this);
        if (_outFocusMouse == null)
            Debug.LogError($"[{nameof(CharacterSleepPolicy)}] OutFocusMouseHook 참조가 없습니다.", this);

        if (_outFocusKey != null) _outFocusKey.KeyPressed += OnOutFocusKey;
        if (_outFocusMouse != null) _outFocusMouse.ButtonPressed += OnOutFocusMouseButton;

        // InFocus 키 — KeyControl만 통과시켜 마우스/게임패드 등 다른 ButtonControl 제외.
        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(ctrl =>
        {
            if (ctrl is KeyControl) RecordInput();
        });
    }

    private void OnDisable()
    {
        if (_outFocusKey != null) _outFocusKey.KeyPressed -= OnOutFocusKey;
        if (_outFocusMouse != null) _outFocusMouse.ButtonPressed -= OnOutFocusMouseButton;
        _anyButtonSubscription?.Dispose();
        _anyButtonSubscription = null;
    }

    private void Update()
    {
        // InFocus 마우스 — OutFocus 시 Mouse.current는 자체적으로 클릭을 받지 못함 (자연 배타).
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame
            || mouse.rightButton.wasPressedThisFrame
            || mouse.middleButton.wasPressedThisFrame))
            RecordInput();

        if (_character == null) return;
        if (_character.CurrentStateId == CharacterStateId.Sleep) return;
        if (Time.time - _lastInputAt < _idleThresholdSeconds) return;
        if (Time.time - _lastCheckAt < _sleepCheckInterval) return;

        _lastCheckAt = Time.time;
        if (UnityEngine.Random.value < _sleepProbabilityPerCheck)
            _character.RequestSleep();
    }

    private void OnOutFocusKey(Key _) => RecordInput();
    private void OnOutFocusMouseButton(MouseButton _) => RecordInput();

    private void RecordInput()
    {
        _lastInputAt = Time.time;
        if (_character != null && _character.CurrentStateId == CharacterStateId.Sleep)
            _character.RequestWakeUp();
    }
}
