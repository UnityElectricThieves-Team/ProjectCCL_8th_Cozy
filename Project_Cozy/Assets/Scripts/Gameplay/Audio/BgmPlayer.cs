using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 유저 설정(<see cref="SettingsManager"/>)의 소리 탭 값을 실제 소리에 반영한다 — 고른 배경음악을 반복 재생하고,
/// 볼륨·음소거를 믹서에 넣는다. 설정을 들고 있지도 저장하지도 않는다. 읽어서 적용만 한다.
///
/// 볼륨은 AudioMixer의 노출 파라미터로 조절한다. 믹서 그룹은 Master 아래 Music·SFX이고,
/// 이 컴포넌트의 AudioSource는 Music 그룹으로 출력한다. 효과음 슬라이더는 SFX 그룹을 조절하며,
/// 효과음 소스가 생기면 그 그룹으로 출력하기만 하면 된다.
///
/// 같은 GameObject의 AudioSource를 쓴다(loop 켬, Play On Awake 끔).
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class BgmPlayer : MonoBehaviour
{
    // 믹서에서 노출한 파라미터 이름. 믹서 에셋의 Exposed Parameters와 글자가 같아야 한다.
    private const string MasterParam = "MasterVolume";
    private const string MusicParam = "MusicVolume";
    private const string SfxParam = "SfxVolume";

    // 음소거·볼륨 0일 때의 dB. 믹서 볼륨의 하한이다.
    private const float SilentDb = -80f;

    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private BgmCatalog _catalog;

    private AudioSource _source;
    private SettingsManager _settings;
    private BgmCatalog.Entry _current;
    private bool _warnedMixerParam;

    private void Awake() => _source = GetComponent<AudioSource>();

    // AudioMixer.SetFloat는 Awake에서 부르면 무시되므로 첫 반영은 Start에서 한다.
    private void Start()
    {
        _settings = SettingsManager.Instance;
        if (_settings == null)
        {
            Debug.LogWarning($"[{nameof(BgmPlayer)}] SettingsManager 없음 — 배경음악을 재생하지 않습니다.", this);
            return;
        }

        ApplyVolumes();
        ApplyTrack();
        _settings.VolumeChanged += ApplyVolumes;
        _settings.Changed += OnSettingsChanged;
    }

    private void OnDestroy()
    {
        if (_settings == null) return;
        _settings.VolumeChanged -= ApplyVolumes;
        _settings.Changed -= OnSettingsChanged;
    }

    // Changed는 어느 설정이 바뀌어도 울린다. 음소거와 곡만 여기 관계있다.
    private void OnSettingsChanged()
    {
        ApplyVolumes();
        ApplyTrack();
    }

    private void ApplyVolumes()
    {
        if (_mixer == null) return;
        SetDb(MasterParam, _settings.Muted ? SilentDb : ToDb(_settings.GetVolume(VolumeChannel.Master)));
        SetDb(MusicParam, ToDb(_settings.GetVolume(VolumeChannel.Music)));
        SetDb(SfxParam, ToDb(_settings.GetVolume(VolumeChannel.Sfx)));
    }

    /// <summary>
    /// 고른 곡이 실제로 바뀐 때만 교체해 처음부터 튼다. 저장값이 아니라 목록에서 찾은 곡끼리 비교한다 —
    /// 저장값 ""(첫 곡)에서 드롭다운으로 첫 곡을 고르면 값은 바뀌지만 같은 곡이라 다시 틀면 안 된다.
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

    // 슬라이더 값 0~1은 사람 귀에 고르게 들리도록 데시벨로 바꾼다. 0은 log가 발산하므로 하한으로 자른다.
    private static float ToDb(float volume) => volume <= 0.0001f ? SilentDb : 20f * Mathf.Log10(volume);

    private void SetDb(string param, float db)
    {
        if (_mixer.SetFloat(param, db) || _warnedMixerParam) return;
        _warnedMixerParam = true;
        Debug.LogWarning($"[{nameof(BgmPlayer)}] 믹서에 노출 파라미터 '{param}'가 없습니다 — 믹서의 Exposed Parameters 이름을 확인하세요.", this);
    }
}
