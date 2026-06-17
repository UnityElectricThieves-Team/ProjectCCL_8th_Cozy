/// <summary>
/// 캐릭터의 단일 통합 상태. 게임 로직(<see cref="StateModule"/>)과 시각(<see cref="VisualModule"/>)이 같은 enum을 공유한다.
/// 명시적 Int 값은 BaseAnimatorController의 State 인덱스와 정합 — 함부로 재정렬 금지.
/// 새 상태 추가 시: enum 값 추가 → BaseAnimatorController에 State + Any State 트랜지션 추가 → 종별 Override 슬롯에 클립 매핑.
/// </summary>
public enum CharacterState
{
    Idle        = 0,
    Walk        = 1,
    Run         = 2,
    Sleep       = 3,
    WakeUp      = 4,
    Pet         = 5,
    Grabbed     = 6,
    Fall        = 7,
    Land        = 8,
    Transform   = 9,
    Interact    = 10,
    SpecialIdle = 11,
    SpecialWalk = 12,
}

/// <summary>캐릭터 폼. <see cref="UnityEngine.AnimatorOverrideController"/> 교체로 표현. Phase 8에서 본격 활용.</summary>
public enum CharacterForm
{
    Animal,
    Girl,
}
