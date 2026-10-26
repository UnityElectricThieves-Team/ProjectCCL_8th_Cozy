// ============================================================
// WindowFeatureTestPanel  (창 시스템 통합 테스트 하니스 — 데모/예시 레이어)
//
// 화면 우하단에 코드로 패널을 만들어 새 창 스택(WindowManager +
// ViewportScreenSettings)의 기능 전부를 버튼으로 찔러본다:
//
//   [캐릭터]   스폰 / x5 스폰        → CharacterManager.Spawn (캡 공통 적용)
//   [편집]     편집 시작 / 저장 / 취소 → ViewportScreenSettings Enter/Save/CancelEdit
//   [프리뷰]   축소 / 확대 / ◀ / ▶   → SetPreviewViewport (편집 중에만 활성) --> 일단 넣었고...폐기해도 되니 이것저것 써보는걸 권장
//   [프리셋]   전체 / 중앙 절반 / 우하단 → SetViewport (창=뷰포트 즉시 재배치)
//
// 창 가장자리 드래그(OS 리사이즈)는 버튼이 필요 없다 — 빌드에서 창 모서리를
// 직접 끌면 WindowManager의 WndProc(NCHITTEST)가 처리한다.
//
// 사용:
//   1) 씬의 아무 활성 GameObject에 이 컴포넌트를 붙인다 (참조는 자동 탐색).
//   2) Character Prefab 슬롯에 스폰할 프리팹 할당 (비우면 스폰 버튼만 경고).
//   3) CharacterManager가 씬에 없으면 런타임에 자동 생성한다.
//
// 버튼은 빌드의 클릭 통과 상태에서도 눌린다 — WindowManager.PollClickThrough가
// UGUI 레이캐스트(IsPointerOverUI)로 UI 위에서는 통과를 끄기 때문.
// 신 Input System 기준 EventSystem을 코드로 보장한다.
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class WindowFeatureTestPanel : MonoBehaviour
{
    [Header("협력자 (미할당 시 자동 탐색/생성)")]
    [SerializeField, Tooltip("뷰포트 정책. 비우면 씬에서 자동 탐색.")]
    private ViewportScreenSettings _viewportSettings;

    [Header("캐릭터 스폰")]
    [SerializeField, Tooltip("스폰할 캐릭터 프리팹")]
    private GameObject _characterPrefab;
    [SerializeField, Tooltip("스폰 기준 위치. 비우면 카메라 중앙.")]
    private Transform _spawnAnchor;
    [SerializeField, Tooltip("매 스폰마다 흩뿌리는 반경 — 겹침 방지")]
    private float _spawnSpread = 1.5f;

    [Header("프리뷰 조작 (편집 중)")]
    [SerializeField, Tooltip("축소/확대/이동 1회당 픽셀 스텝")]
    private int _previewStepPx = 120;

    private const float StatusRefreshInterval = 0.5f;

    private Text _statusLabel;
    private Button[] _editOnlyButtons;
    private Button _enterEditButton;
    private float _nextStatusRefresh;

    private void Start()
    {
        if (_viewportSettings == null) _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        if (CharacterManager.Instance == null)
            new GameObject("CharacterManager (auto)", typeof(CharacterManager));

        // 창 조작 UX 스택이 씬에 빠져 있으면 자동 장착 — 테스트 하니스만 붙이면 전부 동작하게.
        // 장착 직후 참조를 직접 주입(Bind) — Find 순서 의존 제거 (리뷰 합의).
        ViewportEditHandles handles = EnsureCompanion<ViewportEditHandles>();
        if (handles != null && _viewportSettings != null) handles.Bind(_viewportSettings);
        EnsureCompanion<WindowMoveResizeGuide>();
        EnsureCompanion<ViewportResidencyEnforcer>();

        EnsureEventSystem();
        BuildPanel();

        if (_viewportSettings != null)
            _viewportSettings.EditModeChanged += OnEditModeChanged;
        OnEditModeChanged(_viewportSettings != null && _viewportSettings.IsEditing);
    }

    private void OnDestroy()
    {
        if (_viewportSettings != null)
            _viewportSettings.EditModeChanged -= OnEditModeChanged;
    }

    private void Update()
    {
        if (_statusLabel == null || Time.unscaledTime < _nextStatusRefresh) return;
        _nextStatusRefresh = Time.unscaledTime + StatusRefreshInterval;
        RefreshStatus();
    }

    // ===== 버튼 액션 =====
    private void SpawnCharacters(int count)
    {
        if (_characterPrefab == null)
        {
            Debug.LogWarning("[WindowFeatureTestPanel] characterPrefab 미할당 — 스폰할 프리팹을 넣어주세요.");
            return;
        }
        Vector3 basePos = _spawnAnchor != null
            ? _spawnAnchor.position
            : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        basePos.z = 0f;

        for (int i = 0; i < count; i++)
        {
            Vector2 jitter = Random.insideUnitCircle * _spawnSpread;
            if (CharacterManager.Instance.Spawn(_characterPrefab, basePos + new Vector3(jitter.x, jitter.y, 0f)) == null)
            {
                Debug.Log("[WindowFeatureTestPanel] 스폰 캡 도달 — CharacterManager.MaxCount 확인.");
                break;
            }
        }
        RefreshStatus();
    }

    private void NudgePreview(int dx, int dy, int dSize)
    {
        if (_viewportSettings == null || !_viewportSettings.IsEditing) return;
        RectInt p = _viewportSettings.PreviewViewport;
        // dSize는 중심 유지 축소/확대 — 클램프는 ViewportScreenSettings가 담당.
        p.x += dx - dSize / 2;
        p.y += dy - dSize / 2;
        p.width  += dSize;
        p.height += dSize;
        _viewportSettings.SetPreviewViewport(p);
        RefreshStatus();
    }

    private void ApplyViewportPreset(float xRatio, float yRatio, float wRatio, float hRatio)
    {
        if (_viewportSettings == null) return;
        Vector2Int b = _viewportSettings.BaseSpaceSize;
        _viewportSettings.SetViewport(new RectInt(
            Mathf.RoundToInt(b.x * xRatio), Mathf.RoundToInt(b.y * yRatio),
            Mathf.RoundToInt(b.x * wRatio), Mathf.RoundToInt(b.y * hRatio)));
        RefreshStatus();
    }

    private void OnEditModeChanged(bool editing)
    {
        if (_editOnlyButtons != null)
            foreach (Button b in _editOnlyButtons) b.interactable = editing;
        if (_enterEditButton != null) _enterEditButton.interactable = !editing;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (_statusLabel == null) return;
        int alive = CharacterManager.Instance != null ? CharacterManager.Instance.AliveCount : 0;
        int max   = CharacterManager.Instance != null ? CharacterManager.Instance.MaxCount : 0;
        if (_viewportSettings != null)
        {
            RectInt v = _viewportSettings.PreviewViewport;
            _statusLabel.text = string.Format("캐릭터 {0}/{1}   모드: {2}\n뷰포트 {3}x{4} @({5},{6})",
                alive, max,
                !_viewportSettings.IsReady ? "초기화 중" : _viewportSettings.IsEditing ? "편집 중" : "평시",
                v.width, v.height, v.x, v.y);

            // 초기화 완료 전 EnterEdit는 거부되므로(무음 실패 방지) 버튼도 잠근다.
            if (_enterEditButton != null)
                _enterEditButton.interactable = _viewportSettings.IsReady && !_viewportSettings.IsEditing;
        }
        else
        {
            _statusLabel.text = string.Format("캐릭터 {0}/{1}\nViewportScreenSettings 없음 — 창 버튼 비활성", alive, max);
        }
    }

    // ===== UI 빌드 =====

    private T EnsureCompanion<T>() where T : Component
    {
        T existing = FindFirstObjectByType<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void BuildPanel()
    {
        var canvasGo = new GameObject("WindowTestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 다른 데모 UI 위에

        // 패널 — 우하단. 짙은 남색(비검정: ColorKey 레거시와의 시각 구분 관례 유지).
        var panelGo = new GameObject("Panel", typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelGo.GetComponent<Image>().color = new Color(0.10f, 0.14f, 0.22f, 0.92f);
        var prt = panelGo.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0f); // 우하단 기준
        // 작업 표시줄 위 + 게임 메뉴바 위. 메뉴바는 우하단에 붙고 캔버스 기준 높이가 210인데,
        // 그 캔버스가 폭 3840 기준으로 스케일되므로 실제 높이는 화면 폭에 비례해 최대 210px이다.
        // 그보다 높게 잡아야 어떤 해상도에서도 메뉴바를 덮지 않는다.
        prt.anchoredPosition = new Vector2(-20f, 240f);
        prt.sizeDelta = new Vector2(372f, 332f);

        float y = -10f; // 패널 상단부터 아래로 배치

        AddLabel(panelGo.transform, "창 기능 테스트", 15, FontStyle.Bold, ref y, 24f);
        _statusLabel = AddLabel(panelGo.transform, "-", 12, FontStyle.Normal, ref y, 40f);
        y -= 4f;

        // 1행: 캐릭터 스폰
        AddRow(panelGo.transform, ref y, new Color(0.96f, 0.65f, 0.75f, 1f), // 핑크 (CharacterSpawner 관례)
            ("스폰 x1", () => SpawnCharacters(1)),
            ("스폰 x5", () => SpawnCharacters(5)));

        // 2행: 편집 모드 전환
        Button[] row2 = AddRow(panelGo.transform, ref y, new Color(0.55f, 0.75f, 1f, 1f), // 파랑 (편집 계열 관례)
            ("편집 시작", () => { if (_viewportSettings != null) _viewportSettings.EnterEdit(); }),
            ("저장",     () => { if (_viewportSettings != null) _viewportSettings.SaveEdit(); }),
            ("취소",     () => { if (_viewportSettings != null) _viewportSettings.CancelEdit(); }));
        _enterEditButton = row2[0];

        // 3행: 프리뷰 조작 (편집 중에만 활성)
        Button[] row3 = AddRow(panelGo.transform, ref y, new Color(0.62f, 0.55f, 0.90f, 1f),
            ("축소", () => NudgePreview(0, 0, -_previewStepPx)),
            ("확대", () => NudgePreview(0, 0, +_previewStepPx)),
            ("◀",   () => NudgePreview(-_previewStepPx, 0, 0)),
            ("▶",   () => NudgePreview(+_previewStepPx, 0, 0)));

        // 4행: 뷰포트 프리셋 (평시 즉시 적용)
        AddRow(panelGo.transform, ref y, new Color(0.45f, 0.78f, 0.62f, 1f),
            ("전체",      () => ApplyViewportPreset(0f, 0f, 1f, 1f)),
            ("중앙 절반", () => ApplyViewportPreset(0.25f, 0.25f, 0.5f, 0.5f)),
            ("우하단",    () => ApplyViewportPreset(0.55f, 0f, 0.45f, 0.45f)));

        AddLabel(panelGo.transform, "창 가장자리 드래그 = OS 리사이즈 (빌드 전용)", 11, FontStyle.Italic, ref y, 22f);

        // 저장/취소/프리뷰 버튼은 편집 중에만 의미가 있다.
        _editOnlyButtons = new Button[] { row2[1], row2[2], row3[0], row3[1], row3[2], row3[3] };
    }

    private static Text AddLabel(Transform parent, string text, int size, FontStyle style, ref float y, float height)
    {
        var go = new GameObject("Label", typeof(Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<Text>();
        txt.text = text;
        txt.fontSize = size;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.UpperLeft;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f); // 패널 좌상단 기준
        rt.anchoredPosition = new Vector2(14f, y);
        rt.sizeDelta = new Vector2(344f, height);
        y -= height + 4f;
        return txt;
    }

    private static Button[] AddRow(Transform parent, ref float y, Color color, params (string label, UnityAction onClick)[] items)
    {
        const float rowWidth = 344f, gap = 6f, height = 42f;
        float btnWidth = (rowWidth - gap * (items.Length - 1)) / items.Length;

        var buttons = new Button[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            var btnGo = new GameObject("Btn_" + items[i].label, typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            btnGo.GetComponent<Image>().color = color;

            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(14f + (btnWidth + gap) * i, y);
            rt.sizeDelta = new Vector2(btnWidth, height);

            var btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(items[i].onClick);
            buttons[i] = btn;

            var txtGo = new GameObject("Label", typeof(Text));
            txtGo.transform.SetParent(btnGo.transform, false);
            var txt = txtGo.GetComponent<Text>();
            txt.text = items[i].label;
            txt.fontSize = 13;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
        }
        y -= height + 6f;
        return buttons;
    }
}
