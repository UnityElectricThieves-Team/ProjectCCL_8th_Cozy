/// <summary>
/// 장식 배치 모드의 종류. <see cref="DecorationPlacementController.ModeChanged"/>가 이 값을 실어 보낸다(<see cref="None"/> = 모드 아님).
/// 모드마다 어떤 UI를 남기고 무엇을 입력 대상으로 삼는지는 UI 개념이라 여기 두지 않는다(UI의 <c>DecorationModeUi</c>).
/// 규약은 .claude/rules/unity/decoration-placement.md.
/// </summary>
public enum DecorationMode
{
    None,
    Install,
    Move,
    Recall,
    Remove,
}
