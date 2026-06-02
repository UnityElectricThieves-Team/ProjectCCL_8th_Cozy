/// <summary>친밀도 만점 시 WalkState 대체. 거동 동일, enum만 다름. SpecialMode 분기는 <see cref="StateModule"/>에서 처리.</summary>
public sealed class SpecialWalkState : WalkState
{
    public override CharacterState Id => CharacterState.SpecialWalk;
    public override string Name => "SpecialWalk";
}
