using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 설정 패널 그래픽 탭의 두뇌. Figma '옵션 - 그래픽'과 그 미저장·초기화 팝업에 대응한다.
/// 그래픽 탭의 내용 루트(<c>_graphicsRoot</c>)에 붙는다. 탭 전환은 <see cref="SettingsPanelContentController"/>가 하고,
/// 그래픽 탭을 떠나도 되는지만 이 컴포넌트에 묻는다(<see cref="TryLeave"/>).
///
/// 값은 들고 있지 않다. −/+ 줄이 바뀌면 <see cref="SettingsManager"/>에 지금 값(미리보기)을 쓰고,
/// [저장]이 확정, [초기화]는 기본값 미리보기다. 화면에 반영하는 쪽은 매니저의 <see cref="SettingsManager.GraphicsChanged"/>를 듣는다.
/// 화면 크기 [조정]의 미저장 사각형은 <see cref="ViewportScreenSettings"/>가 들고, [저장]·미저장 확인은 둘을 함께 다룬다.
///
/// 버튼 OnClick과 −/+ 줄의 ValueChanged는 인스펙터로 여기 public 메서드에 건다.
/// 패널 오브젝트는 CanvasGroup으로 숨기지만, 탭 루트는 SetActive로 껐다 켜므로 이 컴포넌트의 Start는 그래픽 탭을 처음 열 때 돈다.
/// 그래서 −/+ 줄은 <see cref="OnEnable"/>마다 매니저 값으로 다시 맞춘다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GraphicsTabController : MonoBehaviour
{
    private const string UnsavedMessageId = "UISettings.popup.graphics_unsaved";
    private const string ResetMessageId = "UISettings.popup.graphics_reset";
    private const string MonitorOptionId = "UISettings.option.monitor";

    [SerializeField] private SettingsStepper _characterScaleStepper;
    [SerializeField] private SettingsStepper _uiScaleStepper;
    [SerializeField] private SettingsStepper _worldZoomStepper;
    [Tooltip("모니터 드롭다운. 옵션은 실행할 때 연결된 모니터로 만든다 — 프리팹의 Options는 덮어써진다. OnValueChanged에 OnMonitorChanged를 건다.")]
    [SerializeField] private TMP_Dropdown _monitorDropdown;

    private SettingsManager _settings;
    private ViewportScreenSettings _viewport;
    private MonitorApplier _monitorApplier;
    private LocalizationManager _localization;

    private void OnEnable()
    {
        _settings = SettingsManager.Instance;
        if (_settings == null) return;

        // 패널 프리팹은 씬 오브젝트를 참조할 수 없어 찾아 둔다. 탭을 열 때만 도는 일이라 비용이 없다.
        if (_monitorApplier == null) _monitorApplier = FindFirstObjectByType<MonitorApplier>();
        if (_monitorApplier != null) _monitorApplier.MonitorsChanged += BuildMonitorOptions;
        _localization = LocalizationManager.Instance;
        if (_localization != null) _localization.LanguageChanged += BuildMonitorOptions;
        BuildMonitorOptions();

        _settings.GraphicsChanged += RefreshControls;
        RefreshControls();
    }

    private void OnDisable()
    {
        if (_settings != null) _settings.GraphicsChanged -= RefreshControls;
        if (_monitorApplier != null) _monitorApplier.MonitorsChanged -= BuildMonitorOptions;
        if (_localization != null) _localization.LanguageChanged -= BuildMonitorOptions;
    }

    private void RefreshControls()
    {
        if (_characterScaleStepper != null)
            _characterScaleStepper.Setup(GraphicsScaleSteps.Character.Count, _settings.CharacterScaleStep);
        if (_uiScaleStepper != null)
            _uiScaleStepper.Setup(GraphicsScaleSteps.UI.Count, _settings.UIScaleStep);
        if (_worldZoomStepper != null)
            _worldZoomStepper.Setup(GraphicsScaleSteps.WorldZoom.Count, _settings.WorldZoomStep);
        if (_monitorDropdown != null && _monitorApplier != null)
            _monitorDropdown.SetValueWithoutNotify(_monitorApplier.IndexOf(_settings.Monitor));
    }

    // "모니터1 (1920×1080)"처럼 번호와 해상도를 보인다. 주 모니터가 1번이다(MonitorApplier.Monitors 순서).
    // ClearOptions는 선택을 첫 항목으로 돌리므로 다시 만든 뒤 지금 선택을 알림 없이 맞춘다.
    private void BuildMonitorOptions()
    {
        if (_monitorDropdown == null || _monitorApplier == null) return;
        var monitors = _monitorApplier.Monitors;
        _monitorDropdown.ClearOptions();
        for (int i = 0; i < monitors.Count; i++)
        {
            string label = LocalizationManager.Localize(MonitorOptionId,
                ("NUMBER", i + 1), ("WIDTH", monitors[i].Bounds.width), ("HEIGHT", monitors[i].Bounds.height));
            _monitorDropdown.options.Add(new TMP_Dropdown.OptionData(label));
        }
        LocalizationManager.ApplyFont(_monitorDropdown.captionText);
        LocalizationManager.ApplyFont(_monitorDropdown.itemText);
        if (_settings != null) _monitorDropdown.SetValueWithoutNotify(_monitorApplier.IndexOf(_settings.Monitor));
        _monitorDropdown.RefreshShownValue();
    }

    // ===== 인스펙터로 거는 진입점 =====

    // −/+ 줄의 ValueChanged(int)에 건다(동적 int).
    public void OnCharacterScaleChanged(int step) { if (_settings != null) _settings.SetCharacterScaleStep(step); }
    public void OnUIScaleChanged(int step) { if (_settings != null) _settings.SetUIScaleStep(step); }
    public void OnWorldZoomChanged(int step) { if (_settings != null) _settings.SetWorldZoomStep(step); }

    // 모니터 드롭다운의 OnValueChanged(int)에 건다(동적 int). 주 모니터(1번)를 고르면 "늘 주 모니터"로 둔다 —
    // 주 모니터를 바꿔도 따라가게 하려는 것이다. 나머지는 장치 이름과 rect로 기억한다.
    public void OnMonitorChanged(int index)
    {
        if (_settings == null || _monitorApplier == null) return;
        var monitors = _monitorApplier.Monitors;
        if (index < 0 || index >= monitors.Count) return;
        _settings.SetMonitor(index == 0 ? MonitorSelection.Primary : new MonitorSelection(monitors[index].DeviceName, monitors[index].Bounds));
    }

    // [저장] 버튼. 크기 단계(전체 크기 포함)와 화면 크기 조정을 함께 확정한다.
    // 뷰포트 쪽 확정이 뒤에 와야 한다 — 확정 신호로 장식을 회수할 때 줌이 이미 저장된 값이어야 해서다.
    public void Save()
    {
        if (_settings != null) _settings.CommitGraphics();
        if (FindViewport() != null) _viewport.Commit();
    }

    // 화면 크기 [조정] 버튼. 편집에 들어가면 UIManager가 패널을 모두 숨기고, 편집 화면의 [완료]·ESC로 돌아온다.
    // 조정한 크기는 [저장]을 눌러야 확정된다.
    public void AdjustViewport()
    {
        if (FindViewport() != null && _viewport.IsReady) _viewport.EnterEdit();
    }

    // "화면 다시 맞추기" 버튼. 해상도·작업표시줄·모니터가 바뀐 뒤 창과 카메라를 지금 작업 영역에 다시 맞춘다.
    // 사용자의 선택을 바꾸는 일이 아니라 [저장] 흐름을 타지 않고 바로 적용한다.
    public void ReadjustWindow()
    {
        // 모니터 구성도 바뀌었을 수 있다. 목록을 다시 읽어 고른 모니터의 새 작업 영역부터 넣는다.
        if (_monitorApplier != null) _monitorApplier.Refresh();
        if (FindViewport() != null) _viewport.ReadjustWindow();
    }

    // 패널 프리팹은 씬 오브젝트를 참조할 수 없어 처음 쓸 때 한 번 찾아 둔다.
    private ViewportScreenSettings FindViewport()
    {
        if (_viewport == null) _viewport = FindFirstObjectByType<ViewportScreenSettings>();
        return _viewport;
    }

    // [초기화] 버튼. 확인하면 기본값을 미리보기로 보여 주고, 확정은 [저장]이 한다.
    public void ResetToDefaults()
    {
        if (_settings == null || ConfirmPopup.Instance == null) return;
        ConfirmPopup.Instance.Show(ResetMessageId, _settings.ResetGraphicsToDefaults);
    }

    /// <summary>
    /// 그래픽 탭을 떠나도 되는지(패널 닫기, 다른 탭으로 이동). 저장하지 않은 변경이 없으면 true.
    /// 있으면 미저장 팝업을 띄우고 false를 돌려준다 — 그 자리에서는 떠나지 않는다.
    /// 팝업에서 [확인]하면 변경을 버린 뒤 <paramref name="leave"/>를 부르고, [취소]·ESC면 그대로 머문다.
    /// </summary>
    public bool TryLeave(Action leave)
    {
        bool unsavedSteps = _settings != null && _settings.HasUnsavedGraphics;
        bool unsavedViewport = FindViewport() != null && _viewport.HasPendingViewport;
        if (!unsavedSteps && !unsavedViewport) return true;
        if (ConfirmPopup.Instance == null)
        {
            // 물을 팝업이 없는 씬이면 막지 않는다(닫기가 영영 막히지 않게). 저장 안 한 미리보기는 버린다.
            DiscardUnsaved();
            return true;
        }

        ConfirmPopup.Instance.Show(UnsavedMessageId, () =>
        {
            DiscardUnsaved();
            leave?.Invoke();
        });
        return false;
    }

    // 크기 단계를 먼저 되돌린다 — 뷰포트를 버릴 때 화면 밖 장식을 회수하는데, 그 판정이 되돌린 줌으로 되어야 한다(settings.md).
    private void DiscardUnsaved()
    {
        if (_settings != null) _settings.RevertGraphics();
        if (_viewport != null) _viewport.DiscardPendingViewport();
    }
}
