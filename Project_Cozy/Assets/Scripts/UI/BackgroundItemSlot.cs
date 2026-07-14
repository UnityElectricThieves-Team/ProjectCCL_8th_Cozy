using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 '배경' 탭의 카드 한 칸. 넓은 미리보기 + 이름 + 상태에 따라 바뀌는 버튼 하나를 보여준다.
/// 버튼은 Figma 기획대로 3상태다:
/// - 미보유 → "구매"(가격+하트, 살 수 있으면 활성 스프라이트/없으면 불가 스프라이트)
/// - 보유·미사용 → "사용"
/// - 보유·사용중 → "사용 취소"
///
/// 실제 상태는 <see cref="BackgroundSystem"/>가 들고, 이 슬롯은 표시와 클릭 전달만 한다.
/// 상태가 바뀌면(구매/사용/잔액 변화) 이벤트를 받아 <see cref="RefreshState"/>로 다시 그린다.
/// 배경 슬롯 프리팹 루트에 붙는다.
/// </summary>
public sealed class BackgroundItemSlot : MonoBehaviour
{
    [SerializeField] private Image _preview;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Button _button;
    [SerializeField] private Image _buttonImage;
    [SerializeField] private TMP_Text _buttonLabel;

    [Tooltip("구매 상태에서만 보이는 하트 아이콘(사용/사용취소 때는 숨김).")]
    [SerializeField] private GameObject _heartIcon;

    [Tooltip("살 수 있을 때 버튼 배경 스프라이트.")]
    [SerializeField] private Sprite _buyableSprite;

    [Tooltip("잔액이 모자랄 때 버튼 배경 스프라이트.")]
    [SerializeField] private Sprite _notBuyableSprite;

    // Figma: 구매 가능=진한 글씨, 불가=회색(#898989).
    private static readonly Color AffordableColor = new(0.1f, 0.1f, 0.1f);
    private static readonly Color UnaffordableColor = new(0.537f, 0.537f, 0.537f);

    private ShopItemDefinition _item;

    /// <summary>슬롯을 배경 상품 하나로 채운다.</summary>
    public void Bind(ShopItemDefinition item)
    {
        _item = item;
        if (_nameText != null) _nameText.text = item.displayName;
        if (_preview != null && item.icon != null) _preview.sprite = item.icon;

        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked); // 재바인딩 시 중복 방지
            _button.onClick.AddListener(OnButtonClicked);
        }
        RefreshState();
    }

    /// <summary>보유·사용·잔액 상태에 맞춰 버튼 라벨/스프라이트/하트 표시를 갱신한다.</summary>
    public void RefreshState()
    {
        if (_item == null) return;
        var bg = BackgroundSystem.Instance;
        int hearts = HeartSystem.Instance != null ? HeartSystem.Instance.CurrentHearts : 0;

        bool owned = bg != null && bg.IsOwned(_item.id);
        bool active = bg != null && bg.IsActive(_item.id);

        if (!owned)
        {
            bool affordable = hearts >= _item.price;
            SetLabel(_item.price.ToString(), affordable ? AffordableColor : UnaffordableColor);
            if (_heartIcon != null) _heartIcon.SetActive(true);
            if (_buttonImage != null && (_buyableSprite != null || _notBuyableSprite != null))
                _buttonImage.sprite = affordable ? _buyableSprite : _notBuyableSprite;
        }
        else
        {
            SetLabel(active ? "사용 취소" : "사용", AffordableColor);
            if (_heartIcon != null) _heartIcon.SetActive(false);
            if (_buttonImage != null && _buyableSprite != null)
                _buttonImage.sprite = _buyableSprite; // 사용/사용취소는 활성 스프라이트 재사용
        }
    }

    private void SetLabel(string text, Color color)
    {
        if (_buttonLabel == null) return;
        _buttonLabel.text = text;
        _buttonLabel.color = color;
    }

    // 현재 상태를 읽어 알맞은 동작 하나를 수행한다. 결과(구매/사용/해제)는 이벤트로 슬롯들에 되돌아온다.
    private void OnButtonClicked()
    {
        var bg = BackgroundSystem.Instance;
        if (bg == null || _item == null) return;

        if (!bg.IsOwned(_item.id)) bg.TryBuy(_item);
        else if (!bg.IsActive(_item.id)) bg.Use(_item.id);
        else bg.CancelUse(_item.id);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_preview == null || _nameText == null || _button == null || _buttonLabel == null)
            Debug.LogWarning($"[{nameof(BackgroundItemSlot)}] 참조(_preview/_nameText/_button/_buttonLabel)가 비어 있음.", this);
    }
#endif
}
