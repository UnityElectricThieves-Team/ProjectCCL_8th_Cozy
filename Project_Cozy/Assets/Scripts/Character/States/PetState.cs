/// <summary>
/// 쓰다듬 상태. 마우스 hover 동안 머물고, hover 종료(<see cref="CharacterBasicAI2D.RequestUnpet"/>) 시 Idle로 복귀.
/// 이동 없음. 시각 피드백(scale/tint)은 별도 컴포넌트(PettingReactionTestProbe)가 UnityEvent로 처리.
/// </summary>
public sealed class PetState : BaseCharacterState
{
    public override CharacterStateId Id => CharacterStateId.Pet;
    public override string Name => "Pet";

    public override void Tick(CharacterBasicAI2D owner, float dt) { }
}
