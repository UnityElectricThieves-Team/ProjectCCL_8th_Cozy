using UnityEngine;

/// <summary>
/// 유저 설정(<see cref="SettingsManager"/>)에서 고른 배경음악을 반복 재생한다. 설정을 들고 있지도 저장하지도 않는다.
/// 읽어서 적용만 한다.
///
/// 볼륨·음소거는 여기서 다루지 않는다 — 이 컴포넌트의 AudioSource는 믹서의 Music 그룹으로 출력하고,
/// 그룹 볼륨은 <see cref="AudioVolumeApplier"/>가 건다.
///
/// 같은 GameObject의 AudioSource를 쓴다(loop 켬, Play On Awake 끔).
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class BgmPlayer : MonoBehaviour
{
    [SerializeField] private BgmCatalog _catalog;

    private AudioSource _source;
    private SettingsManager _settings;
    private BgmCatalog.Entry _current;

    private void Awake() => _source = GetComponent<AudioSource>();

    private void Start()
    {
        _settings = SettingsManager.Instance;
        if (_settings == null)
        {
            Debug.LogWarning($"[{nameof(BgmPlayer)}] SettingsManager 없음 — 배경음악을 재생하지 않습니다.", this);
            return;
        }

        ApplyTrack();
        _settings.Changed += ApplyTrack;
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.Changed -= ApplyTrack;
    }

    /// <summary>
    /// 고른 곡이 실제로 바뀐 때만 교체해 처음부터 튼다. 저장값이 아니라 목록에서 찾은 곡끼리 비교한다 —
    /// 저장값 ""(첫 곡)에서 드롭다운으로 첫 곡을 고르면 값은 바뀌지만 같은 곡이라 다시 틀면 안 된다.
    /// Changed는 어느 설정이 바뀌어도 울리므로 이 비교가 곡과 무관한 변경도 걸러 낸다.
    /// </summary>
    private void ApplyTrack()
    {
        var entry = _catalog != null ? _catalog.Resolve(_settings.BgmTrackId) : null;
        if (entry == _current) return;
        _current = entry;

        if (entry == null || entry.clip == null)
        {
            _source.Stop();
            _source.clip = null;
            return;
        }
        _source.clip = entry.clip;
        _source.Play();
    }
}
