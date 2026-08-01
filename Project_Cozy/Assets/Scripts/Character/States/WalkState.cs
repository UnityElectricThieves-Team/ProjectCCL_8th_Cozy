using UnityEngine;

/// <summary>
/// 좌/우 랜덤 방향 직선 이동. owner.NextWalkDuration 후 Idle 전환.
/// 속도는 <see cref="Speed"/> hook으로 노출 — <see cref="RunState"/>가 override.
/// </summary>
public class WalkState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Walk;
    public override string Name => "Walk";

    private float _endsAt;
    private float _direction;

    public override void OnEnter(IStateOwner owner)
    {
        _endsAt = Time.time + owner.NextWalkDuration();
        _direction = Random.value < 0.5f ? -1f : 1f;
        owner.SetFacing(_direction);
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        if (!owner.MoveHorizontal(_direction * Speed(owner) * dt))
        {
            // 거주 영역 경계에 막혔다 — 벽에 대고 제자리걸음 하는 대신 돌아선다.
            _direction = -_direction;
            owner.SetFacing(_direction);
        }

        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterState.Idle);
    }

    protected virtual float Speed(IStateOwner owner) => owner.WalkSpeed;
}
