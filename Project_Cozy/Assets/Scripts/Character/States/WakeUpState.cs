using UnityEngine;

/// <summary>짧은 트랜지션. owner.WakeUpDuration 후 Idle.</summary>
public sealed class WakeUpState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.WakeUp;
    public override string Name => "WakeUp";

    private float _endsAt;

    public override void OnEnter(IStateOwner owner)
    {
        _endsAt = Time.time + owner.WakeUpDuration;
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterState.Idle);
    }
}
