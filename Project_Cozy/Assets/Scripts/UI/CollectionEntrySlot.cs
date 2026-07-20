using System;
using Assets.Scripts.Contents.CollectionSystem.Model;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 좌측 캐릭터 목록의 한 칸(Figma 기준 220x220). 썸네일을 보여주고 클릭을
/// 바깥(<see cref="CollectionPanelContentController"/>)으로 넘긴다.
/// 어떤 항목을 보여줄지·선택했을 때 무엇을 할지는 컨트롤러가 정하고, 이 슬롯은 겉모습만 책임진다.
/// 슬롯 프리팹 루트에 붙는다.
/// </summary>
public sealed class CollectionEntrySlot : MonoBehaviour
{
    // 사진을 못 만든 항목을 눈에 띄게 표시하는 색. 스프라이트가 null인 Image는
    // 단색 사각형으로 그려지므로, 이 색이 곧 "사진 없음" 신호가 된다.
    private static readonly Color MissingPhotoColor = new Color(0.9f, 0.3f, 0.3f, 0.5f);

    [Tooltip("항목 썸네일. 어떤 사진을 넣을지는 컨트롤러가 정한다.")]
    [SerializeField] private Image _thumbnail;
    [SerializeField] private Button _button;
    [Tooltip("지금 보고 있는 항목임을 알리는 테두리·강조. 없으면 비워둬도 된다.")]
    [SerializeField] private GameObject _selectedHighlight;

    private Action<CollectionEntrySlot> _onClicked;

    /// <summary>이 슬롯이 표시 중인 도감 항목.</summary>
    public CollectionData Entry { get; private set; }

    /// <summary>슬롯을 항목 하나로 채운다. 누르면 onClicked(this)를 부른다.</summary>
    public void Bind(CollectionData entry, Sprite thumbnail, Action<CollectionEntrySlot> onClicked)
    {
        Entry = entry;
        _onClicked = onClicked;

        if (_thumbnail != null)
        {
            // 사진이 없어도 칸은 남긴다 — 이미지를 꺼버리면 "사진만 없는 슬롯"과
            // "슬롯이 아예 안 만들어진 것"이 화면에서 구분되지 않는다.
            _thumbnail.sprite = thumbnail;
            _thumbnail.color = thumbnail != null ? Color.white : MissingPhotoColor;
        }

        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClicked); // 재바인딩 시 중복 방지
            _button.onClick.AddListener(HandleClicked);
        }

        SetSelected(false);
    }

    /// <summary>선택 강조를 켜고 끈다.</summary>
    public void SetSelected(bool selected)
    {
        if (_selectedHighlight != null) _selectedHighlight.SetActive(selected);
    }

    private void HandleClicked() => _onClicked?.Invoke(this);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_thumbnail == null || _button == null)
            Debug.LogWarning($"[{nameof(CollectionEntrySlot)}] 슬롯 참조(_thumbnail/_button)가 비어 있음.", this);
    }
#endif
}
