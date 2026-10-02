using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 장식 설치 모드의 안내. 모드 동안 마우스 커서를 전용 그림으로 바꾸고, 커서 옆에 "설치(왼쪽)·취소(오른쪽)" 글자를 붙여 다닌다.
/// 기본과 다른 커서가 "지금은 설치 모드"라는 신호다. 모드가 끝나면 기본 커서로 되돌린다.
///
/// <see cref="DecorationPlacementController.ActiveChanged"/>를 듣고 켜고 끈다 — 모드가 이 컴포넌트를 부르지 않는다.
/// 글자는 프리팹 안의 <see cref="LocalizedText"/>가 채운다(<c>UIPlacement.guide.*</c>).
///
/// 이 UI는 클릭을 받지 않는다. 받으면 설치 모드의 "커서가 UI 위면 놓을 수 없음" 판정에 자기 자신이 걸려
/// 늘 빨강이 된다. 그래서 CanvasGroup의 blocksRaycasts를 끄고, 안의 글자도 Raycast Target을 끈다.
///
/// 커서 그림은 텍스처 임포트 설정을 Cursor로 둔다. 다른 타입이면 Unity가 하드웨어 커서로 쓰지 못한다.
/// 범용 UI 위젯 폴더(Prefabs/UIPanels/UIObjects)의 프리팹 루트에 붙는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class PlacementGuide : MonoBehaviour
{
    [Tooltip("설치 모드 동안 쓸 커서 그림. 임포트 설정 Texture Type = Cursor.")]
    [SerializeField] private Texture2D _cursorTexture;

    [Tooltip("커서 그림에서 클릭 지점(px, 왼쪽 위 원점). 화살표 끝.")]
    [SerializeField] private Vector2 _cursorHotspot;

    [Tooltip("커서 위치에서 글자 묶음까지의 거리(마스터 캔버스 기준 px, 오른쪽 +, 위 +).")]
    [SerializeField] private Vector2 _labelOffset = new Vector2(40f, -40f);

    private CanvasGroup _group;
    private RectTransform _rect;
    private Canvas _canvas;
    private DecorationPlacementController _placement;
    private WindowsCursorToUnityScreen _cursorSource;
    private bool _isShown;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _rect = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
        Show(false);
    }

    private void Start()
    {
        _cursorSource = FindFirstObjectByType<WindowsCursorToUnityScreen>();
        _placement = DecorationPlacementController.Instance;
        if (_placement == null)
        {
            Debug.LogWarning($"[{nameof(PlacementGuide)}] 씬에 {nameof(DecorationPlacementController)}가 없어 안내를 띄우지 않습니다.", this);
            enabled = false;
            return;
        }
        _placement.ActiveChanged += Show;
        if (_placement.IsActive) Show(true);
    }

    private void OnDestroy()
    {
        if (_placement != null) _placement.ActiveChanged -= Show;
        if (_isShown) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // 모드 중 파괴돼도 커서를 되돌린다
    }

    private void Show(bool show)
    {
        _isShown = show;
        _group.alpha = show ? 1f : 0f;

        if (show) Cursor.SetCursor(_cursorTexture, _cursorHotspot, CursorMode.Auto);
        else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        if (show) FollowCursor();
    }

    // 설치 모드 컨트롤러가 Update에서 고스트를 옮긴 뒤에 따라간다.
    private void LateUpdate()
    {
        if (_isShown) FollowCursor();
    }

    private void FollowCursor()
    {
        Vector2 screen = _cursorSource != null
            ? _cursorSource.UnityScreenPosition
            : (Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);

        // UI 캔버스는 Screen Space Overlay라 position이 곧 화면 px다. 거리는 캔버스 배율을 곱해 마스터 캔버스 기준 px로 맞춘다.
        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        _rect.position = screen + _labelOffset * scale;
    }
}
