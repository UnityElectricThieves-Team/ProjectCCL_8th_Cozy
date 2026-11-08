using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Figma 옵션 화면의 알약 모양 ON/OFF 토글 겉모습. uGUI <see cref="Toggle"/>의 기본 체크박스 대신
/// 배경색·손잡이 위치·글자를 직접 갈아끼운다.
///
/// 켜짐: 배경 보라(#948EEB), 손잡이 오른쪽, 글자 "ON"이 왼쪽.
/// 꺼짐: 배경 회색(#CFCFCF), 손잡이 왼쪽, 글자 "OFF"가 오른쪽.
///
/// 배경색과 글자 내용은 즉시 바뀌고, 손잡이와 글자 상자는 <see cref="_slideDuration"/>에 걸쳐
/// 나란히 미끄러진다. 글자는 정렬을 바꾸지 않고 상자째 옮기므로 움직임이 끊기지 않는다.
/// 글자 상자의 정렬은 프리팹에서 가운데로 두고, 상자는 알약 가운데에 앵커를 둔 것을 전제로 한다.
/// 손잡이는 알약 폭을 숫자로 알지 못한다 — 여백 하나(<see cref="_knobPadding"/>)만 들고, 가장자리는 앵커가 정한다.
///
/// 상태는 <see cref="Toggle"/>이 들고 있고 이 컴포넌트는 그리기만 한다.
///
/// 기본값(처음에 켜짐인지 꺼짐인지)은 여기서 정하지 않는다. 프리팹의 Is On은 에디터에서 보이는 그림일 뿐이고,
/// 진짜 기본값은 <see cref="SettingsFileFormat"/>이 들고 있다 — 설정 패널이 시작 시 그 값을 토글에 밀어넣는다.
/// 이 컴포넌트에 기본값 칸을 만들면 같은 값이 두 곳에 생겨 갈라진다.
/// </summary>
[RequireComponent(typeof(Toggle))]
public sealed class SettingsPillToggle : MonoBehaviour
{
    [SerializeField] private Image _background;
    [SerializeField] private RectTransform _knob;
    [SerializeField] private TMP_Text _label;

    [Tooltip("손잡이와 알약 가장자리 사이의 여백(양쪽 같음). Figma 기준 5.56.")]
    [SerializeField, Min(0f)] private float _knobPadding = 5.56f;

    [Tooltip("손잡이와 글자가 반대편까지 미끄러지는 데 걸리는 시간(초). 0이면 즉시 이동.")]
    [SerializeField] private float _slideDuration = 0.12f;

    [Tooltip("배경 알약의 색. 기본값은 Figma 기준 — 켜짐 #948EEB, 꺼짐 #CFCFCF.")]
    [SerializeField] private Color _onBackgroundColor = new(0.580f, 0.557f, 0.922f);
    [SerializeField] private Color _offBackgroundColor = new(0.812f, 0.812f, 0.812f);

    // 모든 언어에서 같은 기호라 번역하지 않는다. 언어 폰트를 따라가면 폰트마다 글자 크기가 달라 보여서,
    // 폰트도 프리팹 것을 그대로 둔다.
    private const string OnText = "ON";
    private const string OffText = "OFF";

    private Toggle _toggle;
    private Coroutine _slide;
    private float _progress; // 꺼짐 0, 켜짐 1. 미끄러지는 중에는 그 사이 값

    private void Awake()
    {
        _toggle = GetComponent<Toggle>();
        _toggle.onValueChanged.AddListener(OnToggled);
    }

    // 미끄러지는 중에 숨겨지면 어중간한 자리에 멈춘다. 다시 보일 때 현재 값으로 맞춘다.
    private void OnEnable() => Redraw(_toggle.isOn);

    private void OnDestroy()
    {
        if (_toggle != null) _toggle.onValueChanged.RemoveListener(OnToggled);
    }

    private void OnToggled(bool on)
    {
        ApplyColorAndText(on);

        // 숨어 있는 탭 안에서는 코루틴을 시작할 수 없어 바로 옮긴다.
        if (_slideDuration <= 0f || !isActiveAndEnabled)
        {
            SetProgress(on ? 1f : 0f);
            return;
        }

        if (_slide != null) StopCoroutine(_slide);
        _slide = StartCoroutine(Slide(on ? 1f : 0f));
    }

    private IEnumerator Slide(float target)
    {
        float from = _progress;

        // 게임이 멈춰도 UI는 움직여야 하므로 unscaled 시간을 쓴다.
        for (float t = 0f; t < _slideDuration; t += Time.unscaledDeltaTime)
        {
            SetProgress(Mathf.Lerp(from, target, t / _slideDuration));
            yield return null;
        }

        SetProgress(target);
        _slide = null;
    }

    /// <summary>애니메이션 없이 현재 값 그대로 그린다.</summary>
    private void Redraw(bool on)
    {
        ApplyColorAndText(on);
        SetProgress(on ? 1f : 0f);
    }

    private void ApplyColorAndText(bool on)
    {
        if (_background != null) _background.color = on ? _onBackgroundColor : _offBackgroundColor;
        if (_label != null) _label.text = on ? OnText : OffText;
    }

    /// <summary>
    /// 손잡이와 글자를 progress(꺼짐 0 ~ 켜짐 1) 자리에 놓는다.
    /// 손잡이는 앵커·피벗의 가로를 progress로 두어 알약 폭과 상관없이 가장자리에 붙는다(여백만 ±로 띄운다).
    /// 글자 상자는 알약 가운데에서 손잡이가 없는 쪽으로 (여백 + 손잡이 폭)의 절반만큼 떨어진다 —
    /// 손잡이가 없는 쪽 공간의 한가운데다.
    /// </summary>
    private void SetProgress(float progress)
    {
        _progress = progress;
        float knobWidth = 0f;

        if (_knob != null)
        {
            knobWidth = _knob.rect.width;
            var min = _knob.anchorMin;
            var max = _knob.anchorMax;
            var pivot = _knob.pivot;
            min.x = max.x = pivot.x = progress;
            _knob.anchorMin = min;
            _knob.anchorMax = max;
            _knob.pivot = pivot;

            // 앵커·피벗을 바꾼 뒤에 위치를 정한다 — 앞의 세 줄이 위치를 함께 옮기기 때문이다.
            var p = _knob.anchoredPosition;
            p.x = Mathf.Lerp(_knobPadding, -_knobPadding, progress);
            _knob.anchoredPosition = p;
        }

        if (_label != null)
        {
            var rt = _label.rectTransform;
            var p = rt.anchoredPosition;
            p.x = (1f - 2f * progress) * (_knobPadding + knobWidth) * 0.5f;
            rt.anchoredPosition = p;
        }
    }
}
