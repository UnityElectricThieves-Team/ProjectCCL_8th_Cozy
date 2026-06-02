using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// 캐릭터 상태 머신 + Sleep 정책 + SpecialMode 분기. 13개 <see cref="CharacterState"/> 중 지속적 상태를 State 클래스로 다룬다.
/// 순수 C# <see cref="SerializableAttribute"/> 클래스 — <see cref="BaseCharacterController"/>가 <c>[SerializeField]</c>로 nested 보유.
///
/// State 전환은 두 경로:
///  1) State.Tick에서 자체 결정 → owner.ChangeState 호출
///  2) 외부의 Request* API 호출
///
/// SpecialMode 분기: <see cref="SpecialMode"/>가 true이면 Idle/Walk 진입 요청은 SpecialIdle/SpecialWalk로 자동 변환.
/// 외부(State 클래스 등)는 기본 enum만 호출하면 된다.
/// </summary>
[Serializable]
public sealed class StateModule
{
    [Header("Phase durations (sec)")]
    [SerializeField] private Vector2 _idleDurationRange = new Vector2(1f, 2f);
    [SerializeField] private Vector2 _walkDurationRange = new Vector2(3f, 5f);
    [SerializeField] private float _wakeUpDuration = 0.6f;
    [SerializeField] private float _landDuration = 0.4f;

    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 1.5f;
    [SerializeField] private float _runSpeed = 3.5f;

    [Header("Sleep policy")]
    [SerializeField] private OutFocusKeyHook _outFocusKey;
    [SerializeField] private OutFocusMouseHook _outFocusMouse;
    [Tooltip("이 시간 동안 무입력이 누적되면 sleep 검사 시작.")]
    [SerializeField] private float _idleThresholdSeconds = 30f;
    [SerializeField] private float _sleepCheckInterval = 5f;
    [Range(0f, 1f)]
    [SerializeField] private float _sleepProbabilityPerCheck = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool _logStateChanges = true;

    private const float MIN_PHASE_SECONDS = 0.05f;

    private BaseCharacterController _owner;
    private IStateOwner _stateOwner;
    private readonly Dictionary<CharacterState, BaseCharacterState> _statesById = new();
    private BaseCharacterState _current;

    private IDisposable _anyButtonSubscription;
    private float _lastInputAt;
    private float _lastCheckAt;

    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float WakeUpDuration => _wakeUpDuration;
    public float LandDuration => _landDuration;
    public float NextIdleDuration() => RandomInRange(_idleDurationRange);
    public float NextWalkDuration() => RandomInRange(_walkDurationRange);

    public CharacterState CurrentStateId => _current != null ? _current.Id : CharacterState.Idle;
    public string CurrentStateName => _current != null ? _current.Name : string.Empty;
    public bool SpecialMode { get; set; }

    public event Action<CharacterState> StateChanged;

    public void Bind(BaseCharacterController owner)
    {
        _owner = owner;
        _stateOwner = owner;

        // 인스펙터 미연결 시 Singleton 인스턴스 참조 — OutFocus hook은 OS-wide라 씬당 1개만 존재.
        // hook 측 [DefaultExecutionOrder(-100)]이 BaseCharacterController보다 먼저 Awake되도록 보장하므로 이 시점에 Instance가 잡혀 있다.
        if (_outFocusKey == null) _outFocusKey = OutFocusKeyHook.Instance;
        if (_outFocusMouse == null) _outFocusMouse = OutFocusMouseHook.Instance;

        RegisterState(new IdleState());
        RegisterState(new WalkState());
        RegisterState(new RunState());
        RegisterState(new SleepState());
        RegisterState(new WakeUpState());
        RegisterState(new PetState());
        RegisterState(new GrabbedState());
        RegisterState(new FallState());
        RegisterState(new LandState());
        RegisterState(new SpecialIdleState());
        RegisterState(new SpecialWalkState());
    }

    /// <summary>자식 클래스의 종별 추가 state 확장점. <see cref="BaseCharacterController.RegisterExtraStates"/>에서 호출.</summary>
    public void RegisterState(BaseCharacterState state)
    {
        _statesById[state.Id] = state;
    }

    /// <summary>BaseCharacterController.Start에서 호출 — 시작 상태 결정.</summary>
    public void StartUp()
    {
        var startId = _stateOwner.IsFootOnGround(out _) ? CharacterState.Idle : CharacterState.Fall;
        EnterState(startId);

        _lastInputAt = Time.time;
        _lastCheckAt = Time.time;
    }

    public void Subscribe()
    {
        if (_outFocusKey != null) _outFocusKey.KeyPressed += OnOutFocusKey;
        if (_outFocusMouse != null) _outFocusMouse.ButtonPressed += OnOutFocusMouseButton;
        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(ctrl =>
        {
            if (ctrl is KeyControl) RecordInput();
        });
    }

    public void Unsubscribe()
    {
        if (_outFocusKey != null) _outFocusKey.KeyPressed -= OnOutFocusKey;
        if (_outFocusMouse != null) _outFocusMouse.ButtonPressed -= OnOutFocusMouseButton;
        _anyButtonSubscription?.Dispose();
        _anyButtonSubscription = null;
    }

    public void Tick(float dt)
    {
        if (_current != null)
            _current.Tick(_stateOwner, dt);

        // InFocus 마우스 폴링
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame
            || mouse.rightButton.wasPressedThisFrame
            || mouse.middleButton.wasPressedThisFrame))
            RecordInput();

        // Sleep 정책 검사
        if (CurrentStateId == CharacterState.Sleep) return;
        if (Time.time - _lastInputAt < _idleThresholdSeconds) return;
        if (Time.time - _lastCheckAt < _sleepCheckInterval) return;

        _lastCheckAt = Time.time;
        if (UnityEngine.Random.value < _sleepProbabilityPerCheck)
            RequestSleep();
    }

    // ===== Request* API =====

    /// <summary>기획서 §🛡️ "상태 잠금" — 이 상태들 중에는 외부 Request* 모두 무시 (모션 중단 방지).</summary>
    private bool IsLockedState =>
        CurrentStateId == CharacterState.WakeUp
        || CurrentStateId == CharacterState.Land
        || CurrentStateId == CharacterState.Transform;

    public void RequestSleep()
    {
        if (IsLockedState) return;
        if (CurrentStateId == CharacterState.Sleep) return;
        if (CurrentStateId == CharacterState.Fall) return;
        if (CurrentStateId == CharacterState.Pet) return;
        if (CurrentStateId == CharacterState.Grabbed) return;
        ChangeState(CharacterState.Sleep);
    }

    public void RequestWakeUp()
    {
        if (CurrentStateId != CharacterState.Sleep) return;
        ChangeState(CharacterState.WakeUp);
    }

    public void RequestFall()
    {
        if (IsLockedState) return;
        if (CurrentStateId == CharacterState.Fall) return;
        ChangeState(CharacterState.Fall);
    }

    public void RequestPet()
    {
        if (IsLockedState) return;
        if (CurrentStateId == CharacterState.Pet) return;
        if (CurrentStateId == CharacterState.Sleep) return;
        if (CurrentStateId == CharacterState.Fall) return;
        if (CurrentStateId == CharacterState.Grabbed) return;
        ChangeState(CharacterState.Pet);
    }

    public void RequestUnpet()
    {
        if (CurrentStateId != CharacterState.Pet) return;
        ChangeState(CharacterState.Idle);
    }

    public void RequestGrab()
    {
        if (IsLockedState) return;
        if (CurrentStateId == CharacterState.Grabbed) return;
        ChangeState(CharacterState.Grabbed);
    }

    // ===== State 전환 =====

    public void ChangeState(CharacterState nextId)
    {
        if (SpecialMode)
        {
            if (nextId == CharacterState.Idle) nextId = CharacterState.SpecialIdle;
            else if (nextId == CharacterState.Walk) nextId = CharacterState.SpecialWalk;
        }

        if (_current != null && _current.Id == nextId) return;
        _current?.OnExit(_stateOwner);
        EnterState(nextId);
    }

    private void EnterState(CharacterState nextId)
    {
        if (!_statesById.TryGetValue(nextId, out var state) || state == null)
        {
            Debug.LogWarning($"[StateModule] State {nextId}가 등록되지 않았습니다.");
            return;
        }
        _current = state;
        _current.OnEnter(_stateOwner);
        StateChanged?.Invoke(nextId);

        if (_logStateChanges && _owner != null)
            Debug.Log($"[StateModule] {_owner.name} → {_current.Name}", _owner);
    }

    private void OnOutFocusKey(Key _) => RecordInput();
    private void OnOutFocusMouseButton(MouseButton _) => RecordInput();

    private void RecordInput()
    {
        _lastInputAt = Time.time;
        if (CurrentStateId == CharacterState.Sleep) RequestWakeUp();
    }

    private static float RandomInRange(Vector2 range)
    {
        var min = Mathf.Min(range.x, range.y);
        var max = Mathf.Max(range.x, range.y);
        return Mathf.Max(MIN_PHASE_SECONDS, UnityEngine.Random.Range(min, max));
    }
}
