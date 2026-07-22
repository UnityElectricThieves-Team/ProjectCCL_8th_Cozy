using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 뷰포트 편집 모드의 핸들 드래그 UI. 편집 중(IsEditing)에만:
///   - 편집 배경 딤: 카메라 클리어 알파를 올려 창 전체를 반투명하게 (DWM 목적지 알파 확보 겸용)
///   - 프리뷰 rect 바깥 추가 딤 + 테두리 + 모서리 원형 핸들 4개 + 변 바 핸들 4개 (OnGUI)
///   - 마우스 드래그(Input System)로 핸들을 끌면 SetPreviewViewport 갱신
///     (모서리/변 = 리사이즈, rect 내부 = 이동)
///
/// 좌표 전제: 편집 중에는 창=모니터, 카메라=베이스 공간 전체 프레이밍이라
/// "화면 px ≒ 베이스 공간 px". 해상도 불일치 대비로 비율 스케일만 걸어 둔다.
/// 핸들의 시각 위치와 히트 판정은 같은 앵커 클램프(_edgeInsetPx)를 공유한다 —
/// 프리뷰=화면 전체(기본값)일 때도 "보이는 곳 = 잡히는 곳"이 유지된다.
///
/// 배경 딤을 카메라 클리어 알파로 처리하는 이유(5인 리뷰 합의): DWM 알파 합성 창에서
/// IMGUI(straight-alpha)는 목적지 알파를 충분히 못 채워 빌드에서 흐리게 보인다.
/// 클리어 알파를 올리면 창 전체의 목적지 알파가 보장되고, "편집 중" 전역 신호도 겸한다.
/// 알파 1(완전 불투명)은 금지 — 데스크톱을 보면서 뷰포트를 배치하는 편집 UX가 죽는다.
///
/// 확정/폐기는 이 컴포넌트 소관이 아님 — SaveEdit()/CancelEdit()는 호출 측(UI 버튼) 담당.
/// Win32를 모른다 — 순수 Unity UI. ViewportScreenSettings와 같은 정책 레이어.
/// </summary>
[DisallowMultipleComponent]
public class ViewportEditHandles : MonoBehaviour
{
    private enum DragZone { None, Move, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }

    [SerializeField, Tooltip("미할당 시 씬에서 자동 탐색. 하니스가 Bind()로 주입 가능.")]
    private ViewportScreenSettings _viewportSettings;

    [Header("핸들 (베이스 공간 px)")]
    [SerializeField, Tooltip("모서리 핸들 히트존 반경")]
    private int _cornerGrabPx = 36;
    [SerializeField, Tooltip("변 핸들 히트존 두께")]
    private int _edgeGrabPx = 18;
    [SerializeField, Tooltip("프리뷰가 화면 가장자리에 붙을 때 핸들(시각+히트 공통)을 안쪽으로 들여놓는 거리")]
    private int _edgeInsetPx = 12;

    [Header("시각")]
    [SerializeField, Tooltip("레퍼런스 사양 — 주황")]
    private Color _accent = new Color(1f, 0.58f, 0.10f, 0.95f);
    [SerializeField] private Color _dim = new Color(0.05f, 0.07f, 0.10f, 0.55f);
    [SerializeField, Range(0f, 0.9f), Tooltip("편집 중 카메라 클리어 알파(창 전체 반투명 딤). 1.0 금지 — 데스크톱이 안 보이게 됨")]
    private float _editBackdropAlpha = 0.45f;
    [SerializeField, Tooltip("모서리 원형 핸들 지름(px)")]
    private int _cornerHandlePx = 28;

    private DragZone _dragZone = DragZone.None;
    private Vector2Int _dragStartMouse;   // 베이스 공간 px
    private RectInt _dragStartRect;
    private static Texture2D _circleTex;  // 모서리 핸들용 원형 (레퍼런스 이미지의 점 형태)
    private GUIStyle _centerLabel;

    // UI 위 클릭 판정 — 무인자 IsPointerOverGameObject는 클릭 통과 이력이 있는 이 앱에서
    // stale할 수 있어(리뷰 합의) WindowManager와 같은 좌표 직접 주입 레이캐스트를 쓴다.
    private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
    private PointerEventData _uiPointerData;

    // 편집 배경 딤용 카메라 클리어 알파 저장/복원
    private Camera _backdropCamera;
    private Color _savedClearColor;
    private bool _backdropApplied;

    /// <summary>협력자 직접 주입 — Find 순서 의존 제거 (하니스의 EnsureCompanion 경로).</summary>
    public void Bind(ViewportScreenSettings settings)
    {
        UnsubscribeEditMode();
        _viewportSettings = settings;
        SubscribeEditMode();
    }

    private void Awake()
    {
        useGUILayout = false; // GUILayout 미사용 — Layout 페이즈 제거로 OnGUI 비용 절반 절감
    }

    private void Start()
    {
        if (_viewportSettings == null) _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        if (_viewportSettings == null)
        {
            Debug.LogWarning("[ViewportEditHandles] ViewportScreenSettings 없음 — 비활성. Bind()로 주입 가능.");
            enabled = false;
            return;
        }
        SubscribeEditMode();
    }

    private void OnDestroy()
    {
        UnsubscribeEditMode();
        RestoreBackdrop(); // 편집 중 파괴돼도 카메라 알파를 평시 값으로 복원
    }

    private void SubscribeEditMode()
    {
        if (_viewportSettings == null) return;
        // Bind() 후 Start()가 또 부를 수 있으므로 멱등으로 — 해제 후 구독.
        _viewportSettings.EditModeChanged -= OnEditModeChanged;
        _viewportSettings.EditModeChanged += OnEditModeChanged;
    }

    private void UnsubscribeEditMode()
    {
        if (_viewportSettings != null) _viewportSettings.EditModeChanged -= OnEditModeChanged;
    }

    // ===== 편집 배경 딤 (카메라 클리어 알파) =====

    private void OnEditModeChanged(bool editing)
    {
        if (editing) ApplyBackdrop();
        else         RestoreBackdrop();
    }

    private void ApplyBackdrop()
    {
        if (_backdropApplied) return;
        _backdropCamera = Camera.main;
        if (_backdropCamera == null) return;

        _savedClearColor = _backdropCamera.backgroundColor;
        // RGB는 검정 유지 — DWM 투명 규칙(WindowManager: 클리어 색은 반드시 (0,0,0))과 동일 계열.
        _backdropCamera.backgroundColor = new Color(0f, 0f, 0f, Mathf.Clamp(_editBackdropAlpha, 0f, 0.9f));
        _backdropApplied = true;
    }

    private void RestoreBackdrop()
    {
        if (!_backdropApplied) return;
        if (_backdropCamera != null) _backdropCamera.backgroundColor = _savedClearColor;
        _backdropApplied = false;
    }

    // ===== 드래그 =====

    private void Update()
    {
        if (_viewportSettings == null) return; // 런타임 파괴 대비 (OnGUI와 대칭)
        if (!_viewportSettings.IsEditing || Mouse.current == null)
        {
            _dragZone = DragZone.None;
            return;
        }

        Vector2Int mouse = MouseToBasePx();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // UGUI(저장/취소 버튼 등) 위 클릭은 핸들 드래그로 잡지 않는다.
            bool overUI = IsPointerOverUI(Mouse.current.position.ReadValue());
            _dragZone = overUI ? DragZone.None : HitTest(mouse, _viewportSettings.PreviewViewport);
            _dragStartMouse = mouse;
            _dragStartRect  = _viewportSettings.PreviewViewport;
        }
        else if (!Mouse.current.leftButton.isPressed)
        {
            _dragZone = DragZone.None;
        }

        if (_dragZone == DragZone.None || !Mouse.current.leftButton.isPressed) return;

        int dx = mouse.x - _dragStartMouse.x;
        int dy = mouse.y - _dragStartMouse.y;
        _viewportSettings.SetPreviewViewport(
            ApplyDrag(_dragStartRect, _dragZone, dx, dy, _viewportSettings.BaseSpaceSize));
    }

    private bool IsPointerOverUI(Vector2 screenPos)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;
        if (_uiPointerData == null) _uiPointerData = new PointerEventData(es);
        _uiPointerData.position = screenPos;
        _uiHits.Clear();
        es.RaycastAll(_uiPointerData, _uiHits);
        bool over = _uiHits.Count > 0;
        _uiHits.Clear();
        return over;
    }

    // ===== 드래그 계산 =====
    // 이동하는 변을 베이스 공간 [0, base]로 직접 클램프해 반대편 앵커를 보존한다.
    // (사후 ClampToBaseSpace의 위치 클램프에만 맡기면 오버슈트 시 앵커가 밀린다 — 리뷰 합의)

    private static RectInt ApplyDrag(RectInt r, DragZone zone, int dx, int dy, Vector2Int baseSize)
    {
        Vector2Int min = ViewportScreenSettings.MinViewportSize;

        if (zone == DragZone.Move)
        {
            int mx = Mathf.Clamp(r.x + dx, 0, Mathf.Max(0, baseSize.x - r.width));
            int my = Mathf.Clamp(r.y + dy, 0, Mathf.Max(0, baseSize.y - r.height));
            return new RectInt(mx, my, r.width, r.height);
        }

        bool left   = zone == DragZone.Left  || zone == DragZone.TopLeft    || zone == DragZone.BottomLeft;
        bool right  = zone == DragZone.Right || zone == DragZone.TopRight   || zone == DragZone.BottomRight;
        bool bottom = zone == DragZone.Bottom|| zone == DragZone.BottomLeft || zone == DragZone.BottomRight;
        bool top    = zone == DragZone.Top   || zone == DragZone.TopLeft    || zone == DragZone.TopRight;

        int xMin = r.xMin, xMax = r.xMax, yMin = r.yMin, yMax = r.yMax;
        if (left)   xMin = Mathf.Clamp(xMin + dx, 0, xMax - min.x);
        if (right)  xMax = Mathf.Clamp(xMax + dx, xMin + min.x, baseSize.x);
        if (bottom) yMin = Mathf.Clamp(yMin + dy, 0, yMax - min.y);
        if (top)    yMax = Mathf.Clamp(yMax + dy, yMin + min.y, baseSize.y);

        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    // ===== 히트 테스트 (시각과 같은 클램프 앵커 공유) =====

    private DragZone HitTest(Vector2Int m, RectInt r)
    {
        int c = _cornerGrabPx, e = _edgeGrabPx;
        Vector2Int bl = ClampAnchor(r.xMin, r.yMin);
        Vector2Int br = ClampAnchor(r.xMax, r.yMin);
        Vector2Int tl = ClampAnchor(r.xMin, r.yMax);
        Vector2Int tr = ClampAnchor(r.xMax, r.yMax);

        // 모서리 우선 (HitTestCalculator와 같은 이유 — 변 판정에 먹히면 대각 리사이즈 불가)
        if (Near(m, tl, c)) return DragZone.TopLeft;
        if (Near(m, tr, c)) return DragZone.TopRight;
        if (Near(m, bl, c)) return DragZone.BottomLeft;
        if (Near(m, br, c)) return DragZone.BottomRight;

        bool nearL = Mathf.Abs(m.x - bl.x) <= e, nearR = Mathf.Abs(m.x - br.x) <= e;
        bool nearB = Mathf.Abs(m.y - bl.y) <= e, nearT = Mathf.Abs(m.y - tl.y) <= e;
        bool inX = m.x >= r.xMin - e && m.x <= r.xMax + e;
        bool inY = m.y >= r.yMin - e && m.y <= r.yMax + e;

        if (nearL && inY) return DragZone.Left;
        if (nearR && inY) return DragZone.Right;
        if (nearT && inX) return DragZone.Top;
        if (nearB && inX) return DragZone.Bottom;

        if (r.Contains(m)) return DragZone.Move;
        return DragZone.None;
    }

    /// <summary>핸들 앵커를 베이스 공간 안쪽으로 클램프 — 시각과 히트가 같은 위치를 쓴다.</summary>
    private Vector2Int ClampAnchor(int x, int y)
    {
        Vector2Int b = _viewportSettings.BaseSpaceSize;
        return new Vector2Int(
            Mathf.Clamp(x, _edgeInsetPx, Mathf.Max(_edgeInsetPx, b.x - _edgeInsetPx)),
            Mathf.Clamp(y, _edgeInsetPx, Mathf.Max(_edgeInsetPx, b.y - _edgeInsetPx)));
    }

    private static bool Near(Vector2Int m, Vector2Int p, int radius)
        => Mathf.Abs(m.x - p.x) <= radius && Mathf.Abs(m.y - p.y) <= radius;

    // ===== 좌표 변환 =====

    private Vector2Int MouseToBasePx()
    {
        Vector2 pos = Mouse.current.position.ReadValue(); // 화면 좌하단 원점, Y 위
        Vector2Int b = _viewportSettings.BaseSpaceSize;
        return new Vector2Int(
            Mathf.RoundToInt(pos.x * b.x / Mathf.Max(1, Screen.width)),
            Mathf.RoundToInt(pos.y * b.y / Mathf.Max(1, Screen.height)));
    }

    private Vector2 BaseToGuiPoint(Vector2Int p)
    {
        Vector2Int b = _viewportSettings.BaseSpaceSize;
        float sx = (float)Screen.width  / Mathf.Max(1, b.x);
        float sy = (float)Screen.height / Mathf.Max(1, b.y);
        return new Vector2(p.x * sx, Screen.height - p.y * sy); // 베이스(Y 위) → GUI(Y 아래)
    }

    private Rect BaseToGui(RectInt r)
    {
        Vector2Int b = _viewportSettings.BaseSpaceSize;
        float sx = (float)Screen.width  / Mathf.Max(1, b.x);
        float sy = (float)Screen.height / Mathf.Max(1, b.y);
        return new Rect(r.x * sx, Screen.height - (r.y + r.height) * sy, r.width * sx, r.height * sy);
    }

    // ===== 그리기 =====

    private void OnGUI()
    {
        if (_viewportSettings == null || !_viewportSettings.IsEditing) return;
        EnsureAssets();

        RectInt pv = _viewportSettings.PreviewViewport;
        Rect v = BaseToGui(pv);
        float w = Screen.width, h = Screen.height;

        // 바깥 추가 딤 4조각 (전역 딤은 카메라 클리어 알파가 담당 — 여긴 "잘려나갈 영역" 강조)
        DrawRect(new Rect(0, 0, w, v.yMin), _dim);
        DrawRect(new Rect(0, v.yMax, w, h - v.yMax), _dim);
        DrawRect(new Rect(0, v.yMin, v.xMin, v.height), _dim);
        DrawRect(new Rect(v.xMax, v.yMin, w - v.xMax, v.height), _dim);

        // 테두리 — rect 안쪽으로 그려서 프리뷰=화면 전체(기본값)일 때도 보이게.
        const float bt = 3f;
        DrawRect(new Rect(v.xMin, v.yMin, v.width, bt), _accent);
        DrawRect(new Rect(v.xMin, v.yMax - bt, v.width, bt), _accent);
        DrawRect(new Rect(v.xMin, v.yMin, bt, v.height), _accent);
        DrawRect(new Rect(v.xMax - bt, v.yMin, bt, v.height), _accent);

        // 모서리 원형 핸들 4개 — 히트 테스트와 같은 클램프 앵커 사용 ("보이는 곳 = 잡히는 곳")
        DrawCorner(ClampAnchor(pv.xMin, pv.yMin));
        DrawCorner(ClampAnchor(pv.xMax, pv.yMin));
        DrawCorner(ClampAnchor(pv.xMin, pv.yMax));
        DrawCorner(ClampAnchor(pv.xMax, pv.yMax));

        // 변 바(pill) 핸들 4개 — 모서리와 모양을 구분해 "단축 리사이즈" 어포던스 표시
        DrawEdgeBar(ClampAnchor((pv.xMin + pv.xMax) / 2, pv.yMin), horizontal: true);
        DrawEdgeBar(ClampAnchor((pv.xMin + pv.xMax) / 2, pv.yMax), horizontal: true);
        DrawEdgeBar(ClampAnchor(pv.xMin, (pv.yMin + pv.yMax) / 2), horizontal: false);
        DrawEdgeBar(ClampAnchor(pv.xMax, (pv.yMin + pv.yMax) / 2), horizontal: false);

        GUI.Label(new Rect(v.xMin, v.center.y - 12f, v.width, 24f),
            "드래그: 내부 = 이동 · 모서리 원/변 바 = 크기 조절", _centerLabel);
    }

    private void DrawCorner(Vector2Int baseAnchor)
    {
        Vector2 p = BaseToGuiPoint(baseAnchor);
        float s = _cornerHandlePx;
        Color prev = GUI.color;
        GUI.color = _accent;
        GUI.DrawTexture(new Rect(p.x - s / 2f, p.y - s / 2f, s, s), _circleTex);
        GUI.color = prev;
    }

    private void DrawEdgeBar(Vector2Int baseAnchor, bool horizontal)
    {
        Vector2 p = BaseToGuiPoint(baseAnchor);
        float len = 26f, thick = 8f;
        Rect r = horizontal
            ? new Rect(p.x - len / 2f, p.y - thick / 2f, len, thick)
            : new Rect(p.x - thick / 2f, p.y - len / 2f, thick, len);
        DrawRect(r, _accent);
    }

    private static void DrawRect(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private void EnsureAssets()
    {
        if (_circleTex == null)
        {
            const int size = 32;
            float rad = size * 0.5f - 1f;
            _circleTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float alpha = Mathf.Clamp01(rad - d + 1f); // 경계 1px 안티앨리어싱
                    _circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            _circleTex.Apply();
        }
        if (_centerLabel == null)
        {
            _centerLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
            };
            _centerLabel.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
        }
    }
}
