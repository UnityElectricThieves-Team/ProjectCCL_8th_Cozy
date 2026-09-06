using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 유저 환경 설정의 런타임 소유자. 설정 패널이 값을 쓰고, 각 설정을 실제로 적용하는 쪽(창·캐릭터 등)이 읽는다.
/// 값이 바뀔 때마다 즉시 파일에 기록한다 — 설정은 가끔 바뀌는 이산 이벤트라 <see cref="SaveScheduler"/>로
/// 빈도를 묶을 이유가 없다(하트·상점과 같은 판단).
///
/// 아직 이 값을 소비하는 쪽은 없다. 설정 패널과 이 매니저 사이의 일치, 그리고 저장·복원까지만 책임진다.
/// 창 Topmost나 캐릭터 변신 같은 실제 적용은 각 소비자가 여기 값을 읽어 처리한다.
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

    public bool AlwaysOnTop
    {
        get => _data.alwaysOnTop;
        set { if (_data.alwaysOnTop == value) return; _data.alwaysOnTop = value; Commit(); }
    }

    public Language Language
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

    /// <summary>
    /// 에디터 세이브는 사람이 열어 고칠 수 있는 평문 JSON이라 enum 범위 밖 정수가 들어올 수 있다.
    /// 그대로 두면 매니저는 그 값을 들고 드롭다운은 옵션 수에 맞춰 잘라 보여줘 둘이 어긋난다. 기본값으로 되돌린다.
    /// </summary>
    private void Sanitize()
    {
        var defaults = new SettingsFileFormat();
        if (!Enum.IsDefined(typeof(Language), _data.language)) _data.language = defaults.language;
        if (!Enum.IsDefined(typeof(CountVisibility), _data.spawnerCountVisibility)) _data.spawnerCountVisibility = defaults.spawnerCountVisibility;
        if (!Enum.IsDefined(typeof(CountVisibility), _data.affinityVisibility)) _data.affinityVisibility = defaults.affinityVisibility;
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
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"[{nameof(SettingsManager)}] 설정 저장 실패: {e.Message}", this);
        }
    }
}
