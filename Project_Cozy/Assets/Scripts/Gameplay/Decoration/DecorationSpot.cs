/// <summary>
/// 장식을 놓는 자리. 배치 모드의 확정 콜백과 장식 레이어가 같은 묶음을 쓴다.
///
/// 두 값 모두 기준점에서 잰 <b>길이</b>(베이스 px)다.
/// - <see cref="XFromRoomCenterPx"/>: 그림의 가로 중앙이 방 가운데(<see cref="ViewportScreenSettings.RoomCenterX"/>)에서 얼마나 떨어졌는가(오른쪽 +).
/// - <see cref="ElevationPx"/>: 그림의 아래 끝이 지면(뷰포트 아래 변)보다 얼마나 위에 있는가. 바닥 장식은 0.
/// 배경 그림도 가로 가운데를 방 가운데에, 아래 끝을 뷰포트 아래 변에 맞추므로, 같은 기준으로 두면 장식이 배경 속 벽·바닥과 함께 움직인다.
/// 방 가운데는 뷰포트를 통째로 옮길 때만 따라가므로, 크기를 바꾸면 장식은 제자리다(.claude/rules/unity/decoration-placement.md).
///
/// 자리를 월드로 옮기는 식은 레이어 <c>Relayout</c>에, 그 역식은 배치 컨트롤러 <c>UpdateGhost</c>에 있다 — 한쪽을 바꾸면 다른 쪽도 맞춘다.
/// </summary>
public readonly struct DecorationSpot
{
    public readonly float XFromRoomCenterPx;
    public readonly float ElevationPx;

    public DecorationSpot(float xFromRoomCenterPx, float elevationPx)
    {
        XFromRoomCenterPx = xFromRoomCenterPx;
        ElevationPx = elevationPx;
    }
}
