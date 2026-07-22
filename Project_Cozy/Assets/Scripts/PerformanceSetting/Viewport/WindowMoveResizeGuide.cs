using UnityEngine;

/// <summary>
/// 평시 창 이동·리사이즈 영역을 보여주는 시각 안내. OnGUI로:
///   - 상단 중앙 이동 핸들(그립 바) — 잡으면 창 이동 (WindowManager의 HTCAPTION 존)
///   - 창 테두리 얇은 라인 — 가장자리 리사이즈 가능 표시
/// 를 그린다. 실제 이동과 리사이즈는 WindowManager와 OS가 NCHITTEST로 처리한다.
///
/// 평소에는 반투명으로 표시하고, 커서가 이동 핸들 위에 있으면 진하게 표시한다.
/// 편집 모드 중에는 그리지 않는다.
///
/// 핸들 크기는 WindowManager의 핫존 값과 동일하게 맞춰야
/// "보이는 곳 = 잡히는 곳"이 된다.
/// </summary>
[DisallowMultipleComponent]
public class WindowMoveResizeGuide : MonoBehaviour
{
    [SerializeField, Tooltip("편집 중 숨김 판정용. 비우면 자동 탐색 (없으면 항상 표시).")]
    private ViewportScreenSettings _viewportSettings;

    [Header("핫존 미러 (WindowManager와 일치)")]
    [SerializeField] private int _captionHeightPx = 28;
    [SerializeField] private int _captionWidthPx = 220;
    [SerializeField] private int _edgeThicknessPx = 6;

    [Header("시각")]
    [SerializeField, Tooltip("ViewportEditHandles와 동일한 주황색을 사용한다.")]
    private Color _accent = new Color(1f, 0.58f, 0.10f, 1f);
    [SerializeField, Range(0f, 1f)] private float _idleAlpha = 0.28f;
    [SerializeField, Range(0f, 1f)] private float _hoverAlpha = 0.85f;

    private GUIStyle _gripLabel;

    private void Awake()
    {
        useGUILayout = false;
    }

    private void Start()
    {
        if (_viewportSettings == null)
            _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
    }

    private void OnGUI()
    {
        if (_viewportSettings != null && _viewportSettings.IsEditing) return;
        EnsureAssets();

        float width = Screen.width;
        Rect grip = new Rect(
            (width - _captionWidthPx) / 2f,
            0f,
            _captionWidthPx,
            _captionHeightPx);

        bool hover = grip.Contains(Event.current.mousePosition);
        float alpha = hover ? _hoverAlpha : _idleAlpha;

        DrawRect(grip, new Color(_accent.r, _accent.g, _accent.b, alpha * 0.55f));
        _gripLabel.normal.textColor = new Color(1f, 1f, 1f, alpha);
        GUI.Label(grip, "≡ 창 이동", _gripLabel);

        DrawResizeEdges(width, Screen.height);
    }

    private void DrawResizeEdges(float width, float height)
    {
        Color edgeColor = new Color(_accent.r, _accent.g, _accent.b, _idleAlpha * 0.6f);
        float thickness = Mathf.Max(1f, _edgeThicknessPx * 0.5f);

        DrawRect(new Rect(0, 0, width, thickness), edgeColor);
        DrawRect(new Rect(0, height - thickness, width, thickness), edgeColor);
        DrawRect(new Rect(0, 0, thickness, height), edgeColor);
        DrawRect(new Rect(width - thickness, 0, thickness, height), edgeColor);
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void EnsureAssets()
    {
        if (_gripLabel != null) return;

        _gripLabel = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
        };
    }
}
