using System;

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
    public bool alwaysOnTop = false;
    /// <summary>표시 언어의 BCP 47 태그(<see cref="LanguageCodes"/>).</summary>
    public string language = LanguageCodes.English;
    /// <summary>스폰 지점의 스폰 기운 카운트 표기. 패널의 '구름 표기'.</summary>
    public CountVisibility spawnerCountVisibility = CountVisibility.AutoHide;
    /// <summary>캐릭터 친밀도 카운트 표기. 패널의 '친밀도 표기'.</summary>
    public CountVisibility affinityVisibility = CountVisibility.AutoHide;
    public bool autoStart = false;
    public bool administratorMode = false;
    public bool girlTransformBanned = false;
}
