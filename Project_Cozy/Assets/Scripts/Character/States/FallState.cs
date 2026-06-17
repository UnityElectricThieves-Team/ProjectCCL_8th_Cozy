using UnityEngine;

/// <summary>자유낙하 — OnEnter에서 v_y = 0, 매 프레임 -gravity*dt. ground hit 시 SnapToGround 후 Land.</summary>
public sealed class FallState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Fall;
    public override string Name => "Fall";

    private float _velocityY;

    public override void OnEnter(IStateOwner owner)
    {
        _velocityY = 0f;
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        _velocityY -= owner.Gravity * dt;
        owner.ApplyVerticalDelta(_velocityY * dt);

        if (owner.IsFootOnGround(out var hitPoint))
        {
            owner.SnapToGround(hitPoint);
            owner.ChangeState(CharacterState.Land);
        }
    }
}
