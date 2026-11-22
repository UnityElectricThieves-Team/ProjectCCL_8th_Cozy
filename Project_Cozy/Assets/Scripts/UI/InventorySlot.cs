using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 격자의 한 칸. 장식 하나의 그림과 보유 개수를 보여 주고, 눌리면 그 장식을 알린다.
///
/// 칸을 누르는 것은 장식 이동·제거 모드에서만 뜻이 있다(Figma 이동/회수/제거 모드 "인벤토리 장식 좌클릭").
/// 모드 밖에서 눌러도 받는 쪽(<see cref="InventoryModeController"/>)이 무시한다. 슬롯은 무엇을 할지 판단하지 않는다.
/// 루트 Button의 OnClick을 인스펙터로 <see cref="OnClicked"/>에 걸고, 알릴 곳은 <see cref="Bind"/>로 받는다(<see cref="ShopItemSlot"/>과 같은 방식).
///
/// 모드 중 커서를 올리면 그림만 붉게 어두워진다(#640000 + 투명도 30%). 같은 그림을 그 색·알파로 위에 덮는 자식 Image(<see cref="_hoverTint"/>)다 —
/// 불투명 픽셀 위의 알파 0.3 덮기는 0.3 비율 섞기와 같아 셰이더가 필요 없고, 슬롯 바탕이 불투명해 반투명이 옅게 나오는 문제도 없다.
/// 색은 "포인터가 안에 있음 && 슬롯 입력 허용"으로 값이 바뀔 때마다 다시 계산한다. 포인터가 머문 채 모드가 끝나거나
/// 팝업이 뜨거나 칸이 재사용되면 enter/exit가 오지 않으므로, 들어올 때 한 번만 판단하면 색이 남거나 사라진다.
/// 허용 여부는 슬롯 클릭을 받는 <see cref="InventoryModeController"/>가 정해 알린다.
///
/// 슬롯 프리팹(InventorySlot.prefab) 루트에 붙는다.
/// </summary>
public sealed class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("장식 그림을 그릴 Image. 프리팹의 ItemThumbnail.")]
    [SerializeField] private Image _thumbnail;
    [Tooltip("보유 개수를 적을 텍스트. 프리팹의 ItemCount.")]
    [SerializeField] private TMP_Text _countText;
    [Tooltip("모드 중 호버 색. ItemThumbnail 위에 같은 크기로 덮는 Image(색 #640000, 알파 0.3, Raycast Target 끔).")]
    [SerializeField] private Image _hoverTint;

    private ShopItemDefinition _item;
    private Action<ShopItemDefinition> _onClick;
    private bool _pointerInside;
    private bool _hoverAllowed;

    /// <summary>
    /// 이 칸을 장식 하나로 채운다. <paramref name="count"/>는 그 장식을 몇 개 가지고 있는지다.
    /// <paramref name="onClick"/>은 칸이 눌렸을 때 알릴 곳, <paramref name="hoverAllowed"/>는 지금 호버 색을 켜도 되는지다.
    ///
    /// 개수가 1일 때는 숫자를 숨긴다 — 기획이 그렇게 정해 두었다(Figma 인벤토리 프레임의
    /// "1 일때는 숫자 표기 X" 메모). 한 개뿐인 칸에 "1"이 붙으면 눈에 걸리기만 한다.
    /// </summary>
    public void Bind(ShopItemDefinition item, int count, Action<ShopItemDefinition> onClick, bool hoverAllowed)
    {
        _item = item;
        _onClick = onClick;
        _hoverAllowed = hoverAllowed;

        if (_thumbnail != null)
        {
            // 그림이 없는 장식은 Image를 꺼서 빈 칸으로 둔다. 스프라이트만 비워두면 Unity가
            // 흰 사각형을 그리는데, 슬롯 배경도 흰색이라 무엇이 잘못됐는지 알아보기 어렵다.
            // 조건 없이 대입하는 것도 중요하다 — 칸을 재사용하므로 건너뛰면 이전 장식 그림이 남는다.
            _thumbnail.sprite = item.icon;
            _thumbnail.enabled = item.icon != null;
        }
        if (_hoverTint != null) _hoverTint.sprite = item.icon;

        if (_countText != null)
        {
            bool showCount = count > 1;
            _countText.gameObject.SetActive(showCount);
            if (showCount) _countText.text = count.ToString();
        }

        RefreshHover();
    }

    /// <summary>호버 색을 켜도 되는지 알린다. 모드·입력 정지·들고 다니기가 바뀔 때 <see cref="InventoryModeController"/>가 부른다.</summary>
    public void SetHoverAllowed(bool allowed)
    {
        if (_hoverAllowed == allowed) return;
        _hoverAllowed = allowed;
        RefreshHover();
    }

    /// <summary>루트 Button의 OnClick()에 인스펙터로 거는 진입점.</summary>
    public void OnClicked()
    {
        if (_item != null) _onClick?.Invoke(_item);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        RefreshHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        RefreshHover();
    }

    // 남는 칸은 꺼 두므로, 꺼질 때 포인터 기록을 지운다 — 다시 켜질 때 exit 없이 색이 남지 않게.
    private void OnDisable()
    {
        _pointerInside = false;
        RefreshHover();
    }

    // 그림이 없는 칸에는 붉은 사각형이 그려지지 않게 그림 유무도 본다.
    private void RefreshHover()
    {
        if (_hoverTint == null) return;
        _hoverTint.enabled = _pointerInside && _hoverAllowed && _item != null && _item.icon != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_thumbnail == null || _countText == null || _hoverTint == null)
            Debug.LogWarning($"[{nameof(InventorySlot)}] 슬롯 참조(_thumbnail/_countText/_hoverTint)가 비어 있음.", this);
    }
#endif
}
