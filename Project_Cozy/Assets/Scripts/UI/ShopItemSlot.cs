using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 상점 '장식' 슬롯 한 칸의 표시. 상품 아이콘·이름·가격을 채우고, 구매 버튼 클릭을 바깥(컨트롤러)으로 넘긴다.
/// 실제 하트 차감은 <see cref="ShopPanelContentController"/>가 하고, 이 슬롯은 구매 가능/불가능 시각만 책임진다.
/// 인스펙터 필드 구성은 <see cref="BackgroundItemSlot"/>과 동일하게 맞춰 두 슬롯을 일관되게 둔다.
/// 슬롯 프리팹 루트에 붙는다.
/// </summary>
public sealed class ShopItemSlot : MonoBehaviour
{
    [FormerlySerializedAs("_icon")]
    [SerializeField] private Image _preview;
    [SerializeField] private TMP_Text _nameText;
    [FormerlySerializedAs("_buyButton")]
    [SerializeField] private Button _button;
    [Tooltip("구매 버튼 배경 이미지. 살 수 있는지에 따라 스프라이트를 바꾼다.")]
    [SerializeField] private Image _buttonImage;
    [FormerlySerializedAs("_priceText")]
    [SerializeField] private TMP_Text _buttonLabel;
    [Tooltip("가격 옆 하트 아이콘. 장식은 항상 표시한다(참조만).")]
    [SerializeField] private GameObject _heartIcon;
    [Tooltip("살 수 있을 때 버튼 스프라이트.")]
    [SerializeField] private Sprite _buyableSprite;
    [Tooltip("잔액이 모자랄 때 버튼 스프라이트.")]
    [SerializeField] private Sprite _notBuyableSprite;

    // Figma: 구매 가능=진한 글씨, 구매 불가능=회색(#898989).
    private static readonly Color AffordableColor = new(0.1f, 0.1f, 0.1f);
    private static readonly Color UnaffordableColor = new(0.537f, 0.537f, 0.537f);

    private ShopItemDefinition _item;
    private Action<ShopItemDefinition> _onBuy;

    /// <summary>슬롯을 상품 하나로 채운다. 구매 버튼을 누르면 onBuy(item)을 부른다.</summary>
    public void Bind(ShopItemDefinition item, Action<ShopItemDefinition> onBuy)
    {
        _item = item;
        _onBuy = onBuy;

        if (_nameText != null) _nameText.text = item.displayName;
        if (_buttonLabel != null) _buttonLabel.text = item.price.ToString();
        if (_preview != null && item.icon != null) _preview.sprite = item.icon;

        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleBuyClicked); // 재바인딩 시 중복 방지
            _button.onClick.AddListener(HandleBuyClicked);
        }
    }

    /// <summary>구매 가능 여부에 따라 가격 글씨색과 버튼 스프라이트(구매가능/불가)를 바꾼다.</summary>
    public void SetAffordable(bool affordable)
    {
        if (_buttonLabel != null) _buttonLabel.color = affordable ? AffordableColor : UnaffordableColor;
        if (_buttonImage != null && (_buyableSprite != null || _notBuyableSprite != null))
            _buttonImage.sprite = affordable ? _buyableSprite : _notBuyableSprite;
    }

    /// <summary>이 슬롯이 표시 중인 상품 가격(컨트롤러가 구매 가능 여부 계산에 쓴다).</summary>
    public int Price => _item != null ? _item.price : 0;

    private void HandleBuyClicked() => _onBuy?.Invoke(_item);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_nameText == null || _buttonLabel == null || _button == null)
            Debug.LogWarning($"[{nameof(ShopItemSlot)}] 슬롯 참조(_nameText/_buttonLabel/_button)가 비어 있음.", this);
    }
#endif
}
