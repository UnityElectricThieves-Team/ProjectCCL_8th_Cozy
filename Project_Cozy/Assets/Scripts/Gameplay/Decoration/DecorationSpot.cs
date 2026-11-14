/// <summary>
/// 장식을 놓는 자리. 배치 모드의 확정 콜백과 장식 레이어가 같은 묶음을 쓴다.
///
/// 두 값은 기준이 다르다.
/// - <see cref="BaseX"/>: 베이스 공간 px의 <b>절대 위치</b>. 그림의 가로 중앙이다.
/// - <see cref="ElevationPx"/>: 지면(뷰포트 아래 변)에서 잰 <b>길이</b>(베이스 px). 그림의 아래 끝이 지면보다 얼마나 위에 있는가. 바닥 장식은 0.
/// 높이를 지면 기준으로 두는 것은 배경도 그림 아래 끝을 뷰포트 아래 변에 맞추기 때문이다 — 뷰포트를 위아래로 옮겨도 벽에 건 장식이
/// 배경 속 벽과 함께 움직인다. 가로는 절대 위치라 그렇지 않다(.claude/rules/unity/decoration-placement.md).
/// </summary>
public readonly struct DecorationSpot
{
    public readonly float BaseX;
    public readonly float ElevationPx;

    public DecorationSpot(float baseX, float elevationPx)
    {
        BaseX = baseX;
        ElevationPx = elevationPx;
    }
}
