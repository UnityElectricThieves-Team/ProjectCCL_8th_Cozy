// <deprecated_for_develop_kk>
// develop-kk 시스템에서는 사용하지 않습니다. develop 머지 시 재논의.
// 새 통합 enum은 ./CharacterState.cs 참조.
// namespace로 격리해 사용자 정의 CharacterState/CharacterForm과의 컴파일 충돌 회피.
// </deprecated_for_develop_kk>

namespace Prototype.Minjun
{

/// <summary>
/// 캐릭터의 시각적 상태. Animator Int 파라미터 값으로 사용되므로 명시적 숫자를 유지한다.
/// 새 상태 추가 시: enum 값 추가 → 베이스 Animator Controller에 State + Any State 트랜지션 추가 →
/// Animal/Girl Override 양쪽 슬롯에 클립 매핑.
/// </summary>
public enum VisualState
{
    Idle        = 0,
    Walk        = 1,
    Run         = 2,
    Sleep       = 3,
    WakeUp      = 4,
    Petting     = 5,
    Grabbed     = 6,
    Fall        = 7,
    Landing     = 8,
    Transform   = 9,
    Interact    = 10,

    SpecialIdle = 11,
    SpecialWalk = 12,
}

/// <summary>캐릭터 폼. <see cref="CharacterAnimator.SetForm"/>으로 교체한다.</summary>
public enum CharacterForm
{
    Animal,
    Girl,
}

}
