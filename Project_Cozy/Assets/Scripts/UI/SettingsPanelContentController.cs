using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 패널 내용물(Content)의 두뇌. Figma의 '옵션 - 일반 / 그래픽 / 소리' 세 프레임이
/// 한 패널의 탭 세 개에 대응한다 — 탭을 누르면 해당 내용 루트만 켜고 나머지는 끈다.
///
/// 탭 전환 방식과 활성/비활성 색은 <see cref="ShopPanelContentController"/>와 같은 규칙을 따른다.
/// 다만 상점과 달리 항목이 고정이라 행을 만들어 넣지 않고, 미리 배치된 루트를 켜고 끄기만 한다.
///
/// <c>Area_Content</c>에 붙는다. 패널은 CanvasGroup으로 숨기므로(SetActive 아님) 이 컴포넌트는 계속 살아 있다.
/// 그래서 다시 열 때 <see cref="OnEnable"/>은 불리지 않지만, 마지막으로 고른 탭은 필드와 루트의 활성 상태로 그대로 남는다.
/// </summary>
public sealed class SettingsPanelContentController : MonoBehaviour
{
    private enum SettingsTab { General, Graphics, Sound }

    [Header("탭 버튼")]
    [SerializeField] private Button _generalTab;
    [SerializeField] private Button _graphicsTab;
    [SerializeField] private Button _soundTab;

    [Tooltip("활성/비활성 색을 칠할 탭 배경 이미지.")]
    [SerializeField] private Image _generalTabImage;
    [SerializeField] private Image _graphicsTabImage;
    [SerializeField] private Image _soundTabImage;

    [Header("탭별 내용 루트")]
    [SerializeField] private GameObject _generalRoot;
    [SerializeField] private GameObject _graphicsRoot;
    [SerializeField] private GameObject _soundRoot;

    // Figma: 활성 탭=시안(#39C9E6), 비활성=회색(#D9D9D9). 상점 탭과 같은 값.
    private static readonly Color ActiveTab = new(0.224f, 0.788f, 0.902f);
    private static readonly Color InactiveTab = new(0.851f, 0.851f, 0.851f);

    private SettingsTab _tab = SettingsTab.General;

    private void Awake()
    {
        if (_generalTab != null) _generalTab.onClick.AddListener(() => SetTab(SettingsTab.General));
        if (_graphicsTab != null) _graphicsTab.onClick.AddListener(() => SetTab(SettingsTab.Graphics));
        if (_soundTab != null) _soundTab.onClick.AddListener(() => SetTab(SettingsTab.Sound));
    }

    private void OnEnable() => SetTab(_tab); // 다시 열릴 때 현재 탭으로 복원

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
