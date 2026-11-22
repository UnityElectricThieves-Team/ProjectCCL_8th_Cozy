using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 패널의 Area_Header(제목 띠)에 붙여, 띠를 잡고 드래그하면 패널 전체를 옮긴다.
/// 누른 패널을 맨 앞으로 가져오는 일은 하지 않는다 — 패널 어디를 누르든 <see cref="UIManager"/>가 한 곳에서 한다.
/// 패널이 화면 밖으로 완전히 나가지 않도록 캔버스 안에 클램프한다.
///
/// 붙는 곳: Base 프리팹의 Area_Header GameObject. 포인터 이벤트를 받으려면 그 위에 Raycast Target인
///          Graphic이 있어야 한다. Area_Header에 붙은 투명 Image가 띠 전체에서 이벤트를 받는다.
///          그 Image를 지우면 제목 상자와 X 버튼 위에서만 끌린다.
/// 옮길 대상: 부모에서 찾은 <see cref="UIPanel"/>의 RectTransform(패널 루트) — 별도 배선 불필요.
/// 전제: 패널 루트가 화면 중앙 앵커(anchor/pivot 0.5)로 배치돼 있음(클램프 계산이 이를 가정).
///
/// 패널 부모의 배율(UI 크기 — <see cref="UIScaleApplier"/>)을 함께 본다. 부모가 커지면 같은 anchoredPosition도
/// 화면에서 그만큼 멀리 놓이므로, 드래그 이동량은 그 배율로 나누고 클램프 범위는 그 배율로 줄인다.
/// </summary>
public class DraggablePanel : MonoBehaviour, IDragHandler
{
    private RectTransform _panelRect;  // 옮길 대상(패널 루트)
    private RectTransform _canvasRect; // 클램프 기준(루트 캔버스)
    private Canvas _canvas;            // scaleFactor 보정용

    private void Awake()
    {
        var panel = GetComponentInParent<UIPanel>();
        if (panel != null) _panelRect = panel.GetComponent<RectTransform>();

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvasRect = _canvas.GetComponent<RectTransform>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_panelRect == null) return;

        float scale = (_canvas != null ? _canvas.scaleFactor : 1f) * ParentScale;
        _panelRect.anchoredPosition = Clamp(_panelRect.anchoredPosition + eventData.delta / scale);
    }

    /// <summary>지금 자리가 캔버스 밖으로 나가 있으면 안으로 당긴다. UI 크기를 키운 뒤 <see cref="UIScaleApplier"/>가 부른다.</summary>
    public void ClampIntoCanvas()
    {
        if (_panelRect != null) _panelRect.anchoredPosition = Clamp(_panelRect.anchoredPosition);
    }

    // 패널 부모(Panels)의 배율. 부모의 위치는 캔버스 가운데라고 가정한다(중앙 앵커 가정과 같은 전제).
    private float ParentScale => _panelRect.parent != null ? Mathf.Max(0.01f, _panelRect.parent.localScale.x) : 1f;

    // 패널이 캔버스 안에 완전히 들어오도록 클램프(중앙 앵커 가정). 부모 배율만큼 캔버스가 작아진 것으로 본다.
    private Vector2 Clamp(Vector2 pos)
    {
        if (_canvasRect == null) return pos;
        Vector2 half = (_canvasRect.rect.size / ParentScale - _panelRect.rect.size) * 0.5f;
        half = Vector2.Max(half, Vector2.zero);
        pos.x = Mathf.Clamp(pos.x, -half.x, half.x);
        pos.y = Mathf.Clamp(pos.y, -half.y, half.y);
        return pos;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponentInParent<UIPanel>() == null)
            Debug.LogWarning($"[{nameof(DraggablePanel)}] 부모에 UIPanel이 없습니다. 패널의 Area_Header에 붙여야 합니다.", this);
    }
#endif
}
