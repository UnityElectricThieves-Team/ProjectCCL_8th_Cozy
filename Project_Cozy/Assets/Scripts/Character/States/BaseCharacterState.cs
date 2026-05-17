/// <summary>
/// <see cref="CharacterBasicAI2D"/>의 상태 식별자. 배열 인덱스로 사용하므로 0부터 연속.
/// </summary>
public enum CharacterStateId
{
    Idle = 0,
    Walk = 1,
    Sleep = 2,
    WakeUp = 3,
    Fall = 4,
    Land = 5,
    Pet = 6,
    Grabbed = 7,
}

/// <summary>
/// 캐릭터 상태 머신의 한 상태. 순수 C# 클래스(MonoBehaviour 아님)로,
/// owner(<see cref="CharacterBasicAI2D"/>)가 5개 인스턴스를 미리 만들어 재사용한다 — 매 전환 시 new를 피해 할당 없음.
///
/// 상태 전환은 두 경로로 일어난다:
///  1) Tick 안에서 자신이 결정 → owner.ChangeState(...) 호출
///  2) 외부(SleepController 등)가 owner.RequestSleep/WakeUp/Fall 호출
///
/// 듀레이션·속도·지면 y 같은 정책 수치는 owner가 보유. State는 owner의 의미 있는 API만 호출하고 인스펙터 필드는 모른다.
/// </summary>
public abstract class BaseCharacterState
{
    public abstract CharacterStateId Id { get; }
    public abstract string Name { get; }

    public virtual void OnEnter(CharacterBasicAI2D owner) { }
    public abstract void Tick(CharacterBasicAI2D owner, float dt);
    public virtual void OnExit(CharacterBasicAI2D owner) { }
}
