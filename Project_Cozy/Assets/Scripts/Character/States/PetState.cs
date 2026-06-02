/// <summary>쓰다듬 상태. 외부 RequestUnpet 호출만 기다린다.</summary>
public sealed class PetState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Pet;
    public override string Name => "Pet";

    public override void Tick(IStateOwner owner, float dt) { }
}
