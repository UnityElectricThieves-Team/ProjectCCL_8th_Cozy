using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 장식 설치 모드. 장식 하나(고스트)를 커서를 따라 지면 위로 움직이며 놓을 수 있는지를 초록/빨강으로 보여주고,
/// 좌클릭으로 확정, 우클릭·ESC로 취소한다. 규약은 .claude/rules/unity/decoration-placement.md.
///
/// <b>상점을 모른다.</b> 무엇을 놓을지(프리팹)와 확정했을 때 할 일(콜백)을 <see cref="Enter"/>로 받는다.
/// 확정 콜백에는 놓을 자리의 가로 위치(베이스 공간 px)만 넘긴다 — 하트 차감·저장은 호출한 쪽의 일이다.
///
/// 모드 동안 두 가지를 건다. 둘 다 자신을 소유자로 걸고, <see cref="Exit"/>와 OnDisable에서 반드시 푼다.
/// - 창의 클릭 통과 정지: 투명한 곳에서도 클릭이 우리 창에 와야 그 자리에 놓을 수 있다.
/// - 월드 입력 잠금: 모드 중 캐릭터·별은 반응하지 않는다(기획 "다른 곳은 좌클릭해도 무반응").
/// 열린 패널을 잠시 숨기는 일은 이 컨트롤러가 하지 않는다. <see cref="ActiveChanged"/>를 듣는 UI 쪽이 맡는다.
///
/// 확정·취소는 <b>모드 안에서 누른 버튼을 뗄 때</b> 처리한다. 월드 폴러와 UI의 ESC 처리는 모두 "누른 순간"에만 반응하므로,
/// 누를 때 아직 잠겨 있으면 같은 프레임에 클릭이 캐릭터나 패널로 새어 나가지 않는다. 모드에 들어오게 한 클릭(상점 버튼)은
/// 모드 밖에서 눌렸으므로 그 뗌은 무시된다.
///
/// 판정은 콜라이더 없이 사각형 계산으로 한다. 장식에 콜라이더를 달지 않기 때문이다.
/// 씬에 하나만 둔다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DecorationPlacementController : MonoBehaviour
{
    public static DecorationPlacementController Instance { get; private set; }

    // 고스트의 Order in Layer. 캐릭터보다 앞(기획: 배치 중 장식은 캐릭터보다 앞, 안내 그림보다는 뒤 — 안내 그림은 UI라 늘 앞이다).
    private const int GHOST_SORTING_ORDER = 100;

    private static readonly Color VALID_COLOR = new Color(0f, 0x90 / 255f, 0f, 1f);   // #009000
    private static readonly Color INVALID_COLOR = new Color(1f, 0f, 0f, 1f);           // #FF0000
    private static readonly int OVERLAY_COLOR_ID = Shader.PropertyToID("_OverlayColor");

    [Tooltip("고스트에 쓰는 덧입히기 셰이더(Assets/Shaders/SpriteTintOverlay). 필드로 참조해야 빌드에 포함된다.")]
    [SerializeField] private Shader _ghostShader;

    // 직렬화 필드는 #if UNITY_EDITOR로 감싸지 않는다 — 에디터와 빌드의 직렬화 배치가 달라져 빌드에서 경고·오류가 난다.
    // 쓰는 메서드(TestEnter)만 에디터 전용이다.
    [Header("에디터 테스트")]
    [Tooltip("인스펙터 ⋮ 메뉴 'Test: Enter'로 들고 들어갈 장식 프리팹. 확정해도 저장하지 않고 로그만 남긴다.")]
    [SerializeField] private GameObject _testPrefab;

    /// <summary>설치 모드에 들어가거나(true) 나올 때(false) 울린다. 강제 종료(OnDisable) 때도 울린다.</summary>
    public event Action<bool> ActiveChanged;

    public bool IsActive { get; private set; }

    private ViewportScreenSettings _viewportSettings;
    private BaseSpaceCameraFitter _cameraFitter;
    private Camera _camera;
    private WindowManager _windowManager;
    private WindowsCursorToUnityScreen _cursorSource;
    private DecorationLayer _layer;

    private Material _ghostMaterial;
    private GameObject _ghost;
    private SpriteRenderer _ghostRenderer;
    private Action<float> _onConfirm;

    private Rect _area;                 // 확정 뷰포트의 월드 사각형(지면 = 아래 변)
    private float _ghostBaseX;          // 지금 고스트 자리의 가로 위치(베이스 공간 px)
    private bool _isValid;
    private bool _hasColor;             // 색을 한 번이라도 칠했는가(첫 프레임은 무조건 칠한다)

    private bool _leftPressedInMode;
    private bool _rightPressedInMode;
    private bool _escPressedInMode;

    private readonly List<Bounds> _obstacles = new();
    private readonly List<Rect> _blockedAreas = new();
    private readonly List<RaycastResult> _uiHits = new();
    private readonly Vector3[] _corners = new Vector3[4];
    private PointerEventData _pointerData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (_ghostShader != null) _ghostMaterial = new Material(_ghostShader);
    }

    private void Start()
    {
        // 프리팹은 씬 오브젝트를 인스펙터로 참조할 수 없고, 인스턴스에서 걸면 씬 override가 남는다.
        // 그래서 Start에서 한 번만 찾는다(BackgroundStrip·ViewportScreenSettings와 같은 방식).
        _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        _cameraFitter = FindFirstObjectByType<BaseSpaceCameraFitter>();
        _windowManager = FindFirstObjectByType<WindowManager>();
        _cursorSource = FindFirstObjectByType<WindowsCursorToUnityScreen>();
        _layer = FindFirstObjectByType<DecorationLayer>();
        if (_cameraFitter != null) _camera = _cameraFitter.GetComponent<Camera>();
    }

    private void OnDisable()
    {
        Exit();

        // 활성 여부와 상관없이 자기 것을 푼다. 남으면 창이 모든 클릭을 흡수하거나 캐릭터가 영영 반응하지 않는다.
        if (_windowManager != null) _windowManager.ReleaseClickThroughSuspend(this);
        WorldInputLock.Release(this);
    }

    private void OnDestroy()
    {
        if (_ghostMaterial != null) Destroy(_ghostMaterial);
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 설치 모드에 들어간다. 확정하면 onConfirm(놓을 자리의 가로 위치, 베이스 공간 px)을 부르고 모드를 나온다.
    /// 들어가지 못하면 경고만 남기고 false — 프리팹이 없거나, 이미 모드 중이거나, 뷰포트 편집 중이거나, 뷰포트 준비 전일 때.
    /// </summary>
    public bool Enter(GameObject prefab, Action<float> onConfirm)
    {
        if (prefab == null) return Refuse("장식 프리팹이 비어 있습니다");
        if (IsActive) return Refuse("이미 설치 모드입니다");
        if (_viewportSettings == null || _cameraFitter == null || _camera == null) return Refuse("뷰포트·카메라를 찾지 못했습니다");
        if (!_viewportSettings.IsReady) return Refuse("뷰포트가 아직 준비되지 않았습니다");
        if (_viewportSettings.IsEditing) return Refuse("뷰포트 편집 중에는 설치할 수 없습니다");
        if (_ghostMaterial == null) return Refuse("고스트 셰이더가 연결되지 않았습니다");

        _ghost = Instantiate(prefab, transform);
        _ghostRenderer = _ghost.GetComponentInChildren<SpriteRenderer>();
        if (_ghostRenderer == null || _ghostRenderer.sprite == null)
        {
            Destroy(_ghost);
            _ghost = null;
            return Refuse($"장식 프리팹 '{prefab.name}'에 그림이 없습니다");
        }
        _ghostRenderer.sharedMaterial = _ghostMaterial;
        _ghostRenderer.sortingOrder = GHOST_SORTING_ORDER;

        _onConfirm = onConfirm;
        _area = _cameraFitter.BaseRectToWorld(_viewportSettings.Viewport, _viewportSettings.BaseSpaceSize);
        CollectBlockedAreas();
        if (_layer != null) _layer.CollectBounds(_obstacles);
        else _obstacles.Clear();

        if (_windowManager != null) _windowManager.AcquireClickThroughSuspend(this);
        WorldInputLock.Acquire(this);

        _leftPressedInMode = _rightPressedInMode = _escPressedInMode = false;
        _hasColor = false;
        IsActive = true;
        UpdateGhost();

        ActiveChanged?.Invoke(true);
        return true;
    }

    /// <summary>설치 모드를 나온다. 확정·취소·강제 종료가 모두 이 한 곳을 지난다. 여러 번 불려도 한 번만 동작한다.</summary>
    public void Exit()
    {
        if (!IsActive) return;
        IsActive = false;

        if (_ghost != null) Destroy(_ghost);
        _ghost = null;
        _ghostRenderer = null;
        _onConfirm = null;

        if (_windowManager != null) _windowManager.ReleaseClickThroughSuspend(this);
        WorldInputLock.Release(this);

        ActiveChanged?.Invoke(false);
    }

    private void Update()
    {
        if (!IsActive) return;
        UpdateGhost();
        HandleInput();
    }

    private void HandleInput()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) _leftPressedInMode = true;
            if (mouse.rightButton.wasPressedThisFrame) _rightPressedInMode = true;
        }
        // 포커스가 없을 때의 ESC는 다른 앱의 것이다(UIManager와 같은 원칙).
        if (keyboard != null && Application.isFocused && keyboard.escapeKey.wasPressedThisFrame) _escPressedInMode = true;

        if (_rightPressedInMode && mouse != null && mouse.rightButton.wasReleasedThisFrame) { Exit(); return; }
        if (_escPressedInMode && keyboard != null && keyboard.escapeKey.wasReleasedThisFrame) { Exit(); return; }

        if (_leftPressedInMode && mouse != null && mouse.leftButton.wasReleasedThisFrame)
        {
            _leftPressedInMode = false;
            if (!_isValid) return; // 놓을 수 없는 자리에서는 무반응

            Action<float> confirm = _onConfirm;
            float baseX = _ghostBaseX;
            try
            {
                confirm?.Invoke(baseX);
            }
            finally
            {
                Exit();
            }
        }
    }

    // 고스트를 커서 가로 위치로 옮겨 지면에 앉히고, 놓을 수 있는지 판정해 색을 칠한다.
    private void UpdateGhost()
    {
        Vector2 screen = ReadCursorScreen();
        Vector3 cursorWorld = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));

        // 그림의 가로 중앙을 커서 x에, 아래 끝을 지면에 맞춘다. 피벗이 장식마다 달라 그림 기준으로 맞춘다.
        Bounds local = _ghostRenderer.sprite.bounds;
        Transform rt = _ghostRenderer.transform;
        Vector3 center = rt.TransformPoint(local.center);
        Vector3 bottom = rt.TransformPoint(new Vector3(local.center.x, local.min.y, 0f));
        Vector3 pos = _ghost.transform.position;
        pos.x += cursorWorld.x - center.x;
        pos.y += _area.yMin - bottom.y;
        _ghost.transform.position = pos;

        Bounds b = _ghostRenderer.bounds;
        _ghostBaseX = _cameraFitter.WorldXToBaseX(b.center.x, _viewportSettings.BaseSpaceSize);

        bool valid = IsInsideViewport(b) && !OverlapsAny(b) && !IsPointerOverUI(screen);
        if (!_hasColor || valid != _isValid)
        {
            _isValid = valid;
            _hasColor = true;
            _ghostMaterial.SetColor(OVERLAY_COLOR_ID, valid ? VALID_COLOR : INVALID_COLOR);
        }
    }

    // 뷰포트 안에 온전히 들어와야 한다. 저장할 때 걸친 장식은 인벤토리로 회수되는 기획과 맞추려는 것이다.
    private bool IsInsideViewport(Bounds b)
        => b.min.x >= _area.xMin && b.max.x <= _area.xMax && b.max.y <= _area.yMax;

    private bool OverlapsAny(Bounds b)
    {
        Rect r = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
        for (int i = 0; i < _obstacles.Count; i++)
        {
            Bounds o = _obstacles[i];
            if (r.Overlaps(new Rect(o.min.x, o.min.y, o.size.x, o.size.y))) return true;
        }
        for (int i = 0; i < _blockedAreas.Count; i++)
        {
            if (r.Overlaps(_blockedAreas[i])) return true;
        }
        return false;
    }

    // 금지 영역(우측 메뉴창 등)을 월드 사각형으로 바꿔 둔다. 창 배치가 바뀌었을 수 있으니 모드에 들어올 때마다 다시 한다.
    // UI 캔버스는 Screen Space Overlay라 GetWorldCorners가 곧 화면 px다.
    private void CollectBlockedAreas()
    {
        _blockedAreas.Clear();
        IReadOnlyList<RectTransform> blockers = PlacementBlocker.Active;
        for (int i = 0; i < blockers.Count; i++)
        {
            if (blockers[i] == null) continue;
            blockers[i].GetWorldCorners(_corners);
            Vector3 min = _camera.ScreenToWorldPoint(_corners[0]);
            Vector3 max = _camera.ScreenToWorldPoint(_corners[2]);
            _blockedAreas.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }
    }

    // 커서가 보이는 UI 위에 있으면 놓지 않는다 — 그 UI의 클릭과 설치가 동시에 일어나기 때문이다.
    // 인자 없는 IsPointerOverGameObject는 클릭 통과를 오가는 이 앱에서 낡은 값을 줄 수 있어, 좌표를 직접 넣어 레이캐스트한다(WindowManager와 같은 방식).
    private bool IsPointerOverUI(Vector2 screen)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        if (_pointerData == null) _pointerData = new PointerEventData(es);
        _pointerData.position = screen;

        _uiHits.Clear();
        es.RaycastAll(_pointerData, _uiHits);
        bool over = _uiHits.Count > 0;
        _uiHits.Clear();
        return over;
    }

    // 클릭 통과 중에는 Mouse.current 좌표가 멈출 수 있어 OS 커서 기반 좌표를 우선 쓴다.
    private Vector2 ReadCursorScreen()
    {
        if (_cursorSource != null) return _cursorSource.UnityScreenPosition;
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }

    private bool Refuse(string reason)
    {
        Debug.LogWarning($"[{nameof(DecorationPlacementController)}] 설치 모드에 들어가지 않습니다: {reason}", this);
        return false;
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Enter")]
    private void TestEnter()
    {
        Enter(_testPrefab, baseX => Debug.Log($"[{nameof(DecorationPlacementController)}] 테스트 확정: x = {baseX:F1} (베이스 px)", this));
    }
#endif
}
