using UnityEngine;

/// <summary>Fall→Idle 사이 짧은 트랜지션. owner.LandDuration 후 Idle.</summary>
public sealed class LandState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Land;
    public override string Name => "Land";

    private float _endsAt;

    public override void OnEnter(IStateOwner owner)
    {
        _endsAt = Time.time + owner.LandDuration;
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterState.Idle);
    }
}
