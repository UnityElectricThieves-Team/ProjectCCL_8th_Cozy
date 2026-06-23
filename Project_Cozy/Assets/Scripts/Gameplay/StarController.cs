using UnityEngine;

/// <summary>
/// 별(스폰 포인트)의 중앙 컨트롤러 — <see cref="BaseCharacterController"/>에 대응하는 별 버전.
///
/// - 진행도(<see cref="InputCounter.Count"/>, 스폰 기운)가 <see cref="_threshold"/> 이상인 동안 Activated,
///   아니면 Idle인 2상태 머신. 상태가 바뀔 때만 Animator의 Int 파라미터(<see cref="StateParameter"/>)를 갱신한다
///   (Character의 VisualModule과 같은 방식이되 상태 클래스 없이 단순화).
/// - 클릭(드래그가 아니었을 때) 시 캐릭터 1개 스폰을 요청한다. 생성·동시존재 캡 판정은 <see cref="CharacterManager"/>에 위임 —
///   캡에 막혀 생성에 실패하면 기운을 차감하지 않는다.
///
/// 같은 GameObject에 <see cref="Collider2D"/>가 있어야 <see cref="InputInteractionManager"/>가 클릭을 라우팅한다.
/// 같은 GameObject에 <see cref="DraggableObject2D"/>가 있으면 드래그가 아니었을 때(mouse up 시점)에만 스폰한다.
/// </summary>
public sealed class StarController : MonoBehaviour, IClickable
{
    private enum StarState { Idle = 0, Activated = 1 }

    [Header("Refs")]
    [Tooltip("Visual 자식의 Animator. Idle/Activated 상태를 가진 StarAnimation 컨트롤러를 연결.")]
    [SerializeField] private Animator _animator;
    [Tooltip("진행도(스폰 기운) 소스. 비우면 Awake에서 같은 GameObject에서 탐색.")]
    [SerializeField] private InputCounter _counter;

    [Header("Spawn")]
    [Tooltip("기운이 이 값 이상이어야 Activated가 되고 클릭 스폰이 가능하다. 클릭 1회 스폰마다 이만큼 차감.")]
    [SerializeField, Min(1)] private int _threshold = 100;
    [Tooltip("클릭마다 1개 생성할 캐릭터 프리팹.")]
    [SerializeField] private GameObject _characterPrefab;
    [Tooltip("스폰 시 부모 Transform (선택). 비우면 Hierarchy 루트에 스폰.")]
    [SerializeField] private Transform _spawnParent;
    [Tooltip("스폰 위치 = 별 위치 + Random(Min..Max). Y는 위쪽(+)이 자연 낙하에 적합.")]
    [SerializeField] private Vector2 _spawnOffsetMin = new Vector2(-0.5f, 1f);
    [SerializeField] private Vector2 _spawnOffsetMax = new Vector2(0.5f, 2f);

    // Animator 파라미터 이름 — StarAnimation 컨트롤러의 Int 파라미터 이름과 글자까지 일치해야 한다.
    private const string StateParameter = "StarState";

    private int _stateHash;
    private StarState _current;
    private DraggableObject2D _draggable;
    private bool _spawnPending;

    private void Awake()
    {
        if (_counter == null) _counter = GetComponent<InputCounter>();
        _stateHash = Animator.StringToHash(StateParameter);
        _draggable = GetComponent<DraggableObject2D>();
    }

    private void Start()
    {
        // 시작 상태를 명시적으로 적용 — 기운 0이면 Idle.
        ApplyState(IsReady() ? StarState.Activated : StarState.Idle);
    }

    private void OnEnable()
    {
        if (_draggable != null) _draggable.PressEnded += OnPressEnded;
    }

    private void OnDisable()
    {
        if (_draggable != null) _draggable.PressEnded -= OnPressEnded;
    }

    private void Update()
    {
        if (_counter == null) return;
        var desired = IsReady() ? StarState.Activated : StarState.Idle;
        if (desired != _current) ApplyState(desired);
    }

    // 스폰 가능(=Activated) 여부. Count는 스폰으로 차감되므로 임계값 아래로 내려가면 다시 Idle.
    private bool IsReady() => _counter != null && _counter.Count >= _threshold;

    /// <summary>현재 Activated(스폰 가능) 상태인가. 디버그 표시 등 외부 노출용.</summary>
    public bool IsActivated => IsReady();

    // ===== 클릭 → 스폰 =====

    public void OnClick()
    {
        // 드래그 컴포넌트가 있으면 드래그/클릭 구분이 끝나는 mouse up(OnPressEnded)에서 스폰.
        if (_draggable != null)
        {
            _spawnPending = true;
            return;
        }
        RequestSpawn();
    }

    private void OnPressEnded(bool wasDrag)
    {
        if (!_spawnPending) return;
        _spawnPending = false;
        if (wasDrag) return;
        RequestSpawn();
    }

    /// <summary>기운이 임계값 이상이고 캡에 여유가 있으면 캐릭터 1개를 스폰하고 기운을 차감한다.</summary>
    private void RequestSpawn()
    {
        if (_characterPrefab == null || CharacterManager.Instance == null) return;
        if (!IsReady()) return;

        var offset = new Vector3(
            Random.Range(_spawnOffsetMin.x, _spawnOffsetMax.x),
            Random.Range(_spawnOffsetMin.y, _spawnOffsetMax.y),
            0f);

        // 생성·캡 판정은 CharacterManager에 위임. null이면 캡 도달이라 기운 차감 없음.
        var instance = CharacterManager.Instance.Spawn(_characterPrefab, transform.position + offset, _spawnParent);
        if (instance != null) _counter.ReduceSpawnEnergy(_threshold);
    }

    private void ApplyState(StarState next)
    {
        _current = next;
        if (_animator != null) _animator.SetInteger(_stateHash, (int)next);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<Collider2D>() == null)
            Debug.LogWarning($"[{nameof(StarController)}] '{name}' needs a Collider2D for clicks to register.", this);
        if (_animator == null)
            Debug.LogWarning($"[{nameof(StarController)}] '{name}' has no Animator — Idle/Activated won't switch.", this);
    }
#endif
}
