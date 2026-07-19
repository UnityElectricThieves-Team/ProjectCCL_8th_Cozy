using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 '배경' 진열의 한 행. 배경 슬롯을 가로로 최대 <see cref="_capacity"/>개(Figma 기준 2) 담는다.
/// 장식용 <see cref="ShopItemRow"/>와 구조는 같지만 슬롯 타입(<see cref="BackgroundItemSlot"/>)이 달라
/// 별도로 둔다. 남는 칸은 보이지 않는 빈 슬롯으로 메워 항상 <see cref="_capacity"/>칸을 유지한다.
/// 배경 행 프리팹 루트에 붙는다.
/// </summary>
[RequireComponent(typeof(HorizontalLayoutGroup))]
public sealed class BackgroundItemRow : MonoBehaviour
{
    [Tooltip("이 행이 담을 수 있는 슬롯 최대 개수. 배경은 Figma 기준 2.")]
    [SerializeField, Min(1)] private int _capacity = 2;

    private readonly List<BackgroundItemSlot> _slots = new();

    public int Capacity => _capacity;

    /// <summary>
    /// <paramref name="items"/>의 <paramref name="start"/>부터 최대 <see cref="Capacity"/>개를 채우고 실제 채운 수를 반환한다.
    /// 부족한 마지막 칸은 투명한 빈 슬롯으로 메워 열 정렬을 유지한다.
    /// </summary>
    public int Populate(IReadOnlyList<ShopItemDefinition> items, int start, BackgroundItemSlot slotPrefab)
    {
        int count = Mathf.Min(_capacity, items.Count - start);
        for (int k = 0; k < count; k++)
        {
            var slot = Instantiate(slotPrefab, transform);
            slot.Bind(items[start + k]);
            _slots.Add(slot);
        }

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

    /// <summary>이 행의 모든 슬롯을 현재 구매/사용/잔액 상태로 다시 그린다.</summary>
    public void RefreshState()
    {
        foreach (var slot in _slots)
            slot.RefreshState();
    }
}
