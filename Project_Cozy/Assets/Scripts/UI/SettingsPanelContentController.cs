using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 패널 내용물(Content)의 두뇌. Figma의 '옵션 - 일반 / 그래픽 / 소리' 세 프레임이
/// 한 패널의 탭 세 개에 대응한다 — 탭을 누르면 해당 내용 루트만 켜고 나머지는 끈다.
///
/// 탭 전환 방식과 활성/비활성 색은 <see cref="ShopPanelContentController"/>와 같은 규칙을 따른다.
/// 다만 상점과 달리 항목이 고정이라 행을 만들어 넣지 않고, 미리 배치된 루트를 켜고 끄기만 한다.
///
/// 일반 탭과 소리 탭의 설정 컨트롤은 <see cref="SettingsManager"/>와 양방향으로 맞춘다.
/// - 컨트롤 → 매니저: 각 컨트롤의 OnValueChanged에 인스펙터로 건 <c>On*Changed</c> 메서드가 값을 넘긴다.
/// - 매니저 → 컨트롤: 시작 시와 매니저의 <see cref="SettingsManager.Changed"/> 때 <see cref="RefreshControls"/>가 값을 밀어넣는다.
/// 밀어넣을 때 되돌아오는 알림은 매니저 setter가 같은 값을 무시하므로 저장이나 재귀를 일으키지 않는다.
///
/// <c>Area_Content</c>에 붙는다. 패널은 CanvasGroup으로 숨기므로(SetActive 아님) 이 컴포넌트는 계속 살아 있다.
/// 그래서 다시 열 때 <see cref="OnEnable"/>은 불리지 않지만, 마지막으로 고른 탭은 필드와 루트의 활성 상태로 그대로 남는다.
/// </summary>
public sealed class SettingsPanelContentController : MonoBehaviour
{
    private enum SettingsTab { General, Graphics, Sound }

    [Header("탭 배경 이미지")]
    [Tooltip("활성/비활성 색을 칠할 탭 배경 이미지.")]
    [SerializeField] private Image _generalTabImage;
    [SerializeField] private Image _graphicsTabImage;
    [SerializeField] private Image _soundTabImage;

    [Header("탭별 내용 루트")]
    [SerializeField] private GameObject _generalRoot;
    [SerializeField] private GameObject _graphicsRoot;
    [SerializeField] private GameObject _soundRoot;

    [Header("일반 탭 컨트롤")]
    [Tooltip("저장된 값을 밀어넣을 대상. 컨트롤 → 매니저 방향은 각 컨트롤의 OnValueChanged에 인스펙터로 건다.")]
    [SerializeField] private Toggle _alwaysOnTopToggle;
    [SerializeField] private TMP_Dropdown _languageDropdown;
    [SerializeField] private TMP_Dropdown _spawnerCountVisibilityDropdown;
    [SerializeField] private TMP_Dropdown _affinityVisibilityDropdown;
    [SerializeField] private Toggle _autoStartToggle;
    [SerializeField] private Toggle _administratorModeToggle;
    [SerializeField] private Toggle _girlTransformBannedToggle;

    [Header("소리 탭 컨트롤")]
    [Tooltip("볼륨 슬라이더는 OnValueChanged에 On*VolumeChanged를, Slider 오브젝트의 SliderReleaseEvent에 OnVolumeReleased를 건다.")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Toggle _mutedToggle;
    [SerializeField] private TMP_Dropdown _bgmTrackDropdown;
    [Tooltip("배경음악 드롭다운의 옵션을 채울 곡 목록. 시작할 때 이 목록의 순서·이름으로 옵션을 만든다.")]
    [SerializeField] private BgmCatalog _bgmCatalog;

    // Figma: 활성 탭=시안(#39C9E6), 비활성=회색(#D9D9D9). 상점 탭과 같은 값.
    private static readonly Color ActiveTab = new(0.224f, 0.788f, 0.902f);
    private static readonly Color InactiveTab = new(0.851f, 0.851f, 0.851f);

    // 카운트 표기 드롭다운의 옵션 문구. CountVisibility 순서와 같아야 한다.
    private static readonly string[] CountVisibilityOptionIds =
    {
        "UISettings.option.count_always",
        "UISettings.option.count_autohide",
        "UISettings.option.count_hidden",
    };

    private SettingsTab _tab = SettingsTab.General;
    private SettingsManager _settings;
    private LocalizationManager _localization;

    private void OnEnable() => SetTab(_tab); // 다시 열릴 때 현재 탭으로 복원

    // 매니저는 실행 순서 -100의 Awake에서 로드를 끝내므로, Start에서는 값이 준비되어 있다.
    private void Start()
    {
        // 드롭다운 옵션은 LocalizedText를 붙일 수 없는 곳이라 여기서 채운다. 언어 드롭다운은 번역하지 않는다.
        RefreshCountVisibilityLabels();
        _localization = LocalizationManager.Instance;
        if (_localization != null) _localization.LanguageChanged += RefreshCountVisibilityLabels;

        // 첫 RefreshControls보다 먼저 만들어야 저장된 곡 위치가 옵션 수에 잘리지 않는다.
        BuildBgmTrackOptions();

        _settings = SettingsManager.Instance;
        if (_settings == null)
        {
            Debug.LogWarning($"[{nameof(SettingsPanelContentController)}] SettingsManager 없음 — 설정이 저장·복원되지 않습니다.", this);
            return;
        }

        RefreshControls();
        _settings.Changed += RefreshControls;
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.Changed -= RefreshControls;
        if (_localization != null) _localization.LanguageChanged -= RefreshCountVisibilityLabels;
    }

    private void RefreshCountVisibilityLabels()
    {
        SetOptionLabels(_spawnerCountVisibilityDropdown, CountVisibilityOptionIds);
        SetOptionLabels(_affinityVisibilityDropdown, CountVisibilityOptionIds);
    }

    /// <summary>
    /// 곡 목록(<see cref="BgmCatalog"/>)으로 배경음악 드롭다운 옵션을 만든다. 곡 목록이 이름·순서의 유일한 정본이라
    /// 프리팹의 Options에 무엇이 적혀 있든 덮는다 — 두 곳에 따로 적으면 순서가 어긋나 고른 이름과 나오는 곡이 달라진다.
    /// 곡 이름은 번역하지 않는 고유명사라 언어가 바뀌어도 다시 만들지 않는다(ClearOptions는 선택을 첫 항목으로 돌린다).
    /// </summary>
    private void BuildBgmTrackOptions()
    {
        if (_bgmTrackDropdown == null) return;
#if UNITY_EDITOR
        WarnIfBgmOptionsEdited();
#endif
        _bgmTrackDropdown.ClearOptions();
        int count = _bgmCatalog != null ? _bgmCatalog.Count : 0;
        for (int i = 0; i < count; i++) _bgmTrackDropdown.options.Add(new TMP_Dropdown.OptionData(_bgmCatalog[i].displayName));
        _bgmTrackDropdown.interactable = count > 0; // 곡이 없으면 고를 것도 없다
        _bgmTrackDropdown.RefreshShownValue();
    }

#if UNITY_EDITOR
    // 실행 중에는 BuildBgmTrackOptions가 채운 옵션이 있으므로 검사하지 않는다.
    // OnValidate는 이 컴포넌트가 바뀌거나 프리팹·씬을 불러올 때만 불려, 드롭다운 쪽을 고친 직후에는 안 불린다.
    // 그래서 실행 시작 때(BuildBgmTrackOptions가 지우기 직전)에도 같은 검사를 한다.
    private void OnValidate()
    {
        if (!Application.isPlaying) WarnIfBgmOptionsEdited();
    }

    // 배경음악 드롭다운의 Options를 인스펙터에서 고쳐도 실행할 때 곡 목록으로 덮어써진다. 헷갈리지 않게 알린다.
    private void WarnIfBgmOptionsEdited()
    {
        if (_bgmTrackDropdown == null || _bgmTrackDropdown.options.Count == 0) return;
        Debug.LogWarning($"[{nameof(SettingsPanelContentController)}] 배경음악 드롭다운의 Options는 실행할 때 덮어써집니다. 곡 목록은 Assets/Audio/BgmCatalog에서 고치고, 여기 Options는 비워 두세요.", _bgmTrackDropdown);
    }
#endif

    private static void SetOptionLabels(TMP_Dropdown dropdown, string[] ids)
    {
        if (dropdown == null) return;
        var options = dropdown.options;
        for (int i = 0; i < options.Count && i < ids.Length; i++) options[i].text = LocalizationManager.Localize(ids[i]);
        dropdown.RefreshShownValue(); // 닫힌 드롭다운에 보이는 선택 항목 글자도 다시 그린다
    }

    // ===== 컨트롤 → 매니저. 각 컨트롤의 OnValueChanged()에 인스펙터로 거는 진입점(동적 bool/int 인자). =====
    public void OnAlwaysOnTopChanged(bool on) { if (_settings != null) _settings.AlwaysOnTop = on; }
    public void OnLanguageChanged(int index)
    {
        // 드롭다운 옵션은 프리팹에, 태그 목록은 코드에 있어 개수가 어긋날 수 있다. 범위 밖이면 무시한다.
        if (_settings != null && index >= 0 && index < LanguageCodes.All.Length) _settings.Language = LanguageCodes.All[index];
    }
    public void OnSpawnerCountVisibilityChanged(int index) { if (_settings != null) _settings.SpawnerCountVisibility = (CountVisibility)index; }
    public void OnAffinityVisibilityChanged(int index) { if (_settings != null) _settings.AffinityVisibility = (CountVisibility)index; }
    public void OnAutoStartChanged(bool on) { if (_settings != null) _settings.AutoStart = on; }
    public void OnAdministratorModeChanged(bool on) { if (_settings != null) _settings.AdministratorMode = on; }
    public void OnGirlTransformBannedChanged(bool on) { if (_settings != null) _settings.GirlTransformBanned = on; }

    // 볼륨은 끄는 동안 소리에만 반영하고, 파일 저장은 손 뗄 때(OnVolumeReleased) 한다.
    public void OnMasterVolumeChanged(float value) { if (_settings != null) _settings.SetVolume(VolumeChannel.Master, value); }
    public void OnMusicVolumeChanged(float value) { if (_settings != null) _settings.SetVolume(VolumeChannel.Music, value); }
    public void OnSfxVolumeChanged(float value) { if (_settings != null) _settings.SetVolume(VolumeChannel.Sfx, value); }
    public void OnVolumeReleased() { if (_settings != null) _settings.CommitVolume(); }
    public void OnMutedChanged(bool on) { if (_settings != null) _settings.Muted = on; }
    public void OnBgmTrackChanged(int index)
    {
        if (_settings != null && _bgmCatalog != null && index >= 0 && index < _bgmCatalog.Count) _settings.BgmTrackId = _bgmCatalog[index].id;
    }

    // ===== 매니저 → 컨트롤 =====

    /// <summary>
    /// 매니저의 현재 값을 컨트롤 전부에 밀어넣는다.
    /// 토글은 알림 없는 <c>SetIsOnWithoutNotify</c>를 쓰면 안 된다 — 알약 모양을 그리는 <see cref="SettingsPillToggle"/>이
    /// onValueChanged로만 다시 그려서, 값은 바뀌었는데 그림은 옛 상태로 남는다. 알림이 가는 <c>isOn</c> 대입을 쓴다.
    /// </summary>
    private void RefreshControls()
    {
        if (_alwaysOnTopToggle != null) _alwaysOnTopToggle.isOn = _settings.AlwaysOnTop;
        if (_languageDropdown != null) _languageDropdown.value = Array.IndexOf(LanguageCodes.All, _settings.Language);
        if (_spawnerCountVisibilityDropdown != null) _spawnerCountVisibilityDropdown.value = (int)_settings.SpawnerCountVisibility;
        if (_affinityVisibilityDropdown != null) _affinityVisibilityDropdown.value = (int)_settings.AffinityVisibility;
        if (_autoStartToggle != null) _autoStartToggle.isOn = _settings.AutoStart;
        if (_administratorModeToggle != null) _administratorModeToggle.isOn = _settings.AdministratorMode;
        if (_girlTransformBannedToggle != null) _girlTransformBannedToggle.isOn = _settings.GirlTransformBanned;

        // 슬라이더 값 대입이 되돌려 보내는 SetVolume은 같은 값이라 무시된다(dirty가 생기지 않는다).
        if (_masterVolumeSlider != null) _masterVolumeSlider.value = _settings.GetVolume(VolumeChannel.Master);
        if (_musicVolumeSlider != null) _musicVolumeSlider.value = _settings.GetVolume(VolumeChannel.Music);
        if (_sfxVolumeSlider != null) _sfxVolumeSlider.value = _settings.GetVolume(VolumeChannel.Sfx);
        if (_mutedToggle != null) _mutedToggle.isOn = _settings.Muted;
        if (_bgmTrackDropdown != null && _bgmCatalog != null)
        {
            int index = _bgmCatalog.IndexOf(_settings.BgmTrackId);
            if (index >= 0 && index < _bgmTrackDropdown.options.Count) _bgmTrackDropdown.value = index;
        }
    }

    // 탭 버튼의 OnClick()에 인스펙터로 거는 진입점. 인스펙터는 enum 인자를 넘길 수 없어 버튼별로 나눈다.
    public void ShowGeneralTab() => SetTab(SettingsTab.General);
    public void ShowGraphicsTab() => SetTab(SettingsTab.Graphics);
    public void ShowSoundTab() => SetTab(SettingsTab.Sound);

    private void SetTab(SettingsTab tab)
    {
        _tab = tab;
        if (_generalRoot != null) _generalRoot.SetActive(tab == SettingsTab.General);
        if (_graphicsRoot != null) _graphicsRoot.SetActive(tab == SettingsTab.Graphics);
        if (_soundRoot != null) _soundRoot.SetActive(tab == SettingsTab.Sound);
        UpdateTabVisuals();
    }

    private void UpdateTabVisuals()
    {
        if (_generalTabImage != null) _generalTabImage.color = _tab == SettingsTab.General ? ActiveTab : InactiveTab;
        if (_graphicsTabImage != null) _graphicsTabImage.color = _tab == SettingsTab.Graphics ? ActiveTab : InactiveTab;
        if (_soundTabImage != null) _soundTabImage.color = _tab == SettingsTab.Sound ? ActiveTab : InactiveTab;
    }
}
