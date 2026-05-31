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
