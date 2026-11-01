using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 구름(스폰 포인트)의 중앙 컨트롤러 — <see cref="BaseCharacterController"/>에 대응하는 구름 버전.
///
/// - 누적 스폰 기운(<see cref="SpawnPointManager.CumulativeEnergy"/>)이 <see cref="_threshold"/> 이상이면 Activated,
///   아니면 Idle인 2상태 머신. 상태가 바뀔 때만 Animator의 Int 파라미터(<see cref="StateParameter"/>)를 갱신한다
///   (Character의 VisualModule과 같은 방식이되 상태 클래스 없이 단순화).
/// - 클릭(드래그가 아니었을 때) 시 캐릭터 1개 스폰을 요청한다. 생성·동시존재 캡 판정은 <see cref="CharacterManager"/>에 위임한다.
///   스폰해도 기운은 차감하지 않는다.
/// - 지금 Cloud.prefab은 <see cref="_characterPrefab"/>을 비워 두어 캐릭터를 소환하지 않는다. 이유는
///   .claude/rules/unity/character-ownership.md의 구름 항목.
///
/// 같은 GameObject에 <see cref="Collider2D"/>가 있어야 <see cref="InputInteractionManager"/>가 클릭을 라우팅한다.
/// 같은 GameObject에 <see cref="DraggableObject2D"/>가 있으면 드래그가 아니었을 때(mouse up 시점)에만 스폰한다.
///
/// 구름은 뷰포트 안에서만 산다. 확정 뷰포트(<see cref="ViewportScreenSettings.ViewportConfirmed"/>)를 받을 때마다
/// 그림이 온전히 들어가는 범위를 다시 구해, 드래그는 그 경계에서 멈추게 하고, 이미 밖에 있으면 안쪽 랜덤 위치로 옮긴다.
/// 기획이 "구름 스폰: 새 화면 안에 랜덤 재배치"로 정했다. 편집 중 프리뷰에는 따라가지 않는다(캐릭터·장식과 같은 판단).
/// </summary>
public sealed class CloudController : MonoBehaviour, IClickable
{
    private enum CloudState { Idle = 0, Activated = 1 }

    [Header("Refs")]
    [Tooltip("Visual 자식의 Animator. Idle/Activated 상태를 가진 BaseCloudAnimatorController를 연결.")]
    [SerializeField] private Animator _animator;
    [Tooltip("스폰 기운 소스(SpawnPointManager). 비우면 Awake에서 같은 GameObject에서 탐색.")]
    [SerializeField, FormerlySerializedAs("_counter")] private SpawnPointManager _spawnPoint;

    [Header("Spawn")]
    [Tooltip("누적 기운이 이 값 이상이어야 Activated가 되고 클릭 스폰이 가능하다.")]
    [SerializeField, Min(1)] private int _threshold = 100;
    [Tooltip("클릭마다 1개 생성할 캐릭터 프리팹.")]
    [SerializeField] private GameObject _characterPrefab;
    [Tooltip("스폰 시 부모 Transform (선택). 비우면 Hierarchy 루트에 스폰.")]
    [SerializeField] private Transform _spawnParent;
    [Tooltip("스폰 위치 = 구름 위치 + Random(Min..Max). Y는 위쪽(+)이 자연 낙하에 적합.")]
    [SerializeField] private Vector2 _spawnOffsetMin = new Vector2(-0.24f, 0.48f);
    [SerializeField] private Vector2 _spawnOffsetMax = new Vector2(0.24f, 0.96f);

    // Animator 파라미터 이름 — BaseCloudAnimatorController의 Int 파라미터 이름과 글자까지 일치해야 한다.
    private const string StateParameter = "CloudState";

    private int _stateHash;
    private CloudState _current;
    private DraggableObject2D _draggable;
    private bool _spawnPending;

    private SpriteRenderer _renderer;               // 뷰포트 안에 넣을 그림(Visual 자식)
    private ViewportScreenSettings _viewportSettings;
    private BaseSpaceCameraFitter _cameraFitter;

    private void Awake()
    {
        if (_spawnPoint == null) _spawnPoint = GetComponent<SpawnPointManager>();
        _stateHash = Animator.StringToHash(StateParameter);
        _draggable = GetComponent<DraggableObject2D>();
        _renderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        // 시작 상태를 명시적으로 적용 — 기운 0이면 Idle.
        ApplyState(IsReady() ? CloudState.Activated : CloudState.Idle);

        _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        _cameraFitter = FindFirstObjectByType<BaseSpaceCameraFitter>();
        if (_viewportSettings == null || _cameraFitter == null || _renderer == null)
        {
            Debug.LogWarning($"[{nameof(CloudController)}] ViewportScreenSettings/BaseSpaceCameraFitter/SpriteRenderer 없음 — 구름이 뷰포트 밖에 놓일 수 있습니다.", this);
            return;
        }
        _viewportSettings.ViewportConfirmed += OnViewportConfirmed;

        // 첫 확정 신호가 이미 지나갔을 수 있다(viewport-coordinates.md의 "뷰포트가 처음 확정되기 전에는").
        if (_viewportSettings.IsReady) OnViewportConfirmed(_viewportSettings.Viewport);
    }

    private void OnDestroy()
    {
        if (_viewportSettings != null) _viewportSettings.ViewportConfirmed -= OnViewportConfirmed;
    }

    // ===== 뷰포트 안에 머물기 =====

    private void OnViewportConfirmed(RectInt viewportPx)
    {
        Rect area = _cameraFitter.BaseRectToWorld(viewportPx, _viewportSettings.BaseSpaceSize);
        Rect allowed = RootAreaKeepingSpriteInside(area);

        Vector3 p = transform.position;
        if (!allowed.Contains(p))
        {
            p.x = Random.Range(allowed.xMin, allowed.xMax);
            p.y = Random.Range(allowed.yMin, allowed.yMax);
            transform.position = p;
        }

        if (_draggable != null) _draggable.SetBounds(allowed);
    }

    // 그림이 area 안에 온전히 들어가려면 루트(transform.position)가 어디까지 갈 수 있는가.
    // 루트와 그림 가장자리 사이 거리만큼 area를 안쪽으로 줄인다. 그림이 area보다 크면 가운데에 묶는다.
    private Rect RootAreaKeepingSpriteInside(Rect area)
    {
        Bounds b = _renderer.bounds;
        Vector3 p = transform.position;

        float xMin = area.xMin + (p.x - b.min.x);
        float xMax = area.xMax - (b.max.x - p.x);
        float yMin = area.yMin + (p.y - b.min.y);
        float yMax = area.yMax - (b.max.y - p.y);
        if (xMin > xMax) xMin = xMax = (xMin + xMax) * 0.5f;
        if (yMin > yMax) yMin = yMax = (yMin + yMax) * 0.5f;

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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
        if (_spawnPoint == null) return;
        var desired = IsReady() ? CloudState.Activated : CloudState.Idle;
        if (desired != _current) ApplyState(desired);
    }

    // 스폰 가능(=Activated) 여부. 누적값은 줄지 않으므로 한 번 넘으면 계속 Activated.
    private bool IsReady() => _spawnPoint != null && _spawnPoint.CumulativeEnergy >= _threshold;

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

    /// <summary>누적 기운이 임계값 이상이고 캡에 여유가 있으면 캐릭터 1개를 스폰한다.</summary>
    private void RequestSpawn()
    {
        if (_characterPrefab == null || CharacterManager.Instance == null) return;
        if (!IsReady()) return;

        var offset = new Vector3(
            Random.Range(_spawnOffsetMin.x, _spawnOffsetMax.x),
            Random.Range(_spawnOffsetMin.y, _spawnOffsetMax.y),
            0f);

        // 생성·캡 판정은 CharacterManager에 위임. 캡에 닿으면 생성하지 않는다.
        CharacterManager.Instance.Spawn(_characterPrefab, transform.position + offset, _spawnParent);
    }

    private void ApplyState(CloudState next)
    {
        _current = next;
        if (_animator != null) _animator.SetInteger(_stateHash, (int)next);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<Collider2D>() == null)
            Debug.LogWarning($"[{nameof(CloudController)}] '{name}' needs a Collider2D for clicks to register.", this);
        if (_animator == null)
            Debug.LogWarning($"[{nameof(CloudController)}] '{name}' has no Animator — Idle/Activated won't switch.", this);
    }
#endif
}
