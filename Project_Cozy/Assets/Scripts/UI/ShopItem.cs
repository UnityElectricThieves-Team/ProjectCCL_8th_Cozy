using System;
using UnityEngine;

/// <summary>
/// 상점에 진열되는 상품 하나의 데이터. 표시 이름·가격(하트)·그림(png import된 Sprite)을 든다.
/// 그림은 비워도(None) 되며, 그때 슬롯은 프리팹의 플레이스홀더를 그대로 보여준다.
/// 구매 후 처리(인벤토리 편입·장식 배치 등)는 상점 밖 책임이라 여기 담지 않는다.
/// <see cref="ShopPanelContentController"/>가 인스펙터에서 배열로 들고, 슬롯으로 펼친다.
/// </summary>
[Serializable]
public class ShopItem
{
    [Tooltip("슬롯에 표시할 상품 이름 (예: 화분, 우체통).")]
    public string displayName;

    [Tooltip("구매에 드는 하트 수.")]
    public int price;

    [Tooltip("상품 그림(png). 비워두면(None) 슬롯은 플레이스홀더 그대로 둔다.")]
    public Sprite icon;
}
