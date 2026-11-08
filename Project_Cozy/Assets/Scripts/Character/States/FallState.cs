using UnityEngine;

/// <summary>자유낙하 — OnEnter에서 v_y = 0, 매 프레임 -gravity*dt. 발이 지면에 닿으면 Land.</summary>
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

        // 닿았으면 상태만 바꾼다. 파고든 만큼을 되돌리는 것은 같은 프레임에서 StateModule이 접지 규칙으로
        // 처리한다 — Land는 세로를 스스로 쥐지 않는 상태라 곧바로 지면에 고정된다.
        if (owner.IsFootOnGround())
            owner.ChangeState(CharacterState.Land);
    }
}
