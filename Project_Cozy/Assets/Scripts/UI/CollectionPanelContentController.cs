using System;
using System.Collections.Generic;
using System.Globalization;
using Assets.Scripts.Contents.CollectionSystem.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 패널 내용물(Content)의 두뇌. CollectionTool이 내보낸 collection.dat를 읽어
/// 좌측에 캐릭터 목록을 깔고, 고른 항목의 일러스트와 정보를 우측에 보여준다.
///
/// collection.dat은 <see cref="GameDataPaths.CollectionData"/> — StreamingAssets에 놓인 <b>배포 데이터</b>다.
/// 즉 여기 담긴 값은 콘텐츠 정의이지 이 유저의 진행 상황이 아니다. 유저가 무엇을 모았는지는
/// persistentDataPath 쪽 유저 데이터에 있어야 하는데 도감용 파일이 아직 없어서,
/// 지금은 수집 여부로 잠그지 않고 항목을 그대로 보여준다(<c>*_Hidden</c> 사진도 아직 쓰지 않는다).
///
/// 항목 그림은 에셋이 아니라 Base64 문자열이라 런타임에 디코드해야 한다(<see cref="PhotoSprite"/>).
/// 좌측 썸네일과 중앙 일러스트가 같은 그림을 쓰므로 항목당 한 장만 만들어 돌려쓰고,
/// 만든 스프라이트는 <see cref="OnDestroy"/>에서 텍스처까지 함께 정리한다.
///
/// 패널 루트에 붙는다. 패널은 CanvasGroup으로 숨기므로(SetActive 아님) 이 컴포넌트는 계속 살아 있다 —
/// 목록은 <see cref="Awake"/>에서 한 번만 만들고 여닫을 때 다시 만들지 않는다.
/// </summary>
public sealed class CollectionPanelContentController : MonoBehaviour
{
    [Tooltip("좌측 캐릭터 목록 슬롯을 넣을 부모(스크롤 뷰의 Content).")]
    [SerializeField] private Transform _listContent;
    [SerializeField] private CollectionEntrySlot _slotPrefab;

    [Header("우측 상세")]
    [Tooltip("중앙 풀 일러스트.")]
    [SerializeField] private Image _illustration;
    [SerializeField] private TMP_Text _nameText;
    [Tooltip("나이·키·생일·취미를 여러 줄로 보여줄 텍스트.")]
    [SerializeField] private TMP_Text _infoText;
    [Tooltip("친밀도. 도감 항목과 씬 캐릭터를 잇는 ID가 아직 없어 지금은 값 없이 표시만 한다.")]
    [SerializeField] private TMP_Text _affinityText;

    // 항목별 그림 캐시. 값이 null이면 "사진이 없거나 깨진 항목"이라는 뜻이라 다시 디코드하지 않는다.
    private readonly Dictionary<CollectionData, Sprite> _pictures = new();
    private CollectionEntrySlot _selected;

    private void Awake()
    {
        BuildSlots(CollectionDataRuntime.Load());
    }

    private void OnDestroy()
    {
        foreach (var sprite in _pictures.Values) PhotoSprite.Destroy(sprite);
        _pictures.Clear();
    }

    private void BuildSlots(CollectionBoolData data)
    {
        if (_listContent == null || _slotPrefab == null || data == null) return;

        CollectionEntrySlot firstSlot = null;
        foreach (var entry in data.collectionDataList)
        {
            if (entry == null) continue;
            var slot = Instantiate(_slotPrefab, _listContent);
            slot.Bind(entry, GetPicture(entry), Select);
            if (firstSlot == null) firstSlot = slot;
        }

        Select(firstSlot); // 열자마자 빈 화면이 보이지 않게 첫 항목을 고른다 (항목이 없으면 무시된다)
    }

    /// <summary>항목 그림을 처음 필요할 때 디코드해 캐시한다. 사진이 없으면 null.</summary>
    private Sprite GetPicture(CollectionData entry)
    {
        if (_pictures.TryGetValue(entry, out var cached)) return cached;

        var sprite = PhotoSprite.Create(entry.ProfilePictureBase64_Main);
        _pictures[entry] = sprite;
        return sprite;
    }

    private void Select(CollectionEntrySlot slot)
    {
        if (slot == null) return;

        if (_selected != null) _selected.SetSelected(false);
        _selected = slot;
        _selected.SetSelected(true);

        ShowDetail(slot.Entry);
    }

    private void ShowDetail(CollectionData entry)
    {
        if (entry == null) return;

        if (_illustration != null)
        {
            var picture = GetPicture(entry);
            _illustration.sprite = picture;
            _illustration.enabled = picture != null;
        }

        if (_nameText != null) _nameText.text = entry.Name;
        if (_infoText != null) _infoText.text = BuildInfo(entry);
        if (_affinityText != null) _affinityText.text = "친밀도: -";
    }

    private static string BuildInfo(CollectionData entry)
    {
        var age = entry.Age > 0 ? $"{entry.Age}세" : "-";
        var height = entry.Height > 0 ? $"{entry.Height}cm" : "-";
        var birthday = entry.Birthday == DateTime.MinValue
            ? "-"
            : entry.Birthday.ToString("M'월' d'일'", CultureInfo.InvariantCulture);
        var hobby = string.IsNullOrEmpty(entry.Hobby) ? "-" : entry.Hobby;

        return $"나이: {age}\n키: {height}\n생일: {birthday}\n취미: {hobby}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_listContent == null || _slotPrefab == null)
            Debug.LogWarning($"[{nameof(CollectionPanelContentController)}] _listContent 또는 _slotPrefab이 비어 있음.", this);
    }
#endif
}
