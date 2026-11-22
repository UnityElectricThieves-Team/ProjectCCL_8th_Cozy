using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 상점 '장식' 슬롯 한 칸의 표시. 상품 아이콘·이름·가격을 채우고, 구매 버튼 클릭을 바깥(컨트롤러)으로 넘긴다.
/// 하트 차감과 소유 기록은 <see cref="ShopSystem"/>가 한 묶음으로 처리하고,
/// 이 슬롯은 구매 가능/불가능 시각과 보유 개수 표시만 책임진다.
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
    private LocalizationManager _localization;
    private float _previewMaxSide; // 프리팹이 정한 그림 칸에 들어가는 정사각형의 한 변

    // 상품 이름이 언어를 따르므로, 언어가 바뀌면 이름(과 개수)을 다시 그린다.
    private void Awake()
    {
        if (_preview != null)
        {
            Rect frame = _preview.rectTransform.rect;
            _previewMaxSide = Mathf.Min(frame.width, frame.height);
        }

        _localization = LocalizationManager.Instance;
        if (_localization != null) _localization.LanguageChanged += RefreshOwned;
    }

    private void OnDestroy()
    {
        if (_localization != null) _localization.LanguageChanged -= RefreshOwned;
    }

    /// <summary>슬롯을 상품 하나로 채운다. 구매 버튼을 누르면 onBuy(item)을 부른다.</summary>
    public void Bind(ShopItemDefinition item, Action<ShopItemDefinition> onBuy)
    {
        _item = item;
        _onBuy = onBuy;

        RefreshOwned();
        if (_buttonLabel != null) _buttonLabel.text = item.price.ToString();
        if (_preview != null && item.icon != null) _preview.sprite = item.icon;
        ApplyRealSize(item);

        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleBuyClicked); // 재바인딩 시 중복 방지
            _button.onClick.AddListener(HandleBuyClicked);
        }
    }

    /// <summary>
    /// 그림을 화면에 놓였을 때와 같은 크기(1:1)로 줄인다. 칸보다 크면 칸에 맞춘다.
    /// UI 1px가 베이스 공간 1px와 같아서(CanvasScaler 기준 폭 = 마스터 캔버스 폭) 장식의 px 크기를 그대로 쓴다.
    /// 썸네일은 정사각형에 물체의 긴 변이 꽉 차게 그려져 있으므로, 정사각형 한 변을 실제 긴 변으로 둔다.
    /// 크기는 <see cref="DecorationBinder"/>가 놓인 장식의 크기를 판정하는 식과 같다 — 한쪽을 바꾸면 다른 쪽도 맞춘다.
    /// 피벗은 프리팹이 가운데로 두어, 칸 가운데를 중심으로 줄어든다.
    /// 그림이나 장식 프리팹이 없으면 건드리지 않아 지금처럼 칸을 채운다.
    /// </summary>
    private void ApplyRealSize(ShopItemDefinition item)
    {
        if (_preview == null || item.icon == null || item.decorationPrefab == null) return;
        SpriteRenderer renderer = item.decorationPrefab.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return;

        Vector3 size = renderer.sprite.bounds.size;
        Vector3 scale = renderer.transform.lossyScale;
        float longSidePx = Mathf.Max(size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.y))
                           * BaseSpaceCameraFitter.PIXELS_PER_UNIT;
        float side = Mathf.Min(longSidePx, _previewMaxSide);

        RectTransform rt = _preview.rectTransform;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, side);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, side);
    }

    /// <summary>구매 가능 여부에 따라 가격 글씨색과 버튼 스프라이트(구매가능/불가)를 바꾼다.</summary>
    public void SetAffordable(bool affordable)
    {
        if (_buttonLabel != null) _buttonLabel.color = affordable ? AffordableColor : UnaffordableColor;
        if (_buttonImage != null && (_buyableSprite != null || _notBuyableSprite != null))
            _buttonImage.sprite = affordable ? _buyableSprite : _notBuyableSprite;
    }

    /// <summary>
    /// 보유 개수를 이름 옆에 붙여 다시 그린다(하나라도 있으면 "쿠션 x1"). 없으면 이름만.
    /// 1개부터 표시하는 이유는, 처음 산 순간에 화면이 그대로면 구매가 됐는지 알 수 없기 때문이다.
    /// 장식은 같은 걸 여러 개 살 수 있어서 "보유/미보유"가 아니라 개수를 보여준다.
    ///
    /// 개수를 위한 별도 라벨을 두지 않고 이름 텍스트를 재구성하는 이유는, 슬롯 프리팹에
    /// 새 오브젝트를 추가하지 않고 표시를 붙이기 위해서다. Figma에 개수 자리가 정해지면 그때 옮긴다.
    /// 이름 뒤에 개수를 이어 붙이지 않고 틀 문장(<c>UIShop.item_count</c>) 하나로 채운다 — 언어마다 어순이 다를 수 있다.
    /// 항상 번역 표의 이름에서 다시 만든다 — 현재 텍스트에 덧붙이면 갱신할 때마다 "x2 x3"로 쌓인다.
    /// </summary>
    public void RefreshOwned()
    {
        if (_nameText == null || _item == null) return;
        int count = ShopSystem.Instance != null ? ShopSystem.Instance.GetCount(_item.id) : 0;
        if (count > 0)
            LocalizationManager.SetText(_nameText, "UIShop.item_count",
                ("NAME", LocalizationManager.Localize(_item.NameKey)), ("ITEM_COUNT", count));
        else
            LocalizationManager.SetText(_nameText, _item.NameKey);
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
