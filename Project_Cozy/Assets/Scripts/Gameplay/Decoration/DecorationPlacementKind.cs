/// <summary>
/// 장식을 어디에 놓을 수 있는가. 상품 정의(<c>ShopItemDefinition.placementKind</c>)가 고르고, 배치 모드와 장식 레이어가 읽는다.
/// Figma: 바닥 장식물은 바닥에 붙은 채로 움직이고, 바닥 전용이 아닌 장식물은 우측 메뉴창을 뺀 어디로든 움직이고 설치할 수 있다.
///
/// 상점(Contents)이 아니라 여기(Gameplay)에 두는 것은 배치 모드·레이어가 이 값을 알아야 하는데 Gameplay 장식 코드는 상점을 모르기 때문이다.
/// 상품 에셋에는 정수로 저장되므로 값을 숫자로 못 박는다 — 순서를 바꾸거나 중간에 끼우면 이미 고른 상품의 종류가 바뀐다.
/// </summary>
public enum DecorationPlacementKind
{
    /// <summary>바닥에 붙은 채로 움직인다.</summary>
    Floor = 0,

    /// <summary>우측 메뉴창을 뺀 어디로든. 그림 아래 끝은 지면 밑으로 내려가지 않는다.</summary>
    Anywhere = 1,
}
