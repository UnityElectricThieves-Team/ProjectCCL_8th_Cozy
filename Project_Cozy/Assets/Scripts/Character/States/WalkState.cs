using UnityEngine;

/// <summary>
/// 거주 영역 안의 랜덤 목적지를 하나 찍고 거기까지 걸어간다. 도착하면 Idle.
///
/// **시간이 아니라 도착으로 끝난다.** 시간 기반으로 두면 캐릭터가 벽에 대고 제자리걸음을 하거나
/// 화면 구석에서만 서성인다. 목적지를 먼저 정하면 어디로 갈지가 영역 전체에 고르게 퍼진다.
///
/// 속도는 <see cref="Speed"/> hook으로 노출 — <see cref="RunState"/>가 override.
/// </summary>
public class WalkState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Walk;
    public override string Name => "Walk";

    /// <summary>거주 영역이 아직 없는 씬에서 쓸 목적지 거리. 본편에서는 뷰포트가 항상 영역을
    /// 걸어주므로 쓰이지 않는다 — 바인더 없는 테스트 씬에서 캐릭터가 굳어 보이지 않게 하는 용도다.</summary>
    private const float FALLBACK_DISTANCE = 3f;

    private float _targetX;

    public override void OnEnter(IStateOwner owner)
    {
        _targetX = PickTargetX(owner);
        owner.SetFacing(_targetX < owner.WorldPosition.x ? -1f : 1f);
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        float remaining = _targetX - owner.WorldPosition.x;
        float step = Speed(owner) * dt;

        // 이번 프레임에 목적지를 지나치면 도착으로 본다. 남은 거리가 한 프레임 이동량보다
        // 작아지는 순간이 있어야 끝나므로, 좌표 일치를 기다리지 않는다.
        if (Mathf.Abs(remaining) <= step)
        {
            owner.ChangeState(CharacterState.Idle);
            return;
        }

        // 경계에 막혔다 — 목적지를 영역 안에서 뽑으므로 정상적으로는 오지 않는다.
        // 걷는 도중 뷰포트가 줄어든 경우이고, 그 자리에 서는 것이 자연스럽다.
        if (!owner.MoveHorizontal(Mathf.Sign(remaining) * step))
            owner.ChangeState(CharacterState.Idle);
    }

    protected virtual float Speed(IStateOwner owner) => owner.WalkSpeed;

    /// <summary>거주 영역 안에서 목적지를 뽑는다. 현재 위치에서 최소 거리 이상 떨어진 곳만 후보이며,
    /// 양쪽 후보 구간의 길이에 비례해 고른다(한쪽이 좁다고 그쪽으로 몰리지 않게).
    /// 영역이 최소 거리를 양쪽으로 뺄 만큼 넓지 않으면 더 먼 끝으로 간다.</summary>
    private static float PickTargetX(IStateOwner owner)
    {
        float x = owner.WorldPosition.x;

        if (!owner.TryGetWalkRange(out float min, out float max))
            return x + (Random.value < 0.5f ? -FALLBACK_DISTANCE : FALLBACK_DISTANCE);

        float gap = Mathf.Max(0f, owner.WalkMinDistance);
        float leftSpan = Mathf.Max(0f, (x - gap) - min);
        float rightSpan = Mathf.Max(0f, max - (x + gap));

        // 양쪽 다 최소 거리를 못 채운다 — 영역이 좁다. 더 먼 끝으로 보낸다.
        if (leftSpan <= 0f && rightSpan <= 0f)
            return (x - min) >= (max - x) ? min : max;

        float roll = Random.Range(0f, leftSpan + rightSpan);
        return roll < leftSpan
            ? min + roll
            : x + gap + (roll - leftSpan);
    }
}
