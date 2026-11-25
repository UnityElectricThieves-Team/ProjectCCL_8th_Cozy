using TMPro;
using UnityEngine;

/// <summary>
/// 튜토리얼의 월드 안내 그림 — 생선 커서 그림, 빨갛게 깜박이는 부채꼴(누를 버튼 자리), 그 옆 카운터 글자(<c>n/목표</c>).
/// 튜토리얼 치즈 프리팹과 구름 프리팹에 꺼 둔 중첩 인스턴스로 들어 있고, <see cref="TutorialDirector"/>가 장면에 따라 부른다.
/// 진행 판단은 하지 않는다 — 보여 달라는 모양과 숫자만 그린다.
///
/// - 대상의 루트 자식이라 움직임을 저절로 따라간다(화면 좌표를 매 프레임 계산하지 않음). 캐릭터는 그림의 flipX로만
///   좌우를 바꾸고 루트 배율은 양수라, 이 그림은 뒤집히지 않는다.
/// - 콜라이더를 달지 않는다. 마우스로 만지는 오브젝트가 아니고, 있으면 그 자리 클릭이 대상 대신 이 그림에 걸리거나
///   바탕화면 클릭 통과를 막는다.
/// - 프리팹에는 GameObject를 꺼 둔 채 저장한다(렌더러만 끄지 않음). 구름·캐릭터가 Awake에서 자식 SpriteRenderer를
///   찾는 코드가 꺼진 오브젝트를 건너뛰기 때문이다. 켜고 끄기는 이 컴포넌트가 맡는다.
/// - 우클릭 모양은 좌클릭 부채꼴을 커서 그림 기준으로 좌우 반전한 것이다(Figma에는 좌클릭 그림만 있다).
/// - 숫자만이라 번역하지 않고 폰트도 바꾸지 않는다(머리 위 수치와 같은 판단).
/// - 꺼져 있으면 Update가 돌지 않아 상주 비용이 없다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialClickHint : MonoBehaviour
{
    [Tooltip("생선 커서 그림. 좌우 반전의 기준점이다.")]
    [SerializeField] private SpriteRenderer _cursor;
    [Tooltip("빨간 부채꼴(누를 버튼 자리). 프리팹에는 좌클릭 자리에 둔다.")]
    [SerializeField] private SpriteRenderer _sector;
    [Tooltip("카운터 글자(월드 TextMeshPro). 처음 SetCounter를 부를 때 보인다.")]
    [SerializeField] private TMP_Text _counter;
    [Tooltip("부채꼴이 켜지고 꺼지는 간격(초).")]
    [SerializeField, Min(0.05f)] private float _blinkInterval = 0.4f;

    private bool _initialized;
    private float _sectorLeftX;
    private float _blinkTimer;
    private int _shownValue = -1;
    private int _shownTarget = -1;

    private void Awake() => Initialize();

    // 프리팹에 저장한 부채꼴 위치(좌클릭)를 기억해 둔다. 우클릭은 커서 그림 기준으로 이 자리를 좌우로 뒤집는다.
    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        if (_sector != null) _sectorLeftX = _sector.transform.localPosition.x;
    }

    /// <summary>좌클릭 안내를 보인다. 꺼져 있었으면 켜고 카운터는 숨긴 채 시작한다. 이미 켜져 있으면 모양만 바꾼다.</summary>
    public void ShowLeftClick() => Show(rightClick: false);

    /// <summary>우클릭 안내를 보인다. 이미 켜져 있었으면 카운터는 그대로 둔다(<c>10/10</c>처럼 끝난 숫자가 남는다).</summary>
    public void ShowRightClick() => Show(rightClick: true);

    /// <summary>카운터 글자를 채운다. 처음 부르면 카운터가 보인다. 값은 목표에서 자른다. 숫자가 바뀔 때만 글자를 다시 만든다.</summary>
    public void SetCounter(int value, int target)
    {
        if (_counter == null) return;
        _counter.enabled = true;
        value = Mathf.Clamp(value, 0, target);
        if (value == _shownValue && target == _shownTarget) return;
        _shownValue = value;
        _shownTarget = target;
        _counter.text = value + "/" + target;
    }

    /// <summary>안내를 끈다.</summary>
    public void Hide() => gameObject.SetActive(false);

    private void Show(bool rightClick)
    {
        Initialize();
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            if (_counter != null) _counter.enabled = false;
            _shownValue = -1;
            _shownTarget = -1;
        }

        if (_sector != null)
        {
            _sector.flipX = rightClick;
            var p = _sector.transform.localPosition;
            float pivotX = _cursor != null ? _cursor.transform.localPosition.x : 0f;
            p.x = rightClick ? 2f * pivotX - _sectorLeftX : _sectorLeftX;
            _sector.transform.localPosition = p;
            _sector.enabled = true;
        }
        _blinkTimer = 0f;
    }

    private void Update()
    {
        if (_sector == null) return;
        _blinkTimer += Time.deltaTime;
        if (_blinkTimer < _blinkInterval) return;
        _blinkTimer -= _blinkInterval;
        _sector.enabled = !_sector.enabled;
    }
}
