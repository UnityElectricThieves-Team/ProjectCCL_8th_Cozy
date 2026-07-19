using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 상점 패널 내용물(Content)의 두뇌. '장식'과 '배경' 두 모드를 하나가 관리한다 —
/// 선택된 탭에 따라 진열 상품·행 프리팹·열 수·버튼 동작이 달라진다.
/// 탭을 누르면 <see cref="SetMode"/>가 기존 행을 비우고 그 모드로 다시 채운다.
///
/// - 장식: <see cref="ShopItemRow"/>에 <see cref="ShopItemSlot"/>을 3개씩. 버튼=구매(하트 차감).
/// - 배경: <see cref="BackgroundItemRow"/>에 <see cref="BackgroundItemSlot"/>을 2개씩. 버튼=구매→사용→사용취소(<see cref="BackgroundSystem"/>).
///
/// 패널 루트에 붙는다. 패널은 CanvasGroup으로 숨기므로(SetActive 아님) 이 컴포넌트는 계속 살아 있다.
/// 구독은 <see cref="OnEnable"/>/<see cref="OnDisable"/>에서 걸고 푼다.
/// </summary>
public sealed class ShopPanelContentController : MonoBehaviour
{
    private enum ShopMode { Decoration, Background }

    [Tooltip("행을 만들어 넣을 부모. 두 모드가 공유한다.")]
    [SerializeField] private Transform _content;

    [Header("장식 탭")]
    [FormerlySerializedAs("_rowPrefab")]
    [SerializeField] private ShopItemRow _decorationRowPrefab;
    [FormerlySerializedAs("_slotPrefab")]
    [SerializeField] private ShopItemSlot _decorationSlotPrefab;
    [FormerlySerializedAs("_items")]
    [SerializeField] private ShopItemDefinition[] _decorationItems;

    [Header("배경 탭")]
    [SerializeField] private BackgroundItemRow _backgroundRowPrefab;
    [SerializeField] private BackgroundItemSlot _backgroundSlotPrefab;
    // 배경 목록은 여기 두지 않는다 — BackgroundSystem이 카탈로그를 들고, 이 화면은 받아서 정렬해 그린다.

    [Header("탭 버튼")]
    [SerializeField] private Button _decorationTab;
    [SerializeField] private Button _backgroundTab;
    [Tooltip("활성/비활성 색을 칠할 탭 배경 이미지.")]
    [SerializeField] private Image _decorationTabImage;
    [SerializeField] private Image _backgroundTabImage;

    // Figma: 활성 탭=시안(#39C9E6), 비활성=회색(#D9D9D9).
    private static readonly Color ActiveTab = new(0.224f, 0.788f, 0.902f);
    private static readonly Color InactiveTab = new(0.851f, 0.851f, 0.851f);

    private readonly List<ShopItemRow> _decorationRows = new();
    private readonly List<BackgroundItemRow> _backgroundRows = new();

    // 진열 순서로 정렬한 배경 목록. 행을 다시 만들 때마다 채워 쓰는 재사용 버퍼다.
    private readonly List<ShopItemDefinition> _sortedBackgrounds = new();
    private ShopMode _mode = ShopMode.Decoration;

    private void Awake()
    {
        if (_decorationTab != null) _decorationTab.onClick.AddListener(() => SetMode(ShopMode.Decoration));
        if (_backgroundTab != null) _backgroundTab.onClick.AddListener(() => SetMode(ShopMode.Background));
    }

    private void OnEnable()
    {
        var hearts = HeartSystem.Instance;
        if (hearts != null) hearts.HeartsChanged += OnHeartsChanged;

        var bg = BackgroundSystem.Instance;
        if (bg != null)
        {
            bg.OwnedChanged += OnBackgroundStateChanged;
            bg.ActiveBackgroundChanged += OnActiveBackgroundChanged;
        }

        // 장식 보유 개수 갱신. HeartsChanged로는 부족하다 — TryBuy가 하트를 먼저 차감하므로
        // 그쪽 갱신은 개수가 올라가기 전에 돌아 옛 개수를 그린다.
        var shop = ShopSystem.Instance;
        if (shop != null) shop.OwnedChanged += OnShopOwnedChanged;

        SetMode(_mode); // 현재 모드로 (재)구성 + 상태 반영
    }

    private void OnDisable()
    {
        var hearts = HeartSystem.Instance;
        if (hearts != null) hearts.HeartsChanged -= OnHeartsChanged;

        var bg = BackgroundSystem.Instance;
        if (bg != null)
        {
            bg.OwnedChanged -= OnBackgroundStateChanged;
            bg.ActiveBackgroundChanged -= OnActiveBackgroundChanged;
        }

        var shop = ShopSystem.Instance;
        if (shop != null) shop.OwnedChanged -= OnShopOwnedChanged;
    }

    private void SetMode(ShopMode mode)
    {
        _mode = mode;
        ClearRows();
        if (mode == ShopMode.Decoration) BuildDecorationRows();
        else BuildBackgroundRows();
        UpdateTabVisuals();
        Refresh();
    }

    private void ClearRows()
    {
        _decorationRows.Clear();
        _backgroundRows.Clear();
        if (_content == null) return;
        for (int i = _content.childCount - 1; i >= 0; i--)
        {
            var child = _content.GetChild(i).gameObject;
            child.SetActive(false); // 이번 프레임 레이아웃에서 즉시 빠지도록
            Destroy(child);
        }
    }

    private void BuildDecorationRows()
    {
        if (_content == null || _decorationRowPrefab == null || _decorationSlotPrefab == null || _decorationItems == null) return;
        int i = 0;
        while (i < _decorationItems.Length)
        {
            var row = Instantiate(_decorationRowPrefab, _content);
            _decorationRows.Add(row);
            i += row.Populate(_decorationItems, i, _decorationSlotPrefab, TryPurchase);
        }
    }

    private void BuildBackgroundRows()
    {
        if (_content == null || _backgroundRowPrefab == null || _backgroundSlotPrefab == null) return;

        // 목록을 못 구하면 배경 탭이 조용히 텅 빈 채로 열린다. 원인을 찾기 어려우므로 시끄럽게 알린다.
        var system = BackgroundSystem.Instance;
        if (system == null)
        {
            Debug.LogWarning($"[{nameof(ShopPanelContentController)}] 씬에 {nameof(BackgroundSystem)}이 없어 배경 탭을 채울 수 없음.", this);
            return;
        }

        BuildSortedBackgrounds(system.AvailableBackgrounds);
        if (_sortedBackgrounds.Count == 0)
        {
            Debug.LogWarning($"[{nameof(ShopPanelContentController)}] {nameof(BackgroundSystem)}의 배경 목록이 비어 있음.", system);
            return;
        }

        int i = 0;
        while (i < _sortedBackgrounds.Count)
        {
            var row = Instantiate(_backgroundRowPrefab, _content);
            _backgroundRows.Add(row);
            i += row.Populate(_sortedBackgrounds, i, _backgroundSlotPrefab);
        }
    }

    /// <summary>
    /// 카탈로그를 진열 순서로 정렬해 <see cref="_sortedBackgrounds"/>에 담는다.
    /// 시스템이 준 순서는 쓰지 않는다 — 무엇이 있는지는 시스템이 알고, 어떤 순서로 보일지는 이 화면이 정한다.
    /// 그래서 진열 규칙을 바꿀 때 시스템이나 인스펙터를 건드릴 필요가 없다.
    ///
    /// 인스펙터에서 칸을 비워둔 채로 두면 null이 섞여 들어오므로 여기서 걸러낸다.
    /// </summary>
    private void BuildSortedBackgrounds(IReadOnlyList<ShopItemDefinition> catalog)
    {
        _sortedBackgrounds.Clear();
        if (catalog == null) return;

        for (int i = 0; i < catalog.Count; i++)
        {
            if (catalog[i] != null) _sortedBackgrounds.Add(catalog[i]);
        }
        _sortedBackgrounds.Sort(CompareForDisplay);
    }

    // 진열 규칙: 싼 것부터. 가격이 같으면 id 순으로 갈라 순서를 고정한다 —
    // List.Sort는 같은 값끼리의 원래 순서를 보장하지 않아, 기준이 가격 하나뿐이면
    // 가격이 같은 상품들의 앞뒤가 실행할 때마다 달라질 수 있다.
    // 문화권에 따라 결과가 달라지지 않도록 문자열 비교는 Ordinal로 한다.
    private static int CompareForDisplay(ShopItemDefinition a, ShopItemDefinition b)
    {
        int byPrice = a.price.CompareTo(b.price);
        return byPrice != 0 ? byPrice : string.CompareOrdinal(a.id, b.id);
    }

    private void TryPurchase(ShopItemDefinition item)
    {
        if (item == null) return;
        // 하트 차감은 ShopSystem이 소유 기록과 함께 처리한다 — 여기서 TrySpend를 직접 부르면
        // 하트만 빠져나가고 산 물건이 아무 데도 남지 않는다(배경 탭이 BackgroundSystem에 맡기는 것과 같은 구조).
        ShopSystem.Instance?.TryBuy(item);
        // 성공하면 HeartsChanged가 울려 Refresh로 이어진다. 실패(잔액 부족)면 아무 변화 없음.
    }

    private void UpdateTabVisuals()
    {
        if (_decorationTabImage != null) _decorationTabImage.color = _mode == ShopMode.Decoration ? ActiveTab : InactiveTab;
        if (_backgroundTabImage != null) _backgroundTabImage.color = _mode == ShopMode.Background ? ActiveTab : InactiveTab;
    }

    private void OnHeartsChanged(int _) => Refresh();
    private void OnShopOwnedChanged() => Refresh();
    private void OnBackgroundStateChanged() => Refresh();
    private void OnActiveBackgroundChanged(string _) => Refresh();

    private void Refresh()
    {
        int hearts = HeartSystem.Instance != null ? HeartSystem.Instance.CurrentHearts : 0;
        if (_mode == ShopMode.Decoration)
        {
            foreach (var row in _decorationRows) row.Refresh(hearts);
        }
        else
        {
            foreach (var row in _backgroundRows) row.RefreshState();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_content == null)
            Debug.LogWarning($"[{nameof(ShopPanelContentController)}] _content가 비어 있음.", this);
    }
#endif
}
