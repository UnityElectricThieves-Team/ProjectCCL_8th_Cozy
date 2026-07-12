using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 패널의 타이틀바에 붙여, 타이틀바를 잡고 드래그하면 패널 전체를 옮긴다.
/// 누르는 순간 그 패널을 맨 앞으로 가져온다(UIManager를 거쳐 최상단 추적도 함께 갱신).
/// 패널이 화면 밖으로 완전히 나가지 않도록 캔버스 안에 클램프한다.
///
/// 붙는 곳: 타이틀바 GameObject. 포인터 이벤트를 받으려면 그 위에 Raycast Target인
///          Graphic이 있어야 한다(Base 프리팹 TitleBar의 Image가 그 역할).
/// 옮길 대상: 부모에서 찾은 <see cref="UIPanel"/>의 RectTransform(패널 루트) — 별도 배선 불필요.
/// 전제: 패널 루트가 화면 중앙 앵커(anchor/pivot 0.5)로 배치돼 있음(클램프 계산이 이를 가정).
/// </summary>
public class DraggablePanel : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private UIPanel _panel;
    private RectTransform _panelRect;  // 옮길 대상(패널 루트)
    private RectTransform _canvasRect; // 클램프 기준(루트 캔버스)
    private Canvas _canvas;            // scaleFactor 보정용

    private void Awake()
    {
        _panel = GetComponentInParent<UIPanel>();
        if (_panel != null) _panelRect = _panel.GetComponent<RectTransform>();

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvasRect = _canvas.GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 누르면 이 패널을 맨 앞으로 + UIManager의 최상단(_open 끝) 추적 갱신.
        if (_panel != null) UIManager.Instance?.Open(_panel);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_panelRect == null) return;

        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        Vector2 pos = _panelRect.anchoredPosition + eventData.delta / scale;

        if (_canvasRect != null)
        {
            // 패널이 캔버스 안에 완전히 들어오도록 클램프(중앙 앵커 가정).
            Vector2 half = (_canvasRect.rect.size - _panelRect.rect.size) * 0.5f;
            pos.x = Mathf.Clamp(pos.x, -half.x, half.x);
            pos.y = Mathf.Clamp(pos.y, -half.y, half.y);
        }

        _panelRect.anchoredPosition = pos;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponentInParent<UIPanel>() == null)
            Debug.LogWarning($"[{nameof(DraggablePanel)}] 부모에 UIPanel이 없습니다. 패널의 타이틀바에 붙여야 합니다.", this);
    }
#endif
}
