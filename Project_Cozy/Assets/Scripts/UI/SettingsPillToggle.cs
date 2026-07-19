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
/// 색과 글자는 즉시 바뀌고 손잡이만 <see cref="_slideDuration"/>에 걸쳐 미끄러진다.
/// 완성된 그림 두 장을 갈아끼우는 방식과 달리 움직임을 인스펙터에서 조절할 수 있다.
///
/// 상태는 <see cref="Toggle"/>이 들고 있고 이 컴포넌트는 그리기만 한다.
/// </summary>
[RequireComponent(typeof(Toggle))]
public sealed class SettingsPillToggle : MonoBehaviour
{
    [SerializeField] private Image _background;
    [SerializeField] private RectTransform _knob;
    [SerializeField] private TMP_Text _label;

    [Tooltip("켜짐/꺼짐일 때 손잡이의 anchoredPosition.x. Figma 100x46 기준 60.8 / 5.56.")]
    [SerializeField] private float _knobOnX = 60.8f;
    [SerializeField] private float _knobOffX = 5.56f;

    [Tooltip("손잡이가 반대편까지 미끄러지는 데 걸리는 시간(초). 0이면 즉시 이동.")]
    [SerializeField] private float _slideDuration = 0.12f;

    [Tooltip("배경 알약의 색. 기본값은 Figma 기준 — 켜짐 #948EEB, 꺼짐 #CFCFCF.")]
    [SerializeField] private Color _onBackgroundColor = new(0.580f, 0.557f, 0.922f);
    [SerializeField] private Color _offBackgroundColor = new(0.812f, 0.812f, 0.812f);

    private Toggle _toggle;
    private Coroutine _slide;

    private void Awake()
    {
        _toggle = GetComponent<Toggle>();
        _toggle.onValueChanged.AddListener(OnToggled);
    }

    // 미끄러지는 중에 숨겨지면 손잡이가 어중간한 자리에 멈춘다. 다시 보일 때 현재 값으로 맞춘다.
    private void OnEnable() => Redraw(_toggle.isOn);

    private void OnDestroy()
    {
        if (_toggle != null) _toggle.onValueChanged.RemoveListener(OnToggled);
    }

    private void OnToggled(bool on)
    {
        ApplyColorAndLabel(on);

        // 숨어 있는 탭 안에서는 코루틴을 시작할 수 없어 바로 옮긴다.
        if (_slideDuration <= 0f || !isActiveAndEnabled)
        {
            SetKnobX(TargetX(on));
            return;
        }

        if (_slide != null) StopCoroutine(_slide);
        _slide = StartCoroutine(SlideKnob(TargetX(on)));
    }

    private IEnumerator SlideKnob(float targetX)
    {
        float startX = _knob != null ? _knob.anchoredPosition.x : targetX;

        // 게임이 멈춰도 UI는 움직여야 하므로 unscaled 시간을 쓴다.
        for (float t = 0f; t < _slideDuration; t += Time.unscaledDeltaTime)
        {
            SetKnobX(Mathf.Lerp(startX, targetX, t / _slideDuration));
            yield return null;
        }

        SetKnobX(targetX);
        _slide = null;
    }

    /// <summary>애니메이션 없이 현재 값 그대로 그린다.</summary>
    private void Redraw(bool on)
    {
        ApplyColorAndLabel(on);
        SetKnobX(TargetX(on));
    }

    private void ApplyColorAndLabel(bool on)
    {
        if (_background != null) _background.color = on ? _onBackgroundColor : _offBackgroundColor;

        if (_label != null)
        {
            _label.text = on ? "ON" : "OFF";
            // 손잡이 반대쪽에 글자가 오도록 정렬만 바꾼다 (글자 상자는 알약 전체를 덮는다는 전제).
            _label.alignment = on ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        }
    }

    private float TargetX(bool on) => on ? _knobOnX : _knobOffX;

    private void SetKnobX(float x)
    {
        if (_knob == null) return;
        var p = _knob.anchoredPosition;
        p.x = x;
        _knob.anchoredPosition = p;
    }
}
