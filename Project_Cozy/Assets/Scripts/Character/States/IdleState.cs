using UnityEngine;

/// <summary>제자리 대기. owner.NextIdleDuration이 지나면 다음 행동을 확률로 고른다 —
/// 대부분 Walk, 가끔 IdleAction. 확률 자체는 owner(StateModule)가 쥐고 있다.</summary>
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
        if (Time.time < _endsAt) return;

        owner.ChangeState(owner.RollIdleAction() ? CharacterState.IdleAction : CharacterState.Walk);
    }
}
