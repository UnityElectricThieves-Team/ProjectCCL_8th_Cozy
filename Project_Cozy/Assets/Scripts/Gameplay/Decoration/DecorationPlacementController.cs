using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 장식 배치 모드(설치·이동·회수·제거). 규약은 .claude/rules/unity/decoration-placement.md.
///
/// 모드는 두 단계로 움직인다.
/// - <b>고르기</b>: 커서 아래의 놓인 장식을 붉게 강조하고, 좌클릭하면 그 장식을 대상으로 삼는다. 회수·제거·이동.
/// - <b>들고 다니기</b>: 장식 하나(고스트)를 커서를 따라 움직이며 놓을 수 있는지를 초록/빨강으로 보여주고, 좌클릭으로 놓는다. 설치·이동.
///   바닥 장식은 지면 위로만, 자유 장식(<see cref="DecorationPlacementKind.Anywhere"/>)은 메뉴창 빼고 어디든 움직인다 — 아래 끝은 지면에서 멈춘다.
/// 설치는 들고 다니기만 하고 한 번 놓으면 모드가 끝난다. 회수·제거는 고르기만 하고, 한 번 처리한 뒤에도 고르기 단계에 남는다.
/// 이동은 고르기 → 들고 다니기(원본은 감춤) → 놓으면 다시 고르기다. 인벤토리 장식도 고르기 단계에서 들어 올 수 있다.
/// 우클릭·ESC는 언제나 모드 전체를 끝낸다. 들고 있던 것은 그대로 남고, 이미 확정한 것은 되돌리지 않는다.
///
/// <b>상점을 모른다.</b> 무엇을 놓을지(프리팹·종류)와 확정했을 때 할 일(콜백)을 진입 메서드로 받는다.
/// 콜백에는 놓을 자리(<see cref="DecorationSpot"/>)나 고른 장식의 저장 목록 칸 번호만 넘긴다 — 하트·저장은 호출한 쪽의 일이다.
///
/// 모드 동안 두 가지를 건다. 둘 다 자신을 소유자로 걸고, <see cref="Exit"/>와 OnDisable에서 반드시 푼다.
/// - 창의 클릭 통과 정지: 투명한 곳에서도 클릭이 우리 창에 와야 그 자리에 놓거나 장식을 고를 수 있다.
/// - 월드 입력 잠금: 모드 중 캐릭터·구름은 반응하지 않는다(기획 "다른 곳은 좌클릭해도 무반응").
/// 열린 패널을 잠시 숨기는 일은 이 컨트롤러가 하지 않는다. <see cref="ModeChanged"/>를 듣는 UI 쪽이 맡는다.
/// 이벤트에 모드 값을 실어 보내므로, 듣는 쪽이 이벤트 전후로 <see cref="Mode"/>를 따로 읽는 순서에 기대지 않는다.
///
/// 확정·취소는 <b>모드 안에서 누른 버튼을 뗄 때</b> 처리한다. 월드 폴러와 UI의 ESC 처리는 모두 "누른 순간"에만 반응하므로,
/// 누를 때 아직 잠겨 있으면 같은 프레임에 클릭이 캐릭터나 패널로 새어 나가지 않는다. 모드에 들어오게 한 클릭(상점 버튼)은
/// 모드 밖에서 눌렸으므로 그 뗌은 무시된다. 커서가 보이는 UI 위에서 누른 클릭은 뗄 때 아무것도 하지 않는다 —
/// 패널 헤더를 누른 채 장식 위로 끌어가서 떼면 그 장식이 처리되는 일을 막는다. 고르기는 누른 대상과 뗀 대상이 같을 때만 한다.
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
    private static readonly Color HOVER_COLOR = new Color(0x64 / 255f, 0f, 0f, 1f);    // #640000
    private const float HOVER_AMOUNT = 0.3f;   // 기획: 호버한 대상에 #640000 + 투명도 30%
    private static readonly int OVERLAY_COLOR_ID = Shader.PropertyToID("_OverlayColor");
    private static readonly int OVERLAY_AMOUNT_ID = Shader.PropertyToID("_OverlayAmount");
    private static readonly int ALPHA_ID = Shader.PropertyToID("_Alpha");

    [Tooltip("고스트와 호버 강조에 쓰는 덧입히기 셰이더(Assets/Shaders/SpriteTintOverlay). 필드로 참조해야 빌드에 포함된다.")]
    [SerializeField] private Shader _ghostShader;

    // 직렬화 필드는 #if UNITY_EDITOR로 감싸지 않는다 — 에디터와 빌드의 직렬화 배치가 달라져 빌드에서 경고·오류가 난다.
    // 쓰는 메서드(TestEnter)만 에디터 전용이다.
    [Header("에디터 테스트")]
    [Tooltip("인스펙터 ⋮ 메뉴 'Test: Enter'로 들고 들어갈 장식 프리팹. 확정해도 저장하지 않고 로그만 남긴다.")]
    [SerializeField] private GameObject _testPrefab;

    /// <summary>모드에 들어가거나 나올 때(<see cref="DecorationMode.None"/>) 울린다. 강제 종료(OnDisable) 때도 울린다.</summary>
    public event Action<DecorationMode> ModeChanged;

    public DecorationMode Mode { get; private set; }

    public bool IsActive => Mode != DecorationMode.None;

    private ViewportScreenSettings _viewportSettings;
    private BaseSpaceCameraFitter _cameraFitter;
    private Camera _camera;
    private WindowManager _windowManager;
    private WindowsCursorToUnityScreen _cursorSource;
    private DecorationLayer _layer;

    // 들고 다니기
    private Material _ghostMaterial;
    private GameObject _ghost;
    private SpriteRenderer _ghostRenderer;
    private Action<DecorationSpot> _onConfirm;
    private Rect _area;                 // 적용된 뷰포트의 월드 사각형(지면 = 아래 변)
    private DecorationPlacementKind _carryKind; // 들고 있는 장식의 종류. 들고 있는 동안에만 뜻이 있고, StartCarry가 늘 새로 쓴다
    // 지금 고스트 자리. UpdateGhost가 매 프레임 통째로 새로 쓴다 — 바닥 장식이면 높이 0이 매번 다시 들어가,
    // 앞서 든 자유 장식의 높이가 남지 않는다.
    private DecorationSpot _ghostSpot;
    private bool _isValid;
    private bool _hasColor;             // 색을 한 번이라도 칠했는가(첫 프레임은 무조건 칠한다)

    // 고르기
    private Material _hoverMaterial;
    private Action<int> _onPick;
    private int _hoveredIndex = -1;     // 커서 아래 놓인 장식의 저장 목록 칸 번호
    private int _pressedIndex = -1;     // 좌클릭을 누른 순간의 대상

    private bool _cursorOverUi;         // 이번 프레임 커서가 보이는 UI 위인가
    private bool _leftPressedInMode;
    private bool _leftPressedOverUi;
    private bool _rightPressedInMode;
    private bool _escPressedInMode;
    private int _unpausedFrame = -1;    // 입력 정지를 푼 프레임. 이 프레임의 눌림은 기록하지 않는다

    private readonly List<Bounds> _obstacles = new();
    private readonly List<Rect> _blockedAreas = new();
    private readonly List<RaycastResult> _uiHits = new();
    private readonly Vector3[] _corners = new Vector3[4];
    private PointerEventData _pointerData;

    // 이동
    private Action<int, DecorationSpot> _onMovePlaced;
    private Action<DecorationSpot> _confirmMovePlaced;   // 들고 있는 놓인 장식을 확정할 때. Awake에서 한 번 만든다
    private int _carriedIndex = -1;             // 들고 있는 놓인 장식의 저장 목록 칸 번호(인벤토리 장식이면 -1)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _confirmMovePlaced = ConfirmMovePlaced;

        if (_ghostShader != null)
        {
            _ghostMaterial = new Material(_ghostShader);

            // 호버 강조는 같은 셰이더로 색만 다르게, 투명하지 않게 덧입힌다. SpriteRenderer.color는 곱하기라 덧입히기가 안 된다.
            // 만드는 것은 여기지만 렌더러에 씌우고 벗기는 것은 레이어다 — 렌더러 상태를 레이어 한 곳이 들게 하려는 것이다.
            _hoverMaterial = new Material(_ghostShader);
            _hoverMaterial.SetColor(OVERLAY_COLOR_ID, HOVER_COLOR);
            _hoverMaterial.SetFloat(OVERLAY_AMOUNT_ID, HOVER_AMOUNT);
            _hoverMaterial.SetFloat(ALPHA_ID, 1f);
        }
    }

    private void Start()
    {
        // 프리팹은 씬 오브젝트를 인스펙터로 참조할 수 없고, 인스턴스에서 걸면 씬 override가 남는다.
        // 그래서 Start에서 한 번만 찾는다(BackgroundStage·ViewportScreenSettings와 같은 방식).
        _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        _cameraFitter = FindFirstObjectByType<BaseSpaceCameraFitter>();
        _windowManager = FindFirstObjectByType<WindowManager>();
        _cursorSource = FindFirstObjectByType<WindowsCursorToUnityScreen>();
        _layer = FindFirstObjectByType<DecorationLayer>();
        if (_cameraFitter != null) _camera = _cameraFitter.GetComponent<Camera>();
        if (_layer != null) _layer.SetHighlightMaterial(_hoverMaterial);
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
        if (_hoverMaterial != null) Destroy(_hoverMaterial);
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 설치 모드에 들어간다. kind대로 움직이고, 확정하면 onConfirm(놓을 자리)을 부르고 모드를 나온다.
    /// 들어가지 못하면 경고만 남기고 false — 프리팹이 없거나, 이미 모드 중이거나, 뷰포트 편집 중이거나, 뷰포트 준비 전일 때.
    /// </summary>
    public bool EnterInstall(GameObject prefab, DecorationPlacementKind kind, Action<DecorationSpot> onConfirm)
    {
        if (prefab == null) return Refuse("장식 프리팹이 비어 있습니다");
        if (!CanEnter()) return false;
        if (!TryCreateGhost(prefab)) return false;

        Begin(DecorationMode.Install);
        StartCarry(kind, onConfirm);
        return true;
    }

    /// <summary>
    /// 회수 모드에 들어간다. 놓인 장식을 고르면 onRecall(저장 목록 칸 번호)을 부르고 고르기 단계에 남는다. 우클릭·ESC로 끝난다.
    /// </summary>
    public bool EnterRecall(Action<int> onRecall) => EnterPicking(DecorationMode.Recall, onRecall);

    /// <summary>
    /// 제거 모드에 들어간다. 놓인 장식을 고르면 onRemove(저장 목록 칸 번호)를 부르고 고르기 단계에 남는다. 우클릭·ESC로 끝난다.
    /// 인벤토리 장식의 제거는 이 컨트롤러를 거치지 않는다(UI가 슬롯 클릭을 직접 받는다).
    /// </summary>
    public bool EnterRemove(Action<int> onRemove) => EnterPicking(DecorationMode.Remove, onRemove);

    /// <summary>
    /// 이동 모드에 들어간다. 놓인 장식을 고르면 그 장식을 그 종류대로 들고 다니고(원본은 감춤), 새 자리에서 좌클릭하면
    /// onMovePlaced(저장 목록 칸 번호, 새 자리)를 부른 뒤 고르기 단계로 돌아간다.
    /// 들고 있는 중 우클릭·ESC면 모드가 끝나고 원본은 원래 자리에 그대로다. 인벤토리 장식은 <see cref="CarryFromInventory"/>로 든다.
    /// </summary>
    public bool EnterMove(Action<int, DecorationSpot> onMovePlaced)
    {
        if (!CanEnter()) return false;

        _onMovePlaced = onMovePlaced;
        Begin(DecorationMode.Move);
        return true;
    }

    /// <summary>
    /// 이동 모드의 고르기 단계에서 인벤토리 장식 하나를 kind대로 들고 다니기 시작한다. 놓으면 onConfirm(놓을 자리)을 부르고 고르기 단계로 돌아간다.
    /// 이동 모드가 아니거나, 이미 들고 있거나, 입력을 멈춘 중이거나, 그림이 없는 프리팹이면 false.
    /// </summary>
    public bool CarryFromInventory(GameObject prefab, DecorationPlacementKind kind, Action<DecorationSpot> onConfirm)
    {
        if (Mode != DecorationMode.Move || IsCarrying || IsInputPaused) return false;
        if (prefab == null) return Refuse("장식 프리팹이 비어 있습니다");
        if (!TryCreateGhost(prefab)) return false;

        StartCarry(kind, onConfirm);
        return true;
    }

    /// <summary>장식을 들고 다니기 시작하거나(true) 내려놓았을 때(false) 울린다. 인벤토리가 들고 다니는 동안 슬롯 입력을 막는 데 쓴다.</summary>
    public event Action<bool> CarryingChanged;

    /// <summary>지금 장식(고스트)을 들고 다니는 중인가.</summary>
    public bool IsCarrying => _ghost != null;

    /// <summary>
    /// 모드 입력을 잠시 멈추거나(true) 되살린다(false). 수량 팝업처럼 모드 위에 질문을 띄우는 쪽이 건다.
    /// 멈추는 순간 눌림 기록을 비우고, 되살린 그 프레임에는 새 눌림을 기록하지 않는다 — 팝업을 ESC로 닫은 그 ESC가
    /// 같은 프레임에 모드의 눌림으로도 잡혀 모드까지 끝나는 일을 막는다(두 스크립트의 실행 순서에 따라 생긴다).
    /// 거는 곳이 하나뿐이라 소유자를 따로 두지 않는다. <see cref="Exit"/>가 풀어 준다.
    /// </summary>
    public void SetInputPaused(bool paused)
    {
        if (IsInputPaused == paused) return;
        IsInputPaused = paused;

        if (paused)
        {
            ClearPresses();
            _hoveredIndex = -1;
            if (_layer != null) _layer.SetHighlighted(-1);
        }
        else
        {
            _unpausedFrame = Time.frameCount;
        }
    }

    public bool IsInputPaused { get; private set; }

    /// <summary>모드를 나온다. 취소·설치 확정·강제 종료가 모두 이 한 곳을 지난다. 여러 번 불려도 한 번만 동작한다.</summary>
    public void Exit()
    {
        if (!IsActive) return;

        bool wasCarrying = IsCarrying;
        DestroyGhost();
        _onConfirm = null;
        _onPick = null;
        _onMovePlaced = null;
        _carriedIndex = -1;
        _hoveredIndex = _pressedIndex = -1;
        IsInputPaused = false;

        // 씬을 내릴 때는 레이어가 먼저 파괴됐을 수 있다(Unity null 검사).
        // 들고 있던 놓인 장식은 숨김을 풀면 원래 자리에 그대로 보인다 — 저장은 확정할 때만 일어나므로 되돌릴 것이 없다.
        if (_layer != null)
        {
            _layer.SetHighlighted(-1);
            _layer.SetHidden(-1);
            _layer.SetBroughtForward(false);
        }

        if (_windowManager != null) _windowManager.ReleaseClickThroughSuspend(this);
        WorldInputLock.Release(this);

        Mode = DecorationMode.None;
        if (wasCarrying) CarryingChanged?.Invoke(false);
        ModeChanged?.Invoke(DecorationMode.None);
    }

    private bool EnterPicking(DecorationMode mode, Action<int> onPick)
    {
        if (!CanEnter()) return false;

        _onPick = onPick;
        Begin(mode);
        return true;
    }

    private bool CanEnter()
    {
        if (IsActive) return Refuse("이미 배치 모드입니다");
        if (_viewportSettings == null || _cameraFitter == null || _camera == null) return Refuse("뷰포트·카메라를 찾지 못했습니다");
        if (!_viewportSettings.IsReady) return Refuse("뷰포트가 아직 준비되지 않았습니다");
        if (_viewportSettings.IsEditing) return Refuse("뷰포트 편집 중에는 배치 모드에 들어갈 수 없습니다");
        if (_ghostMaterial == null) return Refuse("덧입히기 셰이더가 연결되지 않았습니다");
        return true;
    }

    // 모든 모드가 공통으로 거는 것. 설치를 뺀 모드는 놓인 장식을 캐릭터 앞으로 당긴다(기획 이동/회수/제거 모드).
    private void Begin(DecorationMode mode)
    {
        if (_windowManager != null) _windowManager.AcquireClickThroughSuspend(this);
        WorldInputLock.Acquire(this);

        ClearPresses();
        _hoveredIndex = _pressedIndex = -1;
        if (mode != DecorationMode.Install && _layer != null) _layer.SetBroughtForward(true);

        Mode = mode;
        ModeChanged?.Invoke(mode);
    }

    private void Update()
    {
        if (!IsActive || IsInputPaused) return;

        if (IsCarrying) UpdateGhost();
        else UpdatePicking();

        HandleInput();
    }

    private void HandleInput()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool record = Time.frameCount != _unpausedFrame;

        if (mouse != null && record)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _leftPressedInMode = true;
                _leftPressedOverUi = _cursorOverUi;
                _pressedIndex = IsCarrying ? -1 : _hoveredIndex;
            }
            if (mouse.rightButton.wasPressedThisFrame) _rightPressedInMode = true;
        }
        // 포커스가 없을 때의 ESC는 다른 앱의 것이다(UIManager와 같은 원칙).
        if (record && keyboard != null && Application.isFocused && keyboard.escapeKey.wasPressedThisFrame) _escPressedInMode = true;

        if (_rightPressedInMode && mouse != null && mouse.rightButton.wasReleasedThisFrame) { Exit(); return; }
        if (_escPressedInMode && keyboard != null && keyboard.escapeKey.wasReleasedThisFrame) { Exit(); return; }

        if (_leftPressedInMode && mouse != null && mouse.leftButton.wasReleasedThisFrame)
        {
            _leftPressedInMode = false;
            if (_leftPressedOverUi) return; // UI 위에서 누른 클릭은 그 UI의 것이다

            if (IsCarrying) ConfirmCarry();
            else Pick();
        }
    }

    // ── 고르기 ─────────────────────────────────────────────

    private void UpdatePicking()
    {
        Vector2 screen = ReadCursorScreen();
        _cursorOverUi = IsPointerOverUI(screen);

        int index = -1;
        if (!_cursorOverUi && _layer != null)
        {
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            _layer.TryFindAt(world, out index);
        }
        _hoveredIndex = index;
        if (_layer != null) _layer.SetHighlighted(index);
    }

    // 누른 대상과 뗀 대상이 같을 때만 처리한다. 다르거나 빈 곳이면 무반응(기획 "다른 곳은 좌클릭해도 무반응").
    private void Pick()
    {
        int index = _pressedIndex;
        _pressedIndex = -1;
        if (index < 0 || index != _hoveredIndex) return;

        if (Mode == DecorationMode.Move) StartMovePlaced(index);
        else _onPick?.Invoke(index);
    }

    // 고른 놓인 장식을 들고 다니기 시작한다. 원본은 감추고 같은 프리팹·같은 종류로 고스트를 만든다.
    // 감춘 원본은 겹침 판정에서도 빠져, 원래 자리 근처에 다시 놓을 수 있다.
    private void StartMovePlaced(int placedIndex)
    {
        if (_layer == null || !_layer.TryGetPlaced(placedIndex, out PlacedDecoration placed)) return;
        if (!TryCreateGhost(placed.Prefab)) return;

        _layer.SetHighlighted(-1); // 감추기 전에 강조를 벗긴다 — 감춘 렌더러에 강조 머티리얼이 남지 않게
        _layer.SetHidden(placedIndex);
        _carriedIndex = placedIndex;
        StartCarry(placed.Kind, _confirmMovePlaced);
    }

    private void ConfirmMovePlaced(DecorationSpot spot)
    {
        if (_carriedIndex >= 0) _onMovePlaced?.Invoke(_carriedIndex, spot);
    }

    // ── 들고 다니기 ────────────────────────────────────────

    // 고스트를 만든다. 그림이 없는 프리팹이면 만들지 않고 false.
    private bool TryCreateGhost(GameObject prefab)
    {
        _ghost = Instantiate(prefab, transform);
        _ghostRenderer = _ghost.GetComponentInChildren<SpriteRenderer>();
        if (_ghostRenderer == null || _ghostRenderer.sprite == null)
        {
            DestroyGhost();
            return Refuse($"장식 프리팹 '{prefab.name}'에 그림이 없습니다");
        }
        _ghostRenderer.sharedMaterial = _ghostMaterial;
        _ghostRenderer.sortingOrder = GHOST_SORTING_ORDER;
        return true;
    }

    // 만들어 둔 고스트를 들고 다니기 시작한다. 진입로 셋(설치·인벤토리 꺼내 놓기·놓인 장식 집기)이 모두 여기서 종류를 넘긴다 —
    // 종류를 받는 곳이 하나라야 진입로가 늘어도 빠뜨리지 않는다.
    // 판정에 쓸 뷰포트·금지 영역·다른 장식은 들기 시작할 때마다 다시 모은다. 이동 모드는 ESC까지 남아 여러 번 들고 놓으므로,
    // 그 사이 바뀐 지면·장애물을 써야 한다(자유 장식의 높이도 그때의 지면 기준이다). 한 번만 모으도록 줄이지 않는다.
    private void StartCarry(DecorationPlacementKind kind, Action<DecorationSpot> onConfirm)
    {
        _onConfirm = onConfirm;
        _carryKind = kind;
        _area = _cameraFitter.BaseRectToWorld(_viewportSettings.Viewport, _viewportSettings.BaseSpaceSize);
        CollectBlockedAreas();
        if (_layer != null)
        {
            _layer.SetHighlighted(-1);
            _layer.CollectBounds(_obstacles);
        }
        else _obstacles.Clear();

        // 들기 시작하게 한 클릭(놓인 장식을 고른 클릭, 인벤토리 슬롯 클릭)의 뗌으로 바로 놓이지 않게 눌림 기록을 비운다.
        ClearPresses();
        _hasColor = false;
        UpdateGhost();
        CarryingChanged?.Invoke(true);
    }

    private void ConfirmCarry()
    {
        if (!_isValid) return; // 놓을 수 없는 자리에서는 무반응

        Action<DecorationSpot> confirm = _onConfirm;
        DecorationSpot spot = _ghostSpot;
        try
        {
            confirm?.Invoke(spot);
        }
        finally
        {
            // 설치는 한 번 놓으면 끝난다. 이동은 내려놓고 다시 고르기 단계로 돌아간다.
            if (Mode == DecorationMode.Install) Exit();
            else EndCarry();
        }
    }

    // 고스트를 치우고 감춘 원본을 되살린다. 확정 콜백이 이미 목록을 바꿨으면 레이어가 다시 그려 숨김도 비어 있다.
    private void EndCarry()
    {
        if (!IsCarrying) return;

        DestroyGhost();
        _onConfirm = null;
        _carriedIndex = -1;
        if (_layer != null) _layer.SetHidden(-1);
        ClearPresses();
        CarryingChanged?.Invoke(false);
    }

    private void DestroyGhost()
    {
        if (_ghost != null) Destroy(_ghost);
        _ghost = null;
        _ghostRenderer = null;
    }

    // 고스트를 커서로 옮기고, 놓을 수 있는지 판정해 색을 칠한다.
    // 바닥 장식은 가로만 커서를 따르고 아래 끝은 지면에 붙는다. 자유 장식은 그림 가운데가 커서를 따르되,
    // 아래 끝은 지면 밑으로 내려가지 않는다(커서를 바닥 쪽으로 내리면 바닥에 선 채로 따라온다). 위·옆으로는 막지 않고 빨강으로 보여 준다.
    private void UpdateGhost()
    {
        Vector2 screen = ReadCursorScreen();
        Vector3 cursorWorld = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));

        // 피벗이 장식마다 달라 transform이 아니라 그림 기준으로 맞춘다. 목표 아래 끝은 옮기기 전 값으로 구한다.
        Bounds local = _ghostRenderer.sprite.bounds;
        Transform rt = _ghostRenderer.transform;
        Vector3 center = rt.TransformPoint(local.center);
        Vector3 bottom = rt.TransformPoint(new Vector3(local.center.x, local.min.y, 0f));
        float targetBottomY = _carryKind == DecorationPlacementKind.Floor
            ? _area.yMin
            : Mathf.Max(_area.yMin, bottom.y + (cursorWorld.y - center.y));
        Vector3 pos = _ghost.transform.position;
        pos.x += cursorWorld.x - center.x;
        pos.y += targetBottomY - bottom.y;
        _ghost.transform.position = pos;

        // 높이는 길이라 PPU로만 곱한다. 0 아래로 자르는 것은 실수 오차로 −1e-5 같은 값이 파일에 남지 않게 하려는 것이다.
        Bounds b = _ghostRenderer.bounds;
        float elevationPx = _carryKind == DecorationPlacementKind.Floor
            ? 0f
            : Mathf.Max(0f, (targetBottomY - _area.yMin) * _cameraFitter.PixelsPerUnit);
        _ghostSpot = new DecorationSpot(_cameraFitter.WorldXToBaseX(b.center.x, _viewportSettings.BaseSpaceSize), elevationPx);

        _cursorOverUi = IsPointerOverUI(screen);
        bool valid = IsInsideViewport(b) && !OverlapsAny(b) && !_cursorOverUi;
        if (!_hasColor || valid != _isValid)
        {
            _isValid = valid;
            _hasColor = true;
            _ghostMaterial.SetColor(OVERLAY_COLOR_ID, valid ? VALID_COLOR : INVALID_COLOR);
        }
    }

    // 뷰포트 안에 온전히 들어와야 한다. 저장할 때 걸친 장식은 인벤토리로 회수되는 기획과 맞추려는 것이다.
    // 아래 변은 보지 않는다 — 고스트가 지면에서 멈추게 막아 늘 참이고, 넣으면 실수 오차로 바닥 장식이 빨강이 될 수 있다.
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

    // 금지 영역(우측 메뉴창 등)을 월드 사각형으로 바꿔 둔다. 창 배치가 바뀌었을 수 있으니 들기 시작할 때마다 다시 한다.
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

    // ── 공통 ───────────────────────────────────────────────

    private void ClearPresses()
    {
        _leftPressedInMode = _leftPressedOverUi = _rightPressedInMode = _escPressedInMode = false;
        _pressedIndex = -1;
    }

    // 커서가 보이는 UI 위인가. 그 UI의 클릭과 모드의 동작이 동시에 일어나지 않게 한다.
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
        Debug.LogWarning($"[{nameof(DecorationPlacementController)}] 배치 모드에 들어가지 않습니다: {reason}", this);
        return false;
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Enter")]
    private void TestEnter()
    {
        EnterInstall(_testPrefab, DecorationPlacementKind.Floor,
            spot => Debug.Log($"[{nameof(DecorationPlacementController)}] 테스트 확정: x = {spot.BaseX:F1}, 높이 = {spot.ElevationPx:F1} (베이스 px)", this));
    }
#endif
}
