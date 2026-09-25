using TMPro;
using UnityEngine;

/// <summary>
/// 같은 GameObject의 TMP 텍스트를 stringID의 현재 언어 문장으로 채운다.
/// 프리팹에 고정된 문구(탭 이름, 버튼 글자 등)에 붙인다. 코드가 글자를 채우는 텍스트에는 붙이지 않는다 —
/// 그런 곳은 채우는 코드가 <see cref="LocalizationManager.Get"/>을 직접 부르고 언어 변경을 구독한다.
///
/// 패널은 CanvasGroup으로 숨기고 꺼지지 않으므로, 구독은 Start에서 걸어 파괴될 때까지 유지한다.
/// 탭 내용처럼 꺼진 채 시작하는 텍스트는 처음 켜질 때 Start가 돌며 현재 언어로 채워진다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[DisallowMultipleComponent]
public sealed class LocalizedText : MonoBehaviour
{
    [Tooltip("번역 표의 stringID. 예: UISettings.tab.general")]
    [SerializeField] private string _stringId;

    private TMP_Text _text;
    private LocalizationManager _localization;

    private void Awake() => _text = GetComponent<TMP_Text>();

    private void Start()
    {
        _localization = LocalizationManager.Instance;
        if (_localization == null)
        {
            Debug.LogWarning($"[{nameof(LocalizedText)}] LocalizationManager 없음 — '{_stringId}'를 번역하지 못합니다.", this);
            return;
        }
        Refresh();
        _localization.LanguageChanged += Refresh;
    }

    private void OnDestroy()
    {
        if (_localization != null) _localization.LanguageChanged -= Refresh;
    }

    private void Refresh() => _text.text = _localization.Get(_stringId);
}
