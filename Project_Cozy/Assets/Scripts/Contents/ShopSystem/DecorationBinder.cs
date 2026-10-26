using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="ShopSystem"/>에 저장된 "놓인 장식" 목록을 <see cref="DecorationLayer"/>에 이어 주는 얇은 바인더.
/// 상품 id를 장식 프리팹으로 바꿔 넘긴다. 지금 상점 목록에 없는 id나 프리팹이 비어 있는 상품은 건너뛴다 —
/// 저장 데이터는 지우지 않으므로, 그 상품이 다시 생기면 저절로 다시 보인다.
///
/// 레이어(<see cref="DecorationLayer"/>)는 상점을 모르고 그리기만 책임진다. 상점 쪽 상태를 아는 것은 이 컴포넌트뿐이라,
/// 배경의 <see cref="BackgroundBinder"/>와 같은 자리다.
///
/// 부팅 복원은 <see cref="ShopSystem"/>이 이벤트를 쏘지 않으므로, Start에서 현재 목록을 한 번 직접 읽는다.
/// 장식 레이어 프리팹 루트에 <see cref="DecorationLayer"/>와 함께 붙는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DecorationLayer))]
public sealed class DecorationBinder : MonoBehaviour
{
    private readonly List<PlacedDecoration> _buffer = new();

    private DecorationLayer _layer;
    private ShopSystem _system;

    private void Awake() => _layer = GetComponent<DecorationLayer>();

    private void Start()
    {
        // ShopSystem은 실행 순서 -100이라 이 시점엔 Awake(저장 파일 복원)를 마쳤다.
        _system = ShopSystem.Instance;
        if (_system == null)
        {
            Debug.LogWarning($"[{nameof(DecorationBinder)}] 씬에 {nameof(ShopSystem)}이 없어 장식을 연결하지 못했습니다.", this);
            enabled = false;
            return;
        }

        _system.OwnedChanged += Apply;
        Apply();
    }

    private void OnDestroy()
    {
        if (_system != null) _system.OwnedChanged -= Apply;
    }

    private void Apply()
    {
        _buffer.Clear();
        IReadOnlyList<PlacedDecorationData> placed = _system.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            ShopItemDefinition item = FindItem(placed[i].itemId);
            if (item == null || item.decorationPrefab == null) continue;
            _buffer.Add(new PlacedDecoration(item.decorationPrefab, placed[i].x));
        }
        _layer.SetDecorations(_buffer);
    }

    // 상품이 수십 개 수준이고 목록이 바뀔 때만 불리므로 선형 탐색으로 충분하다.
    private ShopItemDefinition FindItem(string id)
    {
        IReadOnlyList<ShopItemDefinition> items = _system.AvailableDecorations;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].id == id) return items[i];
        }
        return null;
    }
}
