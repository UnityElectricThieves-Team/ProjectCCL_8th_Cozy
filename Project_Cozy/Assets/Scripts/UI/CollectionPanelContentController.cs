using System;
using System.Collections.Generic;
using Assets.Scripts.Contents.CollectionSystem.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 패널 내용물(Content)의 두뇌. CollectionTool이 내보낸 collection.dat를 읽어
/// 좌측에 캐릭터 목록을 깔고, 고른 항목의 일러스트와 정보를 우측에 보여준다.
///
/// collection.dat은 <see cref="GameDataPaths.CollectionData"/> — StreamingAssets에 놓인 <b>배포 데이터</b>다.
/// 즉 여기 담긴 값은 콘텐츠 정의이지 이 유저의 진행 상황이 아니다(그래서 정의의 <c>IsCollected</c>는 쓰지 않는다).
/// 해금 여부와 친밀도는 유저 기록의 주인인 <see cref="CharacterOwnership"/>에서 항목의 <c>CharacterId</c>로 찾는다.
/// 해금 전에는 숨김 사진을 썸네일로 쓰고, 상세 대신 해금에 필요한 기운을 안내한다.
/// <c>CharacterId</c>가 빈 항목은 잠글 근거가 없어 해금된 것처럼 보여준다.
///
/// 항목 그림은 에셋이 아니라 Base64 문자열이라 런타임에 디코드해야 한다(<see cref="PhotoSprite"/>).
/// 좌측 썸네일과 중앙 일러스트가 같은 그림을 쓰므로 사진당 한 장만 만들어 돌려쓰고,
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
    [Tooltip("누적 친밀도. 캐릭터 기록에서 읽는다.")]
    [SerializeField] private TMP_Text _affinityText;

    [Header("잠금")]
    [Tooltip("해금된 항목의 상세 화면(Right_Character_Info). 잠긴 항목을 고르면 통째로 꺼진다.")]
    [SerializeField] private GameObject _unlockedView;
    [Tooltip("잠긴 항목의 화면(Right_Character_Locked). 해금된 항목을 고르면 통째로 꺼진다.")]
    [SerializeField] private GameObject _lockedView;
    [Tooltip("잠긴 화면 안의 안내 문구. 항목마다 필요한 기운이 달라 코드가 채운다.")]
    [SerializeField] private TMP_Text _lockedMessage;

    // 사진별 그림 캐시. 값이 null이면 "깨진 사진"이라는 뜻이라 다시 디코드하지 않는다.
    private readonly Dictionary<PhotoData, Sprite> _pictures = new();
    private readonly List<CollectionEntrySlot> _slots = new();
    private CollectionEntrySlot _selected;
    private LocalizationManager _localization;
    private CharacterOwnership _ownership;

    private void Awake()
    {
        // CharacterOwnership은 실행 순서가 앞서(-99) 이 시점에 Instance와 기록이 준비돼 있다.
        _ownership = CharacterOwnership.Instance;
        if (_ownership != null) _ownership.RecordChanged += OnRecordChanged;
        else Debug.LogWarning($"[{nameof(CollectionPanelContentController)}] CharacterOwnership이 씬에 없어 모든 항목을 해금된 것처럼 보여줍니다.", this);

        BuildSlots(CollectionDataRuntime.Load());

        // 상세의 항목 이름(나이·키 등)이 언어를 따르므로, 언어가 바뀌면 고른 항목을 다시 그린다.
        _localization = LocalizationManager.Instance;
        if (_localization != null) _localization.LanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        if (_ownership != null) _ownership.RecordChanged -= OnRecordChanged;
        if (_localization != null) _localization.LanguageChanged -= OnLanguageChanged;
        foreach (var sprite in _pictures.Values) PhotoSprite.Destroy(sprite);
        _pictures.Clear();
    }

    private void OnLanguageChanged()
    {
        if (_selected != null) ShowDetail(_selected.Entry);
    }

    // 해금되면 썸네일이 숨김 사진에서 공개 사진으로 바뀌고, 쓰담으로 친밀도가 오르면 숫자가 바뀐다.
    // 패널은 CanvasGroup으로 숨겨 계속 살아 있으므로, 닫혀 있는 동안 바뀐 것도 여기서 따라간다.
    private void OnRecordChanged(string characterId)
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].Entry.CharacterId == characterId) _slots[i].SetThumbnail(GetThumbnail(_slots[i].Entry));

        if (_selected != null && _selected.Entry.CharacterId == characterId) ShowDetail(_selected.Entry);
    }

    private void BuildSlots(CollectionBoolData data)
    {
        // 목록이 비는 경로는 전부 소리를 낸다 — 조용히 return하면 배선 실수인지
        // 데이터가 없는 것인지 화면만 봐서는 구분할 수 없다.
        if (_listContent == null || _slotPrefab == null)
        {
            Debug.LogWarning($"[{nameof(CollectionPanelContentController)}] _listContent 또는 _slotPrefab이 비어 목록을 만들지 못했습니다.", this);
            return;
        }

        if (data == null || data.collectionDataList == null || data.collectionDataList.Count == 0)
        {
            Debug.LogWarning($"[{nameof(CollectionPanelContentController)}] 도감 데이터가 비어 있습니다. 경로: {GameDataPaths.CollectionData}", this);
            return;
        }

        CollectionEntrySlot firstSlot = null;
        foreach (var entry in data.collectionDataList)
        {
            if (entry == null) continue;
            if (string.IsNullOrEmpty(entry.CharacterId))
                Debug.LogWarning($"[{nameof(CollectionPanelContentController)}] 도감 항목 '{entry.Name}'에 CharacterId가 없어 잠그지 않고 친밀도도 보여주지 않습니다.", this);

            var slot = Instantiate(_slotPrefab, _listContent);
            slot.Bind(entry, GetThumbnail(entry), Select);
            _slots.Add(slot);
            if (firstSlot == null) firstSlot = slot;
        }

        Select(firstSlot); // 열자마자 빈 화면이 보이지 않게 첫 항목을 고른다 (항목이 없으면 무시된다)
    }

    private bool IsUnlocked(CollectionData entry) =>
        _ownership == null || string.IsNullOrEmpty(entry.CharacterId) || _ownership.IsOwned(entry.CharacterId);

    private Sprite GetThumbnail(CollectionData entry) =>
        GetPicture(IsUnlocked(entry) ? entry.ProfilePictureBase64_Main : entry.ProfilePictureBase64_Main_Hidden);

    /// <summary>사진을 처음 필요할 때 디코드해 캐시한다. 사진이 없으면 null.</summary>
    private Sprite GetPicture(PhotoData photo)
    {
        if (photo == null) return null;
        if (_pictures.TryGetValue(photo, out var cached)) return cached;

        var sprite = PhotoSprite.Create(photo);
        _pictures[photo] = sprite;
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

        // 잠김 여부에 따라 두 화면 중 하나만 켠다.
        var unlocked = IsUnlocked(entry);
        if (_unlockedView != null) _unlockedView.SetActive(unlocked);
        if (_lockedView != null) _lockedView.SetActive(!unlocked);

        if (!unlocked)
        {
            if (_lockedMessage != null)
                LocalizationManager.SetText(_lockedMessage, "UICollection.locked_hint", ("ENERGY", entry.RequiredSpawnEnergy));
            return;
        }

        if (_illustration != null)
        {
            var picture = GetPicture(entry.ProfilePictureBase64_Main);
            _illustration.sprite = picture;
            _illustration.enabled = picture != null;
        }

        if (_nameText != null) _nameText.text = entry.Name;
        if (_infoText != null)
        {
            LocalizationManager.ApplyFont(_infoText);
            _infoText.text = BuildInfo(entry);
        }
        if (_affinityText != null)
        {
            if (_ownership != null && !string.IsNullOrEmpty(entry.CharacterId))
                LocalizationManager.SetText(_affinityText, "UICollection.affinity_value", ("AFFINITY", _ownership.GetCumulativeAffinity(entry.CharacterId)));
            else
                LocalizationManager.SetText(_affinityText, "UICollection.affinity_empty");
        }
    }

    // 단위와 날짜 표기도 언어마다 달라서 조각마다 번역 표의 문장을 쓴다. 값이 없으면 "-".
    private static string BuildInfo(CollectionData entry)
    {
        var age = entry.Age > 0 ? LocalizationManager.Localize("UICollection.age_value", ("AGE", entry.Age)) : "-";
        var height = entry.Height > 0 ? LocalizationManager.Localize("UICollection.height_value", ("HEIGHT", entry.Height)) : "-";
        var birthday = entry.Birthday == DateTime.MinValue
            ? "-"
            : LocalizationManager.Localize("UICollection.birthday_value", ("MONTH", entry.Birthday.Month), ("DAY", entry.Birthday.Day));
        var hobby = string.IsNullOrEmpty(entry.Hobby) ? "-" : entry.Hobby;

        return LocalizationManager.Localize("UICollection.profile", ("AGE", age), ("HEIGHT", height), ("BIRTHDAY", birthday), ("HOBBY", hobby));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_listContent == null || _slotPrefab == null)
            Debug.LogWarning($"[{nameof(CollectionPanelContentController)}] _listContent 또는 _slotPrefab이 비어 있음.", this);
    }
#endif
}
