using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 패널 내용물(Content)의 두뇌. 플레이어가 가진 장식을 격자에 늘어놓고,
/// 소유가 바뀌면(장식을 새로 사면) 다시 그린다.
///
/// 칸을 어디에 놓을지는 이 스크립트가 계산하지 않는다 — <see cref="_content"/>에 붙은
/// GridLayoutGroup이 한다. 그래서 상점처럼 행 프리팹(<see cref="ShopItemRow"/>)을 두지 않는다.
/// 상점은 장식과 배경의 칸 크기·한 행에 담기는 수가 달라 '행'이라는 단위가 필요했지만,
/// 인벤토리는 모든 칸이 같은 크기라 격자 하나로 끝난다.
///
/// 지금 보여주는 것은 '산 장식 전부'다. 기획(Figma)의 인벤토리는 산 것 중에서도
/// **아직 화면에 설치하지 않은** 장식만 보여주고 '이동/회수/제거' 모드 버튼을 갖지만,
/// 장식을 화면에 설치하고 회수하는 시스템이 아직 없어 잔여 개수를 계산할 방법이 없다.
/// 그 시스템이 생기면 <see cref="BuildOwnedList"/>가 목록을 어디서 가져오는지만 바꾸면 된다.
///
/// 패널은 CanvasGroup으로 숨기므로(SetActive 아님) 다시 열려도 <see cref="OnEnable"/>이 불리지 않는다.
/// 그래서 화면을 여는 시점이 아니라 <see cref="ShopSystem.OwnedChanged"/>가 울릴 때 다시 그린다.
///
/// Area_Content에 붙인다(.claude/rules/unity/ui-panels.md).
/// </summary>
public sealed class InventoryPanelContentController : MonoBehaviour
{
    [Tooltip("칸을 만들어 넣을 부모. GridLayoutGroup이 붙어 있어야 한다(스크롤뷰의 Content).")]
    [SerializeField] private Transform _content;
    [Tooltip("칸 하나의 프리팹. InventorySlot.prefab.")]
    [SerializeField] private InventorySlot _slotPrefab;

    // 한 번 만든 칸은 부수지 않고 재사용한다. 이 게임은 바탕화면에 항상 떠 있어서, 살 때마다
    // 격자를 통째로 Instantiate/Destroy 하면 그 비용이 플레이 내내 쌓인다.
    // 남는 칸은 지우는 대신 꺼 둔다(다음에 장식을 더 사면 다시 켜서 쓴다).
    private readonly List<InventorySlot> _slots = new();

    // 이번에 그릴 장식 목록. 다시 그릴 때마다 채워 쓰는 재사용 버퍼다.
    private readonly List<ShopItemDefinition> _owned = new();

    private void OnEnable()
    {
        var shop = ShopSystem.Instance;
        if (shop != null) shop.OwnedChanged += Rebuild;

        Rebuild(); // 씬 로드 직후 현재 소유 상태를 한 번 그린다.
    }

    private void OnDisable()
    {
        var shop = ShopSystem.Instance;
        if (shop != null) shop.OwnedChanged -= Rebuild;
    }

    private void Rebuild()
    {
        if (_content == null || _slotPrefab == null) return;

        // 목록을 못 구하면 인벤토리가 조용히 텅 빈 채로 열린다. 원인을 찾기 어려우므로 시끄럽게 알린다.
        var shop = ShopSystem.Instance;
        if (shop == null)
        {
            Debug.LogWarning($"[{nameof(InventoryPanelContentController)}] 씬에 {nameof(ShopSystem)}이 없어 인벤토리를 채울 수 없음.", this);
            return;
        }

        BuildOwnedList(shop);

        for (int i = 0; i < _owned.Count; i++)
        {
            if (i == _slots.Count) _slots.Add(Instantiate(_slotPrefab, _content));

            var slot = _slots[i];
            slot.gameObject.SetActive(true);
            slot.Bind(_owned[i], shop.GetCount(_owned[i].id));
        }

        for (int i = _owned.Count; i < _slots.Count; i++) _slots[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// 가지고 있는 장식만 골라 <see cref="_owned"/>에 담고 진열 순서로 정렬한다.
    ///
    /// 기획의 정렬 규칙은 "가장 최근에 회수한 것부터 앞"인데, 회수라는 개념 자체가 아직 코드에 없어
    /// 이번에는 쓸 수 없다. 대신 상점과 같은 순서(싼 것부터, 가격이 같으면 id 순)로 둔다 —
    /// 상점에서 본 차례대로 인벤토리에도 놓이는 편이 찾기 쉽다.
    ///
    /// 상점의 정렬 함수를 가져다 쓰지 않고 같은 규칙을 여기 다시 적었다. 두 화면이 하나의 정렬
    /// 함수를 공유하면 한쪽의 진열 규칙을 바꿀 때 다른 쪽이 같이 끌려가는데,
    /// 두 화면은 서로 다른 것을 보여주므로 각자 자기 순서를 정하는 쪽이 맞다.
    /// </summary>
    private void BuildOwnedList(ShopSystem shop)
    {
        _owned.Clear();

        var catalog = shop.AvailableDecorations;
        if (catalog == null) return;

        for (int i = 0; i < catalog.Count; i++)
        {
            // 인스펙터에서 칸을 비워둔 채로 두면 null이 섞여 들어오므로 여기서 걸러낸다.
            var item = catalog[i];
            if (item != null && shop.IsOwned(item.id)) _owned.Add(item);
        }

        _owned.Sort(CompareForDisplay);
    }

    // 문화권에 따라 결과가 달라지지 않도록 문자열 비교는 Ordinal로 한다.
    private static int CompareForDisplay(ShopItemDefinition a, ShopItemDefinition b)
    {
        int byPrice = a.price.CompareTo(b.price);
        return byPrice != 0 ? byPrice : string.CompareOrdinal(a.id, b.id);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_content == null || _slotPrefab == null)
            Debug.LogWarning($"[{nameof(InventoryPanelContentController)}] _content 또는 _slotPrefab이 비어 있음.", this);
    }
#endif
}
