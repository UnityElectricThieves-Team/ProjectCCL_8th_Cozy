using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유저 설정의 모니터 선택을 화면에 건다 — 고른 모니터를 찾아 그 작업 영역을 뷰포트 정책에 넣는다.
/// <see cref="WorldZoomApplier"/>와 같은 자리의 적용자로, 설정을 들고 있지도 저장하지도 않는다.
///
/// 찾는 순서는 장치 이름 → 모니터 rect → 주 모니터다(Docs/Development/MultiMonitorPreparation.md D3).
/// 장치 이름은 드라이버 재설치·도킹에서, rect는 배치를 바꾸면 바뀌어서 둘을 함께 본다.
/// 못 찾아 주 모니터로 갈 때도 저장값은 그대로 둔다 — 모니터를 다시 꽂으면 원래 선택이 돌아와야 한다.
///
/// 그래픽 탭 값이라 저장값이 아니라 지금 값(미리보기)을 읽는다. 고르면 창이 바로 옮겨 가고, [저장]하지 않고 나가면
/// 매니저가 저장값으로 되돌려 <see cref="SettingsManager.GraphicsChanged"/>가 울리고, 여기서 원래 모니터로 돌려놓는다.
///
/// 에디터에서는 창을 옮길 수 없고 베이스 공간이 Game 뷰 크기를 따라야 해서 정책에 넣지 않는다.
/// 모니터 분리·재연결을 스스로 알아채지는 않는다(D9 — 드문 일이라 상주 비용을 들이지 않는다). "화면 다시 맞추기"가 <see cref="Refresh"/>를 거쳐 다시 찾는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MonitorApplier : MonoBehaviour
{
    [SerializeField, Tooltip("비우면 씬에서 찾는다.")]
    private ViewportScreenSettings _viewportSettings;

    private SettingsManager _settings;
    private List<MonitorInfo> _monitors;

    /// <summary>연결된 모니터 목록(주 모니터가 0번). 드롭다운이 이 순서로 옵션을 만든다. 처음 쓸 때와 <see cref="Refresh"/> 때 다시 읽는다.</summary>
    public IReadOnlyList<MonitorInfo> Monitors => _monitors ??= MonitorList.Enumerate();

    /// <summary>모니터 목록을 다시 읽었을 때 울린다. 드롭다운이 옵션을 다시 만든다.</summary>
    public event System.Action MonitorsChanged;

    // 첫 적용이 뷰포트의 첫 배치보다 먼저여야 창이 다른 모니터에 놓였다가 옮겨 가지 않는다(WorldZoomApplier와 같은 이유).
    private void Start()
    {
        _settings = SettingsManager.Instance;
        if (_viewportSettings == null) _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        if (_settings == null || _viewportSettings == null)
        {
            Debug.LogWarning($"[{nameof(MonitorApplier)}] SettingsManager/ViewportScreenSettings 없음 — 모니터 설정을 적용하지 않습니다.", this);
            return;
        }

        Apply();
        _settings.GraphicsChanged += Apply;
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.GraphicsChanged -= Apply;
    }

    /// <summary>모니터 목록을 다시 읽고 선택을 다시 찾는다. 모니터·해상도·작업표시줄이 바뀐 뒤 "화면 다시 맞추기"가 부른다.
    /// 찾은 작업 영역이 그대로면 창은 정책의 ReadjustWindow가 다시 맞춘다.</summary>
    public void Refresh()
    {
        _monitors = MonitorList.Enumerate();
        MonitorsChanged?.Invoke();
        if (_settings != null && _viewportSettings != null) Apply();
    }

    /// <summary>선택에 해당하는 모니터의 목록 위치. 못 찾으면 0(주 모니터).</summary>
    public int IndexOf(MonitorSelection selection)
    {
        var monitors = Monitors;
        if (selection.IsPrimary) return 0;
        for (int i = 0; i < monitors.Count; i++)
            if (monitors[i].DeviceName == selection.DeviceName) return i;
        for (int i = 0; i < monitors.Count; i++)
            if (monitors[i].Bounds.Equals(selection.Bounds)) return i;
        return 0;
    }

    // GraphicsChanged는 크기 단계가 바뀔 때도 울리지만, 같은 작업 영역이면 정책이 아무 일도 하지 않는다.
    private void Apply()
    {
#if !UNITY_EDITOR
        var monitors = Monitors;
        if (monitors.Count == 0) return;
        _viewportSettings.SetTargetWorkArea(monitors[IndexOf(_settings.Monitor)].WorkArea);
#endif
    }
}
