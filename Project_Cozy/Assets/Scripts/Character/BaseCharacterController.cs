using UnityEngine;

/// <summary>
/// 캐릭터 단일 개체의 메인 컴포넌트. 라이프사이클을 받아 4개 module(<see cref="StateModule"/>/<see cref="VisualModule"/>/<see cref="AffinityModule"/>/<see cref="ScaleModule"/>)에 위임한다.
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
    [Tooltip("바닥 수평선의 y좌표. 발이 이 높이에 닿으면 착지. (Ground 콜라이더 비의존)")]
    [SerializeField] private float _floorY = 0f;
    [Tooltip("[미사용] 현재 y=_floorY 평면 사용. Ground 콜라이더 재도입 시 사용.")]
    [SerializeField] private LayerMask _groundLayerMask;
    [Tooltip("[미사용] 현재 y=_floorY 평면 사용. Ground 콜라이더 재도입 시 사용.")]
    [SerializeField] private float _groundProbeDistance = 100f;
    [Tooltip("발이 바닥(y=_floorY)에 닿았다고 보는 허용 오차.")]
    [SerializeField] private float _groundContactThreshold = 0.1f;
    [Tooltip("발 위치 오프셋(루트 로컬 기준). 발이 바닥에 닿는 지점을 캐릭터 원점 기준으로 지정. " +
             "스프라이트/콜라이더 bounds에 의존하지 않아 애니메이션 중에도 안정적. " +
             "센터 피벗 스프라이트는 보통 y를 음수(발이 원점 아래)로 둔다.")]
    [SerializeField] private Vector2 _footOffset = Vector2.zero;

    [Header("Modules")]
    [SerializeField] private StateModule _state = new StateModule();
    [SerializeField] private VisualModule _visual = new VisualModule();
    [SerializeField] private AffinityModule _affinity = new AffinityModule();
    [SerializeField] private ScaleModule _scale = new ScaleModule();

    // 스폰 시 CharacterNames에서 할당받는 고유 이름(정체성). 표현(머리 위 라벨)과 분리.
    private string _name;

    // 이 캐릭터가 머무를 수 있는 월드 영역. 밖에서 SetLivingArea로 주입한다(기본은 제한 없음).
    // 캐릭터는 이 사각형이 무엇에서 왔는지 모른다 — 뷰포트를 아는 것은 Gameplay/Viewport 쪽이다.
    private Rect _livingArea;
    private bool _hasLivingArea;

    /// <summary>실제로 서고 걷는 바닥 높이(월드 y).
    /// 거주 영역이 주어졌으면 **그 아래 변이 곧 지면**이다 — 뷰포트를 올리든 내리든 지면이 따라간다
    /// (기획 §2.1.1: "땅바닥은 항상 뷰포트 하단 변에 포함되며 뷰포트와 함께 이동한다").
    /// 직렬화된 <see cref="_floorY"/>는 거주 영역이 없는 씬에서 쓰는 대체값이다.</summary>
    private float FloorY => _hasLivingArea ? _livingArea.yMin : _floorY;

    public Animator Animator => _animator;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;
    public Collider2D VisualCollider => _visualCollider;

    /// <summary>스폰 시 할당된 이름. 머리 위 라벨(<see cref="CharacterNameLabel"/>)이 표시한다.</summary>
    public string Name => _name;

    public StateModule State => _state;
    public VisualModule Visual => _visual;
    public AffinityModule Affinity => _affinity;
    public ScaleModule Scale => _scale;

    // ===== IStateOwner: 정책 수치 (StateModule에 위임) =====
    public float WalkSpeed => _state.WalkSpeed;
    public float RunSpeed => _state.RunSpeed;
    public float Gravity => _gravity;
    public float WakeUpDuration => _state.WakeUpDuration;
    public float LandDuration => _state.LandDuration;
    public float TransformDuration => _state.TransformDuration;
    public float NextIdleDuration() => _state.NextIdleDuration();
    public float NextWalkDuration() => _state.NextWalkDuration();

    // ===== IStateOwner: Transform / 발 위치 =====
    public Transform Transform => transform;
    // 발 위치는 루트 transform + 고정 오프셋(_footOffset)으로 계산한다.
    // 애니메이션 프레임마다 변하는 스프라이트/콜라이더 bounds에 의존하지 않아
    // 매 프레임 안정적(상하 떨림 없음). 오프셋은 인스펙터에서 발 위치에 맞춰 조정.
    public Vector2 FootWorldPosition => transform.TransformPoint(_footOffset);

    // ===== IStateOwner: 상태 전환 =====
    public void ChangeState(CharacterState nextId) => _state.ChangeState(nextId);

    protected virtual void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_visualCollider == null && _spriteRenderer != null)
            _visualCollider = _spriteRenderer.GetComponent<Collider2D>();

        _name = CharacterNames.Acquire();

        _state.Bind(this);
        _visual.Bind(this);
        _affinity.Bind(this);
        _scale.Bind(this);

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
        _scale.Subscribe();
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
        _scale.Unsubscribe();
    }

    protected virtual void OnDestroy()
    {
        CharacterNames.Release(_name);
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

    /// <summary>우클릭 변신 토글. 동물→소녀는 친밀도 만점 필요, 소녀→동물은 언제든. CharacterInteractionRelay.OnRightClick에서 호출.</summary>
    public void RequestTransform()
    {
        // 동물 → 소녀: 친밀도가 변신 임계 이상일 때만 (AILogic.md §Transform). 소녀 → 동물: 무조건 허용.
        if (_visual.CurrentForm == CharacterForm.Animal && !_affinity.CanHumanTransform) return;
        _state.RequestTransform();
    }

    // ===== IStateOwner: 물리/표현 헬퍼 =====

    /// <summary>수평 이동. 거주 영역에 막혀 요청한 만큼 못 갔으면 false를 돌려준다
    /// (WalkState가 이걸 보고 제자리걸음 대신 반대로 돌아선다).</summary>
    public bool MoveHorizontal(float deltaX)
    {
        Vector3 p = transform.position;
        Vector2 foot = FootWorldPosition;

        float desiredFootX = foot.x + deltaX;
        // 발 기준으로 잡는다. 스프라이트 bounds는 애니메이션 프레임마다 달라져 경계가 떨린다
        // (_footOffset이 존재하는 이유와 같다).
        float allowedFootX = _hasLivingArea
            ? Mathf.Clamp(desiredFootX, _livingArea.xMin, _livingArea.xMax)
            : desiredFootX;

        p.x += allowedFootX - foot.x;
        transform.position = p;

        return Mathf.Approximately(allowedFootX, desiredFootX);
    }

    public void ApplyVerticalDelta(float deltaY)
    {
        var p = transform.position;
        p.y += deltaY;
        transform.position = p;
    }

    public void SetWorldPosition(Vector2 worldPos)
    {
        if (_hasLivingArea)
        {
            // MoveHorizontal과 같은 경계(발 기준)를 쓴다. 세로는 바닥선이 아니라 영역 아래 변까지
            // 허용한다 — 바닥 밑으로 끌어내렸다 놓는 기존 동작(놓으면 바닥으로 스냅)을 살리기 위해서다.
            Vector2 footDelta = FootWorldPosition - (Vector2)transform.position;
            Vector2 foot = worldPos + footDelta;
            foot.x = Mathf.Clamp(foot.x, _livingArea.xMin, _livingArea.xMax);
            foot.y = Mathf.Clamp(foot.y, _livingArea.yMin, _livingArea.yMax);
            worldPos = foot - footDelta;
        }
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
    }

    /// <summary>
    /// 이 캐릭터가 머무를 수 있는 월드 영역을 지정한다. 걷기도 드래그도 이 밖으로 나가지 못하고,
    /// 바닥선도 이 영역의 아래 변보다 내려가지 않는다.
    ///
    /// 캐릭터는 이 사각형이 무엇에서 왔는지 모른다 — 월드 좌표로 환산된 결과만 받는다.
    /// 뷰포트에서 환산해 걸어주는 것은 <c>Gameplay/Viewport/ViewportLivingAreaBinder</c>다.
    ///
    /// 지금 밖에 있으면 즉시 안으로 끌어들인다(뷰포트를 캐릭터 밑에서 줄인 경우).
    /// </summary>
    public void SetLivingArea(Rect worldArea)
    {
        // 영역을 바꾸기 **전** 기준으로 "바닥에 있었는가"를 먼저 판정한다.
        // 서 있던 캐릭터는 새 바닥에 계속 서 있어야 한다 — 뷰포트 아래 변을 내리면 같이 내려가야 하는데,
        // Idle/Walk 중에는 아무도 접지를 다시 보지 않아서(FallState·Grabbed 릴리즈·StartUp에서만 본다)
        // 값만 바꿔두면 공중에 뜬 채로 남는다.
        bool wasGrounded = IsFootOnGround(out _);

        _livingArea = worldArea;
        _hasLivingArea = true;

        Vector2 foot = FootWorldPosition;
        float x = Mathf.Clamp(foot.x, _livingArea.xMin, _livingArea.xMax);
        // 공중에 있던 캐릭터(스폰 직후 낙하 등)는 높이를 건드리지 않고 영역 안으로만 넣는다.
        float y = wasGrounded ? FloorY : Mathf.Clamp(foot.y, FloorY, _livingArea.yMax);

        Vector3 p = transform.position;
        p.x += x - foot.x;
        p.y += y - foot.y;
        transform.position = p;
    }

    public void SetFacing(float direction)
    {
        _visual.SetFacing(direction < 0f);
    }

    public CharacterForm CurrentForm => _visual.CurrentForm;
    public void SetForm(CharacterForm form) => _visual.SetForm(form);

    // ===== IStateOwner: Ground =====

    public bool TryGetGroundBelow(out Vector2 hitPoint)
    {
        // 무한 수평 바닥(y=FloorY): 발 바로 아래 바닥점은 항상 존재.
        var foot = FootWorldPosition;
        hitPoint = new Vector2(foot.x, FloorY);
        return true;
    }

    public bool IsFootOnGround(out Vector2 hitPoint)
    {
        var foot = FootWorldPosition;
        float floor = FloorY;
        hitPoint = new Vector2(foot.x, floor);
        // 발이 바닥선(허용오차 내)에 닿았거나 그 아래로 파묻혔으면 접지.
        return foot.y <= floor + _groundContactThreshold;
    }

    public bool IsFootBelowGround(out Vector2 groundTop)
    {
        var foot = FootWorldPosition;
        float floor = FloorY;
        groundTop = new Vector2(foot.x, floor);
        return foot.y < floor;
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
        // 바닥선(y=FloorY)과 발→바닥 거리 표시.
        float floor = FloorY;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(foot.x - 1f, floor, 0f), new Vector3(foot.x + 1f, floor, 0f));
        Gizmos.DrawLine(new Vector3(foot.x, foot.y, 0f), new Vector3(foot.x, floor, 0f));

        // 거주 영역(있으면) — 캐릭터가 나갈 수 없는 경계.
        if (!_hasLivingArea) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(_livingArea.center, new Vector3(_livingArea.width, _livingArea.height, 0f));
    }
#endif
}
