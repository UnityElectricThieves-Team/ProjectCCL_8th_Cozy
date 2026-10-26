using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 유저 환경 설정의 런타임 소유자. 설정 패널이 값을 쓰고, 각 설정을 실제로 적용하는 쪽(창·캐릭터 등)이 읽는다.
/// 값이 바뀔 때마다 즉시 파일에 기록한다 — 설정은 가끔 바뀌는 이산 이벤트라 <see cref="SaveScheduler"/>로
/// 빈도를 묶을 이유가 없다(하트·상점과 같은 판단).
///
/// 예외는 볼륨 하나다. 슬라이더는 끄는 동안 매 프레임 값을 보내므로, <see cref="SetVolume"/>은 값만 바꾸고
/// 파일 기록은 손을 뗄 때 부르는 <see cref="CommitVolume"/>과 종료 시로 미룬다. 같은 이유로 볼륨은
/// <see cref="Changed"/> 대신 전용 <see cref="VolumeChanged"/>를 울린다 — Changed 구독자들은 이산 이벤트를 전제로 만들어졌다.
///
/// 설정 패널과 이 매니저 사이의 일치, 그리고 저장·복원까지만 책임진다.
/// 실제 적용은 각 소비자가 여기 값을 읽어 처리한다. 지금 연결된 소비자는 소녀 변신 금지
/// (<see cref="CharacterManager"/>가 캐릭터에 건다)와 소리 탭(<see cref="BgmPlayer"/>)이다. 나머지 항목은 아직 저장만 된다.
///
/// 씬 단일 인스턴스(Singleton). <see cref="HeartSystem"/>과 같은 패턴.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    private SettingsFileFormat _data = new();

    /// <summary>어느 설정이든 값이 바뀌면 울린다. 설정 패널이 이걸 받아 표시를 다시 맞춘다 —
    /// 패널 외의 곳에서 값이 바뀌어도 화면이 어긋나지 않게 하는 장치다.</summary>
    public event Action Changed;

    /// <summary>볼륨이 바뀌면 울린다. 슬라이더를 끄는 동안 매 프레임 울릴 수 있다.</summary>
    public event Action VolumeChanged;

    // 볼륨이 바뀌었지만 아직 파일에 쓰지 않았다.
    private bool _volumeDirty;

    public bool AlwaysOnTop
    {
        get => _data.alwaysOnTop;
        set { if (_data.alwaysOnTop == value) return; _data.alwaysOnTop = value; Commit(); }
    }

    /// <summary>표시 언어의 BCP 47 태그(<see cref="LanguageCodes"/>).</summary>
    public string Language
    {
        get => _data.language;
        set { if (_data.language == value) return; _data.language = value; Commit(); }
    }

    public CountVisibility SpawnerCountVisibility
    {
        get => _data.spawnerCountVisibility;
        set { if (_data.spawnerCountVisibility == value) return; _data.spawnerCountVisibility = value; Commit(); }
    }

    public CountVisibility AffinityVisibility
    {
        get => _data.affinityVisibility;
        set { if (_data.affinityVisibility == value) return; _data.affinityVisibility = value; Commit(); }
    }

    public bool AutoStart
    {
        get => _data.autoStart;
        set { if (_data.autoStart == value) return; _data.autoStart = value; Commit(); }
    }

    public bool AdministratorMode
    {
        get => _data.administratorMode;
        set { if (_data.administratorMode == value) return; _data.administratorMode = value; Commit(); }
    }

    public bool GirlTransformBanned
    {
        get => _data.girlTransformBanned;
        set { if (_data.girlTransformBanned == value) return; _data.girlTransformBanned = value; Commit(); }
    }

    public bool Muted
    {
        get => _data.muted;
        set { if (_data.muted == value) return; _data.muted = value; Commit(); }
    }

    /// <summary><see cref="BgmCatalog"/>의 곡 id. 빈 문자열이면 목록의 첫 곡.</summary>
    public string BgmTrackId
    {
        get => _data.bgmTrackId;
        set { value ??= ""; if (_data.bgmTrackId == value) return; _data.bgmTrackId = value; Commit(); }
    }

    public float GetVolume(VolumeChannel channel) => channel switch
    {
        VolumeChannel.Music => _data.musicVolume,
        VolumeChannel.Sfx => _data.sfxVolume,
        _ => _data.masterVolume,
    };

    /// <summary>
    /// 볼륨을 바꾸고 <see cref="VolumeChanged"/>를 울린다. **파일에는 쓰지 않는다** — 손을 뗄 때 <see cref="CommitVolume"/>을 부른다.
    /// 다른 설정이 저장될 때나 종료할 때도 함께 기록된다.
    /// </summary>
    public void SetVolume(VolumeChannel channel, float value)
    {
        value = Mathf.Clamp01(value);
        if (GetVolume(channel) == value) return;
        switch (channel)
        {
            case VolumeChannel.Music: _data.musicVolume = value; break;
            case VolumeChannel.Sfx: _data.sfxVolume = value; break;
            default: _data.masterVolume = value; break;
        }
        _volumeDirty = true;
        VolumeChanged?.Invoke();
    }

    /// <summary>아직 파일에 쓰지 않은 볼륨 변경이 있으면 저장한다.</summary>
    public void CommitVolume()
    {
        if (_volumeDirty) Save();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 복원은 이벤트를 쏘지 않는다. 이 시점엔 구독자가 아직 없고(실행 순서 -100),
        // 설정 패널은 자기 Start에서 현재 값을 직접 읽어 그린다(HeartSystem과 같은 방식).
        _data = UserDataSaveIO.Load<SettingsFileFormat>(GameDataPaths.Settings);
        Sanitize();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 슬라이더 손 뗌이 Unity에 오지 않는 경우(드래그 중 투명 영역으로 나가 클릭 통과된 경우, 키보드 조작)의 마지막 대비.
    private void OnApplicationQuit() => CommitVolume();

    /// <summary>
    /// 에디터 세이브는 사람이 열어 고칠 수 있는 평문 JSON이라 enum 범위 밖 정수나 지원하지 않는 언어 태그가 들어올 수 있다.
    /// 그대로 두면 매니저는 그 값을 들고 드롭다운은 옵션 수에 맞춰 잘라 보여줘 둘이 어긋난다. 기본값으로 되돌린다.
    /// 언어를 정수로 저장하던 옛 파일도 여기서 기본 언어로 돌아간다.
    /// </summary>
    private void Sanitize()
    {
        var defaults = new SettingsFileFormat();
        if (Array.IndexOf(LanguageCodes.All, _data.language) < 0) _data.language = defaults.language;
        if (!Enum.IsDefined(typeof(CountVisibility), _data.spawnerCountVisibility)) _data.spawnerCountVisibility = defaults.spawnerCountVisibility;
        if (!Enum.IsDefined(typeof(CountVisibility), _data.affinityVisibility)) _data.affinityVisibility = defaults.affinityVisibility;
        // Clamp01은 NaN을 그대로 돌려주므로 NaN은 따로 기본값으로 되돌린다. 그대로 두면 믹서에 NaN이 들어간다.
        _data.masterVolume = float.IsNaN(_data.masterVolume) ? defaults.masterVolume : Mathf.Clamp01(_data.masterVolume);
        _data.musicVolume = float.IsNaN(_data.musicVolume) ? defaults.musicVolume : Mathf.Clamp01(_data.musicVolume);
        _data.sfxVolume = float.IsNaN(_data.sfxVolume) ? defaults.sfxVolume : Mathf.Clamp01(_data.sfxVolume);
        _data.bgmTrackId ??= defaults.bgmTrackId;
    }

    /// <summary>값이 바뀐 직후. 저장하고 방송한다.</summary>
    private void Commit()
    {
        Save();
        Changed?.Invoke();
    }

    /// <summary>
    /// 현재 설정 전체를 파일에 기록한다. 쓰기 실패(디스크 잠금 등)는 로그만 남기고 삼킨다 —
    /// 저장이 안 됐다고 방금 바꾼 설정을 되돌리면 화면과 값이 갈라져 더 이상해진다.
    /// </summary>
    private void Save()
    {
        try
        {
            UserDataSaveIO.Save(GameDataPaths.Settings, _data);
            _volumeDirty = false; // 볼륨도 _data에 함께 들어 있어 방금 기록됐다
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"[{nameof(SettingsManager)}] 설정 저장 실패: {e.Message}", this);
        }
    }
}
