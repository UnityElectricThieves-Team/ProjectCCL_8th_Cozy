using UnityEngine;

/// <summary>
/// 구름 그림의 연출 — 위아래로 살짝 떠다니는 "둥둥"과, 해금할 수 있을 때의 반짝임.
/// 구름의 Visual 자식(그림)에 붙는다. 게임 로직을 모른다. 상태는 <see cref="CloudController"/>가 넘긴다.
///
/// 루트가 아니라 이 GameObject의 localPosition만 움직인다. 루트 위치의 주인은 드래그와 뷰포트 재배치다.
/// 판정(루트의 콜라이더)은 <see cref="SpritePhysicsShapeSync"/>가 그림의 평행 이동을 따라가게 맞춘다.
/// 콜라이더를 다시 만드는 비용 때문에 이동은 마스터 캔버스 1px(월드 1/PPU) 단위로 끊어, 1px이 바뀔 때만 옮긴다.
///
/// 반짝임은 같은 그림을 조금 키워 구름 뒤에 노랗게 그리는 자식(<see cref="_shine"/>)의 알파를 오르내려,
/// 구름 가장자리 밖으로 빛나는 테두리가 깜박이게 한다. 구름 몸통이 흰색이라 위에 흰색을 덮는 방식은 보이지 않았다.
/// 색·배율·뒤에 그리는 순서는 프리팹의 Shine에 두고, 여기서는 알파만 바꾼다.
/// 반짝이지 않을 때는 그 렌더러를 꺼서 그리는 비용이 없다.
///
/// 수치는 연출 값이라 GameBalance가 아니라 여기 둔다(.claude/rules/unity/interaction-hit.md의 구름 연출 절).
/// </summary>
public sealed class CloudVisual : MonoBehaviour
{
    private const float PixelsPerUnit = BaseSpaceCameraFitter.PIXELS_PER_UNIT;

    [Header("둥둥")]
    [Tooltip("위아래로 움직이는 폭(한쪽). 마스터 캔버스 px.")]
    [SerializeField, Min(0f)] private float _bobAmplitudePx = 8f;
    [Tooltip("한 번 오르내리는 데 걸리는 시간(초).")]
    [SerializeField, Min(0.1f)] private float _bobPeriodSeconds = 3f;

    [Header("반짝임")]
    [Tooltip("같은 그림을 조금 키워 구름 뒤에 노랗게 그리는 자식 SpriteRenderer. 정렬 순서는 이 그림보다 하나 아래.")]
    [SerializeField] private SpriteRenderer _shine;
    [Tooltip("한 번 밝아졌다 돌아오는 데 걸리는 시간(초).")]
    [SerializeField, Min(0.1f)] private float _shinePeriodSeconds = 0.5f;
    [Tooltip("가장 밝을 때 빛 테두리의 알파.")]
    [SerializeField, Range(0f, 1f)] private float _shinePeakAlpha = 0.7f;

    private Vector3 _restLocalPosition;
    private float _bobPhase;
    private int _bobPx;
    private bool _activated;
    private float _shineTime;

    /// <summary>참이면 둥둥을 멈춘다(드래그 중). 놓으면 멈춘 자리에서 이어 간다.</summary>
    public bool Paused { get; set; }

    // 아래 두 값은 localPosition 이동을 그대로 월드 길이로 본다 — 구름 루트와 Visual의 배율이 1이라는 전제다.
    /// <summary>둥둥의 한쪽 폭(월드 길이). 그림이 어느 위상에서도 들어갈 자리를 구할 때 쓴다.</summary>
    public float BobAmplitudeWorld => Mathf.Ceil(_bobAmplitudePx) / PixelsPerUnit;

    /// <summary>지금 쉬는 자리에서 위로 움직인 양(월드 길이, 아래면 음수).</summary>
    public float BobOffsetWorld => _bobPx / PixelsPerUnit;

    private void Awake()
    {
        _restLocalPosition = transform.localPosition;
        if (_shine != null) _shine.enabled = false;
    }

    /// <summary>해금할 수 있는 상태인가. 켜면 반짝임이 0부터 차오르고, 끄면 덧그림을 끈다.</summary>
    public void SetActivated(bool activated)
    {
        _activated = activated;
        _shineTime = 0f;
        if (_shine == null) return;
        _shine.enabled = activated;
        if (activated) SetShineAlpha(0f);
    }

    private void Update()
    {
        UpdateBob();
        if (_activated && _shine != null) UpdateShine();
    }

    private void UpdateBob()
    {
        if (!Paused) _bobPhase = Mathf.Repeat(_bobPhase + Time.deltaTime / _bobPeriodSeconds, 1f);

        int px = Mathf.RoundToInt(Mathf.Sin(_bobPhase * 2f * Mathf.PI) * _bobAmplitudePx);
        if (px == _bobPx) return;

        _bobPx = px;
        transform.localPosition = _restLocalPosition + new Vector3(0f, px / PixelsPerUnit, 0f);
    }

    private void UpdateShine()
    {
        _shineTime = Mathf.Repeat(_shineTime + Time.deltaTime, _shinePeriodSeconds);
        float wave = (1f - Mathf.Cos(_shineTime / _shinePeriodSeconds * 2f * Mathf.PI)) * 0.5f;
        SetShineAlpha(wave * _shinePeakAlpha);
    }

    private void SetShineAlpha(float alpha)
    {
        Color c = _shine.color;
        c.a = alpha;
        _shine.color = c;
    }
}
