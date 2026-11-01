using System;
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

    [SerializeField] private SettingsStepper _characterScaleStepper;
    [SerializeField] private SettingsStepper _uiScaleStepper;

    private SettingsManager _settings;
    private ViewportScreenSettings _viewport;

    private void OnEnable()
    {
        _settings = SettingsManager.Instance;
        if (_settings == null) return;
        _settings.GraphicsChanged += RefreshControls;
        RefreshControls();
    }

    private void OnDisable()
    {
        if (_settings != null) _settings.GraphicsChanged -= RefreshControls;
    }

    private void RefreshControls()
    {
        if (_characterScaleStepper != null)
            _characterScaleStepper.Setup(GraphicsScaleSteps.Character.Count, _settings.CharacterScaleStep);
        if (_uiScaleStepper != null)
            _uiScaleStepper.Setup(GraphicsScaleSteps.UI.Count, _settings.UIScaleStep);
    }

    // ===== 인스펙터로 거는 진입점 =====

    // −/+ 줄의 ValueChanged(int)에 건다(동적 int).
    public void OnCharacterScaleChanged(int step) { if (_settings != null) _settings.SetCharacterScaleStep(step); }
    public void OnUIScaleChanged(int step) { if (_settings != null) _settings.SetUIScaleStep(step); }

    // [저장] 버튼. 크기 단계와 화면 크기 조정을 함께 확정한다.
    public void Save()
    {
        if (_settings != null) _settings.CommitGraphics();
        if (FindViewport() != null) _viewport.CommitPendingViewport();
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

    private void DiscardUnsaved()
    {
        if (_settings != null) _settings.RevertGraphics();
        if (_viewport != null) _viewport.DiscardPendingViewport();
    }
}
