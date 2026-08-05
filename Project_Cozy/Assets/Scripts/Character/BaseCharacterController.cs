using UnityEngine;

/// <summary>
/// 캐릭터 단일 개체의 메인 컴포넌트. 라이프사이클을 받아 4개 module(<see cref="StateModule"/>/<see cref="VisualModule"/>/<see cref="AffinityModule"/>/<see cref="ScaleModule"/>)에 위임한다.
/// non-sealed — 종별 자식 클래스(Cat/Dog 등)가 상속한다. 이번 마일스톤(Phase 1~7)은 base만, 자식 클래스 도입은 Phase 10.
///
/// IStateOwner 구현 — State 클래스가 호출하는 정책 수치·거동 API를 노출. 정책 수치는 StateModule에 위임,
/// 지면 높이·거주 영역·중력·좌표 갱신은 본체가 담당. 접지를 언제 강제할지는 StateModule이 정한다(EnforceFloor).
/// </summary>
public class BaseCharacterController : MonoBehaviour, IStateOwner
{
    [Header("Unity refs")]
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("[미사용] 읽는 코드가 없다. 발 위치 계산에 쓰였으나 이제 루트가 곧 발이라 필요 없다. 제거 대기.")]
    [SerializeField] private Collider2D _visualCollider;

    [Tooltip("좌클릭 입력 어댑터(자식 Visual에 부착). 비우면 Awake에서 자식에서 찾는다.\n" +
             "잡기 오프셋을 '누른 순간'의 커서로 재기 위해 참조한다.")]
    [SerializeField] private HoldClickEvent _holdInput;

    [Header("Gravity")]
    [Tooltip("아래 가속도. FallState가 매 프레임 -gravity*dt로 적용.")]
    [SerializeField] private float _gravity = 12f;

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

    /// <summary>거주 영역이 아직 안 들어온 씬에서 쓰는 지면 높이. 인스펙터로 열지 않는다 —
    /// 조절 가능한 바닥이 있으면 그게 다른 오차(발 위치 어긋남 등)의 보정값 노릇을 하다가,
    /// 지면 정의가 바뀌는 순간 보정이 통째로 사라진다. 실제로 그렇게 어긋난 전례가 있다.</summary>
    private const float FallbackFloorY = 0f;

    /// <summary>실제로 서고 걷는 바닥 높이(월드 y).
    /// 거주 영역이 주어졌으면 **그 아래 변이 곧 지면**이다 — 뷰포트를 올리든 내리든 지면이 따라간다
    /// (기획 §2.1.1: "땅바닥은 항상 뷰포트 하단 변에 포함되며 뷰포트와 함께 이동한다").
    ///
    /// private으로 둔다. 높이를 밖에 내주면 "발이 바닥에 있는가"를 호출자마다 다시 쓰게 되고,
    /// 비교 방식이 갈라진다. 판정이 필요하면 <see cref="IsFootOnGround"/>를 쓴다.</summary>
    private float FloorY => _hasLivingArea ? _livingArea.yMin : FallbackFloorY;

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
    public float WalkMinDistance => _state.WalkMinDistance;
    public float Gravity => _gravity;
    public float WakeUpDuration => _state.WakeUpDuration;
    public float LandDuration => _state.LandDuration;
    public float TransformDuration => _state.TransformDuration;
    public float IdleActionDuration => _state.IdleActionDuration;
    public float PetDuration => _state.PetDuration;
    public float NextIdleDuration() => _state.NextIdleDuration();
    public bool RollIdleAction() => _state.RollIdleAction();

    // ===== IStateOwner: 위치 =====
    // **루트가 곧 발이다.** 프리팹에서 Visual 자식을 올려 스프라이트 아래 끝을 루트 원점에 맞춰 둔다.
    // 그래서 transform.position을 그냥 읽는 코드도 자동으로 발을 가리킨다 — 발 위치를 코드 필드로
    // 들고 있으면 그 필드를 부르는 코드만 옳아지고 나머지는 조용히 틀린다.
    // 왜 이 규약인지는 .claude/rules/unity/character-ground.md.
    public Vector2 WorldPosition => transform.position;

    // ===== IStateOwner: 상태 전환 =====
    public void ChangeState(CharacterState nextId) => _state.ChangeState(nextId);

    protected virtual void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_visualCollider == null && _spriteRenderer != null)
            _visualCollider = _spriteRenderer.GetComponent<Collider2D>();
        if (_holdInput == null) _holdInput = GetComponentInChildren<HoldClickEvent>();

        _name = CharacterNames.Acquire();

        _state.Bind(this);
        _visual.Bind(this);
        _affinity.Bind(this);
        _scale.Bind(this);

        RegisterExtraStates(_state);

        _state.StateChanged += OnStateChanged;
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
    }

    // ===== Hook (자식 클래스 확장점) =====

    protected virtual void RegisterExtraStates(StateModule state) { }

    private void OnStateChanged(CharacterState state)
    {
        _visual.Play(state);
    }

    // ===== UnityEvent에서 호출 가능한 공개 메서드 =====

    /// <summary>캐릭터를 좌클릭한 순간: 친밀도 누적 + 쓰담 진입.
    /// <c>HoldClickEvent</c>의 On Press Start에서 호출한다.
    ///
    /// 호버가 아니라 클릭인 것이 핵심이다 — 마우스를 올려두기만 해서는 아무 일도 일어나지 않는다.
    /// 쓰담은 모션이 끝나면 스스로 Idle로 돌아가므로 짝이 되는 "종료" 메서드가 없다.</summary>
    public void OnPetInput()
    {
        _affinity.AddOnPet();
        RequestPet();
    }

    // ===== External triggers — StateModule에 위임 =====
    public void RequestSleep() => _state.RequestSleep();
    public void RequestWakeUp() => _state.RequestWakeUp();
    public void RequestFall() => _state.RequestFall();
    public void RequestPet() => _state.RequestPet();
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

        float desiredX = p.x + deltaX;
        // 경계는 발 한 점 기준이다 — 몸통 폭은 보지 않는다. 몸의 좌우 절반이 영역을 넘는 것은
        // 알고 있는 한계이지 버그가 아니다(character-ground.md).
        float allowedX = _hasLivingArea
            ? Mathf.Clamp(desiredX, _livingArea.xMin, _livingArea.xMax)
            : desiredX;

        p.x = allowedX;
        transform.position = p;

        return Mathf.Approximately(allowedX, desiredX);
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
            // MoveHorizontal과 같은 경계(발 한 점 기준)를 쓴다.
            worldPos.x = Mathf.Clamp(worldPos.x, _livingArea.xMin, _livingArea.xMax);
            worldPos.y = Mathf.Clamp(worldPos.y, _livingArea.yMin, _livingArea.yMax);
        }
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
    }

    /// <summary>
    /// 이 캐릭터가 머무를 수 있는 월드 영역을 지정한다. 걷기도 드래그도 이 밖으로 나가지 못하고,
    /// **이 영역의 아래 변이 곧 지면**이다.
    ///
    /// 캐릭터는 이 사각형이 무엇에서 왔는지 모른다 — 월드 좌표로 환산된 결과만 받는다.
    /// 뷰포트에서 환산해 걸어주는 것은 <c>Gameplay/Viewport/ViewportLivingAreaBinder</c>다.
    ///
    /// 지금 밖에 있으면 즉시 안으로 끌어들인다(뷰포트를 캐릭터 밑에서 줄인 경우).
    /// </summary>
    public void SetLivingArea(Rect worldArea)
    {
        _livingArea = worldArea;
        _hasLivingArea = true;

        // 가로만 여기서 넣는다.
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, _livingArea.xMin, _livingArea.xMax);
        transform.position = p;

        // 세로는 접지 규칙이 정한다 — 공중에 있는 캐릭터를 바닥으로 끌어내리면 안 되고, 그 판정은 상태가 안다.
        // 다음 Update까지 미루지 않고 여기서 부른다. 미루면 뷰포트가 바뀐 그 프레임 동안
        // 발이 지면에서 떨어진 채로 렌더된다.
        _state.EnforceFloor();
    }

    /// <summary>걸어갈 목적지를 뽑을 가로 범위. 거주 영역이 아직 안 들어왔으면 false.
    ///
    /// 거주 영역 <see cref="Rect"/>를 통째로 내주지 않는다 — 그 아래 변이 곧 지면 높이인데,
    /// 높이를 밖에 내주면 "발이 바닥에 있는가"를 호출자마다 다시 쓰게 되고 비교 방식이 갈라진다
    /// (<see cref="IsFootOnGround"/> 참고). 가로만 내주면 그 규약을 건드리지 않는다.</summary>
    public bool TryGetWalkRange(out float minX, out float maxX)
    {
        if (!_hasLivingArea)
        {
            minX = 0f;
            maxX = 0f;
            return false;
        }

        minX = _livingArea.xMin;
        maxX = _livingArea.xMax;
        return true;
    }

    /// <summary>지금 누르고 있는 좌클릭이 시작된 순간의 커서 월드 좌표. 누르는 중이 아니면 false.
    /// 잡기 오프셋의 기준점이다 — 자세한 이유는 <c>HoldClickEvent.TryGetPressWorld</c>.</summary>
    public bool TryGetPressAnchor(out Vector2 world)
    {
        if (_holdInput != null) return _holdInput.TryGetPressWorld(out world);

        world = Vector2.zero;
        return false;
    }

    public void SetFacing(float direction)
    {
        _visual.SetFacing(direction < 0f);
    }

    public CharacterForm CurrentForm => _visual.CurrentForm;
    public void SetForm(CharacterForm form) => _visual.SetForm(form);

    // ===== IStateOwner: Ground =====

    /// <summary>발이 지면에 닿았거나 그 아래로 파묻혔으면 true.
    /// 허용 오차가 없다 — 접지를 "추적하는 상태"가 아니라 매 프레임 강제되는 결과로 다루므로,
    /// 낙하가 지면을 지나친 프레임에 정확히 걸리는 것으로 충분하다.</summary>
    public bool IsFootOnGround() => transform.position.y <= FloorY;

    public void SnapToFloor()
    {
        var p = transform.position;
        float floor = FloorY;
        // 이미 붙어 있으면 transform을 건드리지 않는다. 정확성 장치가 아니라 매 프레임 쓰기를 아끼는
        // 최적화다 — 아래 대입이 정확한 값을 쓰므로 다음 프레임에는 반드시 걸린다.
        if (Mathf.Approximately(p.y, floor)) return;
        p.y = floor;
        transform.position = p;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var foot = WorldPosition;
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
