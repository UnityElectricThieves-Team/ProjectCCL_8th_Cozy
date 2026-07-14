using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 진열의 한 '행'. 상품 슬롯을 가로로(HorizontalLayoutGroup) 최대 몇 칸까지 담는 얇은 컨테이너다.
/// <see cref="ShopPanelContentController"/>가 상품을 행 단위로 잘라 이 행에 넘기면,
/// 행은 자기 아래에 슬롯을 만들어 채우고, 이후 구매 가능 갱신을 자기 슬롯들에 전파한다.
///
/// 화면 밖 행을 재활용하지 않는(오브젝트 풀링 없는) 단순 추상이다 — 상점 규모가 작아
/// 한 번 만들어 두고 쓰면 충분하다. 큰 리스트를 다루게 되면 그때 진짜 가상화로 바꾼다.
/// 행 프리팹 루트에 붙는다.
/// </summary>
[RequireComponent(typeof(HorizontalLayoutGroup))]
public sealed class ShopItemRow : MonoBehaviour
{
    [Tooltip("이 행이 담을 수 있는 슬롯(상품) 최대 개수. Figma 기준 3.")]
    [SerializeField, Min(1)] private int _capacity = 3;

    private readonly List<ShopItemSlot> _slots = new();

    /// <summary>이 행이 담을 수 있는 슬롯 최대 개수.</summary>
    public int Capacity => _capacity;

    /// <summary>
    /// <paramref name="items"/>의 <paramref name="start"/>부터 최대 <see cref="Capacity"/>개를 이 행에 채우고,
    /// 실제로 채운 개수를 반환한다. 상품 하나마다 <paramref name="slotPrefab"/>을 복제해 이 행 아래에 두고 bind한다.
    /// 상품이 <see cref="Capacity"/>보다 적은 마지막 행은 남는 칸을 보이지 않는 빈 슬롯으로 메워
    /// 항상 <see cref="Capacity"/>칸을 유지한다 — 그래야 상품이 왼쪽 열부터 어긋나지 않게 정렬된다.
    /// </summary>
    public int Populate(ShopItemDefinition[] items, int start,
                        ShopItemSlot slotPrefab, Action<ShopItemDefinition> onBuy)
    {
        int count = Mathf.Min(_capacity, items.Length - start);
        for (int k = 0; k < count; k++)
        {
            var slot = Instantiate(slotPrefab, transform);
            slot.Bind(items[start + k], onBuy);
            _slots.Add(slot);
        }

        // 남는 칸은 슬롯과 같은 크기의 빈 자리로 채운다(같은 프리팹을 복제해 투명하게 숨김).
        // 크기를 슬롯과 동일하게 맞춰야 열 폭이 어긋나지 않는다. 빈 슬롯은 _slots에 넣지 않아
        // 구매 가능 갱신 대상에서 제외된다.
        for (int k = count; k < _capacity; k++)
        {
            var filler = Instantiate(slotPrefab, transform);
            if (!filler.TryGetComponent(out CanvasGroup group))
                group = filler.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        return count;
    }

    /// <summary>이 행의 모든 슬롯에 대해 현재 하트로 살 수 있는지 갱신한다.</summary>
    public void RefreshAffordability(int currentHearts)
    {
        foreach (var slot in _slots)
            slot.SetAffordable(currentHearts >= slot.Price);
    }
}
