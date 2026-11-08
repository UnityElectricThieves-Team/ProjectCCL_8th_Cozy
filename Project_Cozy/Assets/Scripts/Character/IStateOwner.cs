using UnityEngine;

/// <summary>
/// State 클래스가 의존하는 owner 인터페이스. 구현체는 <see cref="BaseCharacterController"/>.
/// 정책 수치(WalkSpeed 등)와 거동(ChangeState, MoveHorizontal 등)을 노출 — State는 BaseCharacterController 구체 타입에 의존하지 않는다.
/// </summary>
public interface IStateOwner
{
    float WalkSpeed { get; }
    float RunSpeed { get; }
    float Gravity { get; }
    float WakeUpDuration { get; }
    float LandDuration { get; }
    float TransformDuration { get; }
    float IdleActionDuration { get; }
    float PetDuration { get; }
    float NextIdleDuration();

    /// <summary>대기 시간이 끝났을 때 특수 대기로 갈지 뽑는다. false면 걷기.
    /// 확률 자체는 owner(정책)가 쥐고 있고, State는 결과만 받는다.</summary>
    bool RollIdleAction();

    /// <summary>걷기 목적지를 현재 위치에서 최소한 이만큼 떨어뜨린다.</summary>
    float WalkMinDistance { get; }

    /// <summary>걸어갈 목적지를 뽑을 가로 범위. 거주 영역이 아직 안 들어왔으면 false.
    /// 거주 영역을 통째로 내주지 않는 이유는 <see cref="IsFootOnGround"/> 아래 설명과 같다 —
    /// 그 사각형의 아래 변이 곧 지면 높이다.</summary>
    bool TryGetWalkRange(out float minX, out float maxX);

    /// <summary>지금 누르고 있는 좌클릭이 *시작된 순간*의 커서 월드 좌표. 누르는 중이 아니면 false.
    /// 잡기 오프셋을 이 좌표로 재야 캐릭터가 커서에서 떨어진 채 끌려다니지 않는다.</summary>
    bool TryGetPressAnchor(out Vector2 world);

    /// <summary>발이 지면에 닿았는가(파묻힌 경우 포함).
    /// 지면 높이 자체는 노출하지 않는다 — 이 판정을 State마다 다시 쓰면 비교 방식이 갈라진다.</summary>
    bool IsFootOnGround();

    /// <summary>발을 지면 높이에 고정한다. 파묻혔으면 올리고, 떠 있으면 끌어내린다.</summary>
    void SnapToFloor();

    /// <summary>루트의 현재 월드 위치. 루트가 곧 발이다.</summary>
    Vector2 WorldPosition { get; }

    /// <summary>수평 이동. 거주 영역 경계에 막혀 요청한 만큼 못 갔으면 false — 호출자가 방향을 되돌릴 수 있다.</summary>
    bool MoveHorizontal(float deltaX);
    void ApplyVerticalDelta(float deltaY);
    void SetWorldPosition(Vector2 worldPos);
    void SetFacing(float direction);

    CharacterForm CurrentForm { get; }
    void SetForm(CharacterForm form);

    void ChangeState(CharacterState nextId);
}
