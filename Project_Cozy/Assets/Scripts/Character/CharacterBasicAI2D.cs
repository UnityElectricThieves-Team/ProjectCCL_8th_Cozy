using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 캐릭터 단일 개체의 자율 거동 상태 머신. Idle/Walk을 스스로 순환하고, 외부(<see cref="SleepController"/>)의
/// RequestSleep/WakeUp/Fall 호출로 강제 전환된다. 시각 표현은 분리 — 본 컴포넌트는 transform 갱신과 상태 전환만.
///
/// 5개 State 인스턴스를 Awake에서 한 번만 만들어 배열로 보관 → 전환 시 new 없음(할당 회피).
/// Rigidbody2D 미사용, transform.position 직접 갱신. 바닥 충돌은 발 위치에서 아래로 짧은 raycast로
/// 능동 질의 — 바닥은 씬의 GameObject(<c>_groundLayerMask</c>에 속하는 Collider2D)이고, 본 컴포넌트는
/// 어떤 ground인지 모른 채 "발 밑에 있나"만 묻는다.
/// </summary>
public sealed class CharacterBasicAI2D : MonoBehaviour
{
    [Header("Phase durations (seconds, x=min, y=max)")]
    [SerializeField] private Vector2 _idleDurationRange = new Vector2(1f, 2f);
    [SerializeField] private Vector2 _walkDurationRange = new Vector2(3f, 5f);
    [SerializeField] private float _wakeUpDuration = 0.6f;
    [FormerlySerializedAs("_spawnDuration")]
    [SerializeField] private float _landDuration = 0.4f;

    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 1.5f;

    [Header("Fall")]
    [Tooltip("아래 가속도. 양수로 입력하면 매 프레임 velocityY -= gravity*dt로 감소.")]
    [SerializeField] private float _gravity = 12f;

    [Header("Ground probe")]
    [Tooltip("ground로 간주할 레이어. 씬에서 만든 Ground GameObject의 레이어를 체크.")]
    [SerializeField] private LayerMask _groundLayerMask;
    [Tooltip("폴백 오프셋. SpriteRenderer가 있으면 그 sprite의 bounds 하단이 우선 사용되고 이 값은 무시. SR 미설정 시에만 적용.")]
    [SerializeField] private Vector2 _footOffset = Vector2.zero;
    [Tooltip("ground를 찾는 raycast 최대 거리. ground의 *위치*를 알아내려면 충분히 길게.")]
    [SerializeField] private float _groundProbeDistance = 100f;
    [Tooltip("발 ~ ground 거리가 이 값 이하면 on ground로 간주 (접지 판정 임계).")]
    [SerializeField] private float _groundContactThreshold = 0.1f;

    [Header("Refs")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField, Tooltip("Visual의 Collider2D. 있으면 이 bounds 하단을 발 위치로 사용 (Custom Physics Shape 기반이라 알파 영역에 정확). 비우면 Awake에서 _spriteRenderer의 GameObject에서 자동 탐색.")]
    private Collider2D _visualCollider;

    [Header("Debug")]
    [Tooltip("상태 전환마다 Debug.Log를 찍는다.")]
    [SerializeField] private bool _logStateChanges = true;

    private const float MIN_PHASE_SECONDS = 0.05f;

    private BaseCharacterState[] _statesById;
    private BaseCharacterState _current;

    /// <summary>현재 상태 ID. 외부 라벨/디버그가 읽는다.</summary>
    public CharacterStateId CurrentStateId => _current != null ? _current.Id : CharacterStateId.Idle;

    /// <summary>현재 상태의 표시용 이름.</summary>
    public string CurrentStateName => _current != null ? _current.Name : string.Empty;

    /// <summary>상태가 바뀔 때마다 새 상태 ID로 호출된다. <see cref="CharacterStateLabel"/> 등이 구독.</summary>
    public event Action<CharacterStateId> StateChanged;

    // ===== State가 읽는 정책 API =====
    public float WalkSpeed => _walkSpeed;
    public float Gravity => _gravity;
    public float WakeUpDuration => _wakeUpDuration;
    public float LandDuration => _landDuration;
    public float NextIdleDuration() => RandomInRange(_idleDurationRange);
    public float NextWalkDuration() => RandomInRange(_walkDurationRange);

    /// <summary>
    /// 월드 좌표에서의 발 위치. 우선순위:
    /// (1) <see cref="_visualCollider"/>.bounds 하단-중앙 — Custom Physics Shape이면 알파 영역에 정확.
    /// (2) <see cref="_spriteRenderer"/>.bounds 하단-중앙 — sprite rect 기준(투명 padding 포함, 부정확할 수 있음).
    /// (3) <see cref="_footOffset"/> 폴백 — SR/Collider 모두 없을 때.
    /// </summary>
    public Vector2 FootWorldPosition
    {
        get
        {
            if (_visualCollider != null)
            {
                var b = _visualCollider.bounds;
                return new Vector2(b.center.x, b.min.y);
            }
            if (_spriteRenderer != null && _spriteRenderer.sprite != null)
            {
                var b = _spriteRenderer.bounds;
                return new Vector2(b.center.x, b.min.y);
            }
            return transform.TransformPoint(_footOffset);
        }
    }

    private void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        // Visual에 Collider2D가 있으면 발 위치 계산에 우선 사용. 보통 PolygonCollider2D 자동 탐색.
        if (_visualCollider == null && _spriteRenderer != null)
            _visualCollider = _spriteRenderer.GetComponent<Collider2D>();

        _statesById = new BaseCharacterState[]
        {
            new IdleState(),
            new WalkState(),
            new SleepState(),
            new WakeUpState(),
            new FallState(),
            new LandState(),
            new PetState(),
            new GrabbedState(),
        };
    }

    private void Start()
    {
        // 발이 ground에 닿아 있으면 Idle, 아니면 Fall — 공중 스폰이면 자연스럽게 떨어진다.
        var startId = IsFootOnGround(out _) ? CharacterStateId.Idle : CharacterStateId.Fall;
        EnterState(startId);
    }

    private void Update()
    {
        _current?.Tick(this, Time.deltaTime);
    }

    // ===== 외부 트리거 =====

    /// <summary>SleepController 등이 호출. Sleep/Fall/Land/Pet/Grabbed 중에는 무시 — 공중·상호작용 중에는 자지 않는다.</summary>
    public void RequestSleep()
    {
        if (CurrentStateId == CharacterStateId.Sleep) return;
        if (CurrentStateId == CharacterStateId.Fall) return;
        if (CurrentStateId == CharacterStateId.Land) return;
        if (CurrentStateId == CharacterStateId.Pet) return;
        if (CurrentStateId == CharacterStateId.Grabbed) return;
        ChangeState(CharacterStateId.Sleep);
    }

    /// <summary>SleepController 등이 호출. Sleep 중일 때만 WakeUp으로.</summary>
    public void RequestWakeUp()
    {
        if (CurrentStateId != CharacterStateId.Sleep) return;
        ChangeState(CharacterStateId.WakeUp);
    }

    /// <summary>Drop 등 외부 트리거. 이미 Fall이면 무시.</summary>
    public void RequestFall()
    {
        if (CurrentStateId == CharacterStateId.Fall) return;
        ChangeState(CharacterStateId.Fall);
    }

    /// <summary>OpaqueHoverable 등 호버 진입이 호출. Sleep/Fall/Land/Pet/Grabbed 중에는 무시.</summary>
    public void RequestPet()
    {
        if (CurrentStateId == CharacterStateId.Pet) return;
        if (CurrentStateId == CharacterStateId.Sleep) return;
        if (CurrentStateId == CharacterStateId.Fall) return;
        if (CurrentStateId == CharacterStateId.Land) return;
        if (CurrentStateId == CharacterStateId.Grabbed) return;
        ChangeState(CharacterStateId.Pet);
    }

    /// <summary>호버 종료가 호출. Pet 상태일 때만 Idle로 복귀.</summary>
    public void RequestUnpet()
    {
        if (CurrentStateId != CharacterStateId.Pet) return;
        ChangeState(CharacterStateId.Idle);
    }

    /// <summary>ClickableEvent 등 클릭 진입이 호출. 이미 Grabbed면 무시. 그 외 상태에선 모두 진입 허용 — 잠자기·낙하 중에도 잡힌다.</summary>
    public void RequestGrab()
    {
        if (CurrentStateId == CharacterStateId.Grabbed) return;
        ChangeState(CharacterStateId.Grabbed);
    }

    // ===== State가 호출하는 전환 API =====

    public void ChangeState(CharacterStateId nextId)
    {
        if (_current != null && _current.Id == nextId) return;
        _current?.OnExit(this);
        EnterState(nextId);
    }

    private void EnterState(CharacterStateId nextId)
    {
        _current = _statesById[(int)nextId];
        _current.OnEnter(this);
        StateChanged?.Invoke(nextId);

        if (_logStateChanges)
            Debug.Log($"[{nameof(CharacterBasicAI2D)}] {name} → {_current.Name}", this);
    }

    // ===== State가 호출하는 물리/표현 헬퍼 =====

    public void MoveHorizontal(float deltaX)
    {
        var p = transform.position;
        p.x += deltaX;
        transform.position = p;
    }

    public void ApplyVerticalDelta(float deltaY)
    {
        var p = transform.position;
        p.y += deltaY;
        transform.position = p;
    }

    /// <summary>월드 좌표로 transform.position을 강제 설정. z는 보존. <see cref="GrabbedState"/>가 마우스 추종에 사용.</summary>
    public void SetWorldPosition(Vector2 worldPos)
    {
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
    }

    public void SetFacing(float direction)
    {
        if (_spriteRenderer == null) return;
        _spriteRenderer.flipX = direction < 0f;
    }

    /// <summary>
    /// 발 위치에서 아래로 raycast해서 ground 레이어에 hit하면 true. hit 위치를 hitPoint로 반환.
    /// </summary>
    // TEMP DIAG: ground raycast miss 진단용 (1초에 한 번 로그). 원인 확인 후 제거.
    private float _nextGroundMissLogTime;

    public bool TryGetGroundBelow(out Vector2 hitPoint)
    {
        var hit = Physics2D.Raycast(FootWorldPosition, Vector2.down, _groundProbeDistance, _groundLayerMask);
        if (hit.collider == null)
        {
            hitPoint = default;
            // TEMP DIAG
            if (Time.time >= _nextGroundMissLogTime)
            {
                Debug.Log($"[Ground MISS] from={FootWorldPosition} dist={_groundProbeDistance} mask=0x{_groundLayerMask.value:X} (mask==0이면 LayerMask가 Nothing)", this);
                _nextGroundMissLogTime = Time.time + 1f;
            }
            return false;
        }
        // TEMP DIAG
        Debug.Log($"[Ground HIT] {hit.collider.name} layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} at {hit.point}", this);
        hitPoint = hit.point;
        return true;
    }

    /// <summary>
    /// 발 ~ ground 거리가 <see cref="_groundContactThreshold"/> 이하면 true — 사실상 접지 상태인지 묻는다.
    /// <see cref="TryGetGroundBelow"/>로 ground를 찾고, 거리를 임계와 비교.
    /// </summary>
    public bool IsFootOnGround(out Vector2 hitPoint)
    {
        if (!TryGetGroundBelow(out hitPoint)) return false;
        var distance = Mathf.Max(0f, FootWorldPosition.y - hitPoint.y);
        return distance <= _groundContactThreshold;
    }

    /// <summary>
    /// 발이 ground collider 내부거나 ground 아래에 있는지 검사. true일 때 <paramref name="groundTop"/>에
    /// 그 ground의 상단 y를 발 X 위치와 묶어 반환 — <see cref="SnapToGround"/>에 그대로 전달 가능.
    /// 사용처: <see cref="GrabbedState"/> 릴리즈 — ground 아래에서 놓을 때 다시 위로 끌어올린다.
    /// </summary>
    public bool IsFootBelowGround(out Vector2 groundTop)
    {
        var foot = FootWorldPosition;
        // 발이 ground collider 안에 박혀 있는 경우
        var inside = Physics2D.OverlapPoint(foot, _groundLayerMask);
        if (inside != null)
        {
            groundTop = new Vector2(foot.x, inside.bounds.max.y);
            return true;
        }
        // 발이 ground 아래에 있어 위로 raycast 시 hit
        var hit = Physics2D.Raycast(foot, Vector2.up, _groundProbeDistance, _groundLayerMask);
        if (hit.collider != null)
        {
            groundTop = new Vector2(foot.x, hit.collider.bounds.max.y);
            return true;
        }
        groundTop = default;
        return false;
    }

    /// <summary>
    /// <see cref="FootWorldPosition"/>.y가 hitPoint.y와 정확히 일치하도록 transform.position.y에 delta 적용. x는 유지.
    /// FootWorldPosition 정의에 따라 sprite bounds 하단 또는 _footOffset 폴백 어느 쪽이든 자동 정렬.
    /// </summary>
    public void SnapToGround(Vector2 hitPoint)
    {
        var p = transform.position;
        p.y += hitPoint.y - FootWorldPosition.y;
        transform.position = p;
    }

    private static float RandomInRange(Vector2 range)
    {
        var min = Mathf.Min(range.x, range.y);
        var max = Mathf.Max(range.x, range.y);
        return Mathf.Max(MIN_PHASE_SECONDS, UnityEngine.Random.Range(min, max));
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var foot = FootWorldPosition;
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(foot, 0.03f);
        Gizmos.DrawLine(foot, foot + Vector2.down * _groundProbeDistance);
    }
#endif
}
