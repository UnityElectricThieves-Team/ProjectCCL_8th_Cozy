/// <summary>
/// 정지 상태. 스스로는 전환하지 않고 외부(SleepController)의 owner.RequestWakeUp 호출만 기다린다.
/// </summary>
public sealed class SleepState : BaseCharacterState
{
    public override CharacterStateId Id => CharacterStateId.Sleep;
    public override string Name => "Sleep";

    public override void Tick(CharacterBasicAI2D owner, float dt)
    {
    }
}