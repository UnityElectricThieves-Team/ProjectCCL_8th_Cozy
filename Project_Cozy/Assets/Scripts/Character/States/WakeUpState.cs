using UnityEngine;

/// <summary>
/// 기상 모션 재생을 위한 짧은 트랜지션 상태. owner의 WakeUpDuration이 지나면 Idle로 전환.
/// </summary>
public sealed class WakeUpState : BaseCharacterState
{
    public override CharacterStateId Id => CharacterStateId.WakeUp;
    public override string Name => "WakeUp";

    private float _endsAt;

    public override void OnEnter(CharacterBasicAI2D owner)
    {
        _endsAt = Time.time + owner.WakeUpDuration;
    }

    public override void Tick(CharacterBasicAI2D owner, float dt)
    {
        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterStateId.Idle);
    }
}