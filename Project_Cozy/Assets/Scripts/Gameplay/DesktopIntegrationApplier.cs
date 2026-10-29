using UnityEngine;

/// <summary>
/// 유저 설정 중 OS와 맞닿는 것(항상 위, 자동 실행)을 실제로 건다. 설정을 들고 있지도 저장하지도 않는다.
/// <see cref="BgmPlayer"/>처럼 <see cref="SettingsManager"/>를 읽어 적용만 한다.
///
/// Platform 쪽(<see cref="WindowManager"/>, <see cref="StartupRegistration"/>)은 게임 설정을 모르므로
/// 언제 무엇을 걸지는 여기서 정한다.
///
/// <see cref="SettingsManager.Changed"/>는 어느 설정이 바뀌어도 울린다. 그래서 마지막으로 건 값과 다를 때만 OS를 부른다 —
/// 언어 하나 바꿀 때마다 창 순서를 다시 잡거나 레지스트리를 쓰지 않게.
///
/// 자동 실행은 시작할 때 설정값대로 한 번 맞춘다. 켜져 있으면 지금 실행 파일 경로로 덮어써서, 게임 폴더를
/// 옮겨도 다음 부팅에 새 경로로 뜬다. 빌드 폴더를 여러 개 번갈아 실행하면 마지막에 켠 쪽이 등록된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DesktopIntegrationApplier : MonoBehaviour
{
    [SerializeField, Tooltip("비우면 씬에서 찾는다.")]
    private WindowManager _windowManager;

    private SettingsManager _settings;
    private bool _appliedTopmost;
    private bool _appliedAutoStart;

    // WindowManager가 HWND를 Awake에서 잡으므로, 첫 적용은 모든 Awake가 끝난 Start에서 한다.
    private void Start()
    {
        _settings = SettingsManager.Instance;
        if (_settings == null)
        {
            Debug.LogWarning($"[{nameof(DesktopIntegrationApplier)}] SettingsManager 없음 — 항상 위·자동 실행 설정을 적용하지 않습니다.", this);
            return;
        }
        if (_windowManager == null) _windowManager = FindFirstObjectByType<WindowManager>();

        ApplyTopmost();
        ApplyAutoStart();
        _settings.Changed += OnSettingsChanged;
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        if (_settings.AlwaysOnTop != _appliedTopmost) ApplyTopmost();
        if (_settings.AutoStart != _appliedAutoStart) ApplyAutoStart();
    }

    private void ApplyTopmost()
    {
        _appliedTopmost = _settings.AlwaysOnTop;
        if (_windowManager != null) _windowManager.SetTopmost(_appliedTopmost);
    }

    private void ApplyAutoStart()
    {
        _appliedAutoStart = _settings.AutoStart;
        if (_appliedAutoStart) StartupRegistration.Register();
        else StartupRegistration.Unregister();
    }
}
