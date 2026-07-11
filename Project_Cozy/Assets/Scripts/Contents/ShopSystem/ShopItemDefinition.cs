using UnityEngine;

/// <summary>
/// 상점에 진열되는 상품 하나의 "정의". 게임 내내 바뀌지 않는 카탈로그 데이터만 담는다 —
/// 저장·조회에 쓰는 안정적 <see cref="id"/>, 표시 이름, 아이콘, 가격(하트).
///
/// 구매 여부 같은 플레이어별 런타임 상태는 여기 담지 않는다. ScriptableObject는 전역 공유 에셋이라
/// 플레이어마다 다른 값을 담을 수 없고, 에디터에서는 그 변경이 .asset 파일에 눌러앉기 때문이다.
/// "누가 무엇을 샀는가"는 별도 런타임 상태(산 id 집합)가 들고, 이 정의는 순수하게 읽기 전용으로 둔다.
///
/// 아이템 하나 = .asset 파일 하나. 우클릭 Create → Cozy/Shop/Shop Item.
/// </summary>
[CreateAssetMenu(menuName = "Cozy/Shop/Shop Item")]
public class ShopItemDefinition : ScriptableObject
{
    [Tooltip("저장·조회에 쓰는 안정적 식별자. 예: shop_item_1_flowerpot. 한번 정하면 바꾸지 않는다.")]
    public string id;

    [Tooltip("슬롯에 표시할 상품 이름 (예: 화분, 우체통).")]
    public string displayName;

    [Tooltip("상품 아이콘(png import된 Sprite).")]
    public Sprite icon;

    [Tooltip("구매에 드는 하트 수.")]
    public int price;
}
