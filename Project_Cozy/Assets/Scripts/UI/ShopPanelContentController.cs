using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 패널 내용물(Content)의 두뇌. 진열 상품 목록으로 슬롯들을 <see cref="_content"/> 아래에 만들고,
/// 하트 보유량이 바뀔 때마다 각 슬롯의 구매 가능/불가능을 갱신한다.
/// 구매는 <see cref="HeartSystem.TrySpend"/>로 처리 — 잔액이 모자라면 아무 일도 없다.
///
/// 패널 루트에 붙는다(내용물 로직이 있는 화면에만 컨트롤러를 둔다 — 여닫기는 <see cref="UIPanel"/> 담당).
/// 패널은 CanvasGroup으로 숨기므로(SetActive 아님) 열고 닫아도 이 컴포넌트는 계속 살아 있다.
/// 구독은 <see cref="OnEnable"/>/<see cref="OnDisable"/>(생성·파괴 시점)에서 걸고 푼다.
/// </summary>
public sealed class ShopPanelContentController : MonoBehaviour
{
    [Tooltip("슬롯을 만들어 넣을 부모. Base 프리팹의 Content(스크롤을 붙였다면 ScrollRect의 Content).")]
    [SerializeField] private Transform _content;

    [Tooltip("복제해서 각 상품 슬롯으로 쓸 ShopItemSlot 프리팹.")]
    [SerializeField] private ShopItemSlot _slotPrefab;

    [Tooltip("진열할 상품들. 배열 순서대로 슬롯이 만들어진다.")]
    [SerializeField] private ShopItem[] _items;

    private readonly List<ShopItemSlot> _slots = new();

    private void Awake()
    {
        BuildSlots();
    }

    private void OnEnable()
    {
        var hearts = HeartSystem.Instance;
        if (hearts == null) return; // 씬에 HeartSystem이 없으면 조용히 넘어간다

        hearts.HeartsChanged += OnHeartsChanged;
        RefreshAffordability(hearts.CurrentHearts); // 현재 잔액으로 초기 상태 맞춤
    }

    private void OnDisable()
    {
        if (HeartSystem.Instance != null)
            HeartSystem.Instance.HeartsChanged -= OnHeartsChanged;
    }

    private void BuildSlots()
    {
        if (_content == null || _slotPrefab == null || _items == null) return;

        foreach (var item in _items)
        {
            var slot = Instantiate(_slotPrefab, _content);
            slot.Bind(item, TryPurchase);
            _slots.Add(slot);
        }
    }

    private void TryPurchase(ShopItem item)
    {
        if (item == null) return;
        HeartSystem.Instance?.TrySpend(item.price);
        // 성공하면 HeartsChanged가 울려 OnHeartsChanged → RefreshAffordability로 이어진다.
        // 실패(잔액 부족)면 TrySpend가 false만 반환하고 아무 변화 없음.
    }

    private void OnHeartsChanged(int currentHearts) => RefreshAffordability(currentHearts);

    private void RefreshAffordability(int currentHearts)
    {
        foreach (var slot in _slots)
            slot.SetAffordable(currentHearts >= slot.Price);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_content == null || _slotPrefab == null)
            Debug.LogWarning($"[{nameof(ShopPanelContentController)}] _content 또는 _slotPrefab이 비어 있음.", this);
    }
#endif
}
