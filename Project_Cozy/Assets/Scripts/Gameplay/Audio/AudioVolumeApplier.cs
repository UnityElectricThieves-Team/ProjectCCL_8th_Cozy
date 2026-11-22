using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 유저 설정(<see cref="SettingsManager"/>)의 볼륨·음소거를 AudioMixer에 건다. 설정을 들고 있지도 저장하지도 않는다.
/// <see cref="DesktopIntegrationApplier"/>처럼 읽어서 적용만 한다.
///
/// 믹서 그룹은 Master 아래 Music·SFX다. 소리 내는 쪽(<see cref="BgmPlayer"/>, 나중의 효과음 소스)은 자기 그룹으로
/// 출력하기만 하고, 볼륨은 여기 한 곳에서 그룹 단위로 건다.
///
/// <see cref="SettingsManager.Changed"/>는 어느 설정이 바뀌어도 울려 그때마다 SetFloat를 세 번 다시 부르지만,
/// 비용이 작아 마지막으로 건 값과 비교하지 않는다. 음소거는 Changed로만 온다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioVolumeApplier : MonoBehaviour
{
    // 믹서에서 노출한 파라미터 이름. 믹서 에셋의 Exposed Parameters와 글자가 같아야 한다.
    private const string MasterParam = "MasterVolume";
    private const string MusicParam = "MusicVolume";
    private const string SfxParam = "SfxVolume";

    // 음소거·볼륨 0일 때의 dB. 믹서 볼륨의 하한이다.
    private const float SilentDb = -80f;

    [SerializeField] private AudioMixer _mixer;

    private SettingsManager _settings;
    private bool _warnedMixerParam;

    // AudioMixer.SetFloat는 Awake에서 부르면 무시되므로 첫 반영은 Start에서 한다.
    private void Start()
    {
        _settings = SettingsManager.Instance;
        if (_settings == null || _mixer == null)
        {
            Debug.LogWarning($"[{nameof(AudioVolumeApplier)}] SettingsManager/AudioMixer 없음 — 볼륨 설정을 적용하지 않습니다.", this);
            return;
        }

        ApplyVolumes();
        _settings.VolumeChanged += ApplyVolumes;
        _settings.Changed += ApplyVolumes;
    }

    private void OnDestroy()
    {
        if (_settings == null) return;
        _settings.VolumeChanged -= ApplyVolumes;
        _settings.Changed -= ApplyVolumes;
    }

    private void ApplyVolumes()
    {
        SetDb(MasterParam, _settings.Muted ? SilentDb : ToDb(_settings.GetVolume(VolumeChannel.Master)));
        SetDb(MusicParam, ToDb(_settings.GetVolume(VolumeChannel.Music)));
        SetDb(SfxParam, ToDb(_settings.GetVolume(VolumeChannel.Sfx)));
    }

    // 슬라이더 값 0~1은 사람 귀에 고르게 들리도록 데시벨로 바꾼다. 0은 log가 발산하므로 하한으로 자른다.
    private static float ToDb(float volume) => volume <= 0.0001f ? SilentDb : 20f * Mathf.Log10(volume);

    private void SetDb(string param, float db)
    {
        if (_mixer.SetFloat(param, db) || _warnedMixerParam) return;
        _warnedMixerParam = true;
        Debug.LogWarning($"[{nameof(AudioVolumeApplier)}] 믹서에 노출 파라미터 '{param}'가 없습니다 — 믹서의 Exposed Parameters 이름을 확인하세요.", this);
    }
}
