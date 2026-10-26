using UnityEngine;

/// <summary>특수 대기 — 하품·기지개 같은 일회성 동작. owner.IdleActionDuration이 지나면 Idle로 돌아온다.
/// 스스로 반복하지 않는다: 다음에 무엇을 할지는 Idle에서 다시 뽑는다.</summary>
public sealed class IdleActionState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.IdleAction;
    public override string Name => "IdleAction";

    private float _endsAt;

    public override void OnEnter(IStateOwner owner)
    {
        _endsAt = Time.time + owner.IdleActionDuration;
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterState.Idle);
    }
}
