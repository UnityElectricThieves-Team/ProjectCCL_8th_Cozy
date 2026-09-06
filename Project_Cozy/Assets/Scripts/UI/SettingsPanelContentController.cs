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
/// 일반 탭의 설정 컨트롤은 <see cref="SettingsManager"/>와 양방향으로 맞춘다.
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

    // Figma: 활성 탭=시안(#39C9E6), 비활성=회색(#D9D9D9). 상점 탭과 같은 값.
    private static readonly Color ActiveTab = new(0.224f, 0.788f, 0.902f);
    private static readonly Color InactiveTab = new(0.851f, 0.851f, 0.851f);

    private SettingsTab _tab = SettingsTab.General;
    private SettingsManager _settings;

    private void OnEnable() => SetTab(_tab); // 다시 열릴 때 현재 탭으로 복원

    // 매니저는 실행 순서 -100의 Awake에서 로드를 끝내므로, Start에서는 값이 준비되어 있다.
    private void Start()
    {
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
    }

    // ===== 컨트롤 → 매니저. 각 컨트롤의 OnValueChanged()에 인스펙터로 거는 진입점(동적 bool/int 인자). =====
    public void OnAlwaysOnTopChanged(bool on) { if (_settings != null) _settings.AlwaysOnTop = on; }
    public void OnLanguageChanged(int index) { if (_settings != null) _settings.Language = (Language)index; }
    public void OnSpawnerCountVisibilityChanged(int index) { if (_settings != null) _settings.SpawnerCountVisibility = (CountVisibility)index; }
    public void OnAffinityVisibilityChanged(int index) { if (_settings != null) _settings.AffinityVisibility = (CountVisibility)index; }
    public void OnAutoStartChanged(bool on) { if (_settings != null) _settings.AutoStart = on; }
    public void OnAdministratorModeChanged(bool on) { if (_settings != null) _settings.AdministratorMode = on; }
    public void OnGirlTransformBannedChanged(bool on) { if (_settings != null) _settings.GirlTransformBanned = on; }

    // ===== 매니저 → 컨트롤 =====

    /// <summary>
    /// 매니저의 현재 값을 컨트롤 전부에 밀어넣는다.
    /// 토글은 알림 없는 <c>SetIsOnWithoutNotify</c>를 쓰면 안 된다 — 알약 모양을 그리는 <see cref="SettingsPillToggle"/>이
    /// onValueChanged로만 다시 그려서, 값은 바뀌었는데 그림은 옛 상태로 남는다. 알림이 가는 <c>isOn</c> 대입을 쓴다.
    /// </summary>
    private void RefreshControls()
    {
        if (_alwaysOnTopToggle != null) _alwaysOnTopToggle.isOn = _settings.AlwaysOnTop;
        if (_languageDropdown != null) _languageDropdown.value = (int)_settings.Language;
        if (_spawnerCountVisibilityDropdown != null) _spawnerCountVisibilityDropdown.value = (int)_settings.SpawnerCountVisibility;
        if (_affinityVisibilityDropdown != null) _affinityVisibilityDropdown.value = (int)_settings.AffinityVisibility;
        if (_autoStartToggle != null) _autoStartToggle.isOn = _settings.AutoStart;
        if (_administratorModeToggle != null) _administratorModeToggle.isOn = _settings.AdministratorMode;
        if (_girlTransformBannedToggle != null) _girlTransformBannedToggle.isOn = _settings.GirlTransformBanned;
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
