using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 화면 문장의 번역 표를 들고, stringID로 현재 언어의 문장을 내준다.
/// 표는 <c>stringID → { 언어 태그 → 문장 }</c> 형태의 JSON 하나이고, 시작할 때 한 번 읽어 전부 들고 있는다 —
/// 빈 칸을 기본 언어로 채우려면 어차피 여러 언어를 함께 들고 있어야 하고, 문장 수백 개 규모라 부담이 없다.
///
/// 현재 언어 칸이 비면 en-US로, 그것도 비면 가짜 번역(ko-KR 문장을 괄호로 감싸 늘린 것)으로 대신한다.
/// 가짜 번역이 화면에 보이면 그 문장은 번역이 빠졌다는 뜻이다.
///
/// 언어는 <see cref="SettingsManager"/>가 소유하고, 이 매니저는 그걸 따라간다. 설정의 <c>Changed</c>는
/// 토글 하나만 바뀌어도 울리므로, 언어가 실제로 바뀐 때만 <see cref="LanguageChanged"/>로 다시 알린다.
///
/// 씬 단일 인스턴스(Singleton). 실행 순서 -90 — <see cref="SettingsManager"/>(-100)가 설정을 읽은 뒤에
/// 언어를 가져오고, 텍스트들의 Start보다 먼저 표를 준비한다.
/// </summary>
[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public sealed class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    [Tooltip("번역 표 JSON(Assets/Localization/Strings.json).")]
    [SerializeField] private TextAsset _table;

    // 현재 언어 칸이 비었을 때 대신 쓰는 언어.
    private const string FallbackLanguage = LanguageCodes.English;
    // 가짜 번역의 재료. 번역을 요청하는 쪽(기획)이 채우는 원문 언어다.
    private const string SourceLanguage = LanguageCodes.Korean;

    private Dictionary<string, Dictionary<string, string>> _entries = new();
    private SettingsManager _settings;
    private string _language = FallbackLanguage;

    /// <summary>언어가 실제로 바뀌었을 때만 울린다. 텍스트들이 받아서 글자를 다시 채운다.</summary>
    public event Action LanguageChanged;

    /// <summary>코드가 글자를 채울 때 쓰는 진입점. 매니저가 없는 씬에서도 멈추지 않게 stringID를 그대로 돌려준다.</summary>
    public static string Localize(string id) => Instance != null ? Instance.Get(id) : id;

    /// <summary><see cref="Localize(string)"/>에 <see cref="Format"/>처럼 이름 붙은 자리 채우기를 더한 것.</summary>
    public static string Localize(string id, params (string name, object value)[] args) => Instance != null ? Instance.Format(id, args) : id;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadTable();

        _settings = SettingsManager.Instance;
        if (_settings == null)
        {
            Debug.LogWarning($"[{nameof(LocalizationManager)}] SettingsManager 없음 — 기본 언어({FallbackLanguage})로만 표시합니다.", this);
            return;
        }
        _language = _settings.Language;
        _settings.Changed += OnSettingsChanged;
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.Changed -= OnSettingsChanged;
        if (Instance == this) Instance = null;
    }

    /// <summary>stringID에 해당하는 현재 언어의 문장. 빈 칸이면 en-US, 그것도 비면 가짜 번역을 돌려준다.</summary>
    public string Get(string id)
    {
        _entries.TryGetValue(id, out var row);
        if (TryPick(row, _language, out var text)) return text;
        if (TryPick(row, FallbackLanguage, out text)) return text;

#if UNITY_EDITOR
        var missing = _language == FallbackLanguage ? _language : $"{_language}, {FallbackLanguage} 모두";
        Debug.LogWarning($"[{nameof(LocalizationManager)}] 번역 없음: '{id}' ({missing} 빈 칸) — 가짜 번역으로 표시합니다.", this);
#endif
        return Pseudo(TryPick(row, SourceLanguage, out text) ? text : id);
    }

    /// <summary>
    /// <see cref="Get"/>으로 찾은 문장의 <c>{이름}</c> 자리를 채운다. 예: <c>Format("UICollection.age", ("AGE", 11))</c>.
    /// 언어가 바뀔 때만 불리는 경로라 문자열 할당은 신경 쓰지 않는다.
    /// </summary>
    public string Format(string id, params (string name, object value)[] args)
    {
        var text = Get(id);
        foreach (var (name, value) in args) text = text.Replace("{" + name + "}", value?.ToString());
        return text;
    }

    private void OnSettingsChanged()
    {
        if (_settings.Language == _language) return;
        _language = _settings.Language;
        LanguageChanged?.Invoke();
    }

    private void LoadTable()
    {
        if (_table == null)
        {
            Debug.LogWarning($"[{nameof(LocalizationManager)}] 번역 표가 비어 있음 — 모든 문장이 가짜 번역으로 표시됩니다.", this);
            return;
        }
        try
        {
            _entries = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(_table.text) ?? new();
        }
        catch (JsonException e)
        {
            Debug.LogError($"[{nameof(LocalizationManager)}] 번역 표를 읽지 못함: {e.Message}", this);
        }
    }

    private static bool TryPick(Dictionary<string, string> row, string language, out string text)
    {
        text = null;
        return row != null && row.TryGetValue(language, out text) && !string.IsNullOrEmpty(text);
    }

    // 괄호로 감싸고 약 40% 늘린다. 번역이 빠진 곳과, 긴 언어가 들어왔을 때 넘칠 UI가 함께 드러난다.
    private static string Pseudo(string text) => "[" + text + new string('~', Mathf.Max(1, text.Length * 2 / 5)) + "]";
}
