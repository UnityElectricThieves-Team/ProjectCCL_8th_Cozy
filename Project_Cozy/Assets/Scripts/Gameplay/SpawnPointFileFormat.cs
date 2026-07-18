using System;

/// <summary>
/// 스폰 기운(입력 누적)의 저장 데이터 컨테이너. 미래 저장 시스템이 주고받을 직렬화 타입. <see cref="HeartFileFormat"/>과 같은 패턴이다.
///
/// currentEnergy    = 소비형 스폰 기운(<see cref="SpawnPointManager.CurrentEnergy"/>). 스폰으로 차감된다.
/// cumulativeEnergy = 줄지 않는 누적 스폰 기운(<see cref="SpawnPointManager.CumulativeEnergy"/>). 캐릭터 해금 진행도·재파밍 방지용.
///
/// 둘 다 저장하는 이유: currentEnergy만 저장하면 재접속 시 누적 진행도가 사라지고, cumulativeEnergy만 저장하면
/// 모아둔 스폰 기운이 날아간다. SpawnPointManager의 상태 두 개가 곧 저장 대상 전부다.
/// </summary>
[Serializable]
public class SpawnPointFileFormat
{
    public int currentEnergy;
    public int cumulativeEnergy;
}
