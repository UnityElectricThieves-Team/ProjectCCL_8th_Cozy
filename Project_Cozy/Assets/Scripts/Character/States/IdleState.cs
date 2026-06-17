using UnityEngine;

/// <summary>제자리 대기. owner.NextIdleDuration이 지나면 Walk로 전환.</summary>
public class IdleState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Idle;
    public override string Name => "Idle";

    private float _endsAt;

    public override void OnEnter(IStateOwner owner)
    {
        _endsAt = Time.time + owner.NextIdleDuration();
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterState.Walk);
    }
}
