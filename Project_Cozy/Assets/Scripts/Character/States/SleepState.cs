/// <summary>정지. 외부의 RequestWakeUp 호출만 기다린다.</summary>
public sealed class SleepState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Sleep;
    public override string Name => "Sleep";

    public override void Tick(IStateOwner owner, float dt) { }
}
