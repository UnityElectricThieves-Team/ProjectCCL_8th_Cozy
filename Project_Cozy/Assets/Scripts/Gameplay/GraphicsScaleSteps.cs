using UnityEngine;

/// <summary>
/// 그래픽 탭 −/+ 줄의 단계 번호와 실제 배율의 대응. 단계를 바꿀 때는 여기만 고친다.
/// 설정 파일에는 배율이 아니라 단계 번호를 저장한다 — 실수를 저장·비교하면 오차 때문에
/// "바꾼 게 없는데 저장 안 됨" 팝업이 뜰 수 있다.
///
/// **값은 모두 임시다.** Figma가 "빌드 직접 해보면서 결정"이라 적었고, 기획·아트와 빌드를 보며 다시 맞춘다.
/// 단계 수를 줄이면 옛 파일의 단계 번호는 범위 안으로 잘린다(<see cref="SettingsManager"/>의 Sanitize).
/// </summary>
public sealed class GraphicsScaleSteps
{
    /// <summary>캐릭터 크기. 0.5~1.5배, 0.1 단계. 기본 1.0배.</summary>
    public static readonly GraphicsScaleSteps Character = new GraphicsScaleSteps(0.5f, 0.1f, 11, 5);

    /// <summary>UI 크기. 0.5~1.5배, 0.1 단계. 기본 1.0배.</summary>
    public static readonly GraphicsScaleSteps UI = new GraphicsScaleSteps(0.5f, 0.1f, 11, 5);

    /// <summary>전체 크기(줌). 1.0~1.5배, 0.1 단계. 기본 1.0배.
    /// 1.0 미만은 두지 않는다 — 마스터 캔버스(아트 기준) 밖이 보이게 되기 때문이다. 상한·단계는 임시다.</summary>
    public static readonly GraphicsScaleSteps WorldZoom = new GraphicsScaleSteps(1.0f, 0.1f, 6, 0);

    private readonly float _min;
    private readonly float _step;

    /// <summary>단계 수.</summary>
    public int Count { get; }

    /// <summary>기본 단계 번호(초기화가 돌아가는 곳).</summary>
    public int DefaultIndex { get; }

    private GraphicsScaleSteps(float min, float step, int count, int defaultIndex)
    {
        _min = min;
        _step = step;
        Count = count;
        DefaultIndex = defaultIndex;
    }

    /// <summary>단계 번호의 배율. 범위 밖 번호는 끝 단계로 자른다.</summary>
    public float ToScale(int index) => _min + _step * Clamp(index);

    public int Clamp(int index) => Mathf.Clamp(index, 0, Count - 1);
}
