using System;

/// <summary>
/// 스폰 기운(입력 누적)의 저장 데이터 컨테이너. 미래 저장 시스템이 주고받을 직렬화 타입. <see cref="HeartFileFormat"/>과 같은 패턴이다.
///
/// cumulativeEnergy = 줄지 않는 누적 스폰 기운(<see cref="SpawnPointManager.CumulativeEnergy"/>). 스폰해도 차감되지 않는다.
/// SpawnPointManager의 상태는 이 값 하나가 전부다.
/// </summary>
[Serializable]
public class SpawnPointFileFormat
{
    public int cumulativeEnergy;
}
