using UnityEngine;

/// <summary>
/// 제자리 대기. owner의 인스펙터 범위에서 뽑힌 시간이 지나면 스스로 Walk로 전환.
/// </summary>
public sealed class IdleState : BaseCharacterState
{
    public override CharacterStateId Id => CharacterStateId.Idle;
    public override string Name => "Idle";

    private float _endsAt;

    public override void OnEnter(CharacterBasicAI2D owner)
    {
        _endsAt = Time.time + owner.NextIdleDuration();
    }

    public override void Tick(CharacterBasicAI2D owner, float dt)
    {
        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterStateId.Walk);
    }
}