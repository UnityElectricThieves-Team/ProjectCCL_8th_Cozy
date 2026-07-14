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
    [SerializeField] private ShopItemDefinition[] _backgroundItems;

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
        if (_content == null || _backgroundRowPrefab == null || _backgroundSlotPrefab == null || _backgroundItems == null) return;
        int i = 0;
        while (i < _backgroundItems.Length)
        {
            var row = Instantiate(_backgroundRowPrefab, _content);
            _backgroundRows.Add(row);
            i += row.Populate(_backgroundItems, i, _backgroundSlotPrefab);
        }
    }

    private void TryPurchase(ShopItemDefinition item)
    {
        if (item == null) return;
        HeartSystem.Instance?.TrySpend(item.price);
        // 성공하면 HeartsChanged가 울려 Refresh로 이어진다. 실패(잔액 부족)면 아무 변화 없음.
    }

    private void UpdateTabVisuals()
    {
        if (_decorationTabImage != null) _decorationTabImage.color = _mode == ShopMode.Decoration ? ActiveTab : InactiveTab;
        if (_backgroundTabImage != null) _backgroundTabImage.color = _mode == ShopMode.Background ? ActiveTab : InactiveTab;
    }

    private void OnHeartsChanged(int _) => Refresh();
    private void OnBackgroundStateChanged() => Refresh();
    private void OnActiveBackgroundChanged(string _) => Refresh();

    private void Refresh()
    {
        int hearts = HeartSystem.Instance != null ? HeartSystem.Instance.CurrentHearts : 0;
        if (_mode == ShopMode.Decoration)
        {
            foreach (var row in _decorationRows) row.RefreshAffordability(hearts);
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
