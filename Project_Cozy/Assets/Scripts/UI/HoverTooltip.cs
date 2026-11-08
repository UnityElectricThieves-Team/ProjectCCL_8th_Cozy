using TMPro;
using UnityEngine;

/// <summary>
/// 마우스를 올린 항목 바로 위에 뜨는 말풍선. 씬에 하나만 두고 모든 항목(<see cref="HoverTooltipSource"/>)이 빌려 쓴다.
/// 마우스는 하나라 동시에 두 개가 뜰 일이 없고, 하나를 켜고 끄면 생성·파괴 비용도 없다.
///
/// 크기는 글자 폭에 맞추고, 최대 폭(Figma: 3840 기준 740)을 넘으면 줄을 바꾼다. 여백은 글자 RectTransform의
/// offset(프리팹 값)에서 읽는다. 위치는 항목 바로 위이고, 항목이 속한 패널과 화면이 겹치는 영역 안으로 민다
/// (Figma "메뉴창 안쪽으로, 호버한 곳 바로 위"). 패널 밖 항목(메뉴 바)은 화면 안으로만 민다.
/// 위치와 크기를 코드가 정하는 것은 호버 대상과 글자 길이가 실행 중에만 정해지기 때문이다(ui-panels.md).
///
/// UIRoot 맨 뒤 형제인 층 루트에 붙는다. 층은 UIScaleApplier의 대상이라 UI 크기 설정을 따라 커진다.
/// 말풍선과 글자의 Raycast Target은 꺼 둔다 — 켜 두면 말풍선이 항목 위에 겹칠 때 호버를 가져가 깜빡인다.
///
/// 떠 있는 동안에는 매 프레임 "아직 띄워도 되는가"를 확인한다. Exit 이벤트만 믿으면 말풍선이 남는 길이 있어서다.
/// - 패널은 CanvasGroup으로 숨겨서, 닫히거나 잠시 숨겨져도 항목의 OnDisable이 불리지 않는다.
/// - 창이 클릭 통과 중이면 Input System의 포인터가 멈춰 Exit이 오지 않을 수 있다.
/// - 휠로 스크롤하면 커서는 그대로인데 그 밑의 행이 바뀐다.
/// 이 컴포넌트의 enabled는 이 확인(LateUpdate)을 돌릴지만 뜻한다. 프리팹에는 꺼서 저장하고, 떠 있을 때만 켠다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HoverTooltip : MonoBehaviour
{
    public static HoverTooltip Instance { get; private set; }

    [Tooltip("말풍선 배경. 평소에는 꺼 둔다.")]
    [SerializeField] private RectTransform _bubble;

    [Tooltip("말풍선 글자. 말풍선 안에 상하좌우로 늘여 두고, offset을 여백으로 쓴다.")]
    [SerializeField] private TMP_Text _text;

    [Tooltip("말풍선의 최대 폭(마스터 캔버스 px, 여백 포함). 넘으면 줄을 바꾼다.")]
    [SerializeField] private float _maxWidth = 740f;

    [Tooltip("항목 위 변과 말풍선 아래 변 사이의 간격(마스터 캔버스 px).")]
    [SerializeField] private float _gap = 20f;

    // TMP가 "제한 없음"으로 쓰는 값. 무한대를 넘기면 계산이 틀어지는 경로가 있다.
    private const float Unbounded = 32767f;

    private readonly Vector3[] _corners = new Vector3[4];

    private RectTransform _layer;
    private RectTransform _screen;
    private WindowsCursorToUnityScreen _cursorSource;
    private float _padLeft, _padRight, _padTop, _padBottom;
    private HoverTooltipSource _owner;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _layer = (RectTransform)transform;
        // 층 자신은 UI 크기 배율이 걸려 화면 경계로 쓸 수 없다. 씬 참조를 직렬화하지 않으려고 여기서 찾는다.
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) _screen = (RectTransform)canvas.rootCanvas.transform;

        _cursorSource = FindFirstObjectByType<WindowsCursorToUnityScreen>();
        if (_cursorSource == null)
            Debug.LogWarning($"[{nameof(HoverTooltip)}] {nameof(WindowsCursorToUnityScreen)} 없음 — 커서가 항목을 벗어났는지 직접 확인하지 않습니다.", this);

        if (_text != null)
        {
            RectTransform t = _text.rectTransform;
            _padLeft = t.offsetMin.x;
            _padBottom = t.offsetMin.y;
            _padRight = -t.offsetMax.x;
            _padTop = -t.offsetMax.y;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>항목의 문구로 말풍선을 띄운다. 이미 다른 항목의 말풍선이 떠 있으면 바꾼다.</summary>
    public void Show(HoverTooltipSource owner)
    {
        if (owner == null || _bubble == null || _text == null) return;
        if (string.IsNullOrEmpty(owner.StringId))
        {
            Hide(owner);
            return;
        }
        if (!CanShowFor(owner)) return;

        _owner = owner;
        // 켜고 나서 잰다. 꺼진 TMP로 재면 첫 측정이 틀릴 수 있다.
        _bubble.gameObject.SetActive(true);
        LocalizationManager.SetText(_text, owner.StringId);
        Layout();
        enabled = true;
    }

    /// <summary>
    /// 그 항목의 말풍선이 떠 있으면 내린다. 다른 항목의 말풍선이면 그대로 둔다 — 옮겨 갈 때 Enter·Exit 순서가 어떻든
    /// 새 말풍선을 옛 항목의 Exit이 지우지 않게.
    /// </summary>
    public void Hide(HoverTooltipSource owner)
    {
        if (ReferenceEquals(_owner, owner)) HideCurrent();
    }

    private void LateUpdate()
    {
        // 파괴된 항목은 Unity의 null 비교에서 null이 된다.
        if (_owner == null || !CanShowFor(_owner) || !IsCursorOver(_owner.Rect)) HideCurrent();
    }

    private void HideCurrent()
    {
        _owner = null;
        // 씬을 내릴 때는 자식이 먼저 파괴됐을 수 있다.
        if (_bubble != null) _bubble.gameObject.SetActive(false);
        enabled = false;
    }

    // 항목이 켜져 있고, 속한 패널이 보이며 조작 가능해야 한다(확인 팝업에 막혔거나 닫혔거나 잠시 숨겨졌으면 안 된다).
    // 패널 밖 항목은 확인 팝업이 떠 있지 않아야 한다 — 말풍선이 팝업 앞에 겹치지 않게.
    private static bool CanShowFor(HoverTooltipSource owner)
    {
        if (!owner.isActiveAndEnabled) return false;
        if (owner.Panel != null) return owner.Panel.IsInteractable;
        UIManager ui = UIManager.Instance;
        return ui == null || !ui.HasModal;
    }

    // 캔버스가 Screen Space - Overlay라 카메라 없이 판정한다. 커서 좌표는 클릭 통과 중에도 OS 커서를 읽는 단일 소스를 쓴다.
    private bool IsCursorOver(RectTransform rect)
    {
        if (_cursorSource == null) return true;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, _cursorSource.UnityScreenPosition, null);
    }

    private void Layout()
    {
        // 크기 — 한 줄로 쟀을 때의 폭과 최대 폭 중 좁은 쪽으로 두고, 그 폭에서 줄을 바꿨을 때의 높이를 쓴다.
        // 한 줄 폭은 올림해서, 반올림 차이로 마지막 글자가 다음 줄로 넘어가지 않게 한다.
        string message = _text.text;
        float padX = _padLeft + _padRight;
        float padY = _padTop + _padBottom;
        float lineWidth = Mathf.Ceil(_text.GetPreferredValues(message, Unbounded, Unbounded).x);
        float textWidth = Mathf.Min(lineWidth, _maxWidth - padX);
        float textHeight = Mathf.Ceil(_text.GetPreferredValues(message, textWidth, Unbounded).y);
        float width = textWidth + padX;
        float height = textHeight + padY;
        _bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        // 위치 — 앵커 위 변 가운데에서 간격만큼 위. 좌표는 모두 층의 로컬 공간(UI 크기 배율이 걸린 공간)이다.
        _owner.Anchor.GetWorldCorners(_corners); // 0 왼아래, 1 왼위, 2 오른위, 3 오른아래
        Vector3 anchorTopLeft = _layer.InverseTransformPoint(_corners[1]);
        Vector3 anchorTopRight = _layer.InverseTransformPoint(_corners[2]);
        float left = (anchorTopLeft.x + anchorTopRight.x) * 0.5f - width * 0.5f;
        float bottom = anchorTopLeft.y + _gap;

        // 경계 안으로 민다. 오른쪽 → 왼쪽 → 위 순서라, 폭이 모자라면 왼쪽 변이 보인다.
        if (TryGetBounds(_owner.Panel, out float minX, out float maxX, out float maxY))
        {
            if (left + width > maxX) left = maxX - width;
            if (left < minX) left = minX;
            if (bottom + height > maxY) bottom = maxY - height;
        }

        Vector2 pivot = _bubble.pivot;
        _bubble.localPosition = new Vector3(left + width * pivot.x, bottom + height * pivot.y, 0f);
    }

    // 패널과 화면이 겹치는 영역. UI 크기를 최대로 올리면 패널 일부가 화면 밖에 남을 수 있어서 화면과 겹친 부분만 쓴다.
    private bool TryGetBounds(UIPanel panel, out float minX, out float maxX, out float maxY)
    {
        minX = float.MinValue;
        maxX = float.MaxValue;
        maxY = float.MaxValue;
        bool any = false;

        if (_screen != null)
        {
            Intersect(_screen, ref minX, ref maxX, ref maxY);
            any = true;
        }
        if (panel != null)
        {
            Intersect((RectTransform)panel.transform, ref minX, ref maxX, ref maxY);
            any = true;
        }
        return any;
    }

    private void Intersect(RectTransform rect, ref float minX, ref float maxX, ref float maxY)
    {
        rect.GetWorldCorners(_corners);
        Vector3 bottomLeft = _layer.InverseTransformPoint(_corners[0]);
        Vector3 topRight = _layer.InverseTransformPoint(_corners[2]);
        minX = Mathf.Max(minX, bottomLeft.x);
        maxX = Mathf.Min(maxX, topRight.x);
        maxY = Mathf.Min(maxY, topRight.y);
    }
}
