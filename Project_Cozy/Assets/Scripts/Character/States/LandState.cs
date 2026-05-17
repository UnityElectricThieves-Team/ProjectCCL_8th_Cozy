using UnityEngine;

/// <summary>
/// Fall 이후 바닥에 안착하는 짧은 트랜지션 상태. owner의 LandDuration이 지나면 Idle로 전환.
/// 실제 착지 모션 자산이 붙으면 OnEnter에서 재생을 트리거.
///
/// 주: 기획서(Animation_List.md, AI_Logic.md)에서는 이 상태를 `Spawn (착지)`로 명명하지만,
/// "spawn"은 영어로 *나타남/생성*의 의미라 *착지*에는 맞지 않아 코드는 `Land`로 통일했다.
/// </summary>
public sealed class LandState : BaseCharacterState
{
    public override CharacterStateId Id => CharacterStateId.Land;
    public override string Name => "Land";

    private float _endsAt;

    public override void OnEnter(CharacterBasicAI2D owner)
    {
        _endsAt = Time.time + owner.LandDuration;
    }

    public override void Tick(CharacterBasicAI2D owner, float dt)
    {
        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterStateId.Idle);
    }
}
