using UnityEngine;

/// <summary>쓰담. owner.PetDuration만큼 모션을 재생하고 스스로 Idle로 돌아온다.
///
/// **재생 중에 다시 눌러도 모션이 처음부터 다시 시작하지 않는다**(기획 확정).
/// 여기서 따로 막을 것은 없다 — 재진입 요청은 <c>StateModule.RequestPet</c>이
/// "이미 Pet이면 무시"로 걸러낸다. 다만 누른 채로 홀드 시간을 채우면 Grabbed로는 넘어간다.</summary>
public sealed class PetState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Pet;
    public override string Name => "Pet";

    private float _endsAt;

    public override void OnEnter(IStateOwner owner)
    {
        _endsAt = Time.time + owner.PetDuration;
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterState.Idle);
    }
}
