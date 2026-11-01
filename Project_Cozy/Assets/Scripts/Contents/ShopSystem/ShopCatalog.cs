using UnityEngine;

/// <summary>
/// 게임에 존재하는 모든 상점 상품 목록. 장식은 <see cref="ShopSystem"/>이, 배경은 <see cref="BackgroundSystem"/>이 이 에셋 하나를 읽는다.
///
/// 목록을 씬의 두 시스템 인스펙터에 직접 두지 않는 이유: 상품이 수십 개라, 하나 늘 때마다 <c>GameScene.unity</c>가 바뀌면
/// 씬을 얇게 쓰는 원칙(루트 CLAUDE.md §2)이 깨진다. 이 에셋으로 빼면 씬은 이 에셋을 가리키기만 하고, 상품 추가는 에셋에서 끝난다.
///
/// 여기 늘어놓은 순서는 상점 진열 순서와 무관하다 — 진열 순서는 상점이 따로 정한다.
/// 아트 폴더에서 상품을 한꺼번에 만들어 이 목록에 넣는 메뉴는 <c>Editor/ShopItemGenerator.cs</c>에 있다.
/// </summary>
[CreateAssetMenu(menuName = "Cozy/Shop/Shop Catalog")]
public sealed class ShopCatalog : ScriptableObject
{
    [Tooltip("게임에 존재하는 모든 장식 상품.")]
    public ShopItemDefinition[] decorations;

    [Tooltip("게임에 존재하는 모든 배경 상품.")]
    public ShopItemDefinition[] backgrounds;

#if UNITY_EDITOR
    // 상품인데 화면에 놓일 프리팹이 비어 있으면, 사거나 사용해도 화면에 아무것도 안 그려지고
    // 어디서도 에러가 나지 않는다. 상품을 새로 넣을 때 빠뜨린 것을 여기서 시끄럽게 알린다.
    private void OnValidate()
    {
        if (decorations != null)
        {
            for (int i = 0; i < decorations.Length; i++)
            {
                var item = decorations[i];
                if (item != null && item.decorationPrefab == null)
                    Debug.LogWarning($"[{nameof(ShopCatalog)}] 장식 '{item.id}'의 decorationPrefab이 비어 있음 — 설치해도 화면에 아무것도 안 그려진다.", item);
            }
        }
        if (backgrounds != null)
        {
            for (int i = 0; i < backgrounds.Length; i++)
            {
                var item = backgrounds[i];
                if (item != null && item.backgroundPrefab == null)
                    Debug.LogWarning($"[{nameof(ShopCatalog)}] 배경 '{item.id}'의 backgroundPrefab이 비어 있음 — 사용해도 화면에 아무것도 안 그려진다.", item);
            }
        }
    }
#endif
}
