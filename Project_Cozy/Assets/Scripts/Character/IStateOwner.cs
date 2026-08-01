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

    Vector2 FootWorldPosition { get; }
    Transform Transform { get; }

    bool TryGetGroundBelow(out Vector2 hitPoint);
    bool IsFootOnGround(out Vector2 hitPoint);
    bool IsFootBelowGround(out Vector2 groundTop);
    void SnapToGround(Vector2 hitPoint);

    /// <summary>수평 이동. 거주 영역 경계에 막혀 요청한 만큼 못 갔으면 false — 호출자가 방향을 되돌릴 수 있다.</summary>
    bool MoveHorizontal(float deltaX);
    void ApplyVerticalDelta(float deltaY);
    void SetWorldPosition(Vector2 worldPos);
    void SetFacing(float direction);

    CharacterForm CurrentForm { get; }
    void SetForm(CharacterForm form);

    void ChangeState(CharacterState nextId);
}
