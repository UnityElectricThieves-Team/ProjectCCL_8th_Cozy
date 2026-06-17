/// <summary>
/// 캐릭터 상태 머신의 한 상태. 순수 C# 클래스 — owner(<see cref="IStateOwner"/>)가 인스턴스를 미리 만들어 재사용한다.
/// 정책 수치(idle 듀레이션 등)는 owner가 보유. State는 owner의 의미 있는 API만 호출한다.
///
/// 상태 전환은 두 경로로 일어난다:
///  1) Tick 안에서 자신이 결정 → owner.ChangeState(...) 호출
///  2) 외부(StateModule.Request* 등)가 ChangeState 호출
/// </summary>
public abstract class BaseCharacterState
{
    public abstract CharacterState Id { get; }
    public abstract string Name { get; }

    public virtual void OnEnter(IStateOwner owner) { }
    public abstract void Tick(IStateOwner owner, float dt);
    public virtual void OnExit(IStateOwner owner) { }
}
