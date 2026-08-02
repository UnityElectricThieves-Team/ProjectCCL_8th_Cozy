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
    float NextIdleDuration();
    float NextWalkDuration();

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
