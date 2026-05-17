using UnityEngine;

/// <summary>
/// 단순 자유낙하. OnEnter에서 수직 속도 0으로 시작, 매 프레임 owner.Gravity로 가속.
/// 발 아래 짧은 raycast가 ground 레이어에 hit하면 그 hit 위치로 클램프하고 Land(착지)로 전환.
/// </summary>
public sealed class FallState : BaseCharacterState
{
    public override CharacterStateId Id => CharacterStateId.Fall;
    public override string Name => "Fall";

    private float _velocityY;

    public override void OnEnter(CharacterBasicAI2D owner)
    {
        _velocityY = 0f;
    }

    public override void Tick(CharacterBasicAI2D owner, float dt)
    {
        _velocityY -= owner.Gravity * dt;
        owner.ApplyVerticalDelta(_velocityY * dt);

        if (owner.IsFootOnGround(out Vector2 hitPoint))
        {
            owner.SnapToGround(hitPoint);
            owner.ChangeState(CharacterStateId.Land);
        }
    }
}
