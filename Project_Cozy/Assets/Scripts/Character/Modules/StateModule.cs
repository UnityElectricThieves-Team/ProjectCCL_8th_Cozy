using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// 캐릭터 상태 머신 + Sleep 정책. 11개 <see cref="CharacterState"/> 중 지속적 상태를 State 클래스로 다룬다.
/// 순수 C# <see cref="SerializableAttribute"/> 클래스 — <see cref="BaseCharacterController"/>가 <c>[SerializeField]</c>로 nested 보유.
///
/// State 전환은 두 경로:
///  1) State.Tick에서 자체 결정 → owner.ChangeState 호출
///  2) 외부의 Request* API 호출
/// </summary>
[Serializable]
public sealed class StateModule
{
    [Header("Durations (모두 초 단위)")]
    [Tooltip("대기 상태에서 다음 행동을 고르기까지 기다리는 시간의 최소~최대(초).\n" +
             "이 시간이 지나면 걷기와 특수 대기 중 하나를 확률로 고른다.\n" +
             "기획 확정값 10~12초.")]
    [SerializeField] private Vector2 _idleDurationRange = new Vector2(10f, 12f);

    [Range(0f, 1f)]
    [Tooltip("대기 시간이 끝났을 때 특수 대기(하품·기지개 등)를 고를 확률.\n" +
             "나머지 확률로 걷기를 고른다. 기획 확정값 0.1.\n" +
             "동작을 확인할 때는 0.5쯤으로 올렸다가 되돌릴 것.")]
    [SerializeField] private float _idleActionProbability = 0.1f;

    [Tooltip("특수 대기 모션을 재생하는 시간(초). 이 시간이 지나면 대기로 돌아온다.\n" +
             "실제 애니메이션 클립 길이에 맞춰 조정할 것.")]
    [SerializeField] private float _idleActionDuration = 1.5f;

    [Tooltip("쓰담 모션을 재생하는 시간(초). 이 시간이 지나면 스스로 대기로 돌아온다.\n" +
             "재생 중에 다시 클릭해도 모션이 처음부터 다시 시작하지 않는다.\n" +
             "실제 애니메이션 클립 길이에 맞춰 조정할 것.")]
    [SerializeField] private float _petDuration = 1.2f;

    [Tooltip("취침에서 깨어나 대기로 넘어가기까지의 기상 모션 시간(초).\n" +
             "이 동안에는 쓰담·잡기 같은 외부 요청을 모두 무시한다.")]
    [SerializeField] private float _wakeUpDuration = 0.6f;

    [Tooltip("낙하 후 착지 모션 시간(초).\n" +
             "이 동안에는 쓰담·잡기 같은 외부 요청을 모두 무시한다.")]
    [SerializeField] private float _landDuration = 0.4f;

    [Tooltip("변신 이펙트 시간(초). 중간 지점에서 동물↔소녀 폼이 바뀐다.\n" +
             "이 동안에는 쓰담·잡기 같은 외부 요청을 모두 무시한다.")]
    [SerializeField] private float _transformDuration = 0.8f;

    [Header("Movement")]
    [Tooltip("걷기 속도(초당 월드 단위). 뷰포트 안 목적지까지 이 속도로 이동한다.")]
    [SerializeField] private float _walkSpeed = 1.5f;

    [Tooltip("달리기 속도(초당 월드 단위). 지금은 이 상태로 전환하는 곳이 없어 쓰이지 않는다.")]
    [SerializeField] private float _runSpeed = 3.5f;

    [Tooltip("걷기 목적지를 뽑을 때 현재 위치에서 최소한 이만큼 떨어진 곳을 고른다(월드 단위).\n" +
             "너무 가까운 목적지를 뽑으면 걷자마자 도착해 제자리에서 멈칫거린다.\n" +
             "거주 영역이 이 값의 두 배보다 좁으면 반대쪽 끝으로 간다.")]
    [SerializeField] private float _walkMinDistance = 0.5f;

    [Header("Sleep policy")]
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
    public float WalkMinDistance => _walkMinDistance;
    public float WakeUpDuration => _wakeUpDuration;
    public float LandDuration => _landDuration;
    public float TransformDuration => _transformDuration;
    public float IdleActionDuration => _idleActionDuration;
    public float PetDuration => _petDuration;
    public float NextIdleDuration() => RandomInRange(_idleDurationRange);

    /// <summary>대기 시간이 끝났을 때 특수 대기로 갈지 뽑는다. false면 걷기.</summary>
    public bool RollIdleAction() => UnityEngine.Random.value < _idleActionProbability;

    public CharacterState CurrentStateId => _current != null ? _current.Id : CharacterState.Idle;
    public string CurrentStateName => _current != null ? _current.Name : string.Empty;

    public event Action<CharacterState> StateChanged;

    public void Bind(BaseCharacterController owner)
    {
        _owner = owner;
        _stateOwner = owner;

        RegisterState(new IdleState());
        RegisterState(new WalkState());
        RegisterState(new RunState());
        RegisterState(new SleepState());
        RegisterState(new WakeUpState());
        RegisterState(new PetState());
        RegisterState(new GrabbedState());
        RegisterState(new FallState());
        RegisterState(new LandState());
        RegisterState(new TransformState());
        RegisterState(new IdleActionState());
    }

    /// <summary>자식 클래스의 종별 추가 state 확장점. <see cref="BaseCharacterController.RegisterExtraStates"/>에서 호출.</summary>
    public void RegisterState(BaseCharacterState state)
    {
        _statesById[state.Id] = state;
    }

    /// <summary>BaseCharacterController.Start에서 호출 — 시작 상태 결정.</summary>
    public void StartUp()
    {
        var startId = _stateOwner.IsFootOnGround() ? CharacterState.Idle : CharacterState.Fall;
        EnterState(startId);

        _lastInputAt = Time.time;
        _lastCheckAt = Time.time;
    }

    public void Subscribe()
    {
        OutFocusKeyHook.KeyPressed += OnOutFocusKey;
        OutFocusMouseHook.ButtonPressed += OnOutFocusMouseButton;
        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(ctrl =>
        {
            if (ctrl is KeyControl) RecordInput();
        });
    }

    public void Unsubscribe()
    {
        OutFocusKeyHook.KeyPressed -= OnOutFocusKey;
        OutFocusMouseHook.ButtonPressed -= OnOutFocusMouseButton;
        _anyButtonSubscription?.Dispose();
        _anyButtonSubscription = null;
    }

    public void Tick(float dt)
    {
        if (_current != null)
            _current.Tick(_stateOwner, dt);

        EnforceFloor();

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

    // ===== 접지 규칙 =====

    /// <summary>이 상태는 자기 세로 위치를 스스로 쥐고 있다 — 바닥 고정에서 제외된다.
    /// Grabbed가 여기 있는 것은 공중이라서가 아니라 마우스가 y를 정하기 때문이다.
    ///
    /// **세로로 스스로 움직이는 상태를 새로 만들면 여기 넣어야 한다.** 빠뜨리면 그 상태는 매 프레임
    /// 지면으로 끌려내려가고, 증상은 "모션이 안 나온다"로 조용히 나타난다.</summary>
    private bool OwnsVerticalPosition =>
        CurrentStateId == CharacterState.Fall
        || CurrentStateId == CharacterState.Grabbed;

    /// <summary>접지 규칙을 적용한다 — 세로를 스스로 쥔 상태가 아니면 발을 지면에 고정.
    /// 접지를 여러 곳에서 스냅하는 대신 이 한 곳으로 모았다. 흩뿌리면 반드시 빠지는 경로가 생긴다
    /// (실제로 Idle·Walk 중에는 아무도 접지를 다시 보지 않아 공중에 뜬 채 굳는 버그가 있었다).</summary>
    public void EnforceFloor()
    {
        // StartUp 전 — 시작 상태를 Idle로 볼지 Fall로 볼지는 StartUp이 정한다. 여기서 먼저 바닥에
        // 붙여버리면 스폰 직후 낙하가 사라진다(별에서 스폰된 캐릭터는 Start보다 먼저 거주 영역을 받는다).
        if (_current == null) return;
        if (OwnsVerticalPosition) return;
        _stateOwner.SnapToFloor();
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

    /// <summary>쓰담 진입. **자는 중에도 허용한다** — 확정안은 자는 캐릭터를 눌러도 깨는 대신
    /// 쓰담이 뜨고, 계속 누르면 잡힘으로 넘어가도록 정하고 있다.
    ///
    /// 이미 Pet이면 무시하는 것이 "추가 입력으로 모션이 재시작되지 않는다"를 만든다.</summary>
    public void RequestPet()
    {
        if (IsLockedState) return;
        if (CurrentStateId == CharacterState.Pet) return;
        if (CurrentStateId == CharacterState.Fall) return;
        if (CurrentStateId == CharacterState.Grabbed) return;
        ChangeState(CharacterState.Pet);
    }

    public void RequestGrab()
    {
        if (IsLockedState) return;
        if (CurrentStateId == CharacterState.Grabbed) return;
        ChangeState(CharacterState.Grabbed);
    }

    /// <summary>변신 진입. 잠금 상태(WakeUp/Land/Transform 진행 중)면 무시. 폼 방향·친밀도 게이트는 호출자(BaseCharacterController)가 판정.</summary>
    public void RequestTransform()
    {
        if (IsLockedState) return;
        // 공중/들린 상태에서는 변신 금지 — 중력·접지 처리가 변신과 얽혀 공중 정지하는 버그 방지.
        if (CurrentStateId == CharacterState.Fall) return;
        if (CurrentStateId == CharacterState.Grabbed) return;
        // 수면 중 우클릭은 기상(RecordInput→RequestWakeUp) 입력으로 처리되게 변신 금지(경합 제거).
        if (CurrentStateId == CharacterState.Sleep) return;
        ChangeState(CharacterState.Transform);
    }

    // ===== State 전환 =====

    public void ChangeState(CharacterState nextId)
    {
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
