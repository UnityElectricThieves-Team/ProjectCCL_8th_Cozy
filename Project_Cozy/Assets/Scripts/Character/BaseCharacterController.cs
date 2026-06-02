using UnityEngine;

/// <summary>
/// 캐릭터 단일 개체의 메인 컴포넌트. 라이프사이클을 받아 3개 module(<see cref="StateModule"/>/<see cref="VisualModule"/>/<see cref="AffinityModule"/>)에 위임한다.
/// non-sealed — 종별 자식 클래스(Cat/Dog 등)가 상속한다. 이번 마일스톤(Phase 1~7)은 base만, 자식 클래스 도입은 Phase 10.
///
/// IStateOwner 구현 — State 클래스가 호출하는 정책 수치·거동 API를 노출. 정책 수치는 StateModule에 위임, Ground 시스템·중력·물리는 본체가 담당.
/// </summary>
public class BaseCharacterController : MonoBehaviour, IStateOwner
{
    [Header("Unity refs")]
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("기존 PolygonCollider2D 참조 — 발 위치 계산용. 비어 있으면 Awake에서 _spriteRenderer의 GameObject에서 자동 탐색.")]
    [SerializeField] private Collider2D _visualCollider;

    [Header("Ground & Gravity")]
    [Tooltip("아래 가속도. FallState가 매 프레임 -gravity*dt로 적용.")]
    [SerializeField] private float _gravity = 12f;
    [Tooltip("ground로 간주할 레이어.")]
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private float _groundProbeDistance = 100f;
    [SerializeField] private float _groundContactThreshold = 0.1f;
    [Tooltip("SR/Collider 미설정 시 폴백 발 오프셋.")]
    [SerializeField] private Vector2 _footOffset = Vector2.zero;

    [Header("Modules")]
    [SerializeField] private StateModule _state = new StateModule();
    [SerializeField] private VisualModule _visual = new VisualModule();
    [SerializeField] private AffinityModule _affinity = new AffinityModule();

    public Animator Animator => _animator;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;
    public Collider2D VisualCollider => _visualCollider;

    public StateModule State => _state;
    public VisualModule Visual => _visual;
    public AffinityModule Affinity => _affinity;

    // ===== IStateOwner: 정책 수치 (StateModule에 위임) =====
    public float WalkSpeed => _state.WalkSpeed;
    public float RunSpeed => _state.RunSpeed;
    public float Gravity => _gravity;
    public float WakeUpDuration => _state.WakeUpDuration;
    public float LandDuration => _state.LandDuration;
    public float NextIdleDuration() => _state.NextIdleDuration();
    public float NextWalkDuration() => _state.NextWalkDuration();

    // ===== IStateOwner: Transform / 발 위치 =====
    public Transform Transform => transform;
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

    // ===== IStateOwner: 상태 전환 =====
    public void ChangeState(CharacterState nextId) => _state.ChangeState(nextId);

    protected virtual void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_visualCollider == null && _spriteRenderer != null)
            _visualCollider = _spriteRenderer.GetComponent<Collider2D>();

        _state.Bind(this);
        _visual.Bind(this);
        _affinity.Bind(this);

        RegisterExtraStates(_state);

        _state.StateChanged += OnStateChanged;
        _affinity.SpecialActivated += OnAffinityActivated;
        _affinity.SpecialReleased += OnAffinityReleased;
    }

    protected virtual void Start()
    {
        _visual.ApplyStartup();
        _state.StartUp();
    }

    protected virtual void OnEnable()
    {
        _state.Subscribe();
    }

    protected virtual void Update()
    {
        var dt = Time.deltaTime;
        _state.Tick(dt);
        _visual.Tick(dt);
    }

    protected virtual void OnDisable()
    {
        _state.Unsubscribe();
    }

    protected virtual void OnDestroy()
    {
        _state.StateChanged -= OnStateChanged;
        if (_affinity != null)
        {
            _affinity.SpecialActivated -= OnAffinityActivated;
            _affinity.SpecialReleased -= OnAffinityReleased;
        }
    }

    // ===== Hook (자식 클래스 확장점) =====

    protected virtual void RegisterExtraStates(StateModule state) { }
    protected virtual void OnSpecialActivated() { }
    protected virtual void OnSpecialReleased() { }

    private void OnStateChanged(CharacterState state)
    {
        _visual.Play(state);
    }

    // ===== Affinity 이벤트 핸들러 — SpecialMode 토글 + 현재 state Special 분기로 즉시 전환 =====

    private void OnAffinityActivated()
    {
        _state.SpecialMode = true;
        var cur = _state.CurrentStateId;
        if (cur == CharacterState.Idle) _state.ChangeState(CharacterState.Idle);
        else if (cur == CharacterState.Walk) _state.ChangeState(CharacterState.Walk);
        OnSpecialActivated();
    }

    private void OnAffinityReleased()
    {
        _state.SpecialMode = false;
        var cur = _state.CurrentStateId;
        if (cur == CharacterState.SpecialIdle) _state.ChangeState(CharacterState.Idle);
        else if (cur == CharacterState.SpecialWalk) _state.ChangeState(CharacterState.Walk);
        OnSpecialReleased();
    }

    // ===== UnityEvent에서 호출 가능한 공개 메서드 (호버/언호버) =====

    /// <summary>호버 진입 시: 친밀도 누적 + Pet 상태 진입. OpaqueHoverable.UnityEvent에서 m_Target으로 호출.</summary>
    public void OnHover()
    {
        _affinity.AddOnHoverEnter();
        RequestPet();
    }

    /// <summary>호버 종료 시: Pet 종료. OpaqueHoverable.UnityEvent에서 호출.</summary>
    public void OnHoverEnd()
    {
        RequestUnpet();
    }

    // ===== External triggers — StateModule에 위임 =====
    public void RequestSleep() => _state.RequestSleep();
    public void RequestWakeUp() => _state.RequestWakeUp();
    public void RequestFall() => _state.RequestFall();
    public void RequestPet() => _state.RequestPet();
    public void RequestUnpet() => _state.RequestUnpet();
    public void RequestGrab() => _state.RequestGrab();

    // ===== IStateOwner: 물리/표현 헬퍼 =====

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

    public void SetWorldPosition(Vector2 worldPos)
    {
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
    }

    public void SetFacing(float direction)
    {
        _visual.SetFacing(direction < 0f);
    }

    // ===== IStateOwner: Ground =====

    public bool TryGetGroundBelow(out Vector2 hitPoint)
    {
        var hit = Physics2D.Raycast(FootWorldPosition, Vector2.down, _groundProbeDistance, _groundLayerMask);
        if (hit.collider == null)
        {
            hitPoint = default;
            return false;
        }
        hitPoint = hit.point;
        return true;
    }

    public bool IsFootOnGround(out Vector2 hitPoint)
    {
        if (!TryGetGroundBelow(out hitPoint)) return false;
        var distance = Mathf.Max(0f, FootWorldPosition.y - hitPoint.y);
        return distance <= _groundContactThreshold;
    }

    public bool IsFootBelowGround(out Vector2 groundTop)
    {
        var foot = FootWorldPosition;
        var inside = Physics2D.OverlapPoint(foot, _groundLayerMask);
        if (inside != null)
        {
            groundTop = new Vector2(foot.x, inside.bounds.max.y);
            return true;
        }
        var hit = Physics2D.Raycast(foot, Vector2.up, _groundProbeDistance, _groundLayerMask);
        if (hit.collider != null)
        {
            groundTop = new Vector2(foot.x, hit.collider.bounds.max.y);
            return true;
        }
        groundTop = default;
        return false;
    }

    public void SnapToGround(Vector2 hitPoint)
    {
        var p = transform.position;
        p.y += hitPoint.y - FootWorldPosition.y;
        transform.position = p;
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
