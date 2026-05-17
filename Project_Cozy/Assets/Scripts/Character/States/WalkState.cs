using UnityEngine;

/// <summary>
/// 좌/우 랜덤 방향으로 직선 이동. owner의 인스펙터 범위에서 뽑힌 시간이 지나면 Idle로 전환.
/// 정식 명세(AI_Logic.md)는 "화면 내 랜덤 좌표를 찍고 그쪽으로 이동"이지만, 오늘 최소 구현은 직선 이동.
/// </summary>
public sealed class WalkState : BaseCharacterState
{
    public override CharacterStateId Id => CharacterStateId.Walk;
    public override string Name => "Walk";

    private float _endsAt;
    private float _direction;

    public override void OnEnter(CharacterBasicAI2D owner)
    {
        _endsAt = Time.time + owner.NextWalkDuration();
        _direction = Random.value < 0.5f ? -1f : 1f;
        owner.SetFacing(_direction);
    }

    public override void Tick(CharacterBasicAI2D owner, float dt)
    {
        owner.MoveHorizontal(_direction * owner.WalkSpeed * dt);

        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterStateId.Idle);
    }
}