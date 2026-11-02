using System;
using UnityEngine;

/// <summary>
/// 지원하는 표시 언어의 BCP 47 태그. 설정 파일에는 순서 번호가 아니라 이 태그 문자열을 저장한다 —
/// 번호로 저장하면 언어를 끼워 넣거나 순서를 바꾸는 순간 옛 파일이 다른 언어를 가리키게 된다.
/// <see cref="All"/>의 순서는 설정 패널 언어 드롭다운의 옵션 순서와 같아야 한다 — 드롭다운 인덱스로 이 배열을 바로 찾는다.
/// </summary>
public static class LanguageCodes
{
    public const string English = "en-US";
    public const string Korean = "ko-KR";
    public const string ChineseSimplified = "zh-Hans";
    public const string ChineseTraditional = "zh-Hant";
    public const string Japanese = "ja-JP";

    public static readonly string[] All = { English, Korean, ChineseSimplified, ChineseTraditional, Japanese };

    /// <summary>OS 언어에 맞는 지원 언어. 지원하지 않는 언어는 영어(Figma 옵션 - 일반 설명).
    /// <see cref="SystemLanguage.Chinese"/>는 간체·번체를 가리지 않는 옛 값이라 간체로 본다.</summary>
    public static string FromSystemLanguage(SystemLanguage language) => language switch
    {
        SystemLanguage.Korean => Korean,
        SystemLanguage.ChineseSimplified => ChineseSimplified,
        SystemLanguage.Chinese => ChineseSimplified,
        SystemLanguage.ChineseTraditional => ChineseTraditional,
        SystemLanguage.Japanese => Japanese,
        _ => English,
    };
}

/// <summary>카운트(스폰 기운·친밀도) 표기 방식. 설정 패널 드롭다운의 옵션 순서와 같아야 한다.</summary>
public enum CountVisibility
{
    /// <summary>항상 표시.</summary>
    Always,
    /// <summary>자동 숨기기 — 평소엔 숨기고 호버 시에만 표시(UserSettings.md §2.2).</summary>
    AutoHide,
    /// <summary>표시 안함.</summary>
    Hidden,
}

/// <summary>
/// 유저 환경 설정의 저장 데이터 컨테이너. <see cref="HeartFileFormat"/>과 같은 패턴이며,
/// <see cref="SettingsManager"/>가 런타임 상태로 그대로 들고 쓴다.
///
/// 기본값은 여기 필드 초기화가 유일한 정본이다(UserSettings.md의 기본값 열). 파일이 없을 때는 물론,
/// 나중에 필드가 추가되어 옛 파일에 그 항목이 없을 때도 같은 기본값이 채워진다.
/// 프리팹의 Is On이나 드롭다운 Value는 에디터에서 보이는 그림일 뿐, 시작 시 이 값으로 덮인다.
/// </summary>
[Serializable]
public class SettingsFileFormat
{
    /// <summary>항상 위. 옛 키 <c>alwaysOnTop</c>(기본값 false)를 버리고 새 키로 바꿨다 — 그 토글은 실제로 아무 일도
    /// 하지 않았는데도, 저장이 모든 필드를 쓰는 탓에 사용자가 고른 적 없는 false가 옛 파일에 남아 있다.
    /// 같은 키로 기본값만 바꾸면 그 false가 이겨 업데이트 직후 창이 맨 위에서 내려온다. 규약은 .claude/rules/unity/settings.md.</summary>
    public bool topmost = true;
    /// <summary>표시 언어의 BCP 47 태그(<see cref="LanguageCodes"/>). 예외로 이 항목만 첫 실행(파일 없음) 때
    /// <see cref="SettingsManager"/>가 OS 언어로 덮는다. 여기 값은 파일이 깨졌을 때의 기본값이다.</summary>
    public string language = LanguageCodes.English;
    /// <summary>스폰 지점의 스폰 기운 카운트 표기. 패널의 '구름 수치'.</summary>
    public CountVisibility spawnerCountVisibility = CountVisibility.AutoHide;
    /// <summary>캐릭터 친밀도 카운트 표기. 패널의 '친밀도 표기'.</summary>
    public CountVisibility affinityVisibility = CountVisibility.AutoHide;
    public bool autoStart = false;
    public bool administratorMode = false;
    public bool girlTransformBanned = false;

    // ===== 소리 탭. 볼륨은 0~1 슬라이더 값 그대로(데시벨 변환은 BgmPlayer가 한다). =====
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    /// <summary>볼륨과 따로 둔다 — 음소거를 풀면 저장된 볼륨으로 돌아간다.</summary>
    public bool muted = false;
    /// <summary><see cref="BgmCatalog"/>의 곡 id. 빈 문자열이면 목록의 첫 곡.</summary>
    public string bgmTrackId = "";

    // ===== 그래픽 탭. 배율이 아니라 단계 번호를 저장한다 — 대응은 GraphicsScaleSteps. =====
    public int characterScaleStep = GraphicsScaleSteps.Character.DefaultIndex;
    public int uiScaleStep = GraphicsScaleSteps.UI.DefaultIndex;
    public int worldZoomStep = GraphicsScaleSteps.WorldZoom.DefaultIndex;

    // 그래픽 탭의 모니터. 장치 이름과 모니터 rect를 함께 들어, 한쪽이 바뀌어도 다시 찾을 수 있게 한다
    // (Docs/Development/MultiMonitorPreparation.md D3). 장치 이름이 비어 있으면 주 모니터 — 옛 파일도 저절로 주 모니터가 된다.
    // RectInt를 그대로 담지 않고 정수로 푼다(ViewportFileFormat과 같은 이유).
    public string monitorDevice = "";
    public int monitorX;
    public int monitorY;
    public int monitorWidth;
    public int monitorHeight;
}

/// <summary>설정에 저장하는 모니터 선택. 장치 이름이 비어 있으면 주 모니터다.</summary>
public readonly struct MonitorSelection : IEquatable<MonitorSelection>
{
    public static readonly MonitorSelection Primary = new MonitorSelection("", default);

    public readonly string DeviceName;
    public readonly RectInt Bounds;

    public MonitorSelection(string deviceName, RectInt bounds)
    {
        DeviceName = deviceName ?? "";
        Bounds = bounds;
    }

    public bool IsPrimary => string.IsNullOrEmpty(DeviceName);

    public bool Equals(MonitorSelection other) => DeviceName == other.DeviceName && Bounds.Equals(other.Bounds);
    public override bool Equals(object obj) => obj is MonitorSelection other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(DeviceName, Bounds);
}

/// <summary>설정 패널 소리 탭의 볼륨 슬라이더 하나에 대응하는 믹서 채널.</summary>
public enum VolumeChannel
{
    Master,
    Music,
    Sfx,
}
