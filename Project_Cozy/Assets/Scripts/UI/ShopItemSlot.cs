using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 슬롯 한 칸의 표시. 상품 아이콘·이름·가격을 채우고, 구매 버튼 클릭을 바깥(컨트롤러)으로 넘긴다.
/// 실제 하트 차감은 <see cref="ShopPanelContentController"/>가 하고, 이 슬롯은 구매 가능/불가능 시각만 책임진다.
/// 슬롯 프리팹 루트에 붙는다.
/// </summary>
public sealed class ShopItemSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private Button _buyButton;

    // Figma: 구매 가능=진한 글씨, 구매 불가능=회색(#898989).
    private static readonly Color AffordableColor = new(0.1f, 0.1f, 0.1f);
    private static readonly Color UnaffordableColor = new(0.537f, 0.537f, 0.537f);

    private ShopItem _item;
    private Action<ShopItem> _onBuy;

    /// <summary>슬롯을 상품 하나로 채운다. 구매 버튼을 누르면 onBuy(item)을 부른다.</summary>
    public void Bind(ShopItem item, Action<ShopItem> onBuy)
    {
        _item = item;
        _onBuy = onBuy;

        if (_nameText != null) _nameText.text = item.displayName;
        if (_priceText != null) _priceText.text = item.price.ToString();
        if (_icon != null && item.icon != null) _icon.sprite = item.icon;

        if (_buyButton != null)
        {
            _buyButton.onClick.RemoveListener(HandleBuyClicked); // 재바인딩 시 중복 방지
            _buyButton.onClick.AddListener(HandleBuyClicked);
        }
    }

    /// <summary>구매 가능 여부에 따라 버튼 활성/가격 글씨색을 바꾼다.</summary>
    public void SetAffordable(bool affordable)
    {
        if (_buyButton != null) _buyButton.interactable = affordable;
        if (_priceText != null) _priceText.color = affordable ? AffordableColor : UnaffordableColor;
    }

    /// <summary>이 슬롯이 표시 중인 상품 가격(컨트롤러가 구매 가능 여부 계산에 쓴다).</summary>
    public int Price => _item != null ? _item.price : 0;

    private void HandleBuyClicked() => _onBuy?.Invoke(_item);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_nameText == null || _priceText == null || _buyButton == null)
            Debug.LogWarning($"[{nameof(ShopItemSlot)}] 슬롯 참조(_nameText/_priceText/_buyButton)가 비어 있음.", this);
    }
#endif
}
