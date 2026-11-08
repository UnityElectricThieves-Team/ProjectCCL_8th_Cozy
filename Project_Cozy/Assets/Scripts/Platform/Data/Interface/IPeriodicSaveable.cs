/// <summary>
/// SaveScheduler가 주기적으로 저장을 위임하는 대상의 계약.
/// 값이 변할 때 IsDirty를 올려두면, 스케줄러가 주기 틱과 종료 시점에 dirty인 대상만 골라 저장한다.
/// 매 틱마다 안 바뀐 데이터까지 직렬화·암호화·디스크 쓰기를 반복하지 않기 위한 최소 장치다.
/// </summary>
public interface IPeriodicSaveable
{
    /// <summary>마지막 저장 이후 변경이 있으면 true.</summary>
    bool IsDirty { get; }

    /// <summary>현재 상태를 파일에 저장한다. 저장 후 IsDirty 해제까지 구현체가 책임진다.</summary>
    void Save();
}
