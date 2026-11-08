using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스를 올리면 씬에 하나 있는 말풍선(<see cref="HoverTooltip"/>)에 이 항목의 문구를 띄워 달라고 부탁한다.
/// 항목 쪽은 번역 키 하나만 든다. 말풍선의 모양·위치·숨김 판단은 전부 말풍선 쪽이 한다.
///
/// 키가 비어 있으면 말풍선을 띄우지 않는다. <see cref="LocalizedText"/>는 빈 키를 실수로 보고 드러내지만,
/// 말풍선은 대부분의 행에 문구가 없는 것이 정상이라 빈 키가 곧 "말풍선 없음"이다.
/// 설정 행처럼 공통 프리팹(SettingsRow)에 빈 키로 한 번 붙이고, 문구가 있는 행만 인스턴스에서 키를 덮어쓴다.
///
/// 고정 문구는 인스펙터의 키 칸에, 상태에 따라 바뀌는 문구(도감 귀환/소환)는 <see cref="SetStringId"/>로 넣는다.
/// 한 항목에 둘을 섞지 않는다.
///
/// 씬의 UI 입력 모듈은 Send Pointer Hover To Parent가 켜져 있어, 자식(토글·이름 글자 등)에 올려도 이 컴포넌트가
/// Enter를 받고, 자식끼리 옮겨 다닐 때는 Exit을 받지 않는다. 그래서 항목 루트에 한 번 붙이면 된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HoverTooltipSource : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("말풍선 문구의 번역 키. 예: UISettings.tooltip.auto_start. 비우면 말풍선을 띄우지 않는다.")]
    [SerializeField] private string _stringId;

    [Tooltip("말풍선을 이 사각형 바로 위에 띄운다. 비우면 이 오브젝트 자신. 설정 행은 이름 글자(Label)를 넣는다.")]
    [SerializeField] private RectTransform _anchor;

    private RectTransform _rect;
    private UIPanel _panel;
    private bool _hovered;

    public string StringId => _stringId;

    /// <summary>말풍선 위치의 기준.</summary>
    public RectTransform Anchor => _anchor;

    /// <summary>커서가 아직 이 항목 위에 있는지 볼 때 쓰는 사각형. 위치 기준(<see cref="Anchor"/>)이 아니라 항목 자신이다.</summary>
    public RectTransform Rect => _rect;

    /// <summary>이 항목이 속한 패널. 메뉴 바처럼 패널 밖이면 null.</summary>
    public UIPanel Panel => _panel;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        if (_anchor == null) _anchor = _rect;
        _panel = GetComponentInParent<UIPanel>(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        if (string.IsNullOrEmpty(_stringId)) return;
        if (HoverTooltip.Instance != null) HoverTooltip.Instance.Show(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        RequestHide();
    }

    private void OnDisable()
    {
        _hovered = false;
        RequestHide();
    }

    /// <summary>
    /// 문구 키를 코드에서 바꾼다. 버튼 하나가 상태에 따라 다른 일을 하는 곳(도감 귀환/소환)에서 쓴다.
    /// 커서가 올라가 있는 동안 바뀌면 말풍선도 새 문구로 바꾼다. 빈 키로 바뀌면 말풍선을 내린다.
    /// </summary>
    public void SetStringId(string id)
    {
        if (_stringId == id) return;
        _stringId = id;
        if (_hovered && HoverTooltip.Instance != null) HoverTooltip.Instance.Show(this);
    }

    private void RequestHide()
    {
        if (HoverTooltip.Instance != null) HoverTooltip.Instance.Hide(this);
    }
}
