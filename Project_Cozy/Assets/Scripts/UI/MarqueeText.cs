using TMPro;
using UnityEngine;

/// <summary>
/// 글자가 칸보다 길면 왼쪽으로 흘려 끝까지 보여 준다(Figma 옵션 - 소리: "노래 이름이 창보다 길 경우, 왼쪽으로 샤샤샥 글씨 무한 루프").
/// 처음에 잠깐 멈춤 → 끝까지 흐름 → 잠깐 멈춤 → 처음으로 돌아감을 되풀이한다. 칸에 들어가면 가운데 정렬로 가만히 둔다.
///
/// 흘릴 TMP 글자에 붙이고, **그 부모가 칸 역할을 한다.** 부모에 RectMask2D를 달아 칸 밖을 잘라야 한다.
/// 글자의 앵커·피벗·폭은 이 컴포넌트가 맞춘다(왼쪽 끝 기준으로 옮겨야 해서다). 프리팹에서 손으로 맞추지 않는다.
///
/// 글자가 바뀌었는지는 매 프레임 문자열 참조로 본다 — 드롭다운이 고른 항목 글자를 새 문자열로 넣기 때문이다.
/// 칸에 들어가는 동안은 그 비교만 하고 아무것도 하지 않는다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class MarqueeText : MonoBehaviour
{
    [Tooltip("흐르는 속도(캔버스 px/초).")]
    [SerializeField, Min(1f)] private float _speed = 80f;
    [Tooltip("처음과 끝에서 멈춰 있는 시간(초).")]
    [SerializeField, Min(0f)] private float _pause = 1.5f;

    private TMP_Text _text;
    private RectTransform _rect;
    private RectTransform _viewport;
    private string _measuredText;
    private float _overflow;    // 칸을 넘는 길이. 0 이하면 흘리지 않는다
    private float _offset;      // 지금 왼쪽으로 옮긴 거리
    private float _wait;
    private bool _reachedEnd;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _rect = (RectTransform)transform;
        _viewport = _rect.parent as RectTransform;

        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.overflowMode = TextOverflowModes.Overflow;
        // 칸의 왼쪽 끝을 기준으로 위치를 옮기고, 높이는 칸을 꽉 채운다.
        _rect.anchorMin = new Vector2(0f, 0f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 0.5f);
        _rect.sizeDelta = new Vector2(_rect.sizeDelta.x, 0f);
    }

    private void OnEnable() => _measuredText = null; // 다시 켜질 때 칸 크기가 바뀌었을 수 있다

    private void Update()
    {
        if (_viewport == null) return;
        if (!ReferenceEquals(_text.text, _measuredText)) Measure();
        if (_overflow <= 0f) return;

        if (_wait > 0f)
        {
            _wait -= Time.unscaledDeltaTime;
            return;
        }

        if (_reachedEnd)
        {
            // 끝에서 멈춘 뒤 처음으로 돌아가 다시 멈춘다.
            _reachedEnd = false;
            _offset = 0f;
            _wait = _pause;
        }
        else
        {
            _offset = Mathf.Min(_offset + _speed * Time.unscaledDeltaTime, _overflow);
            if (_offset >= _overflow)
            {
                _reachedEnd = true;
                _wait = _pause;
            }
        }
        _rect.anchoredPosition = new Vector2(-_offset, 0f);
    }

    private void Measure()
    {
        _measuredText = _text.text;
        float viewWidth = _viewport.rect.width;
        float textWidth = _text.GetPreferredValues(_measuredText).x;
        _overflow = textWidth - viewWidth;

        bool flows = _overflow > 0f;
        _text.horizontalAlignment = flows ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Center;
        _rect.sizeDelta = new Vector2(flows ? textWidth : viewWidth, 0f);
        _offset = 0f;
        _reachedEnd = false;
        _wait = _pause;
        _rect.anchoredPosition = Vector2.zero;
    }
}
