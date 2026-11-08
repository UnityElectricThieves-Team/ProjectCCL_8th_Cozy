using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 장식 배치 모드(설치·이동·회수·제거)의 안내. 모드 동안 기본 마우스 커서를 숨기고, 프리팹 안의 커서 그림(UI 이미지)과
/// "모드 이름(왼쪽)·취소(오른쪽)" 글자를 마우스 위치에 붙여 다닌다. 왼쪽 라벨은 모드마다 하나씩 두고 지금 모드의 것만 켠다
/// (Figma 이동/회수/제거 모드의 안내 쥐 그림 — 설치 모드와 같은 모양에 왼쪽 글자만 모드 이름이다).
/// 기본과 다른 커서가 "지금은 배치 모드"라는 신호다. 모드가 끝나면 기본 커서를 되돌린다.
///
/// 커서 그림도 UI라서 글자와 같은 캔버스 배율을 따른다. OS가 그리는 커서는 해상도와 상관없이 같은 픽셀 크기라
/// 글자와의 간격이 배율마다 달라졌기 때문에, 그림까지 UI로 그린다. 대가는 그림이 입력보다 한 프레임쯤 늦을 수 있다는 점이다.
///
/// <see cref="DecorationPlacementController.ModeChanged"/>를 듣고 켜고 끈다 — 모드가 이 컴포넌트를 부르지 않는다.
/// 글자는 프리팹 안의 <see cref="LocalizedText"/>가 채운다(<c>UIPlacement.guide.*</c>).
/// 루트의 피벗이 곧 커서 끝이다 — 피벗을 라벨 줄의 아래 변(커서 그림 위쪽 가운데 부근)에 두면, 루트 안에서
/// 모드 이름은 그림 왼쪽 위, "취소"는 오른쪽 위에 놓인다(Figma 안내 그림). 커서 그림의 클릭 지점은
/// 그 이미지의 피벗이다. 위치는 모두 프리팹의 피벗·앵커·크기가 정한다.
/// 알파는 프리팹에 0으로 저장해 두어, 에디터에서 Play 전에도 보이지 않는다.
///
/// 이 UI는 클릭을 받지 않는다. 받으면 배치 모드의 "커서가 UI 위면 놓을 수 없음" 판정에 자기 자신이 걸려
/// 늘 빨강이 된다. 그래서 CanvasGroup의 blocksRaycasts를 끄고, 안의 글자와 커서 그림도 Raycast Target을 끈다.
///
/// 범용 UI 위젯 폴더(Prefabs/UIPanels/UIObjects)의 프리팹 루트에 붙는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class PlacementGuide : MonoBehaviour
{
    [Header("왼쪽 라벨 — 모드마다 하나, 같은 자리")]
    [SerializeField] private GameObject _installLabel;
    [SerializeField] private GameObject _moveLabel;
    [SerializeField] private GameObject _recallLabel;
    [SerializeField] private GameObject _removeLabel;

    private CanvasGroup _group;
    private RectTransform _rect;
    private DecorationPlacementController _placement;
    private WindowsCursorToUnityScreen _cursorSource;
    private bool _isShown;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _rect = (RectTransform)transform;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        Show(DecorationMode.None);
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
        _placement.ModeChanged += Show;
        if (_placement.IsActive) Show(_placement.Mode);
    }

    private void OnDestroy()
    {
        if (_placement != null) _placement.ModeChanged -= Show;
        if (_isShown) Cursor.visible = true; // 모드 중 파괴돼도 기본 커서를 되돌린다
    }

    private void Show(DecorationMode mode)
    {
        bool show = mode != DecorationMode.None;

        // 커서는 켜진 채로 시작하므로, 모드가 시작되고 끝날 때만 바꾼다(Awake의 Show가 다른 곳이 만진 커서를 덮지 않게).
        if (_isShown != show) Cursor.visible = !show;
        _isShown = show;
        _group.alpha = show ? 1f : 0f;

        SetLabel(_installLabel, mode == DecorationMode.Install);
        SetLabel(_moveLabel, mode == DecorationMode.Move);
        SetLabel(_recallLabel, mode == DecorationMode.Recall);
        SetLabel(_removeLabel, mode == DecorationMode.Remove);

        if (show) FollowCursor();
    }

    private static void SetLabel(GameObject label, bool active)
    {
        if (label != null && label.activeSelf != active) label.SetActive(active);
    }

    // 배치 모드 컨트롤러가 Update에서 고스트를 옮긴 뒤에 따라간다.
    private void LateUpdate()
    {
        if (_isShown) FollowCursor();
    }

    private void FollowCursor()
    {
        Vector2 screen = _cursorSource != null
            ? _cursorSource.UnityScreenPosition
            : (Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);

        // UI 캔버스는 Screen Space Overlay라 position이 곧 화면 px다. 루트의 피벗을 커서에 놓기만 한다.
        // 커서에서 글자까지의 거리는 프리팹의 피벗·크기가 들고 있어 코드에 숫자를 두지 않는다.
        _rect.position = screen;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_installLabel == null || _moveLabel == null || _recallLabel == null || _removeLabel == null)
            Debug.LogWarning($"[{nameof(PlacementGuide)}] 모드별 왼쪽 라벨 네 칸 중 빈 칸이 있습니다.", this);
    }
#endif
}
