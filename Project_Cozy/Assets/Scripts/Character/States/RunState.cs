/// <summary><see cref="WalkState"/>의 속도 변형 — owner.RunSpeed 사용.</summary>
public sealed class RunState : WalkState
{
    public override CharacterState Id => CharacterState.Run;
    public override string Name => "Run";

    protected override float Speed(IStateOwner owner) => owner.RunSpeed;
}
