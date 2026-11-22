using UnityEngine;

/// <summary>
/// 유저 설정의 전체 크기(줌)를 화면에 건다. 설정을 들고 있지도 저장하지도 않는다.
/// <see cref="DesktopIntegrationApplier"/>처럼 <see cref="SettingsManager"/>를 읽어 적용만 한다.
///
/// 뷰포트 정책(<see cref="ViewportScreenSettings"/>)은 PerformanceSetting 레이어라 게임 설정을 모른다.
/// 그래서 단계 번호를 배율로 바꿔 넣는 일은 여기서 한다.
///
/// 그래픽 탭은 미리보기가 바로 화면에 보여야 하므로 저장값이 아니라 지금 값을 읽는다.
/// <see cref="SettingsManager.GraphicsChanged"/>는 캐릭터·UI 크기가 바뀔 때도 울리지만, 같은 배율이면
/// <see cref="ViewportScreenSettings.SetZoom"/>이 아무 일도 하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldZoomApplier : MonoBehaviour
{
    [SerializeField, Tooltip("비우면 씬에서 찾는다.")]
    private ViewportScreenSettings _viewportSettings;

    private SettingsManager _settings;

    // 첫 적용이 뷰포트의 첫 확정보다 먼저여야 캐릭터가 줌 1 화면에 나왔다가 옮겨지지 않는다.
    // ViewportScreenSettings는 Start에서 몇 프레임 기다린 뒤 확정하고, 그 전에 받은 줌은 들고 있다가 쓴다.
    private void Start()
    {
        _settings = SettingsManager.Instance;
        if (_viewportSettings == null) _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        if (_settings == null || _viewportSettings == null)
        {
            Debug.LogWarning($"[{nameof(WorldZoomApplier)}] SettingsManager/ViewportScreenSettings 없음 — 전체 크기 설정을 적용하지 않습니다.", this);
            return;
        }

        Apply();
        _settings.GraphicsChanged += Apply;
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.GraphicsChanged -= Apply;
    }

    private void Apply() => _viewportSettings.SetZoom(GraphicsScaleSteps.WorldZoom.ToScale(_settings.WorldZoomStep));
}
